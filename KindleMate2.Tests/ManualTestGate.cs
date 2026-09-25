using Xunit.Abstractions;

namespace KindleMate2.Tests;

/// <summary>
/// 「探测类」用例(真机 / 真联网)的手动开关。默认**全部跳过**,避免：
/// <list type="bullet">
///   <item>随手 <c>dotnet test</c> 时,插着 Kindle 的机器上真机用例去删写设备上的
///     <c>My Clippings.txt</c>(<c>Probe_MtpWriteBack_RoundTripsByteForByte</c>);</item>
///   <item>CI 依赖外网、被限流或代理不可达时莫名变红。</item>
/// </list>
/// 需要跑时设置对应环境变量为 <c>1</c>。这些类同时带 <c>[Trait("Category","Manual")]</c>,
/// CI 可再用 <c>--filter "Category!=Manual"</c> 显式排除。
/// </summary>
internal static class ManualTestGate {
    private const string DeviceEnvVar = "KM2_MANUAL_DEVICE_TESTS";
    private const string NetworkEnvVar = "KM2_MANUAL_NETWORK_TESTS";

    /// <summary>真机用例是否启用。启用时返回 true;否则打印跳过说明并返回 false,调用方直接 return。</summary>
    public static bool RequireDevice(ITestOutputHelper output) =>
        Require(output, DeviceEnvVar, "真机探测(会读写连接的 Kindle)");

    /// <summary>联网用例是否启用。</summary>
    public static bool RequireNetwork(ITestOutputHelper output) =>
        Require(output, NetworkEnvVar, "真实 GitHub Releases 探测");

    private static bool Require(ITestOutputHelper output, string envVar, string what) {
        if (Environment.GetEnvironmentVariable(envVar) == "1") {
            return true;
        }
        output.WriteLine($"跳过:{what}默认不跑。手动运行请设 {envVar}=1。");
        return false;
    }
}
