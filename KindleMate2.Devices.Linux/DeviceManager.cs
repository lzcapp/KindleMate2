using KindleMate2.Devices.Posix;

namespace KindleMate2.Devices.Linux;

/// <summary>
/// Linux 的 <see cref="KindleMate2.Application.Services.IDeviceManager"/> 实现 ——
/// 复用 POSIX 共用实现(<see cref="PosixDeviceManager"/>),只提供 Linux 特有的挂载点根候选。
///
/// 与 macOS 的关键差别就是**卷挂在哪里**:Linux 没有统一的挂载点,常见几种:
/// <list type="bullet">
///   <item><c>/media/&lt;用户&gt;/Kindle</c> —— Ubuntu / Debian 等 GNOME 桌面自动挂载;</item>
///   <item><c>/run/media/&lt;用户&gt;/Kindle</c> —— Fedora / Arch 等较新的 udisks2 行为;</item>
///   <item><c>/media/Kindle</c> 或 <c>/mnt/...</c> —— 老式/服务器环境或手工挂载。</item>
/// </list>
/// 所以按列表依次扫,取第一个"卷内有 documents/My Clippings.txt"的目录。
///
/// MTP 机型(2024 年及以后发布的 Kindle)在 Linux 上同样走 libmtp —— 而且 Linux 上 libmtp/libusb
/// 是常见系统库,通常无需随包分发(<see cref="PosixDeviceManager"/> 里说明了动态库的查找顺序)。
/// </summary>
public sealed class DeviceManager : PosixDeviceManager {
    /// <summary>
    /// Linux 上可能的挂载根,按常见程度排序。用当前登录用户名拼出 <c>/media/&lt;用户&gt;</c> 这类路径 ——
    /// 桌面环境默认把可移动卷挂在用户名之下,但也会有不带用户层的老式布局,所以两种都列上。
    /// </summary>
    public static IReadOnlyList<string> DefaultVolumeRoots => [
        Path.Combine("/media", Environment.UserName),
        Path.Combine("/run/media", Environment.UserName),
        "/media",
        "/mnt",
    ];

    /// <param name="versionFilePath">设备侧 version.txt 的相对路径(相对卷根)。</param>
    /// <param name="volumeRoots">候选挂载根,默认 <see cref="DefaultVolumeRoots"/>。</param>
    /// <param name="pollInterval">轮询间隔,默认 <see cref="PosixDeviceManager.DefaultPollInterval"/>。</param>
    /// <param name="debounceInterval">防抖时长,默认 <see cref="PosixDeviceManager.DefaultDebounceInterval"/>。</param>
    /// <param name="detectMtpDevices">是否启用 MTP 机型支持(检测 + 导入/写回);测试里关掉以保证确定性。</param>
    public DeviceManager(string versionFilePath,
        IReadOnlyList<string>? volumeRoots = null,
        TimeSpan? pollInterval = null,
        TimeSpan? debounceInterval = null,
        bool detectMtpDevices = true)
        : base(versionFilePath, volumeRoots ?? DefaultVolumeRoots, pollInterval, debounceInterval, detectMtpDevices) {
    }
}
