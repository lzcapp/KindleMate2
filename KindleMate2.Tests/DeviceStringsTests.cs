using System.Globalization;
using Xunit;
using KindleMate2.Shared;

namespace KindleMate2.Tests;

/// <summary>
/// 设备相关提示文案的多语言资源用例。
///
/// 为什么专门测这个:这些字符串要**同时**改 4 个 .resx 与手工维护的 Strings.Designer.cs ——
/// 漏一处的后果是"编译通过、运行时拿到空串"(Designer 有属性但 resx 没条目),
/// 用户看到的就是一个空对话框。这类错误只能靠断言资源真能取到来兜住。
/// </summary>
public sealed class DeviceStringsTests {
    private static readonly string[] DeviceStringKeys = [
        "Device_Mtp_Connect_Failed",
        "Device_Clippings_Not_Found",
        "Device_Clippings_Read_Failed",
        "Device_Mtp_Sync_Failed",
    ];

    [Theory]
    [InlineData("zh-Hans")]
    [InlineData("zh-Hant")]
    [InlineData("en")]
    public void DeviceStrings_ResolveInEverySupportedLanguage(string cultureName) {
        var culture = new CultureInfo(cultureName);

        foreach (var key in DeviceStringKeys) {
            var value = Strings.ResourceManager.GetString(key, culture);
            Assert.False(string.IsNullOrWhiteSpace(value), $"{cultureName} 下 {key} 取不到");
        }
    }

    [Theory]
    [InlineData("zh-Hans")]
    [InlineData("zh-Hant")]
    [InlineData("en")]
    public void DeviceStrings_ArePresentInEachLanguageFile_NotInheritedFromNeutral(string cultureName) {
        // 这一条才是真正管用的:ResourceManager.GetString 在**该语言文件缺条目时会回退到
        // neutral(Strings.resx)**,于是"忘了往 zh-Hant 里加"会照样返回一个非空字符串 ——
        // 上面那条断言因此抓不到漏翻译。要精确判断"这个语言文件里到底有没有",
        // 得直接拿它自己的资源集,并且 tryParents: false 关掉回退。
        var culture = new CultureInfo(cultureName);
        // 注意**不能** using/Dispose:GetResourceSet 返回的是 ResourceManager 内部缓存的
        // 同一个实例,释放它会把这个 culture 的缓存关掉 —— 之后任何 GetString 都抛
        // ObjectDisposedException,而且失败会扩散到同进程里的其他用例(踩过)。
        var resourceSet = Strings.ResourceManager.GetResourceSet(culture, createIfNotExists: true, tryParents: false);

        Assert.NotNull(resourceSet);
        foreach (var key in DeviceStringKeys) {
            Assert.True(resourceSet!.GetString(key) is not null, $"{cultureName} 的语言文件里没有 {key}(会静默回退到 neutral)");
        }
    }

    [Fact]
    public void MtpConnectFailedHint_MentionsAllThreeLikelyCauses() {
        // libmtp 无法区分"没插 / 没授权 / 被别的程序占用",所以文案必须把三种可能都说清楚 ——
        // 只写"连接失败"会让用户在三者之间瞎试(真机上一路踩过来的)。
        var zh = Strings.ResourceManager.GetString("Device_Mtp_Connect_Failed", new CultureInfo("zh-Hans"))!;

        Assert.Contains("数据线", zh);
        Assert.Contains("允许", zh);          // macOS 的「允许配件连接?」
        Assert.Contains("MTP", zh);           // 被别的 MTP 客户端占用的可能

        var en = Strings.ResourceManager.GetString("Device_Mtp_Connect_Failed", new CultureInfo("en"))!;
        Assert.Contains("Allow", en);
        Assert.Contains("MTP", en);
    }

    [Fact]
    public void ClippingsReadFailed_KeepsItsFormatPlaceholder() {
        // 这条要带 {0}(文件名),翻译时丢了占位符会抛 FormatException
        foreach (var cultureName in new[] { "zh-Hans", "zh-Hant", "en" }) {
            var value = Strings.ResourceManager.GetString("Device_Clippings_Read_Failed", new CultureInfo(cultureName))!;
            Assert.Contains("{0}", value);
        }
    }
}
