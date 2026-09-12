using KindleMate2.Shared.Entities;

namespace KindleMate2.Application.Services;

/// <summary>
/// 非 Windows 平台的设备管理器空实现:始终报告「未连接」,导入/同步操作以失败告终但不抛异常失控。
///
/// 存在的意义是让跨平台壳能够统一构造 <see cref="IDeviceManager"/> 而不需要条件编译,
/// 同时把「本平台暂无设备支持」这一事实如实暴露给 UI(状态栏显示未连接、同步前会拦下)。
/// 后续如需 Linux/macOS 的 MTP 支持,新增对应实现替换即可。
/// </summary>
public sealed class NullDeviceManager : IDeviceManager {
    public Device.Type DeviceType => Device.Type.Unknown;

    public string DriveLetter => string.Empty;

    public bool IsConnected => false;

    /// <summary>永不触发。用显式访问器实现以避免「事件未使用」告警。</summary>
    public event Action<bool>? ConnectionChanged {
        add { }
        remove { }
    }

    public void StartWatching() {
        // 无平台实现,不做任何监听
    }

    public bool IsKindleConnected() => false;

    public string GetKindleVersionText() => string.Empty;

    public bool ImportFilesFromDevice(string backupClippingsPath, string backupWordsPath, out Exception? exception) {
        exception = new PlatformNotSupportedException("当前平台暂无 Kindle 设备支持。");
        return false;
    }

    public void SyncFileToDevice(string exportedFilePath, string targetFileName) =>
        throw new PlatformNotSupportedException("当前平台暂无 Kindle 设备支持。");

    public void Dispose() {
        // 无需释放资源
    }
}
