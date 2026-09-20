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

        // 分隔符必须先归一化再比:退化为"所在目录"这件事与分隔符无关,但 Path.GetDirectoryName
        // 用的是**宿主平台**语义 —— 在 Windows 上它把 '/' 也当分隔符,并输出 '\'
        // (实际 CI 里得到的是 \home\laurence\.local\share\KindleMate2\Statistics)。
        // 直接写死 POSIX 字面量会让这条用例在 Windows 作业上假失败,而它并不代表产品有问题。
        Assert.Equal("/home/laurence/.local/share/KindleMate2/Statistics", ToPosixSeparators(psi.FileName));
        Assert.True(psi.UseShellExecute);
    }

    /// <summary>
    /// 断言用的分隔符归一化。之所以单列一个用例并把 Windows 侧的实际输出形式也钉进来:
    /// 本机(macOS)跑不出 Windows 的分隔符形态,只能靠这条把"两个平台的产出都能归一到同一期望值"
    /// 变成可执行的事实,而不是仅凭推理。
    /// </summary>
    [Theory]
    [InlineData("/home/laurence/.local/share/KindleMate2/Statistics")]      // Unix 宿主的产出
    [InlineData(@"\home\laurence\.local\share\KindleMate2\Statistics")]     // Windows 宿主的产出(CI 实测)
    [InlineData(@"C:\Users\rainy\AppData\Roaming\KindleMate2\Statistics")]  // 纯 Windows 形态
    public void ToPosixSeparators_NormalizesBothHostStyles(string hostProduced) {
        var normalized = ToPosixSeparators(hostProduced);

        if (hostProduced.StartsWith('C')) {
            Assert.Equal("C:/Users/rainy/AppData/Roaming/KindleMate2/Statistics", normalized);
        } else {
            Assert.Equal("/home/laurence/.local/share/KindleMate2/Statistics", normalized);
        }
    }

    private static string ToPosixSeparators(string path) => path.Replace('\\', '/');

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
