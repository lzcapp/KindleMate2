using KindleMate2.Devices.Posix;

namespace KindleMate2.Devices.MacOS;

/// <summary>
/// macOS 的 <see cref="KindleMate2.Application.Services.IDeviceManager"/> 实现 ——
/// 就是 POSIX 共用实现(<see cref="PosixDeviceManager"/>),只是把挂载点根钉成 <c>/Volumes</c>。
///
/// 之所以只是一个薄壳:USB 大容量存储与 MTP 的逻辑与 Linux 完全一样,差别只在"卷挂在哪里"。
/// 真正的实现在 <c>KindleMate2.Devices.Posix</c>,那里有完整的发现/判定/监听/MTP 说明。
/// </summary>
public sealed class DeviceManager : PosixDeviceManager {
    /// <summary>macOS 上外接卷的挂载根目录(可被构造函数覆盖,便于测试)。</summary>
    public const string DefaultVolumesRoot = "/Volumes";

    /// <param name="versionFilePath">设备侧 version.txt 的相对路径(相对卷根)。</param>
    /// <param name="volumesRoot">卷的挂载根目录,默认 <see cref="DefaultVolumesRoot"/>。</param>
    /// <param name="pollInterval">轮询间隔,默认 <see cref="PosixDeviceManager.DefaultPollInterval"/>。</param>
    /// <param name="debounceInterval">防抖时长,默认 <see cref="PosixDeviceManager.DefaultDebounceInterval"/>。</param>
    /// <param name="detectMtpDevices">是否启用 MTP 机型支持(检测 + 导入/写回);测试里关掉以保证确定性。</param>
    public DeviceManager(string versionFilePath,
        string volumesRoot = DefaultVolumesRoot,
        TimeSpan? pollInterval = null,
        TimeSpan? debounceInterval = null,
        bool detectMtpDevices = true)
        : base(versionFilePath, [volumesRoot], pollInterval, debounceInterval, detectMtpDevices) {
    }
}
