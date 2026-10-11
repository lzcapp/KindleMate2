using System.Diagnostics;
using Xunit;
using KindleMate2.Application.Models;
using KindleMate2.Devices.MacOS;
using KindleMate2.Shared.Constants;
using KindleMate2.Shared.Entities;

namespace KindleMate2.Tests;

/// <summary>
/// macOS 设备实现(<c>KindleMate2.Devices.MacOS.DeviceManager</c>)的用例。
///
/// 这段逻辑此前完全无法回归 —— 卷根目录写死在 <c>private const "/Volumes"</c> 上,而 CI 里既没有
/// /Volumes 也没有 Kindle,当初只能 sed 改一份源码副本手工验。现在构造函数可注入卷根与轮询/防抖
/// 时长,于是"扫卷判定 + 插拔上报"就是普通单测,任何平台都能跑。
///
/// 用例同时钉住两条设计决定:
/// <list type="number">
///   <item>判定只认「卷内存在 documents/My Clippings.txt」这一内容特征 —— 卷名与卷数量都不可靠
///         (真实 /Volumes 下混着系统卷、恢复卷与 DMG 挂载卷);</item>
///   <item>防抖只能由「观测值发生变化」启动。轮询间隔短于防抖窗口,若改用「观测值 ≠ 已上报值」
///         判断,每一轮都会重置计时器,拔除设备后永远不会上报(实现时实测踩过)。</item>
/// </list>
/// </summary>
public sealed class MacOSDeviceManagerTests : IDisposable {
    /// <summary>测试用的"卷根",替代真实的 /Volumes。</summary>
    private readonly string _volumesRoot;

    public MacOSDeviceManagerTests() {
        _volumesRoot = Path.Combine(Path.GetTempPath(), "km2-volumes-" + Guid.NewGuid().ToString("N")[..10]);
        Directory.CreateDirectory(_volumesRoot);
    }

    public void Dispose() {
        try { Directory.Delete(_volumesRoot, true); } catch { /* best effort */ }
    }

    private string KindleVolume => Path.Combine(_volumesRoot, "Kindle");

    // ————————————————————————— 设备发现 —————————————————————————

    [Fact]
    public void NoVolume_ReportsNotConnected() {
        using var manager = CreateManager();

        Assert.False(manager.IsKindleConnected());
        Assert.False(manager.IsConnected);
        Assert.Equal(string.Empty, manager.DriveLetter);
    }

    [Fact]
    public void VolumeWithoutClippings_IsNotMistakenForKindle() {
        // 磁盘映像挂载卷 / 别的 U 盘:即便有 documents 目录也不算 Kindle
        Directory.CreateDirectory(Path.Combine(_volumesRoot, "SomeDmg", "documents"));

        using var manager = CreateManager();

        Assert.False(manager.IsKindleConnected());
    }

    [Fact]
    public void KindleVolume_IsDetected_AsUsb_WithVolumeRootAsDriveLetter() {
        MountKindleVolume();
        using var manager = CreateManager();

        Assert.True(manager.IsKindleConnected());
        Assert.True(manager.IsConnected);
        Assert.Equal(KindleVolume, manager.DriveLetter);
        Assert.Equal(Device.Type.USB, manager.DeviceType);
    }

    [Fact]
    public void VolumeStillMountedButClippingsGone_ReportsDisconnected() {
        // 内容特征必须每次复查:卷目录还在但特征文件没了(残留挂载点)不能算已连接
        MountKindleVolume();
        using var manager = CreateManager();
        Assert.True(manager.IsKindleConnected());

        File.Delete(Path.Combine(KindleVolume, AppConstants.DocumentsPathName, AppConstants.ClippingsFileName));

        Assert.False(manager.IsKindleConnected());
    }

    [Fact]
    public void GetKindleVersionText_ReadsDeviceVersionFile() {
        MountKindleVolume();
        using var manager = CreateManager();
        Assert.True(manager.IsKindleConnected());

        Assert.StartsWith("Kindle 5.16.2", manager.GetKindleVersionText());
    }

    // ————————————————————————— 设备概览(关于窗口「设备」段) —————————————————————————
    // GetDeviceInfo 不额外开会话,字段全来自卷文件与 DriveInfo。

    [Fact]
    public void GetDeviceInfo_WhenNotConnected_ReturnsNull() {
        using var manager = CreateManager();

        Assert.Null(manager.GetDeviceInfo());
    }

    [Fact]
    public void GetDeviceInfo_UsbConnected_ReportsTypePathFirmwareFirstLineAndCapacity() {
        MountKindleVolume();
        using var manager = CreateManager();
        Assert.True(manager.IsKindleConnected());

        var info = manager.GetDeviceInfo();

        Assert.NotNull(info);
        Assert.Equal(Device.Type.USB, info!.Type);
        Assert.Equal(KindleVolume, info.DrivePath);
        // version.txt 只取首行非空行 —— 即便文件写成多行也只显示一行
        Assert.Equal("Kindle 5.16.2", info.Firmware);
        // 容量来自 DriveInfo(假卷落在本机 temp 盘上)—— 只钉"拿得到且自洽",不钉具体数字
        Assert.NotNull(info.TotalBytes);
        Assert.NotNull(info.FreeBytes);
        Assert.True(info.TotalBytes > 0);
        Assert.InRange(info.FreeBytes!.Value, 0, info.TotalBytes!.Value);
    }

