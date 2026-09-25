using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using KindleMate2.Shared.Diagnostics;

namespace KindleMate2.Application.Services;

/// <summary>发布页上的一个下载资产。</summary>
/// <param name="ChecksumUrl">同一发布页上 <c>SHA256SUMS</c> 资产的下载地址(旧发布可能没有,此时为 null)。</param>
public sealed record UpdateAsset(string Name, string DownloadUrl, long Size, string? ChecksumUrl = null);

/// <summary>一次「有新版本」的结果。</summary>
/// <param name="Version">发布页上的版本(取自 tag,形如 <c>2026.09.17</c>)。</param>
/// <param name="ReleaseUrl">发布页地址(用户想看说明时用)。</param>
/// <param name="Asset">与当前平台匹配的下载资产;找不到时为 null(仍可提示用户去发布页手动下载)。</param>
public sealed record UpdateInfo(string Version, string ReleaseUrl, UpdateAsset? Asset);

/// <summary>
/// 检查是否有新版本 —— 数据源是本仓库的 GitHub Releases(latest)。
///
/// 只负责「检查」:下载与替换在 <see cref="UpdateInstaller"/>。这样拆开是因为两者的可测性完全不同 ——
/// 版本比较与资产匹配是纯函数(可以穷举单测),而替换正在运行的程序只能在真机上验。
/// </summary>
public static class UpdateChecker {
    /// <summary>latest release 的 API 地址。</summary>
    public const string ReleasesApiUrl = "https://api.github.com/repos/lzcapp/KindleMate2/releases/latest";

    /// <summary>GitHub 的 API 要求带 User-Agent,否则直接 403。</summary>
    private const string UserAgent = "KindleMate2-UpdateChecker";

    /// <summary>检查超时。检查更新是"顺手看一眼",不该让用户等太久。</summary>
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(20);

    /// <summary>
    /// 查最新版本。返回 null 表示**没有更新或无法判断** —— 网络不通、被限流、JSON 结构变化都归为这一类,
    /// 因为"检查更新失败"不该拦着用户用软件:细节写日志,界面按"已是最新"处理。
    /// </summary>
    /// <param name="currentVersion">当前版本(程序集版本即可,如 <c>2026.9.17.0</c>)。</param>
    /// <param name="runtimeIdentifier">当前运行时标识,默认 <see cref="RuntimeInformation.RuntimeIdentifier"/>。</param>
    public static async Task<UpdateInfo?> CheckAsync(string currentVersion,
        string? runtimeIdentifier = null, HttpClient? httpClient = null, CancellationToken cancellationToken = default) {
        var rid = runtimeIdentifier ?? RuntimeInformation.RuntimeIdentifier;

        try {
            using var owned = httpClient is null ? CreateHttpClient() : null;
            var client = httpClient ?? owned!;

            using var response = await client.GetAsync(ReleasesApiUrl, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) {
                AppLog.Write($"[UpdateChecker] 查询失败:HTTP {(int)response.StatusCode}");
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return ParseRelease(json, currentVersion, rid);
        } catch (Exception ex) {
            // 网络不通 / DNS / 超时 / JSON 结构变了 —— 一律当作"这次没查到"
            AppLog.Write($"[UpdateChecker] 检查更新出错:{ex.Message}");
            return null;
        }
    }

    /// <summary>从 latest release 的 JSON 得出结论。拆出来是为了能用固定样本单测(不碰网络)。</summary>
    public static UpdateInfo? ParseRelease(string releaseJson, string currentVersion, string runtimeIdentifier) {
        using var document = JsonDocument.Parse(releaseJson);
        var root = document.RootElement;

        var tag = root.TryGetProperty("tag_name", out var tagElement) ? tagElement.GetString() : null;
        if (string.IsNullOrWhiteSpace(tag)) {
            AppLog.Write("[UpdateChecker] 响应里没有 tag_name");
            return null;
        }

        if (Compare(tag!, currentVersion) <= 0) {
            return null;   // 不比当前新(含相同)
        }

        var releaseUrl = root.TryGetProperty("html_url", out var urlElement)
            ? urlElement.GetString() ?? string.Empty
            : string.Empty;

        return new UpdateInfo(Normalize(tag!), releaseUrl, FindAsset(root, runtimeIdentifier));
    }

    /// <summary>
    /// 按运行时标识挑出对应的发布资产(见 <see cref="AssetNameCandidates"/>)。
    /// </summary>
    public static UpdateAsset? FindAsset(JsonElement releaseRoot, string runtimeIdentifier) {
        if (!releaseRoot.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array) {
            return null;
        }

        var wanted = AssetNameCandidates(runtimeIdentifier);
        if (wanted.Count == 0) {
            AppLog.Write($"[UpdateChecker] 不认识的运行时标识:{runtimeIdentifier}");
            return null;
        }

        var available = new List<UpdateAsset>();
        foreach (var asset in assets.EnumerateArray()) {
            var name = asset.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
            var url = asset.TryGetProperty("browser_download_url", out var urlElement) ? urlElement.GetString() : null;
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(url)) {
                continue;
            }

            var size = asset.TryGetProperty("size", out var sizeElement) && sizeElement.TryGetInt64(out var value) ? value : 0;
            available.Add(new UpdateAsset(name!, url!, size));
        }

        // SHA256SUMS 是发布流程附带生成的校验和清单(见 release.yml);旧发布没有该资产时为 null。
        var checksumUrl = available
            .FirstOrDefault(a => string.Equals(a.Name, "SHA256SUMS", StringComparison.OrdinalIgnoreCase))
            ?.DownloadUrl;

        foreach (var candidate in wanted) {
            var match = available.FirstOrDefault(a => string.Equals(a.Name, candidate, StringComparison.OrdinalIgnoreCase));
            if (match is not null) {
                return match with { ChecksumUrl = checksumUrl };
            }
        }

        return null;
    }

