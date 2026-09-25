using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
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

    // ————————————————————— 下载 + SHA-256 校验 —————————————————————

    [Theory]
    [InlineData("abc123  pkg.zip\n", "pkg.zip", "abc123")]
    [InlineData("abc123 *pkg.zip\n", "pkg.zip", "abc123")]                 // GNU 文本模式的 * 前缀
    [InlineData("aaa  other.zip\nbbb  pkg.zip\n", "pkg.zip", "bbb")]
    [InlineData("aaa  pkg.tar.gz\n", "pkg.zip", null)]                     // 清单里没有该文件
    public void ParseExpectedHash_ReadsSha256SumLines(string text, string name, string? expected) {
        Assert.Equal(expected, UpdateInstaller.ParseExpectedHash(text, name));
    }

    [Fact]
    public async Task DownloadAsync_VerifiesSha256_AndAbortsOnMismatchOrMissingEntry() {
        var payload = Encoding.UTF8.GetBytes("hello world");
        var goodHash = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
        const string url = "https://example.test/pkg.zip";
        const string sums = "https://example.test/SHA256SUMS";

        // ① 哈希匹配 → 通过
        var ok = await UpdateInstaller.DownloadAsync(
            new UpdateAsset("pkg.zip", url, payload.Length, sums),
            null, new HttpClient(new ChecksumHandler(payload, $"{goodHash}  pkg.zip\n")));
        try {
            Assert.Equal("hello world", File.ReadAllText(ok));
        } finally {
            Cleanup(ok);
        }

        // ② 哈希不匹配(包被篡改)→ 中止
        await Assert.ThrowsAsync<InvalidOperationException>(() => UpdateInstaller.DownloadAsync(
            new UpdateAsset("pkg.zip", url, payload.Length, sums),
            null, new HttpClient(new ChecksumHandler(payload, "deadbeef  pkg.zip\n"))));

        // ③ 清单里没有该文件 → 中止(不静默放行)
        await Assert.ThrowsAsync<InvalidOperationException>(() => UpdateInstaller.DownloadAsync(
            new UpdateAsset("pkg.zip", url, payload.Length, sums),
            null, new HttpClient(new ChecksumHandler(payload, "deadbeef  other.zip\n"))));

        // ④ 旧发布没有 SHA256SUMS 资产 → 跳过校验(兼容)
        var legacy = await UpdateInstaller.DownloadAsync(
            new UpdateAsset("pkg.zip", url, payload.Length),
            null, new HttpClient(new ChecksumHandler(payload, null)));
        try {
            Assert.Equal("hello world", File.ReadAllText(legacy));
        } finally {
            Cleanup(legacy);
        }
    }

    private static void Cleanup(string file) {
        try { Directory.Delete(Path.GetDirectoryName(file)!, true); } catch { /* best effort */ }
    }

    /// <summary>按 URI 分发:含 SHA256SUMS 的返回清单正文,其余返回资产字节。</summary>
    private sealed class ChecksumHandler(byte[] payload, string? checksum) : HttpMessageHandler {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            if (request.RequestUri!.AbsoluteUri.EndsWith("SHA256SUMS", StringComparison.Ordinal)) {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
                    Content = new StringContent(checksum ?? string.Empty)
                });
            }
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(payload) };
            response.Content.Headers.ContentLength = payload.Length;
            return Task.FromResult(response);
        }
    }

    // ————————————————————— 反推 .app 路径 —————————————————————

    [Fact]
    public void ResolveAppBundlePath_FindsTheNearestAppBundle() {
        // 用**宿主自己的临时目录**当根,而不是写死 "/Applications/...":
        // Windows 上 DirectoryInfo.FullName 会给"无盘符的绝对路径"补上当前盘符,
        // 于是 /Applications/... 会变成 D:\Applications\... —— 断言里的期望值写死就会假失败
        // (CI 实测:Expected "/Applications\Kindle Mate 2.app" vs Actual "D:\Applications\Kindle Mate 2.app")。
        // 这类"断言写成了宿主相关"的坑本项目踩过多次,写路径断言时务必用宿主拼出来的路径做期望值。
        var appPath = Path.Combine(Path.GetTempPath(), "Kindle Mate 2.app");
        var executable = Path.Combine(appPath, "Contents", "MacOS", "KindleMate2.Avalonia");

        var found = UpdateInstaller.ResolveAppBundlePath(executable);

        // 真正要验的性质(与宿主无关):找到的是那个 .app 祖先本身,而不是它的某层子目录
        Assert.Equal("Kindle Mate 2.app", Path.GetFileName(found));
        Assert.EndsWith(".app", found, StringComparison.OrdinalIgnoreCase);

        // 并且就是输入路径里那个 .app 目录
        Assert.Equal(appPath, found);
    }

    [Fact]
    public void ResolveAppBundlePath_WithoutAppBundle_Throws() {
        // 开发期直接跑 dll 的情形:自动替换本就不适用,应当明确报错而不是乱删目录
        Assert.Throws<InvalidOperationException>(() =>
            UpdateInstaller.ResolveAppBundlePath(Path.Combine(_work, "KindleMate2.Avalonia")));
    }
}
