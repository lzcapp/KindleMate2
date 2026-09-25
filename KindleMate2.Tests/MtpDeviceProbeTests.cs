using Xunit;
using Xunit.Abstractions;
using KindleMate2.Application.Models;
using KindleMate2.Devices.MacOS;
using KindleMate2.Devices.Posix;
using KindleMate2.Shared.Constants;
using KindleMate2.Shared.Entities;

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
///   <item>首次插入必须在系统弹窗点「允许」;</item>
///   <item>曾经"关一次会话设备就掉线、必须重插"的老问题已解决(清掉了 libmtp 的
///         FORCE_RESET_ON_CLOSE 标记),见 <c>Probe_TwoConsecutiveSessions_SecondStillOpens</c>。</item>
/// </list>
/// </summary>
[Trait("Category", "Manual")]
public sealed class MtpDeviceProbeTests {
    private readonly ITestOutputHelper _output;

    public MtpDeviceProbeTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Probe_PrintsWhatLibMtpSees() {
        if (!ManualTestGate.RequireDevice(_output)) return;
        if (!MtpInterop.IsAvailable()) {
            _output.WriteLine("未安装 libmtp —— 跳过(CI 环境属正常)。本地可 brew install libmtp。");
            return;
        }

        _output.WriteLine($"libmtp = {MtpInterop.LoadedPath ?? "<未找到>"}");
        _output.WriteLine($"libusb = {MtpInterop.LibusbLoadedPath ?? "<未找到>"}");

        using var session = MtpDeviceSession.TryOpenKindle();
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
        if (!ManualTestGate.RequireDevice(_output)) return;
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

            // MTP 机型没有卷路径,版本是靠"导入时顺手读 system/version.txt 并缓存"拿到的
            // (不为读 27 字节单独开一次会话)。导入成功后这里应当非空。
            var version = device.GetKindleVersionText();
            _output.WriteLine($"GetKindleVersionText() = '{version}'");
            Assert.False(string.IsNullOrWhiteSpace(version), "MTP 导入成功后应当已缓存设备固件版本");
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
    /// **真机**验证写回路径(删除 + 上传)能往返:把设备上的 My Clippings.txt 原样写回,再读回来比 SHA-256。
    ///
    /// 刻意在**一次会话**里做完(开 → 下载原始 → 删 → 上传同内容 → 重新定位 → 再下载 → 比哈希):
    /// 一次会话跑完更快,也少占设备(连续会话本身已经可行,见另一条探针)。
    /// 内容保持字节一致,因此这个用例**不会改动用户的数据**。
    /// 产品入口 <c>DeviceManager.SyncFileToDevice</c> 就是这些原语加一层回滚(见其注释)。
    /// </summary>
    [Fact]
    public void Probe_MtpWriteBack_RoundTripsByteForByte() {
        if (!ManualTestGate.RequireDevice(_output)) return;
        if (!MtpInterop.IsAvailable()) {
            _output.WriteLine("未安装 libmtp —— 跳过。");
            return;
        }

        var work = Path.Combine(Path.GetTempPath(), "km2-mtp-writeback-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(work);
        var before = Path.Combine(work, "before.txt");
        var after = Path.Combine(work, "after.txt");

        try {
            using var session = MtpDeviceSession.TryOpenKindle();
            if (session is null) {
                _output.WriteLine("没有可打开的 MTP 设备 —— 跳过(未插 / 未授权 / 被别的 MTP 程序占用)。");
                return;
            }

            var clippings = session.FindByPath(AppConstants.DocumentsPathName, AppConstants.ClippingsFileName);
            if (clippings is not { } target) {
                _output.WriteLine("设备上没有 My Clippings.txt —— 跳过。");
                return;
            }

            Assert.True(session.Download(target.ItemId, before), "下载原始文件失败");
            var originalHash = Sha256(before);
            _output.WriteLine($"原始:{new FileInfo(before).Length} 字节, sha256={originalHash[..16]}…");

            Assert.True(session.Delete(target.ItemId), "删除失败");
            Assert.True(session.Upload(before, AppConstants.ClippingsFileName,
                target.ParentId, session.StorageId, target.FileType), "上传失败");

            var replaced = session.FindByPath(AppConstants.DocumentsPathName, AppConstants.ClippingsFileName);
            Assert.NotNull(replaced);
            _output.WriteLine($"写回后: id={replaced!.Value.ItemId} size={replaced.Value.Size}(原 id={target.ItemId} size={target.Size})");

            Assert.True(session.Download(replaced.Value.ItemId, after), "重新下载失败");
            var roundTripHash = Sha256(after);
            _output.WriteLine($"往返后: {new FileInfo(after).Length} 字节, sha256={roundTripHash[..16]}…");

            Assert.Equal(originalHash, roundTripHash);

            // ─——— 第二阶段:故意让上传失败,验证回滚兜底真的会把原文件放回去 ————
            // 源文件不存在 → 删除之后上传必然失败 → 必须触发 RestoreRollback。
            // 这段是整个写回里最安全关键的代码:它要是坏的,一次失败的同步就会把用户设备上的
            // My Clippings.txt 弄没,而应用的重试流程(第一步是从设备导入)又救不回来。
            var missingSource = Path.Combine(work, "这个文件不存在.txt");
            var threw = false;
            try {
                DeviceManager.ReplaceFileOnDevice(session, missingSource, AppConstants.ClippingsFileName);
            } catch (Exception ex) {
                threw = true;
                _output.WriteLine($"上传失败时按预期抛出:{ex.Message}");
            }

            Assert.True(threw, "上传失败时应当抛出异常");

            var afterRollback = session.FindByPath(AppConstants.DocumentsPathName, AppConstants.ClippingsFileName);
            Assert.NotNull(afterRollback);
            Assert.True(session.Download(afterRollback!.Value.ItemId, after), "回滚后重新下载失败");
            var rollbackHash = Sha256(after);
            _output.WriteLine($"回滚后: {new FileInfo(after).Length} 字节, sha256={rollbackHash[..16]}…");

            // 设备上的文件必须还在,且内容与最初逐字节一致
            Assert.Equal(originalHash, rollbackHash);
        } finally {
            try { Directory.Delete(work, true); } catch { /* best effort */ }
        }
    }

    private static string Sha256(string path) {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream));
    }

