using System.Text.Json;
using Xunit;
using KindleMate2.Application.Services;

namespace KindleMate2.Tests;

/// <summary>
/// 检查更新的用例 —— 全部不碰网络:版本比较与资产匹配都是纯函数,用固定样本喂进去即可。
/// (真·联网那一条在 <c>UpdateProbeTests</c> 里,由人手动触发,免得 CI 依赖外网。)
/// </summary>
public sealed class UpdateCheckerTests {
    /// <summary>
    /// 一份贴近真实的 latest release 响应(字段名与 GitHub 一致)。
    /// **资产清单刻意与实际发布页一一对应**(12 个,见 release.yml 文件头的命名规则):
    /// 少列一个变体,下面那些"优先取自包含"的用例就会变成空断言。
    /// </summary>
    private const string SampleRelease = """
        {
          "tag_name": "2026.09.17",
          "html_url": "https://github.com/lzcapp/KindleMate2/releases/tag/2026.09.17",
          "assets": [
            { "name": "KindleMate2_macos-arm64.dmg",             "browser_download_url": "https://example.test/macos-arm64.dmg",         "size": 53617032 },
            { "name": "KindleMate2_macos-x64.dmg",               "browser_download_url": "https://example.test/macos-x64.dmg",           "size": 53617032 },
            { "name": "KindleMate2_linux-x64.tar.gz",            "browser_download_url": "https://example.test/linux-x64.tar.gz",        "size": 12000000 },
            { "name": "KindleMate2_linux-x64_runtime.tar.gz",    "browser_download_url": "https://example.test/linux-x64_runtime.tar.gz","size": 78000000 },
            { "name": "KindleMate2_linux-arm64.tar.gz",          "browser_download_url": "https://example.test/linux-arm64.tar.gz",      "size": 12000000 },
            { "name": "KindleMate2_linux-arm64_runtime.tar.gz",  "browser_download_url": "https://example.test/linux-arm64_runtime.tar.gz","size": 78000000 },
            { "name": "KindleMate2_win-x64.zip",                     "browser_download_url": "https://example.test/x64.zip",                 "size": 888 },
            { "name": "KindleMate2_win-x64_runtime.zip",             "browser_download_url": "https://example.test/x64_runtime.zip",         "size": 999 },
            { "name": "KindleMate2_win-arm64.zip",                   "browser_download_url": "https://example.test/arm64.zip",               "size": 888 },
            { "name": "KindleMate2_win-arm64_runtime.zip",           "browser_download_url": "https://example.test/arm64_runtime.zip",       "size": 999 },
            { "name": "KindleMate2_win-x86.zip",                     "browser_download_url": "https://example.test/x86.zip",                 "size": 888 },
            { "name": "KindleMate2_win-x86_runtime.zip",             "browser_download_url": "https://example.test/x86_runtime.zip",         "size": 999 }
          ]
        }
        """;

    // ————————————————————— 版本比较 —————————————————————

    [Fact]
    public void Compare_TreatsTagAndAssemblyVersionOfTheSameReleaseAsEqual() {
        // 这是最关键的一条:tag 写 2026.09.17、程序集版本是 2026.9.17.0 ——
        // 若把两者判成不同,用户每次点"检查更新"都会被告知有新版本(自己更新自己)。
        Assert.Equal(0, UpdateChecker.Compare("2026.09.17", "2026.9.17.0"));
        Assert.Equal(0, UpdateChecker.Compare("v2026.9.17", "2026.09.17"));
        Assert.Equal(0, UpdateChecker.Compare("2026.9.17.0", "2026.9.17"));
    }

    [Theory]
    [InlineData("2026.09.18", "2026.9.17.0", 1)]    // 新版
    [InlineData("2026.9.17", "2026.9.18", -1)]      // 旧版
    [InlineData("2027.1.1", "2026.12.31", 1)]       // 跨年
    [InlineData("2026.10.1", "2026.9.30", 1)]       // 月份十进制比较,不是字符串比较
    public void Compare_OrdersByNumericSegments(string left, string right, int expectedSign) {
        Assert.Equal(expectedSign, Math.Sign(UpdateChecker.Compare(left, right)));
    }

    [Fact]
    public void ParseRelease_ReturnsNullWhenNotNewer() {
        Assert.Null(UpdateChecker.ParseRelease(SampleRelease, "2026.9.17.0", "osx-arm64"));   // 同一个版本
        Assert.Null(UpdateChecker.ParseRelease(SampleRelease, "2026.9.18", "osx-arm64"));    // 比发布页还新
    }

