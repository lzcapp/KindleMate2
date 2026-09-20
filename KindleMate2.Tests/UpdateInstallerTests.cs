using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;
using Xunit;
using KindleMate2.Application.Services;

namespace KindleMate2.Tests;

/// <summary>
/// 更新安装器:解压、脚本生成,以及**真的执行一次替换脚本**(用已退出的假 PID + 假的安装目录 +
/// 会留标记的"重启"命令)。
///
/// 为什么要真的跑脚本:字符串断言只能证明"脚本里有那几个词",证明不了它真能按顺序干活 ——
/// 而这段脚本要在用户机器上删掉旧包、拷贝新包、再启动,是最不该出错的一段。用假 PID(一定已退出)
/// 与假安装目录就能在测试里完整走一遍,不必真的替换自己。
/// </summary>
public sealed class UpdateInstallerTests : IDisposable {
    private readonly string _work;

    public UpdateInstallerTests() {
        _work = Path.Combine(Path.GetTempPath(), "km2-update-tests-" + Guid.NewGuid().ToString("N")[..10]);
        Directory.CreateDirectory(_work);
    }

    public void Dispose() {
        try { Directory.Delete(_work, true); } catch { /* best effort */ }
    }

    // ————————————————————— 解压 —————————————————————

    [Fact]
    public void Extract_TarGz_RestoresTheWholeTree() {
        var source = Path.Combine(_work, "src");
        Directory.CreateDirectory(Path.Combine(source, "sub"));
        File.WriteAllText(Path.Combine(source, "kindlemate2"), "launcher");
        File.WriteAllText(Path.Combine(source, "sub", "lib.txt"), "payload");

        var archive = Path.Combine(_work, "pkg.tar.gz");
        using (var file = File.Create(archive))
        using (var gzip = new GZipStream(file, CompressionMode.Compress)) {
            TarFile.CreateFromDirectory(source, gzip, includeBaseDirectory: false);
        }

        var destination = Path.Combine(_work, "extracted-tar");
        UpdateInstaller.Extract(archive, destination);

        Assert.Equal("launcher", File.ReadAllText(Path.Combine(destination, "kindlemate2")));
        Assert.Equal("payload", File.ReadAllText(Path.Combine(destination, "sub", "lib.txt")));
    }

