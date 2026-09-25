using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using KindleMate2.Shared.Diagnostics;

namespace KindleMate2.Application.Services;

/// <summary>要替换的目标形态。</summary>
public enum UpdateTarget {
    /// <summary>macOS 的 <c>.app</c> 包(从 .dmg 里取出)。</summary>
    MacOsAppBundle,

    /// <summary>解压即用的目录(Linux 的 tar.gz、Windows 的 zip)。</summary>
    Directory,
}

/// <summary>
/// 下载并安装更新 —— "更新并自动重启"的落地部分。
///
/// **为什么不让当前进程自己替换自己**:Windows 上运行中的 exe 被占用、macOS 上正在运行的 .app
/// 也不宜原地改写。通行做法是**派一个独立脚本**:它先等当前进程退出,再替换文件,最后重新启动。
///
/// 分工上刻意让 .NET 侧多做一点:解压(<see cref="Extract"/>)与校验在托管代码里完成,脚本只做
/// "等退出 → 合并覆盖 → 重启"三件事。这样脚本足够简单以致能读懂,而解压/覆盖这些真正会出错的
/// 步骤可以在测试里真的跑一遍(不需要真的替换自己)。
/// </summary>
public static class UpdateInstaller {
    /// <summary>
    /// 下载资产到临时目录并解压好,返回**待安装内容的目录**;macOS 例外 —— 那里返回下载到的 dmg
    /// (挂载镜像只能靠 <c>hdiutil</c>,不适合在托管代码里做)。
    /// </summary>
    public static async Task<string> PrepareAsync(UpdateAsset asset, UpdateTarget target,
        IProgress<double>? progress = null, HttpClient? httpClient = null, CancellationToken cancellationToken = default) {
        var downloaded = await DownloadAsync(asset, progress, httpClient, cancellationToken).ConfigureAwait(false);
        if (target == UpdateTarget.MacOsAppBundle) {
            return downloaded;
        }

        var prepared = Path.Combine(Path.GetDirectoryName(downloaded)!, "prepared");
        Directory.CreateDirectory(prepared);
        Extract(downloaded, prepared);
        AppLog.Write($"[UpdateInstaller] 已解压到 {prepared}");
        return prepared;
    }