    /// <summary>
    /// 运行时标识 → 可能的资产名,按优先级排列。
    ///
    /// 产物命名见 release.yml:<c>KindleMate2_macos-arm64.dmg</c>、<c>KindleMate2_linux-x64[_runtime].tar.gz</c>、
    /// <c>KindleMate2_win-x64[_runtime].zip</c>(Windows)。macOS 只发自包含包。
    ///
    /// **凡是同时存在 <c>_runtime</c>(自包含)与无后缀(框架依赖)两种变体的平台,一律优先自包含。**
    /// 理由是两种选错的代价完全不对称:自包含包在任何机器上都能跑,而框架依赖包在没装对应 .NET
    /// 运行时的机器上**装完就打不开** —— "更新把能用的软件换成打不开的"是这个功能最不能犯的错。
    /// 反过来说,把框架依赖的用户换成自包含只多下载几十 MB,不影响可用性。
    ///
    /// (2026-09-21 修:Linux 分支原先只列了无后缀名,而线上确实发 <c>KindleMate2_linux-x64_runtime.tar.gz</c>
    /// / <c>linux-arm64_runtime.tar.gz</c> —— 于是从自包含包安装的 Linux 用户会被换成框架依赖包。)
    ///
    /// Windows 自本版起资产名加了 <c>win-</c> 前缀(与 <c>linux-</c>/<c>macos-</c> 对齐)。候选里**同时保留旧名**
    /// (<c>KindleMate2_x64[_runtime].zip</c>):新版本能继续认领改名之前发布的资产。反过来,
    /// 改名之前安装的旧版本认不出新名,会退化成"提示有新版本 + 给发布页链接"(仍可手动更新)。
    /// </summary>
    internal static IReadOnlyList<string> AssetNameCandidates(string runtimeIdentifier) =>
        NormalizeRuntimeIdentifier(runtimeIdentifier) switch {
            "osx-arm64" => ["KindleMate2_macos-arm64.dmg"],
            "osx-x64" => ["KindleMate2_macos-x64.dmg"],
            "linux-x64" => ["KindleMate2_linux-x64_runtime.tar.gz", "KindleMate2_linux-x64.tar.gz"],
            "linux-arm64" => ["KindleMate2_linux-arm64_runtime.tar.gz", "KindleMate2_linux-arm64.tar.gz"],
            "win-x64" => ["KindleMate2_win-x64_runtime.zip", "KindleMate2_win-x64.zip", "KindleMate2_x64_runtime.zip", "KindleMate2_x64.zip"],
            "win-arm64" => ["KindleMate2_win-arm64_runtime.zip", "KindleMate2_win-arm64.zip", "KindleMate2_arm64_runtime.zip", "KindleMate2_arm64.zip"],
            "win-x86" => ["KindleMate2_win-x86_runtime.zip", "KindleMate2_win-x86.zip", "KindleMate2_x86_runtime.zip", "KindleMate2_x86.zip"],
            _ => [],
        };

    /// <summary>
    /// 把实际的 RID 归一成「平台-架构」两段。<c>RuntimeInformation.RuntimeIdentifier</c> 可能带
    /// 版本或 libc 变体(<c>osx.12-arm64</c>、<c>linux-musl-x64</c>),而发布产物只有固定的几种名字。
    /// </summary>
    internal static string NormalizeRuntimeIdentifier(string runtimeIdentifier) {
        var parts = runtimeIdentifier.Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) {
            return string.Empty;
        }

        var platform = parts[0].Split('.')[0].ToLowerInvariant();   // osx.12 → osx
        if (parts.Length == 1) {
            return platform;
        }

        return $"{platform}-{parts[^1].ToLowerInvariant()}";
    }

    /// <summary>
    /// 按数字段比较版本,返回正数表示 <paramref name="left"/> 更新。
    ///
    /// 版本形如 <c>2026.09.17</c>(tag)或 <c>2026.9.17.0</c>(程序集):段数不同时缺段按 0 处理,
    /// 前导零不影响比较 —— 所以这两者会被判定为**同一个版本**。这点很关键,否则每次检查都会
    /// 反复"发现有新版本"。
    /// </summary>
    public static int Compare(string left, string right) {
        var a = ParseSegments(left);
        var b = ParseSegments(right);
        var length = Math.Max(a.Count, b.Count);

        for (var i = 0; i < length; i++) {
            var x = i < a.Count ? a[i] : 0;
            var y = i < b.Count ? b[i] : 0;
            if (x != y) {
                return x.CompareTo(y);
            }
        }

        return 0;
    }

    /// <summary>去掉 <c>v</c> 前缀并 trim,便于比较与显示。</summary>
    internal static string Normalize(string version) => version.Trim().TrimStart('v', 'V').Trim();

    private static List<long> ParseSegments(string version) {
        var segments = new List<long>();
        foreach (var part in Normalize(version).Split('.', '-', '+')) {
            // 非数字段(如 2026.09.17-beta 的 beta)记 0:预发布不该被当成正式更新
            segments.Add(long.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0);
        }
        return segments;
    }

    private static HttpClient CreateHttpClient() {
        var client = new HttpClient { Timeout = RequestTimeout };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        return client;
    }
}
