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
        // 防抖的意义就在这条:摘掉又在窗口内插回,不应把界面状态来回抖两次
        MountKindleVolume();
        using var manager = CreateManager(debounce: TimeSpan.FromMilliseconds(400));
        var events = new List<bool>();
        manager.ConnectionChanged += value => { lock (events) events.Add(value); };
        manager.StartWatching();
        Thread.Sleep(100);

        UnmountKindleVolume();
        Thread.Sleep(120);      // 足够轮询观测到断开(20ms 一轮),但远不到 400ms 防抖窗口
        MountKindleVolume();
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