    [Fact]
    public void GetDeviceInfo_VersionFileWithMultipleLines_TakesFirstNonEmptyLine() {
        MountKindleVolume();
        File.WriteAllText(
            Path.Combine(KindleVolume, AppConstants.SystemPathName, AppConstants.VersionFileName),
            "\n\nKindle 5.16.2\n515102000\ndevice details\n");
        using var manager = CreateManager();

        var info = manager.GetDeviceInfo();

        Assert.NotNull(info);
        Assert.Equal("Kindle 5.16.2", info!.Firmware);
    }

    // ————————————————————————— 文件传输 —————————————————————————

    [Fact]
    public void ImportFilesFromDevice_CopiesBothFiles_AndReportsProgressPerFile() {
        MountKindleVolume();
        using var manager = CreateManager();
        Assert.True(manager.IsKindleConnected());

        var clippingsTarget = Path.Combine(_volumesRoot, "out-clippings.txt");
        var vocabTarget = Path.Combine(_volumesRoot, "out-vocab.db");
        var progress = new RecordingProgress();

        var ok = manager.ImportFilesFromDevice(clippingsTarget, vocabTarget, out var exception, progress);

        Assert.True(ok);
        Assert.Null(exception);
        Assert.Equal("clippings-body", File.ReadAllText(clippingsTarget));
        Assert.Equal("vocab-body", File.ReadAllText(vocabTarget));

        // 与 Windows 实现同一语义:按「第几个文件」上报,总数恒为 2
        Assert.Equal(3, progress.Reports.Count);
        Assert.All(progress.Reports, r => Assert.Equal(2, r.Total));
        Assert.Equal(2, progress.Reports[^1].Current);
        Assert.Equal(1d, progress.Reports[^1].Fraction);
    }

    [Fact]
    public void ImportFilesFromDevice_WhenNotConnected_FailsWithException_WithoutThrowing() {
        var clippingsTarget = Path.Combine(_volumesRoot, "never-clippings.txt");
        var vocabTarget = Path.Combine(_volumesRoot, "never-vocab.db");
        using var manager = CreateManager();

        var ok = manager.ImportFilesFromDevice(clippingsTarget, vocabTarget, out var exception);

        Assert.False(ok);
        Assert.NotNull(exception);
        Assert.False(File.Exists(clippingsTarget));
        Assert.False(File.Exists(vocabTarget));
    }

    [Fact]
    public void SyncFileToDevice_OverwritesTargetOnDevice() {
        MountKindleVolume();
        using var manager = CreateManager();
        Assert.True(manager.IsKindleConnected());
        var source = Path.Combine(_volumesRoot, "exported.txt");
        File.WriteAllText(source, "exported-body");

        manager.SyncFileToDevice(source, AppConstants.ClippingsFileName);

        var onDevice = Path.Combine(KindleVolume, AppConstants.DocumentsPathName, AppConstants.ClippingsFileName);
        Assert.Equal("exported-body", File.ReadAllText(onDevice));
    }

    [Fact]
    public void SyncFileToDevice_WhenNotConnected_Throws() {
        using var manager = CreateManager();

        Assert.Throws<Exception>(() =>
            manager.SyncFileToDevice("whatever.txt", AppConstants.ClippingsFileName));
    }

    // ————————————————————————— 插拔监听 —————————————————————————

    [Fact]
    public void Watching_DoesNotReportOnStartup_ThenReportsDisconnectAndReconnectOnceEach() {
        MountKindleVolume();
        using var manager = CreateManager();
        var events = new List<bool>();
        manager.ConnectionChanged += value => { lock (events) events.Add(value); };

        manager.StartWatching();
        Thread.Sleep(150);
        lock (events) Assert.Empty(events);   // 启动时把现状记为"已上报",不该平白上报一次

        UnmountKindleVolume();
        Assert.True(WaitForEventCount(events, 1), "卸载后应上报一次");
        lock (events) Assert.Equal([false], events.ToArray());

        MountKindleVolume();
        Assert.True(WaitForEventCount(events, 2), "重新挂载后应上报一次");
        lock (events) Assert.Equal([false, true], events.ToArray());
    }

