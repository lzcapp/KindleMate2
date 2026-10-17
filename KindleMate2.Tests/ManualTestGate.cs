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
/// 为了不让"假绿"骗过 CI,有两层防护:
/// <list type="number">
///   <item>运行期:<c>build.yml</c> 断言 <c>Category=Manual</c> 用例数 &gt; 0,且"不带 filter 的
///     用例数 = 带 <c>Category!=Manual</c> 的用例数 + Manual 用例数" —— 它能发现「Manual 集合
///     整体丢失/类别名整体写错」与「过滤器失效/被忽略」,但**发现不了单独某一类漏打 Trait**
///     (总数关系仍是过滤器互补的恒等式);</item>
///   <item>源码期:<c>ManualTraitCoverageTests</c> 扫描测试源码,要求凡调用
///     <see cref="ManualTestGate"/> 的文件必须同时声明 <c>[Trait("Category", "Manual")]</c>,
///     补上「单点漏打 Trait」这个缺口。</item>
/// </list>
/// 此外 gate 本身的 env 早退保证 CI 不会真的读写设备/联网 —— 即使 Trait 漏了也只是"被记为
/// PASSED 的执行",不会产生真机/外网副作用。
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
