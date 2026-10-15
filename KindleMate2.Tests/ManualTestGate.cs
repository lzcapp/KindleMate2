using Xunit.Abstractions;

namespace KindleMate2.Tests;

/// <summary>
/// 「探测类」用例(真机 / 真联网)的手动开关。默认**不执行**,避免:
/// <list type="bullet">
///   <item>随手 <c>dotnet test</c> 时,插着 Kindle 的机器上真机用例去删写设备上的
///     <c>My Clippings.txt</c>(<c>Probe_MtpWriteBack_RoundTripsByteForByte</c>);</item>
///   <item>CI 依赖外网、被限流或代理不可达时莫名变红。</item>
/// </list>
/// 需要跑时设置对应环境变量为 <c>1</c>。这些类同时带 <c>[Trait("Category","Manual")]</c>,
/// CI 用 <c>--filter "Category!=Manual"</c> 显式排除。
///
/// <para>
/// ⚠️ <b>措辞必须精确:未设环境变量时,这些用例的报告状态是 PASSED(通过),不是 SKIPPED(跳过)。</b>
/// 原因:xUnit.net <b>2.9.3 不支持运行期动态跳过</b> —— 已实测,抛
/// <c>Xunit.Sdk.SkipException.ForSkip(...)</c> 会被运行器记为 <b>失败</b>(消息形如
/// <c>$XunitDynamicSkip$…</c>),而 <c>SkipException</c> 也没有可用的公开构造/静态入口可供"跳过"。
/// 真正支持动态跳过的是 xUnit v3 的 <c>Assert.Skip</c>(本项目尚未升级)。
/// 因此这里只能保留"打印说明 + 早退",并**如实**向调用方说明它会被记成通过 ——
/// 所谓"默认全部跳过"仅指"默认不产生任何真机/外网副作用",不代表测试报告里显示 skipped。
/// </para>
/// <para>
/// 为了不让"假绿"骗过 CI,<c>build.yml</c> 里另有一条断言:统计
/// <c>--filter "Category=Manual"</c> 的用例数必须 &gt; 0,且"不带 filter 的用例数 = 带
/// <c>Category!=Manual</c> 的用例数 + Manual 用例数"。这样若有人误删 Trait、
/// 或把 Manual 用例挪出过滤范围,CI 会直接报错,而不是悄悄多跑几条真机用例。
/// </para>
/// </summary>
internal static class ManualTestGate {
    private const string DeviceEnvVar = "KM2_MANUAL_DEVICE_TESTS";
    private const string NetworkEnvVar = "KM2_MANUAL_NETWORK_TESTS";

    /// <summary>真机用例是否启用。未启用时打印说明并返回 false,调用方直接 return(报告状态为 PASSED,见类注释)。</summary>
    public static bool RequireDevice(ITestOutputHelper output) =>
        Require(output, DeviceEnvVar, "真机探测(会读写连接的 Kindle)");

    /// <summary>真机用例是否启用;<paramref name="what"/> 用于把"跳过的是什么"写清楚。</summary>
    public static bool RequireDevice(ITestOutputHelper output, string what) =>
        Require(output, DeviceEnvVar, what);

    /// <summary>联网用例是否启用。</summary>
    public static bool RequireNetwork(ITestOutputHelper output) =>
        Require(output, NetworkEnvVar, "真实 GitHub Releases 探测");

    private static bool Require(ITestOutputHelper output, string envVar, string what) {
        if (Environment.GetEnvironmentVariable(envVar) == "1") {
            return true;
        }
        output.WriteLine($"跳过:{what}默认不跑。手动运行请设 {envVar}=1。" +
                         "(注意:xUnit 2.x 无动态跳过,本用例会被记为通过而非跳过。)");
        return false;
    }
}
