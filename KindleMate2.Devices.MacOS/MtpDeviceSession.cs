using System.Runtime.InteropServices;
using KindleMate2.Shared.Diagnostics;

namespace KindleMate2.Devices.MacOS;

/// <summary>
/// 一个打开的 MTP 设备会话 —— 把 libmtp 的原始指针包装成可用的对象模型。
///
/// 生命周期:MTP 是**单会话**协议(同一时刻只允许一个客户端连着设备),因此:
/// <list type="bullet">
///   <item>打开与释放必须配对。历史上的大坑已经解决:libmtp 会在关闭会话时复位 USB 口
///         (设备标记 FORCE_RESET_ON_CLOSE),真机实测"关一次会话设备就离开总线、必须物理重插",
///         连"先导入再写回"这种两段式流程都做不完。现在我们在传进去的结构体里清掉了那一位
///         (见 <see cref="TryOpenKindle"/> 的注释),连续会话不再需要重插;
///         但 MTP 仍是单会话协议,别长期占着设备不放。</item>
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
    /// 打开第一台 **Amazon** MTP 设备。返回 null 表示**没有可用设备**:没插、没点系统那个
    /// "允许配件连接"、或已被别的 MTP 客户端占用。失败细节只写日志 —— 对调用方而言这就是"没连上"。
    ///
    /// 只认 Amazon 的 VID(`0x1949`)是有意的:① 本应用是给 Kindle 用的,别的 MTP 设备(手机、相机)
    /// 上根本不会有我们要的文件;② 下面那句"清掉 FORCE_RESET_ON_CLOSE"是针对 Kindle 这个怪癖的补偿,
    /// **不该施加到别的设备上** —— 那个标记对它们可能是必需的。
    /// </summary>
    internal static MtpDeviceSession? TryOpenKindle() {
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

                if (!KindleUsbProbe.IsAmazonVendor(rawDevice.DeviceEntry.VendorId)) {
                    continue;   // 不是 Kindle,不碰
                }

                // 主动清掉「关闭会话时复位 USB 口」这一位。
                //
                // 为什么必须清:本机 Kindle(PID 0x9981)在 libmtp 设备库里带 DEVICE_FLAG_FORCE_RESET_ON_CLOSE,
                // 而 libusb1-glue.c 的 close_usb() 会据此调 libusb_reset_device()。真机实测后果是:
                // **关闭一次会话后设备就离开总线,必须物理重插才能再打开** —— 于是
                // "先导入、再写回"这类两段式操作根本做不完(SyncToKindle 就是这么两步)。
                //
                // 为什么可以在这里清:设备标记是 libmtp 在 Detect 时填进**返回给我们的结构体**的
                // (libusb1-glue.c: retdevs[i].device_entry.device_flags = mtp_device_table[j].device_flags),
                // 而 Open 又把它整体拷进内部状态(memcpy(&ptp_usb->rawdevice, device, ...)),
                // 所有 FLAG_* 判断读的都是这份副本。所以改这里等价于改设备库,**libmtp 无需修改**
                // (也就没有"修改后须按 LGPL 公开"的义务)。
                //
                // 风险与验证:libmtp 给设备打这个标记是因为"有些设备不复位就连不上第二次"。
                // 所以我们用真机用例钉住"连续两次会话都能打开"—— 若哪天这台设备或新固件开始需要复位,
                // 那条用例会红,届时再考虑把这行去掉。
                rawDevice.DeviceEntry.DeviceFlags &= ~MtpInterop.DeviceFlagForceResetOnClose;

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

    /// <summary>
    /// 按路径取一个**小文本文件**的内容(用于 <c>system/version.txt</c>),取不到返回空串。
    /// 在已打开的会话里做,所以调用方不必为"读 27 个字节"再开一次会话。
    /// </summary>
    internal string ReadFileText(params string[] segments) {
        if (_disposed) {
            return string.Empty;
        }

        if (FindByPath(segments) is not { } file) {
            return string.Empty;
        }

        var tempPath = Path.Combine(Path.GetTempPath(), "km2-mtp-read-" + Guid.NewGuid().ToString("N")[..8]);
        try {
            return Download(file.ItemId, tempPath) ? File.ReadAllText(tempPath).Trim() : string.Empty;
        } catch (Exception ex) {
            AppLog.Write($"[MtpDeviceSession] 读取文本文件失败:{ex}");
            return string.Empty;
        } finally {
            try { File.Delete(tempPath); } catch { /* 临时文件删不掉不影响结果 */ }
        }
    }

    /// <summary>删除设备上的一个对象。</summary>
    internal bool Delete(uint objectId) {
        if (_disposed || _device == IntPtr.Zero) {
            return false;
        }

        var code = MtpInterop.LIBMTP_Delete_Object(_device, objectId);
        if (code != 0) {
            AppLog.Write($"[MtpDeviceSession] 删除失败 objectId={objectId} code={code}");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 往设备的一个目录里上传文件。
    ///
    /// 注意:libmtp 的 <c>Send_File_From_File</c> **只会新建对象**,同名文件不会被替换
    /// (读了 libmtp.c 的实现确认:它只是填 SendObjectInfo 再传数据,没有任何同名检查),
    /// 所以"覆盖"必须靠调用方先 <see cref="Delete"/> 再上传。
    /// </summary>
    /// <param name="localPath">本地源文件。</param>
    /// <param name="fileName">设备上显示的文件名。</param>
    /// <param name="parentId">目标目录的 item_id(如 documents 的 id)。</param>
    /// <param name="storageId">真实存储区 id。</param>
    /// <param name="fileType">MTP 文件类型(如 <see cref="MtpInterop.FileType.Text"/>,27)。</param>
    internal bool Upload(string localPath, string fileName, uint parentId, uint storageId, int fileType) {
        if (_disposed || _device == IntPtr.Zero) {
            return false;
        }

        if (!File.Exists(localPath)) {
            AppLog.Write($"[MtpDeviceSession] 上传源文件不存在:{localPath}");
            return false;
        }

        // filename 是 char*,得自己分配 UTF-8 内存并在调用后释放 —— 不能直接把托管字符串塞进去。
        var fileNamePtr = Marshal.StringToCoTaskMemUTF8(fileName);
        try {
            var fileData = new MtpInterop.MtpFile {
                ItemId = 0,                  // 0 = 由设备分配
                ParentId = parentId,
                StorageId = storageId,
                Filename = fileNamePtr,
                FileSize = (ulong)new FileInfo(localPath).Length,
                ModificationDate = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                FileType = fileType,
                Next = IntPtr.Zero,
            };

            var code = MtpInterop.LIBMTP_Send_File_From_File(_device, localPath, ref fileData, null, IntPtr.Zero);
            if (code != 0) {
                AppLog.Write($"[MtpDeviceSession] 上传失败 '{fileName}' code={code}");
                return false;
            }

            return true;
        } finally {
            Marshal.FreeCoTaskMem(fileNamePtr);
        }
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
            // 已清掉 FORCE_RESET_ON_CLOSE,所以释放不再复位 USB 口、设备也不会离开总线。
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