    [Fact]
    public void Extract_Zip_RestoresTheWholeTree() {
        var source = Path.Combine(_work, "zipsrc");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "KindleMate2.Avalonia.exe"), "exe");

        var archive = Path.Combine(_work, "pkg.zip");
        ZipFile.CreateFromDirectory(source, archive);

        var destination = Path.Combine(_work, "extracted-zip");
        UpdateInstaller.Extract(archive, destination);

        Assert.Equal("exe", File.ReadAllText(Path.Combine(destination, "KindleMate2.Avalonia.exe")));
    }

    [Fact]
    public void Extract_UnknownFormat_FailsLoudly() {
        var bogus = Path.Combine(_work, "pkg.rar");
        File.WriteAllText(bogus, "x");

        Assert.Throws<NotSupportedException>(() => UpdateInstaller.Extract(bogus, Path.Combine(_work, "out")));
    }

    // ————————————————————— 脚本内容 —————————————————————

    [Fact]
    public void BuildLinuxScript_WaitsForExitThenMergesAndRestarts() {
        var script = UpdateInstaller.BuildLinuxScript("/tmp/prepared", "/opt/kindlemate2", 4321);

        Assert.Contains("while kill -0 4321", script);
        Assert.Contains("cp -R \"/tmp/prepared\"/. \"/opt/kindlemate2/\"", script);
        Assert.Contains("./kindlemate2", script);
        // 刻意不 rm -rf 安装目录:那是用户自己解压出来的位置
        Assert.DoesNotContain("rm -rf \"/opt/kindlemate2\"", script);
    }

    [Fact]
    public void BuildWindowsScript_WaitsViaTasklistThenCopiesAndStarts() {
        var script = UpdateInstaller.BuildWindowsScript(@"C:\temp\prepared", @"C:\apps\km2", 4321);

        Assert.Contains("tasklist /FI \"PID eq 4321\"", script);
        Assert.Contains("timeout /t 1", script);
        Assert.Contains("xcopy /E /Y /I", script);
        Assert.Contains("start \"\"", script);
    }

    [Fact]
    public void BuildMacOsScript_MountsDmgReplacesBundleAndReopens() {
        var script = UpdateInstaller.BuildMacOsScript("/tmp/KindleMate2.dmg", "/Applications/Kindle Mate 2.app", 4321);

        Assert.Contains("while kill -0 4321", script);
        Assert.Contains("hdiutil attach \"/tmp/KindleMate2.dmg\"", script);
        Assert.Contains("rm -rf \"/Applications/Kindle Mate 2.app\"", script);
        Assert.Contains("cp -R", script);
        Assert.Contains("xattr -dr com.apple.quarantine", script);   // 不清隔离标记会被 Gatekeeper 拦
        Assert.Contains("hdiutil detach", script);
        Assert.Contains("open \"/Applications/Kindle Mate 2.app\"", script);
    }

    // ————————————————————— 真跑一次脚本 —————————————————————

    [Fact]
    public void LinuxScript_ActuallyMergesFilesAndRestarts() {
        if (OperatingSystem.IsWindows()) {
            return;   // 这一段只验 POSIX 脚本
        }

        // 假的"已解压的新版本"
        var prepared = Path.Combine(_work, "prepared");
        Directory.CreateDirectory(prepared);
        File.WriteAllText(Path.Combine(prepared, "kindlemate2"), "new-launcher");
        File.WriteAllText(Path.Combine(prepared, "kindlemate2.Avalonia.dll"), "new-dll");

        // 假的"安装目录":已有一个旧文件,且**应当被保留**(合并覆盖而非清空)
        var install = Path.Combine(_work, "install");
        Directory.CreateDirectory(install);
        File.WriteAllText(Path.Combine(install, "kindlemate2"), "old-launcher");
        File.WriteAllText(Path.Combine(install, "user-data.txt"), "should-survive");

        // "重启"用一个小脚本代替:它留个标记文件,这样能验证脚本确实走到了最后一步
        var marker = Path.Combine(_work, "restarted.marker");
        var launcher = Path.Combine(install, "fake-launch.sh");
        File.WriteAllText(launcher, $"#!/bin/bash\ntouch \"{marker}\"\n");
        File.SetUnixFileMode(launcher, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        // 用一个**一定不存在**的 PID:kill -0 立即失败,脚本不必真的等
        var script = UpdateInstaller.BuildLinuxScript(prepared, install, 999_999, "fake-launch.sh");
        var scriptPath = Path.Combine(_work, "apply.sh");
        File.WriteAllText(scriptPath, script);

        using var process = Process.Start(new ProcessStartInfo {
            FileName = "/bin/sh",
            Arguments = $"\"{scriptPath}\"",
            UseShellExecute = false,
        })!;
        process.WaitForExit(15_000);

        Assert.Equal("new-launcher", File.ReadAllText(Path.Combine(install, "kindlemate2")));
        Assert.Equal("should-survive", File.ReadAllText(Path.Combine(install, "user-data.txt")));

        // 脚本末尾是 `./fake-launch.sh &`(后台启动),给它一点时间落标记
        var deadline = Stopwatch.StartNew();
        while (!File.Exists(marker) && deadline.Elapsed < TimeSpan.FromSeconds(5)) {
            Thread.Sleep(50);
        }
        Assert.True(File.Exists(marker), "脚本应当以重启结尾(标记文件未出现)");
    }

    // ————————————————————— 反推 .app 路径 —————————————————————

    [Fact]
    public void ResolveAppBundlePath_FindsTheNearestAppBundle() {
        var executable = Path.Combine("/Applications", "Kindle Mate 2.app", "Contents", "MacOS", "KindleMate2.Avalonia");

        Assert.Equal(Path.Combine("/Applications", "Kindle Mate 2.app"),
            UpdateInstaller.ResolveAppBundlePath(executable));
    }

    [Fact]
    public void ResolveAppBundlePath_WithoutAppBundle_Throws() {
        // 开发期直接跑 dll 的情形:自动替换本就不适用,应当明确报错而不是乱删目录
        Assert.Throws<InvalidOperationException>(() =>
            UpdateInstaller.ResolveAppBundlePath(Path.Combine(_work, "KindleMate2.Avalonia")));
    }
}
