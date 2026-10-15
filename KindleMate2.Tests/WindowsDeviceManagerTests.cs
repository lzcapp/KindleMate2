#if WINDOWS
using KindleMate2.Devices.Windows;
using KindleMate2.Shared;
using KindleMate2.Shared.Entities;
using Xunit;

namespace KindleMate2.Tests;

/// <summary>
/// Windows <see cref="DeviceManager"/> 的**纯逻辑**用例。
///
/// 为什么只有这些:该类的其余部分(WMI 订阅/退订、<c>DriveInfo</c> 盘符枚举、
/// MediaDevices 的 MTP 探测与写回)全部与真实设备/COM 绑定 —— 为了"凑覆盖率"去 mock
/// <c>ManagementEventWatcher</c> 或 <c>MediaDevice</c> 只会造出一条**证明不了任何事**的绿,
/// 所以这里只钉两类**不需要设备**、却真实影响用户的行为:
/// <list type="number">
///   <item>构造后的默认状态(未连接 / 未知类型),它是状态栏与「关于→设备」两处 UI 的数据源;</item>
///   <item>无设备时导入 / 写回 / 释放的**守卫** —— 停止在"静默返回成功"或"崩进程"上。</item>
/// </list>
/// 真机链路由 <c>[Trait("Category","Manual")]</c> 的 <see cref="MtpDeviceProbeTests"/> 覆盖。
/// </summary>
public sealed class WindowsDeviceManagerTests {
    private static DeviceManager NewManager() =>
        // 版本文件路径只在真的连上设备后被拼进盘符下读取,构造阶段不碰文件系统。
        new(Path.Combine("system", "version.txt"));

    [Fact]
    public void NewManager_StartsDisconnected() {
        using var manager = NewManager();

        Assert.Equal(Device.Type.Unknown, manager.DeviceType);
        Assert.Equal(string.Empty, manager.DriveLetter);
        Assert.False(manager.IsConnected);
    }

    [Fact]
    public void Dispose_BeforeStartWatching_IsIdempotent() {
        // 释放竞态的统一处理在 DebouncedAction 里;这里只需保证:
        // ① 没调过 StartWatching(四个 watcher 都是 null)也能安全释放;
        // ② 二次释放不抛(退出流程会重复调用,Dispose 抛异常会挂掉关机路径)。
        var manager = NewManager();

        manager.Dispose();
        manager.Dispose();
    }

    [Fact]
    public void GetKindleVersionText_WithoutConnectedDevice_ReturnsEmpty() {
        // 拔出设备后 DriveLetter 为空:此时绝不能按空串去 Path.Combine 而误读工作目录里的同名文件。
        using var manager = NewManager();

        Assert.Equal(string.Empty, manager.GetKindleVersionText());
    }

    [Fact]
    public void ImportFilesFromDevice_WithoutDevice_FailsWithConnectError_InsteadOfSilentlySucceeding() {
        using var manager = NewManager();

        var ok = manager.ImportFilesFromDevice("clippings.bak", "vocab.bak", out var exception);

        Assert.False(ok);
        // 必须把"没连设备"作为异常原样报出:此前这里是静默返回,调用方会当成导入成功、
        // 继续用一份根本不存在的备份。
        Assert.NotNull(exception);
        Assert.Equal(Strings.Kindle_Connect_Failed, exception!.Message);
    }

    [Fact]
    public void SyncFileToDevice_WithoutDevice_ThrowsConnectError() {
        using var manager = NewManager();

        var ex = Assert.Throws<Exception>(() => manager.SyncFileToDevice("exported.txt", "My Clippings.txt"));
        Assert.Equal(Strings.Kindle_Connect_Failed, ex.Message);
    }
}
#endif