    /// <summary>
    /// **真机**判定「连续两次会话」是否都打得开 —— 这决定应用里"先导入、再写回"的两段式流程能不能用。
    ///
    /// 这条同时是**回归防线**:libmtp 给这台设备打了 FORCE_RESET_ON_CLOSE,关闭会话时会复位 USB 口,
    /// 真机实测后果是"关一次会话设备就离开总线、必须物理重插"——那时本用例第 2 轮必然失败,
    /// 而"先导入、再写回"的两段式流程(ExportManager.SyncToKindle)也因此做不完。
    /// 现在我们在传进去的结构体里清掉了那个标记(见 MtpDeviceSession.TryOpenKindle)。
    /// 若哪天新固件开始需要复位才能连第二次,这里会红 —— 那时再改回去,不要盲目放宽断言。
    /// </summary>
    [Fact]
    public void Probe_TwoConsecutiveSessions_SecondStillOpens() {
        if (!ManualTestGate.RequireDevice(_output)) return;
        if (!MtpInterop.IsAvailable()) {
            _output.WriteLine("未安装 libmtp —— 跳过。");
            return;
        }

        string? firstModel = null;
        using (var first = MtpDeviceSession.TryOpenKindle()) {
            if (first is null) {
                _output.WriteLine("第一次会话就没打开(未插 / 未授权 / 被占用)—— 跳过。");
                return;
            }

            firstModel = first.Model;
            _output.WriteLine($"第 1 次会话:已打开 '{first.Model}'");
        }

        // 第 2、3 次:关键断言。清掉 FORCE_RESET_ON_CLOSE 之前,第 2 次就必然失败
        // (设备已被复位、离开总线,要物理重插)。
        for (var round = 2; round <= 3; round++) {
            using var again = MtpDeviceSession.TryOpenKindle();
            _output.WriteLine(again is null
                ? $"第 {round} 次会话:打不开"
                : $"第 {round} 次会话:也能打开 '{again.Model}'");
            Assert.NotNull(again);
            Assert.Equal(firstModel, again!.Model);
        }

        // 会话结束后设备应当仍在总线上(只读枚举判定,不碰设备)
        var stillOnBus = KindleUsbProbe.TryFindKindle(out var productId);
        _output.WriteLine($"三次会话之后只读枚举:{(stillOnBus ? $"仍在(VID=0x1949 PID=0x{productId:x4})" : "设备已消失")}");
        Assert.True(stillOnBus, "会话结束后设备不应从总线上消失(那是 FORCE_RESET_ON_CLOSE 的副作用)");
    }

    /// <summary>
    /// **真机**验证状态栏那条探测链:USB 上有 Amazon 设备 → <c>IsKindleConnected()</c> 为真 →
    /// 类型为 MTP、卷为空(界面因此显示通用的"设备已连接")。
    ///
    /// 这条链走到的是**只读枚举**,不会开 MTP 会话 —— 也就是说它跑完**不会**把设备弄掉线
    /// (这正是它能被 2 秒轮询调用的前提)。跑完可以顺手再跑一次,应当仍然检测得到。
    /// </summary>
    [Fact]
    public void Probe_DetectsMtpKindleOnUsb_WithoutSessionOrReset() {
        if (!ManualTestGate.RequireDevice(_output)) return;
        if (!KindleUsbProbe.IsAvailable()) {
            _output.WriteLine("libusb 不可用(未随包也未安装)—— 跳过。");
            return;
        }

        var found = KindleUsbProbe.TryFindKindle(out var productId);
        _output.WriteLine(found
            ? $"只读枚举:检测到 Amazon USB 设备 VID=0x{KindleUsbProbe.AmazonVendorId:x4} PID=0x{productId:x4}"
            : "只读枚举:未检测到 Amazon USB 设备");

        using var manager = new DeviceManager(
            Path.Combine(AppConstants.SystemPathName, AppConstants.VersionFileName));
        var connected = manager.IsKindleConnected();
        _output.WriteLine($"IsKindleConnected()={connected} Type={manager.DeviceType} Drive='{manager.DriveLetter}'");

        if (!found) {
            _output.WriteLine("(没有设备时 IsKindleConnected 应为 false,这里不做断言以免依赖环境)");
            return;
        }

        Assert.True(connected, "USB 上检测到 Amazon 设备时,IsKindleConnected 应当为真");
        Assert.Equal(Device.Type.MTP, manager.DeviceType);
        Assert.Equal(string.Empty, manager.DriveLetter);   // MTP 没有卷路径 → 界面显示通用文案

        // 再探一次:只读枚举不该有副作用(不是"探一次就掉线")
        var stillThere = KindleUsbProbe.TryFindKindle(out _);
        _output.WriteLine($"紧接着再探一次:{stillThere}(应当仍为 True —— 只读枚举不改设备状态)");
        Assert.True(stillThere, "只读枚举不应把设备弄掉线");
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
