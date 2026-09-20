using Xunit;
using KindleMate2.Infrastructure.Helpers;

namespace KindleMate2.Tests;

/// <summary>
/// 「在文件管理器中定位文件」的平台命令构造(ShellHelper)。
///
/// 回归背景:原实现把 Windows 专用的 explorer.exe + /select, 写死在 AppConstants 并由调用方直接
/// Process.Start,在 macOS 上必抛异常(「截图后打开所在位置」功能失效)。这些用例把三种平台的
/// 命令形态钉住,确保平台分支不会被再次抹平。
/// </summary>
public sealed class ShellHelperTests {
    private const string WindowsPath = @"C:\Users\rainy\AppData\Roaming\KindleMate2\Statistics\20260920_210000.png";
    private const string MacPath = "/Users/laurence/Library/Application Support/KindleMate2/Statistics/20260920_210000.png";
    private const string LinuxPath = "/home/laurence/.local/share/KindleMate2/Statistics/20260920_210000.png";

    [Fact]
    public void BuildRevealStartInfo_Windows_UsesExplorerSelect() {
        var psi = ShellHelper.BuildRevealStartInfo(WindowsPath, isWindows: true, isMacOs: false);

        Assert.Equal("explorer.exe", psi.FileName);
        Assert.Equal("/select,\"" + WindowsPath + "\"", psi.Arguments);
        Assert.True(psi.UseShellExecute);
    }

    [Fact]
    public void BuildRevealStartInfo_MacOS_UsesOpenReveal() {
        var psi = ShellHelper.BuildRevealStartInfo(MacPath, isWindows: false, isMacOs: true);

        Assert.Equal("open", psi.FileName);
        // 必须是两个独立参数:open -R <path>。拼成单串会在含空格路径上被拆错。
        Assert.Equal(new[] { "-R", MacPath }, psi.ArgumentList);
        Assert.Empty(psi.Arguments);
        // open 是可执行文件,不需要 shell 解析;走 ShellExecute 会把引号再包一层。
        Assert.False(psi.UseShellExecute);
    }

    [Theory]
    [InlineData("/Users/laurence/My Folder/截图 2026-09-20.png")]
    [InlineData("/Users/laurence/带\"引号\"/a.png")]
    [InlineData("/Users/laurence/a b c.png")]
    public void BuildRevealStartInfo_MacOS_KeepsPathAsSingleArgument(string path) {
        var psi = ShellHelper.BuildRevealStartInfo(path, isWindows: false, isMacOs: true);

        // 含空格 / 引号 / 中文的路径都必须原样作为单个参数传出,不做任何手工转义
        Assert.Equal(new[] { "-R", path }, psi.ArgumentList);
    }

    [Fact]
    public void BuildRevealStartInfo_Linux_FallsBackToContainingDirectory() {
        var psi = ShellHelper.BuildRevealStartInfo(LinuxPath, isWindows: false, isMacOs: false);

        Assert.Equal("/home/laurence/.local/share/KindleMate2/Statistics", psi.FileName);
        Assert.True(psi.UseShellExecute);
    }

    [Fact]
    public void BuildRevealStartInfo_Linux_BareFileNameFallsBackToCurrentDirectory() {
        var psi = ShellHelper.BuildRevealStartInfo("screenshot.png", isWindows: false, isMacOs: false);

        // 相对文件名取不到目录时不能给出空 FileName(Process.Start 会直接抛)
        Assert.Equal(".", psi.FileName);
    }

    [Theory]
    [InlineData(false, true)]   // macOS
    [InlineData(false, false)]  // Linux 等
    public void BuildRevealStartInfo_NonWindows_NeverUsesExplorer(bool isWindows, bool isMacOs) {
        var psi = ShellHelper.BuildRevealStartInfo("/tmp/x.png", isWindows, isMacOs);

        Assert.NotEqual("explorer.exe", psi.FileName);
        Assert.DoesNotContain("explorer.exe", psi.Arguments);
    }
}
