using System.Runtime.InteropServices;
using KindleMate2.Shared.Diagnostics;

namespace KindleMate2.Devices.MacOS;

/// <summary>
/// 一个打开的 MTP 设备会话 —— 把 libmtp 的原始指针包装成可用的对象模型。
///
/// 生命周期:MTP 是**单会话**协议(同一时刻只允许一个客户端连着设备),因此:
/// <list type="bullet">
///   <item>打开与释放必须配对,且**尽量在一次会话里做完所有事**再释放 —— 真机实测每次会话结束时
///         设备会重新枚举(macOS 上 libusb 释放接口触发 USB reset),系统会当成新配件、可能再问一次
///         "允许配件连接?";会话开得越少,用户被打断的次数越少;</item>
///   <item>用户的 Amazon「USB File Manager」/ OpenMTP / Calibre 任一在跑,我们就打不开设备
///         (表现为打开返回 NULL)——这属于预期内的互斥,要给人话提示,不是故障。</item>
/// </list>
/// </summary>
internal sealed class MtpDeviceSession : IDisposable {
    private IntPtr _device;
    private bool _disposed;

    private MtpDeviceSession(IntPtr device, uint storageId, IReadOnlyList<MtpEntry> rootEntries,
        string model, string friendlyName) {
        _device = device;
        StorageId = storageId;
        RootEntries = rootEntries;
        Model = model;
        FriendlyName = friendlyName;
    }

    internal string Model { get; }
    internal string FriendlyName { get; }

    /// <summary>
    /// 真实存储区 id(本机 Kindle 上是 65537)。**列子目录必须用它** ——
    /// 继续传 <c>0xFFFFFFFF</c> 会得到 <c>PTP Layer error 02fe</c> 并把设备惹到掉线(真机实测)。
    /// </summary>
    internal uint StorageId { get; }

    /// <summary>根一层的条目(打开时取一次),也用于核对设备上实际有哪些顶层目录。</summary>
    internal IReadOnlyList<MtpEntry> RootEntries { get; }

    /// <summary>
    /// 打开第一个 MTP 设备。返回 null 表示**没有可用设备**:没插、没点系统那个"允许配件连接"、
    /// 或已被别的 MTP 客户端占用。失败细节只写日志 —— 对调用方而言这就是"没连上"。
    /// </summary>
    internal static MtpDeviceSession? TryOpenFirst() {
        if (!MtpInterop.IsAvailable()) {
            AppLog.Write("[MtpDeviceSession] 未找到 libmtp 动态库");
            return null;
        }

        MtpInterop.LIBMTP_Init();

        var code = MtpInterop.LIBMTP_Detect_Raw_Devices(out var rawDevices, out var count);
        if (code != MtpInterop.MtpError.None || count <= 0 || rawDevices == IntPtr.Zero) {
            if (rawDevices != IntPtr.Zero) {
                MtpInterop.LIBMTP_FreeMemory(rawDevices);
            }
            AppLog.Write($"[MtpDeviceSession] 设备探测结果:{code}({MtpInterop.MtpError.Describe(code)}),数量 {count}");
            return null;
        }

        try {
            var stride = Marshal.SizeOf<MtpInterop.MtpRawDevice>();
            for (var i = 0; i < count; i++) {
                var rawDevice = Marshal.PtrToStructure<MtpInterop.MtpRawDevice>(rawDevices + (i * stride));
                var device = MtpInterop.LIBMTP_Open_Raw_Device_Uncached(ref rawDevice);
                if (device == IntPtr.Zero) {
                    // 打不开通常是被别的 MTP 客户端占着,或设备处于锁屏/忙碌态 —— 试下一个
                    AppLog.Write($"[MtpDeviceSession] 第 {i} 个设备打开失败" +
                                 $"(VID=0x{rawDevice.DeviceEntry.VendorId:x4} PID=0x{rawDevice.DeviceEntry.ProductId:x4})");
                    continue;
                }

                // 根一层:storageId 与 parentId 都传 0xFFFFFFFF。顺带从任一条目拿到真实存储区 id。
                var rootEntries = ListLevel(device, MtpInterop.FilesAndFoldersRoot, MtpInterop.FilesAndFoldersRoot);
                var storageId = rootEntries.Count > 0 ? rootEntries[0].StorageId : 0;

                return new MtpDeviceSession(
                    device, storageId, rootEntries,
                    MtpInterop.ReadUtf8(MtpInterop.LIBMTP_Get_Modelname(device)),
                    MtpInterop.ReadUtf8(MtpInterop.LIBMTP_Get_Friendlyname(device)));
            }

            return null;
        } finally {
            MtpInterop.LIBMTP_FreeMemory(rawDevices);
        }
    }

