using Xunit;
using Xunit.Abstractions;
using KindleMate2.Application.Models;
using KindleMate2.Devices.MacOS;
using KindleMate2.Shared.Constants;

namespace KindleMate2.Tests;

/// <summary>
/// **需要在真机上手动跑**的 MTP 探测:插上 Kindle(并在 macOS 弹出的「允许配件连接?」里点允许)后
/// 运行本文件的用例,即可看到 libmtp 实际认出了什么、设备上有哪些目录、我们要的那两个文件在不在。
///
/// 之所以做成用例而不是临时脚本:MTP 设备 I/O **无法在 CI 里验证**(CI 既没有设备,而 macOS 沙箱
/// 还会拦掉 USB 枚举),所以"能重复执行的、由人在真机上触发的探测"是这门功能唯一的验证入口。
/// 没有设备时这些用例**直接通过(跳过)**,不会让 CI 变红。
///
/// 运行方式:
/// <code>dotnet test KindleMate2.Tests --filter "FullyQualifiedName~MtpDeviceProbeTests" --logger "console;verbosity=detailed"</code>
///
/// 实测注意事项:
/// <list type="bullet">
///   <item>没插 / 没点「允许」/ 被别的 MTP 客户端占用 → 探测不到设备,这是预期结果;</item>
///   <item>每次访问会话结束时设备会重新枚举一次(macOS 的 USB reset),系统可能再问一次授权 ——
///         不是故障,但意味着"少开几次会话"对体验很重要。</item>
/// </list>
/// </summary>
public sealed class MtpDeviceProbeTests {
    private readonly ITestOutputHelper _output;

    public MtpDeviceProbeTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Probe_PrintsWhatLibMtpSees() {
        if (!MtpInterop.IsAvailable()) {
            _output.WriteLine("未安装 libmtp —— 跳过(CI 环境属正常)。本地可 brew install libmtp。");
            return;
        }

        _output.WriteLine($"libmtp = {MtpInterop.LoadedPath}");

        using var session = MtpDeviceSession.TryOpenFirst();
        if (session == null) {
            // 没插设备 / 未被授权 / 被别的 MTP 客户端占着 —— 都不是失败
            _output.WriteLine("没有可打开的 MTP 设备(未连接、未在系统弹窗点「允许」,或被 Amazon USB File Manager / OpenMTP / Calibre 占用)");
            return;
        }

        _output.WriteLine($"已打开:Model='{session.Model}'");
        _output.WriteLine($"        FriendlyName='{session.FriendlyName}'");
        _output.WriteLine($"真实存储区 id = {session.StorageId}(列子目录必须用它;传 0xFFFFFFFF 会 PTP 报错并掉线)");
        _output.WriteLine($"根一层 {session.RootEntries.Count} 个对象:");

        foreach (var entry in session.RootEntries) {
            _output.WriteLine($"  [{(entry.IsFolder ? "目录" : "文件")}] '{entry.Name}' id={entry.ItemId} size={entry.Size}");
        }

        // 我们真正需要的两条路径
        Report(session, "documents/My Clippings.txt", "documents", "My Clippings.txt");
        Report(session, "system/vocabulary/vocab.db", "system", "vocabulary", "vocab.db");
    }

    /// <summary>
    /// 走**产品真实入口**(<c>DeviceManager.ImportFilesFromDevice</c>)从真机取回两个文件 ——
    /// 这才是端到端:USB 分支找不到卷 → 自动转 MTP 分支 → 逐层定位 → 下载 → 上报进度。
    /// 没有设备时只打印说明,不算失败(CI 上必然走这条路)。
    /// </summary>
    [Fact]
    public void Probe_ImportsFromRealDevice_ViaDeviceManager() {
        if (!MtpInterop.IsAvailable()) {
            _output.WriteLine("未安装 libmtp —— 跳过。");
            return;
        }

        var work = Path.Combine(Path.GetTempPath(), "km2-mtp-import-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(work);
        var clippingsPath = Path.Combine(work, "My Clippings.txt");
        var vocabPath = Path.Combine(work, "vocab.db");

        try {
            // 卷根指向一个空目录 → USB 分支必然不命中,于是走 MTP
            using var device = new DeviceManager(
                Path.Combine(AppConstants.SystemPathName, AppConstants.VersionFileName), Path.Combine(work, "volumes"));
            var progress = new RecordingProgress();

            var ok = device.ImportFilesFromDevice(clippingsPath, vocabPath, out var exception, progress);

            if (!ok) {
                // 没插 / 没授权 / 被别的 MTP 客户端占用 —— 都落在这里,不算用例失败
                _output.WriteLine($"未从设备导入:{exception?.Message}");
                return;
            }

            _output.WriteLine($"导入成功。进度上报 {progress.Reports.Count} 次,最后 current={progress.Reports[^1].Current}/{progress.Reports[^1].Total}");
            ReportFile(clippingsPath, "My Clippings.txt");
            ReportFile(vocabPath, "vocab.db");
        } finally {
            try { Directory.Delete(work, true); } catch { /* best effort */ }
        }
    }

    private void ReportFile(string path, string label) {
        if (!File.Exists(path)) {
            _output.WriteLine($"{label}: 本地不存在(可能设备上没有)");
            return;
        }

        var info = new FileInfo(path);
        _output.WriteLine($"{label}: {info.Length} 字节 -> {path}");
    }

    /// <summary>
    /// 同步收集进度 —— 不用 <c>Progress&lt;T&gt;</c>:它把回调投递到线程池,断言时不一定收到全部条目,
    /// 会变成偶发失败(设备用例里踩过一次)。
    /// </summary>
    private sealed class RecordingProgress : IProgress<OperationProgress> {
        public List<OperationProgress> Reports { get; } = [];

        public void Report(OperationProgress value) {
            lock (Reports) Reports.Add(value);
        }
    }

    private void Report(MtpDeviceSession session, string display, params string[] segments) {
        var found = session.FindByPath(segments);

        if (found is not { } entry) {
            _output.WriteLine($"{display} -> 未找到");
            return;
        }

        _output.WriteLine($"{display} -> id={entry.ItemId} size={entry.Size} storage={entry.StorageId} type={entry.FileType}");
    }
}
