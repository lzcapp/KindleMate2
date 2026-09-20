using System.Text.Json;
using Xunit;
using KindleMate2.Application.Services;

namespace KindleMate2.Tests;

/// <summary>
/// 检查更新的用例 —— 全部不碰网络:版本比较与资产匹配都是纯函数,用固定样本喂进去即可。
/// (真·联网那一条在 <c>UpdateProbeTests</c> 里,由人手动触发,免得 CI 依赖外网。)
/// </summary>
public sealed class UpdateCheckerTests {
    /// <summary>一份贴近真实的 latest release 响应(字段名与 GitHub 一致)。</summary>
    private const string SampleRelease = """
        {
          "tag_name": "2026.09.17",
          "html_url": "https://github.com/lzcapp/KindleMate2/releases/tag/2026.09.17",
          "assets": [
            { "name": "KindleMate2_macos-arm64.dmg", "browser_download_url": "https://example.test/macos-arm64.dmg", "size": 123456 },
            { "name": "KindleMate2_macos-x64.dmg",   "browser_download_url": "https://example.test/macos-x64.dmg",   "size": 123456 },
            { "name": "KindleMate2_linux-x64.tar.gz","browser_download_url": "https://example.test/linux-x64.tar.gz","size": 654321 },
            { "name": "KindleMate2_linux-arm64.tar.gz","browser_download_url": "https://example.test/linux-arm64.tar.gz","size": 654321 },
            { "name": "KindleMate2_x64_runtime.zip", "browser_download_url": "https://example.test/x64_runtime.zip","size": 999 },
            { "name": "KindleMate2_x64.zip",         "browser_download_url": "https://example.test/x64.zip",         "size": 888 }
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
    [InlineData("linux-x64", "KindleMate2_linux-x64.tar.gz")]
    [InlineData("linux-arm64", "KindleMate2_linux-arm64.tar.gz")]
    public void ParseRelease_FindsAssetOnEveryPlatform(string rid, string expectedName) {
        var info = UpdateChecker.ParseRelease(SampleRelease, "2026.9.16", rid);

        Assert.Equal(expectedName, info!.Asset!.Name);
    }

    [Fact]
    public void ParseRelease_PrefersSelfContainedPackageOnWindows() {
        var info = UpdateChecker.ParseRelease(SampleRelease, "2026.9.16", "win-x64");

        // 两个都在时优先自包含包:与本项目随包分发的方式一致,换包后不必再装 .NET 运行时
        Assert.Equal("KindleMate2_x64_runtime.zip", info!.Asset!.Name);
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