    /// <summary>下载到临时目录,返回文件路径。失败抛异常(调用方要提示用户)。</summary>
    public static async Task<string> DownloadAsync(UpdateAsset asset, IProgress<double>? progress = null,
        HttpClient? httpClient = null, CancellationToken cancellationToken = default) {
        var directory = Path.Combine(Path.GetTempPath(), "km2-update-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(directory);
        var target = Path.Combine(directory, asset.Name);

        using var owned = httpClient is null ? new HttpClient { Timeout = TimeSpan.FromMinutes(10) } : null;
        var client = httpClient ?? owned!;

        using var response = await client.GetAsync(asset.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var total = asset.Size > 0 ? asset.Size : response.Content.Headers.ContentLength ?? 0;

        // 写入流必须在校验之前关闭/落盘 —— 否则 File.OpenRead 会因文件仍被占用而失败。
        long written = 0;
        await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
        await using (var destination = File.Create(target)) {
            var buffer = new byte[81920];
            int read;
            while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0) {
                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                written += read;
                if (total > 0) {
                    progress?.Report((double)written / total);
                }
            }
        }

        AppLog.Write($"[UpdateInstaller] 已下载 {asset.Name}({written} 字节)");

        // 下载完立刻校验完整性(有清单时)。放在解压/替换之前 —— 坏包不该走到"删旧包"那一步。
        await VerifyChecksumAsync(asset, target, client, cancellationToken).ConfigureAwait(false);

        return target;
    }

    /// <summary>
    /// 用发布页的 <c>SHA256SUMS</c> 校验下载文件。清单存在时必须命中且哈希一致,否则抛异常中止更新;
    /// 清单缺失(旧发布没有该资产)时跳过并记日志 —— 兼容历史版本。
    /// </summary>
    private static async Task VerifyChecksumAsync(UpdateAsset asset, string filePath, HttpClient client,
        CancellationToken cancellationToken) {
        if (string.IsNullOrWhiteSpace(asset.ChecksumUrl)) {
            AppLog.Write($"[UpdateInstaller] {asset.Name} 发布页无 SHA256SUMS,跳过完整性校验");
            return;
        }

        string checksumText;
        try {
            checksumText = await client.GetStringAsync(asset.ChecksumUrl, cancellationToken).ConfigureAwait(false);
        } catch (Exception ex) {
            throw new InvalidOperationException($"下载校验和失败,已中止更新:{ex.Message}", ex);
        }

        var expected = ParseExpectedHash(checksumText, asset.Name);
        if (expected is null) {
            throw new InvalidOperationException($"SHA256SUMS 里没有 {asset.Name} 的记录,已中止更新");
        }

        await using var stream = File.OpenRead(filePath);
        var actual = Convert.ToHexString(
            await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false));

        if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase)) {
            throw new InvalidOperationException(
                $"{asset.Name} 的 SHA-256 不匹配(期望 {expected},实际 {actual}),已中止更新");
        }

        AppLog.Write($"[UpdateInstaller] {asset.Name} SHA-256 校验通过");
    }

    /// <summary>
    /// 解析 <c>sha256sum</c> 风格清单里某个文件名的哈希:行形如 <c>&lt;hex&gt;␠␠&lt;文件名&gt;</c>
    /// (GNU coreutils 在文本模式下可能带 <c>*</c> 前缀)。找不到返回 null。
    /// </summary>
    internal static string? ParseExpectedHash(string checksumText, string fileName) {
        foreach (var raw in checksumText.Split('\n')) {
            var line = raw.Trim();
            if (line.Length == 0) {
                continue;
            }
            var separator = line.IndexOf(' ');
            if (separator <= 0) {
                continue;
            }
            var hash = line[..separator];
            var name = line[(separator + 1)..].Trim().TrimStart('*');
            if (string.Equals(name, fileName, StringComparison.Ordinal)) {
                return hash;
            }
        }
        return null;
    }

    /// <summary>按扩展名解压(tar.gz / zip)。两种发布产物都用得上,且两条路径都要能被测试真的跑一遍。</summary>
    public static void Extract(string archivePath, string destination) {
        Directory.CreateDirectory(destination);

        if (archivePath.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) ||
            archivePath.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase)) {
            // TarFile 能直接读 gzip 流,不必先手动解压
            using var file = File.OpenRead(archivePath);
            using var gzip = new GZipStream(file, CompressionMode.Decompress);
            TarFile.ExtractToDirectory(gzip, destination, overwriteFiles: true);
            return;
        }

        if (archivePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) {
            ZipFile.ExtractToDirectory(archivePath, destination, overwriteFiles: true);
            return;
        }

        throw new NotSupportedException($"不认识的安装包格式:{Path.GetFileName(archivePath)}");
    }

    /// <summary>当前平台对应的目标形态。</summary>
    public static UpdateTarget TargetForCurrentPlatform() =>
        OperatingSystem.IsMacOS() ? UpdateTarget.MacOsAppBundle : UpdateTarget.Directory;

    /// <summary>
    /// 生成替换脚本并分离启动它。**调用方随后应立即退出应用** —— 脚本在等我们退出。
    /// </summary>
    /// <param name="preparedPath"><see cref="PrepareAsync"/> 的返回值(dmg 或已解压目录)。</param>
    /// <param name="executablePath">当前可执行文件完整路径。</param>
    /// <param name="processId">当前进程 id,脚本靠它等我们退出。</param>
    public static string ApplyAndRestart(string preparedPath, string executablePath, int processId) {
        var installDirectory = Path.GetDirectoryName(executablePath)!;

        var (script, extension) = OperatingSystem.IsWindows()
            ? (BuildWindowsScript(preparedPath, installDirectory, processId, Path.GetFileName(executablePath)), ".cmd")
            : OperatingSystem.IsMacOS()
                ? (BuildMacOsScript(preparedPath, ResolveAppBundlePath(executablePath), processId), ".sh")
                : (BuildLinuxScript(preparedPath, installDirectory, processId), ".sh");

        var scriptPath = Path.Combine(Path.GetTempPath(), "km2-apply-update-" + Guid.NewGuid().ToString("N")[..8] + extension);
        File.WriteAllText(scriptPath, script);

        // 分离启动:脚本要活过当前进程,所以不能用"父进程等子进程"的普通方式
        var startInfo = OperatingSystem.IsWindows()
            ? new ProcessStartInfo { FileName = "cmd.exe", Arguments = $"/c \"{scriptPath}\"", UseShellExecute = false, CreateNoWindow = true }
            : new ProcessStartInfo { FileName = "/bin/sh", Arguments = $"\"{scriptPath}\"", UseShellExecute = false };

        Process.Start(startInfo);
        AppLog.Write($"[UpdateInstaller] 已启动替换脚本 {scriptPath}(等待 PID {processId} 退出)");
        return scriptPath;
    }

    /// <summary>
    /// 从可执行文件路径反推 <c>.app</c> 包路径(<c>/Applications/Kindle Mate 2.app/Contents/MacOS/…</c>)。
    /// 不在 .app 里(开发期直接跑 dll)时抛异常 —— 那种情况下"自动替换"本来就不适用。
    /// </summary>
    internal static string ResolveAppBundlePath(string executablePath) {
        var directory = new DirectoryInfo(Path.GetDirectoryName(executablePath)!);
        while (directory is not null) {
            if (directory.Name.EndsWith(".app", StringComparison.OrdinalIgnoreCase)) {
                return directory.FullName;
            }
            directory = directory.Parent;
        }

        throw new InvalidOperationException("当前不是从 .app 包启动的,无法自动替换");
    }

    /// <summary>
    /// macOS:等进程退出 → 挂载 dmg → **整包替换** → 去隔离标记 → 卸下并重启。
    /// 整包替换是 macOS 的标准做法(<c>.app</c> 自带全部内容),顺序必须是先移除旧包再拷新的。
    /// </summary>
    internal static string BuildMacOsScript(string dmgPath, string appPath, int processId) => $"""
        #!/bin/bash
        # Kindle Mate 2 自动更新脚本(由应用生成)。等旧进程退出后再动手 —— 否则等于拆掉自己脚下的地板。
        while kill -0 {processId} 2>/dev/null; do sleep 0.3; done

        mount="$(mktemp -d)"
        hdiutil attach "{dmgPath}" -nobrowse -readonly -mountpoint "$mount" >/dev/null || exit 1

        rm -rf "{appPath}"
        cp -R "$mount/KindleMate2.app" "{appPath}" || exit 1

        # 下载来的包带隔离标记,不清掉会被 Gatekeeper 拦下
        xattr -dr com.apple.quarantine "{appPath}" 2>/dev/null || true

        hdiutil detach "$mount" >/dev/null 2>&1 || true
        rmdir "$mount" 2>/dev/null || true

        open "{appPath}"
        """;

    /// <summary>
    /// Linux:等进程退出 → **合并覆盖**安装目录 → 重新启动。
    /// 刻意不 <c>rm -rf</c> 安装目录:那是用户自己解压出来的位置,不归我们管;
    /// 合并覆盖最多留下几个旧文件,远比误删用户目录安全。
    /// </summary>
    internal static string BuildLinuxScript(string preparedDirectory, string installDirectory, int processId,
        string launcherName = "kindlemate2") => $"""
        #!/bin/bash
        # Kindle Mate 2 自动更新脚本(由应用生成)。
        while kill -0 {processId} 2>/dev/null; do sleep 0.3; done

        cp -R "{preparedDirectory}"/. "{installDirectory}/" || exit 1
        cd "{installDirectory}" && ./{launcherName} &
        """;

    /// <summary>
    /// Windows:等进程退出 → 覆盖安装目录 → 重新启动。
    /// 必须等进程真的退出:运行中的 exe 被占用,直接覆盖会失败(这正是不能自己更新自己的原因)。
    /// </summary>
    internal static string BuildWindowsScript(string preparedDirectory, string installDirectory, int processId,
        string exeName = "KindleMate2.exe") => $"""
        @echo off
        rem Kindle Mate 2 自动更新脚本(由应用生成)
        :waitloop
        tasklist /FI "PID eq {processId}" 2>NUL | find "{processId}" >NUL
        if not errorlevel 1 (
            timeout /t 1 /nobreak >NUL
            goto waitloop
        )

        xcopy /E /Y /I "{preparedDirectory}" "{installDirectory}" >NUL || exit /b 1
        start "" "{Path.Combine(installDirectory, exeName)}"
        """;
}