    /// <summary>
    /// 按路径逐级向下找一个对象(如 <c>"documents"</c> / <c>"My Clippings.txt"</c>)。
    /// 第一级查已缓存的根一层,更深层按需向设备取 —— **只取要用的那几层**,不做全树遍历:
    /// 设备对连续请求很敏感,少发请求就少一次掉线机会(真机实测)。
    /// </summary>
    internal MtpEntry? FindByPath(params string[] segments) {
        if (_disposed || segments.Length == 0) {
            return null;
        }

        var level = RootEntries;
        uint parentId = MtpInterop.TopLevelReportedParentId;
        MtpEntry? current = null;

        for (var i = 0; i < segments.Length; i++) {
            current = FindChild(level, parentId, segments[i]);
            if (current is not { } found) {
                return null;
            }

            parentId = found.ItemId;
            if (i < segments.Length - 1) {
                level = ListLevel(_device, StorageId, parentId);
            }
        }

        return current;
    }

    /// <summary>
    /// 在一层里按名字找一个直接子项(纯函数,便于用合成的层级单测)。
    ///
    /// 刻意**手写查找而不是 <c>FirstOrDefault</c>**:<see cref="MtpEntry"/> 是值类型,
    /// <c>FirstOrDefault</c> 没命中时返回的是 <c>default</c>(全零的假条目)而不是 null,
    /// 用 <c>is not { } x</c> 判空**恒为真** —— 这会让"找不到"被当成"找到了一个 id=0 的对象",
    /// 真机探测时正是这样把空列表报成了命中。
    /// </summary>
    /// <param name="expectedParentId">该层条目的父 id。顶层传 0(设备回报值,不是查询用的 0xFFFFFFFF)。</param>
    internal static MtpEntry? FindChild(IReadOnlyList<MtpEntry> level, uint expectedParentId, string name) {
        foreach (var entry in level) {
            if (entry.ParentId == expectedParentId &&
                string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase)) {
                return entry;
            }
        }

        return null;
    }

    /// <summary>把设备上的对象下载到本地路径(整文件)。</summary>
    internal bool Download(uint fileId, string targetPath, IProgress<long>? progress = null) {
        if (_disposed || _device == IntPtr.Zero) {
            return false;
        }

        // 不能把托管委托直接交给原生回调(可能被 GC 移动),用局部变量持有到调用结束。
        MtpInterop.ProgressCallback? callback = null;
        if (progress != null) {
            callback = (sent, total, _) => {
                progress.Report((long)sent);
                return 0;   // 返回非 0 会中断传输
            };
        }

        var code = MtpInterop.LIBMTP_Get_File_To_File(_device, fileId, targetPath, callback, IntPtr.Zero);
        GC.KeepAlive(callback);

        if (code != 0) {
            AppLog.Write($"[MtpDeviceSession] 下载失败 fileId={fileId} code={code}");
            return false;
        }

        return true;
    }

    /// <summary>取一层目录。失败返回空列表并记日志(不抛:设备上的目录偶尔取不到不该炸掉调用方)。</summary>
    private static List<MtpEntry> ListLevel(IntPtr device, uint storageId, uint parentId) {
        var head = MtpInterop.LIBMTP_Get_Files_And_Folders(device, storageId, parentId);
        if (head == IntPtr.Zero) {
            AppLog.Write($"[MtpDeviceSession] 列目录失败 storageId={storageId} parentId={parentId}");
            return [];
        }

        return MtpInterop.ReadFileLevel(head);
    }

    public void Dispose() {
        if (_disposed) {
            return;
        }
        _disposed = true;

        if (_device != IntPtr.Zero) {
            // 释放后设备通常会重新枚举一次(macOS 的 USB reset),这不是错误 —— 见类型注释。
            MtpInterop.LIBMTP_Release_Device(_device);
            _device = IntPtr.Zero;
        }
    }
}

/// <summary>设备上的一个对象(文件或文件夹),来自 libmtp 的文件链表。</summary>
internal readonly record struct MtpEntry(
    uint ItemId, uint ParentId, uint StorageId, string Name, ulong Size, int FileType) {
    internal bool IsFolder => FileType == MtpInterop.FileType.Folder;
}