    [Fact]
    public void Watching_IgnoresTransientBlip_ShorterThanDebounceWindow() {
        // 防抖的意义就在这条:摘掉又在窗口内插回,不应把界面状态来回抖两次。
        MountKindleVolume();
        using var manager = CreateManager(debounce: TimeSpan.FromMilliseconds(400));
        var events = new List<bool>();
        manager.ConnectionChanged += value => { lock (events) events.Add(value); };
        manager.StartWatching();
        Thread.Sleep(100);

        // 制造抖动:**改名单个特征文件再改回**。
        //
        // 刻意不用 UnmountKindleVolume() —— 它内部是 Directory.Delete(recursive: true),
        // 在 Windows 上可能耗时数百毫秒(本机实测 ~757ms,受杀软实时扫描与磁盘影响),
        // 于是"抖动窗口"从预期的 120ms 变成「删除耗时 + 120ms ≈ 880ms」,**反而超过了 400ms 防抖窗口**:
        // 那就不再是抖动,而是一次真实的长时间断开,防抖理所当然不吸收(实测本机稳定失败,CI 的
        // ubuntu/macOS 因删除是毫秒级而恰好通过)。卷判定只认「documents/My Clippings.txt 存在」,
        // 所以改名同样能让判定翻转,且同卷改名是元数据操作、微秒级 —— 这才真的是"短于防抖窗口的抖动"。
        var clippings = Path.Combine(KindleVolume, AppConstants.DocumentsPathName, AppConstants.ClippingsFileName);
        var parked = clippings + ".parked";
        File.Move(clippings, parked);

        // 夹具自检:改名是同步的,判定必须**立刻**翻转 —— 否则下面的断言会退化成"什么都没发生"。
        Assert.False(manager.IsKindleConnected(), "改名后判定应立刻翻转为未连接");

        // 保持 100ms:① 长于 20ms 的轮询间隔,保证管理器的轮询**真的观测到**这次断开
        // (否则本用例退化成空断言 —— 实测把这段缩到"改回即走"时,连"去掉防抖复检守卫"这种
        // 变异都能通过);② 又远短于 400ms 防抖窗口,才是"窗口内的抖动"。
        Thread.Sleep(100);
        File.Move(parked, clippings);
        Thread.Sleep(900);      // 等过防抖窗口

        lock (events) Assert.Empty(events);
        Assert.True(manager.IsKindleConnected());
    }

    [Fact]
    public void Dispose_StopsWatching() {
        MountKindleVolume();
        var manager = CreateManager();
        var events = new List<bool>();
        manager.ConnectionChanged += value => { lock (events) events.Add(value); };
        manager.StartWatching();
        Thread.Sleep(100);

        manager.Dispose();
        UnmountKindleVolume();
        Thread.Sleep(400);

        lock (events) Assert.Empty(events);
    }

    // ————————————————————————— helpers —————————————————————————

    /// <summary>
    /// 轮询/防抖默认都调快,让上面几条"等事件"的用例在毫秒级完成 —— 生产值(2s / 2.5s)照旧,
    /// 注入点只为测试而存在。
    ///
    /// <c>detectMtpDevices: false</c> 也是为测试:本文件验的是**卷判定**逻辑,
    /// 而"没有卷时再去 USB 上找 MTP 设备"会用真实设备总线 —— 开发机插着 Kindle 时
    /// "无卷即未连接"这类断言就会失败,CI 上却通过。同一个用例在不同环境给出不同结果是最糟的。
    /// </summary>
    private DeviceManager CreateManager(TimeSpan? poll = null, TimeSpan? debounce = null) =>
        new(Path.Combine(AppConstants.SystemPathName, AppConstants.VersionFileName),
            _volumesRoot,
            poll ?? TimeSpan.FromMilliseconds(20),
            debounce ?? TimeSpan.FromMilliseconds(100),
            detectMtpDevices: false);

    private void MountKindleVolume() {
        Directory.CreateDirectory(Path.Combine(KindleVolume, AppConstants.DocumentsPathName));
        Directory.CreateDirectory(Path.Combine(KindleVolume, AppConstants.SystemPathName, AppConstants.VocabularyPathName));
        File.WriteAllText(Path.Combine(KindleVolume, AppConstants.SystemPathName, AppConstants.VersionFileName),
            "Kindle 5.16.2\n");
        File.WriteAllText(Path.Combine(KindleVolume, AppConstants.DocumentsPathName, AppConstants.ClippingsFileName),
            "clippings-body");
        File.WriteAllText(
            Path.Combine(KindleVolume, AppConstants.SystemPathName, AppConstants.VocabularyPathName, AppConstants.VocabFileName),
            "vocab-body");
    }

    private void UnmountKindleVolume() {
        if (Directory.Exists(KindleVolume)) Directory.Delete(KindleVolume, true);
    }

    private static bool WaitForEventCount(List<bool> events, int expected) {
        var timeout = Stopwatch.StartNew();
        while (timeout.Elapsed < TimeSpan.FromSeconds(5)) {
            lock (events) {
                if (events.Count >= expected) return true;
            }
            Thread.Sleep(10);
        }

        lock (events) {
            return events.Count >= expected;
        }
    }

    /// <summary>
    /// 同步收集进度 —— 不用 <see cref="Progress{T}"/>:它把回调投递到线程池(或捕获的同步上下文),
    /// 于是断言时不一定已经收到全部条目,会变成偶发失败。
    /// </summary>
    private sealed class RecordingProgress : IProgress<OperationProgress> {
        public List<OperationProgress> Reports { get; } = [];

        public void Report(OperationProgress value) {
            lock (Reports) Reports.Add(value);
        }
    }
}