    [Fact]
    public void ParseRelease_PicksTheAssetForThisPlatform() {
        var info = UpdateChecker.ParseRelease(SampleRelease, "2026.9.16", "osx-arm64");

        Assert.NotNull(info);
        Assert.Equal("2026.09.17", info!.Version);
        Assert.Contains("releases/tag/2026.09.17", info.ReleaseUrl);
        Assert.Equal("KindleMate2_macos-arm64.dmg", info.Asset!.Name);
        Assert.Equal("https://example.test/macos-arm64.dmg", info.Asset.DownloadUrl);
        // 本样本没有 SHA256SUMS 资产 → 校验和地址为 null(旧发布,更新时跳过校验)
        Assert.Null(info.Asset.ChecksumUrl);
    }

    [Fact]
    public void ParseRelease_AttachesSha256SumsUrlToTheChosenAsset() {
        const string json = """
            {
              "tag_name": "2026.09.17",
              "assets": [
                { "name": "KindleMate2_win-x64_runtime.zip", "browser_download_url": "https://example.test/x64_runtime.zip", "size": 1 },
                { "name": "SHA256SUMS", "browser_download_url": "https://example.test/SHA256SUMS", "size": 2 }
              ]
            }
            """;

        var info = UpdateChecker.ParseRelease(json, "2026.9.16", "win-x64");

        Assert.Equal("https://example.test/SHA256SUMS", info!.Asset!.ChecksumUrl);
    }

    [Fact]
    public void ParseRelease_ReturnsNullWhenTagMissing() {
        Assert.Null(UpdateChecker.ParseRelease("""{ "assets": [] }""", "2026.9.16", "osx-arm64"));
    }

    // ————————————————————— 运行时标识 → 资产 —————————————————————

    [Theory]
    [InlineData("osx-arm64", "osx-arm64")]
    [InlineData("osx.12-arm64", "osx-arm64")]        // 带系统版本的形式
    [InlineData("linux-musl-x64", "linux-x64")]      // 带 libc 变体
    [InlineData("win-x64", "win-x64")]
    [InlineData("linux", "linux")]                   // 只有平台段
    public void NormalizeRuntimeIdentifier_KeepsPlatformAndArchitecture(string rid, string expected) {
        Assert.Equal(expected, UpdateChecker.NormalizeRuntimeIdentifier(rid));
    }

    [Theory]
    [InlineData("osx-arm64", "KindleMate2_macos-arm64.dmg")]
    [InlineData("osx-x64", "KindleMate2_macos-x64.dmg")]
    [InlineData("linux-x64", "KindleMate2_linux-x64_runtime.tar.gz")]
    [InlineData("linux-arm64", "KindleMate2_linux-arm64_runtime.tar.gz")]
    [InlineData("win-x64", "KindleMate2_win-x64_runtime.zip")]
    [InlineData("win-arm64", "KindleMate2_win-arm64_runtime.zip")]
    [InlineData("win-x86", "KindleMate2_win-x86_runtime.zip")]
    public void ParseRelease_SelectsTheRightAssetForEveryPlatform(string rid, string expectedName) {
        var info = UpdateChecker.ParseRelease(SampleRelease, "2026.9.16", rid);

        Assert.Equal(expectedName, info!.Asset!.Name);
    }

    /// <summary>
    /// 发布页同时提供自包含(<c>_runtime</c>)与框架依赖两种变体时,必须选**自包含**那个。
    ///
    /// 两种选错的代价不对称:自包含包在任何机器上都能跑,而框架依赖包在没装对应 .NET 运行时的
    /// 机器上**更新完就打不开** —— "更新把能用的软件换成打不开的"是这个功能最不能犯的错。
    /// macOS 只发自包含 dmg,故不在本用例内(没有可比的两种变体)。
    ///
    /// 2026-09-21 补:Linux 分支原先只列了无后缀名,而线上确实发
    /// <c>KindleMate2_linux-x64_runtime.tar.gz</c> / <c>linux-arm64_runtime.tar.gz</c>,
    /// 于是从自包含包装上去的 Linux 用户会被换成框架依赖包。这条用例就是它的回归防线。
    /// </summary>
    [Theory]
    [InlineData("linux-x64", "KindleMate2_linux-x64_runtime.tar.gz", "KindleMate2_linux-x64.tar.gz")]
    [InlineData("linux-arm64", "KindleMate2_linux-arm64_runtime.tar.gz", "KindleMate2_linux-arm64.tar.gz")]
    [InlineData("win-x64", "KindleMate2_win-x64_runtime.zip", "KindleMate2_win-x64.zip")]
    [InlineData("win-arm64", "KindleMate2_win-arm64_runtime.zip", "KindleMate2_win-arm64.zip")]
    [InlineData("win-x86", "KindleMate2_win-x86_runtime.zip", "KindleMate2_win-x86.zip")]
    public void ParseRelease_PrefersSelfContainedOverFrameworkDependent(string rid, string selfContained, string frameworkDependent) {
        var info = UpdateChecker.ParseRelease(SampleRelease, "2026.9.16", rid);

        Assert.Equal(selfContained, info!.Asset!.Name);
        Assert.NotEqual(frameworkDependent, info.Asset.Name);
    }

