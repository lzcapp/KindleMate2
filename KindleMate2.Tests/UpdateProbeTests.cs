using Xunit;
using Xunit.Abstractions;
using KindleMate2.Application.Services;

namespace KindleMate2.Tests;

/// <summary>
/// **需要联网**的检查更新探测:对着真实的 GitHub Releases 跑一次,验证解析器与线上 JSON 契约一致。
///
/// 为什么单独放一条而不是塞进常规用例:CI 不该依赖外网(限流、代理、仓库不可达都会让构建莫名变红)。
/// 这条由人手动触发,失败也只说明"这次没连上",不阻塞 CI。
///
/// 运行方式:
/// <code>dotnet test KindleMate2.Tests --filter "FullyQualifiedName~UpdateProbeTests" --logger "console;verbosity=detailed"</code>
/// </summary>
[Trait("Category", "Manual")]
public sealed class UpdateProbeTests {
    private readonly ITestOutputHelper _output;

    public UpdateProbeTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task Probe_QueriesRealGitHubReleases() {
        if (!ManualTestGate.RequireNetwork(_output)) return;
        // 故意传一个很老的版本,这样只要仓库有发布就应当判定"有更新"
        var info = await UpdateChecker.CheckAsync("0.0.1", "osx-arm64");

        if (info is null) {
            // 没网 / 被限流 / 仓库暂无 release —— 都只是"这次没查到",不算失败
            _output.WriteLine("未能从 GitHub 取到发布信息(网络不可达、被限流,或仓库暂无 release)。");
            return;
        }

        _output.WriteLine($"最新版本 = {info.Version}");
        _output.WriteLine($"发布页   = {info.ReleaseUrl}");
        _output.WriteLine($"匹配资产 = {(info.Asset is null ? "(本次没有 osx-arm64 资产)" : info.Asset.Name)}");
        if (info.Asset is not null) {
            _output.WriteLine($"下载地址 = {info.Asset.DownloadUrl}");
            _output.WriteLine($"大小     = {info.Asset.Size} 字节");
        }

        Assert.False(string.IsNullOrWhiteSpace(info.Version));
        Assert.StartsWith("http", info.ReleaseUrl, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Probe_TreatsCurrentReleaseAsUpToDate() {
        if (!ManualTestGate.RequireNetwork(_output)) return;
        // 与上一条相反:把"当前版本"设成一个极大的版本,应当判定没有更新
        var info = await UpdateChecker.CheckAsync("9999.12.31", "osx-arm64");

        _output.WriteLine(info is null
            ? "按预期:没有更新(或这次没查到)"
            : $"异常:竟然认为有更新 -> {info.Version}");

        Assert.Null(info);
    }
}