    /// <summary>
    /// 候选表必须**同时**列出自包含与框架依赖两个变体:前者是首选,后者是"某次只发了框架依赖包"
    /// 时的回退。只断言"最后选出来的那个对"抓不住候选表漏项,这条从表本身再钉一次。
    /// </summary>
    [Theory]
    [InlineData("linux-x64", "KindleMate2_linux-x64_runtime.tar.gz", "KindleMate2_linux-x64.tar.gz")]
    [InlineData("linux-arm64", "KindleMate2_linux-arm64_runtime.tar.gz", "KindleMate2_linux-arm64.tar.gz")]
    [InlineData("win-x64", "KindleMate2_win-x64_runtime.zip", "KindleMate2_win-x64.zip")]
    [InlineData("win-arm64", "KindleMate2_win-arm64_runtime.zip", "KindleMate2_win-arm64.zip")]
    [InlineData("win-x86", "KindleMate2_win-x86_runtime.zip", "KindleMate2_win-x86.zip")]
    public void AssetNameCandidates_ListBothVariantsWithSelfContainedFirst(string rid, string selfContained, string frameworkDependent) {
        var candidates = UpdateChecker.AssetNameCandidates(rid);

        Assert.Equal(selfContained, candidates[0]);
        Assert.Contains(frameworkDependent, candidates);
    }

    /// <summary>
    /// Windows 资产自本版起加了 <c>win-</c> 前缀(与 linux-/macos- 对齐)。候选里必须**同时保留旧名**,
    /// 否则新版本认不出「改名之前」发布的 Release 资产;反之旧版本认不出新名(退化为给发布页链接)。
    /// </summary>
    [Theory]
    [InlineData("win-x64", "KindleMate2_x64_runtime.zip", "KindleMate2_x64.zip")]
    [InlineData("win-arm64", "KindleMate2_arm64_runtime.zip", "KindleMate2_arm64.zip")]
    [InlineData("win-x86", "KindleMate2_x86_runtime.zip", "KindleMate2_x86.zip")]
    public void AssetNameCandidates_KeepLegacyWindowsNamesAsFallback(string rid, string legacyRuntime, string legacyFramework) {
        var candidates = UpdateChecker.AssetNameCandidates(rid);

        Assert.StartsWith("KindleMate2_win-", candidates[0]);   // 新名优先
        Assert.Contains(legacyRuntime, candidates);
        Assert.Contains(legacyFramework, candidates);
    }

    [Fact]
    public void ParseRelease_StillResolvesLegacyNamedWindowsAsset() {
        // 改名之前发布的 Release 资产仍是旧名,新版本必须还能选中它。
        const string legacyRelease = """
            {
              "tag_name": "2026.09.17",
              "assets": [
                { "name": "KindleMate2_x64_runtime.zip", "browser_download_url": "https://example.test/x64_runtime.zip", "size": 1 }
              ]
            }
            """;

        var info = UpdateChecker.ParseRelease(legacyRelease, "2026.9.16", "win-x64");

        Assert.Equal("KindleMate2_x64_runtime.zip", info!.Asset!.Name);
    }

    [Fact]
    public void AssetNameCandidates_UnknownRuntimeIdentifierHasNoCandidates() {
        // 不认识的运行时不给候选 —— 调用方据此只提示"有新版本"、不提供自动下载
        Assert.Empty(UpdateChecker.AssetNameCandidates("freebsd-x64"));
    }

    [Fact]
    public void ParseRelease_WithoutMatchingAsset_StillReportsTheVersion() {
        // 平台没有对应资产(如某次只发了部分平台)时,仍应告诉用户有新版本 —— 只是没有自动下载的入口
        var info = UpdateChecker.ParseRelease(SampleRelease, "2026.9.16", "freebsd-x64");

        Assert.NotNull(info);
        Assert.Null(info!.Asset);
    }

    [Fact]
    public void FindAsset_IgnoresAssetsWithMissingFields() {
        const string json = """
            {
              "tag_name": "2026.09.17",
              "assets": [
                { "name": "KindleMate2_macos-arm64.dmg" },
                { "browser_download_url": "https://example.test/x.dmg" },
                { "name": "KindleMate2_macos-arm64.dmg", "browser_download_url": "https://example.test/ok.dmg", "size": 42 }
              ]
            }
            """;

        using var document = JsonDocument.Parse(json);
        var asset = UpdateChecker.FindAsset(document.RootElement, "osx-arm64");

        Assert.Equal("https://example.test/ok.dmg", asset!.DownloadUrl);
        Assert.Equal(42, asset.Size);
    }
}
