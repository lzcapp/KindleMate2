using System.Reflection;
using System.Runtime.InteropServices;

namespace KindleMate2.Devices.MacOS;

/// <summary>
/// libmtp 的最小 P/Invoke 绑定 —— 只覆盖本应用真正用到的那几个函数。
///
/// 签名与结构体布局**逐条对照实际安装的 libmtp 1.1.23 头文件**
/// (<c>/opt/homebrew/opt/libmtp/include/libmtp.h</c>,行号见各成员注释);导出符号也用
/// <c>nm -gU libmtp.9.dylib</c> 核对过存在。改这里之前请重新核对头文件,别凭记忆改:
/// 结构体布局错的后果不是编译失败,而是在运行时**静默写坏内存**。
///
/// 许可:libmtp 与 libusb 均为 LGPL-2.1-or-later。随包分发这两个动态库时,必须同时提供
/// 其源码(见项目 README 的第三方许可说明),这属于分发义务、与是否修改无关。
/// </summary>
internal static class MtpInterop {
    private const string LibraryName = "libmtp";

    /// <summary>
    /// libusb 的逻辑库名。<see cref="KindleUsbProbe"/> 只做**只读枚举**也用它 ——
    /// 注意 <c>NativeLibrary.SetDllImportResolver</c> **每个程序集只能设置一次**,
    /// 所以两个库的解析必须都由本类的解析器负责,不能另开一个。
    /// </summary>
    internal const string LibusbLibraryName = "libusb-1.0";

    /// <summary>libmtp 在 macOS 上等待写事务/设备响应的默认超时(毫秒),与 CLI 工具一致。</summary>
    internal const int DefaultTimeoutMs = 5000;

    static MtpInterop() {
        // 不能只靠 [DllImport("libmtp")] 的默认搜索路径:Homebrew 装在 /opt/homebrew/lib,
        // 而 .app 里我们把它放进 Contents/Frameworks —— 两处都不在 dyld 的默认搜索范围内。
        EnsureLibraryResolver();
    }

    private static int _resolverRegistered;

    /// <summary>
    /// 注册本程序集的 DllImport 解析器(**幂等**)。
    ///
    /// 之所以要单独暴露:<c>SetDllImportResolver</c> 每个程序集只能设一次,而解析器只挂在
    /// <see cref="MtpInterop"/> 的静态构造上 —— 其他类(如 <see cref="KindleUsbProbe"/>)自己发起
    /// DllImport 时,如果此前没碰过本类,静态构造不会跑、解析器就没注册,于是明明装着的库也「找不到」。
    /// 实测踩过:探测 libusb 时直接报不可用。所以每个用原生库的类都要先调这个方法。
    /// </summary>
    internal static void EnsureLibraryResolver() {
        if (Interlocked.Exchange(ref _resolverRegistered, 1) == 0) {
            NativeLibrary.SetDllImportResolver(typeof(MtpInterop).Assembly, Resolve);
        }
    }

    private static IntPtr Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPaths) {
        var candidates = CandidatePathsFor(libraryName);
        if (candidates.Count == 0) {
            return IntPtr.Zero;   // 不认识的库名,交给默认规则
        }

        foreach (var candidate in candidates) {
            if (NativeLibrary.TryLoad(candidate, out var handle)) {
                LoadedPath = candidate;
                return handle;
            }
        }

        return IntPtr.Zero;   // 交给默认规则再试(例如用户自己设了 DYLD_LIBRARY_PATH)
    }

    /// <summary>实际加载到的动态库路径;未加载成功时为 null。</summary>
    internal static string? LoadedPath { get; private set; }

    /// <summary>
    /// 候选目录,按优先级:**随包分发的**(发布版在 <c>Contents/Frameworks</c>)优先,
    /// 再退到 Homebrew 的安装位置(开发机 / 用户自行 brew install 的情形)。
    /// </summary>
    internal static IEnumerable<string> CandidateDirectories() {
        yield return AppContext.BaseDirectory;                                    // 开发布局:与程序同目录
        yield return Path.Combine(AppContext.BaseDirectory, "..", "Frameworks");   // .app 内
        yield return "/opt/homebrew/lib";                                         // Apple Silicon Homebrew
        yield return "/usr/local/lib";                                            // Intel Homebrew / 手工安装
    }

    /// <summary>逻辑库名 → 实际文件名(macOS 上带版本号后缀)。不认识的库名返回空。</summary>
    private static string[] FileNamesFor(string libraryName) => libraryName switch {
        LibraryName => ["libmtp.9.dylib", "libmtp.dylib"],
        LibusbLibraryName => ["libusb-1.0.0.dylib", "libusb-1.0.dylib"],
        _ => [],
    };

    /// <summary>
    /// 某个库的候选完整路径。顺序刻意是「首选文件名 × 各目录」再看备选文件名 ——
    /// 这样 <c>.app</c> 里的首选名一定排在最前(测试钉住了这一顺序)。
    /// </summary>
    private static IReadOnlyList<string> CandidatePathsFor(string libraryName) {
        var paths = new List<string>();
        foreach (var fileName in FileNamesFor(libraryName)) {
            foreach (var directory in CandidateDirectories()) {
                paths.Add(Path.Combine(directory, fileName));
            }
        }
        return paths;
    }

    /// <summary>libmtp 的候选路径(供测试断言顺序)。</summary>
    internal static IEnumerable<string> CandidateLibraryPaths() => CandidatePathsFor(LibraryName);

    /// <summary>libmtp 是否可用(未捕获异常地探一次)。</summary>
    internal static bool IsAvailable() {
        if (LoadedPath != null) {
            return true;
        }

        foreach (var candidate in CandidateLibraryPaths()) {
            if (File.Exists(candidate)) {
                LoadedPath = candidate;
                return true;
            }
        }

        return false;
    }

    // ————————————————————— 函数(对照 libmtp.h 行号) —————————————————————

    /// <summary><c>void LIBMTP_Init(void);</c>(libmtp.h:832)</summary>
    [DllImport(LibraryName)]
    internal static extern void LIBMTP_Init();

    /// <summary>
    /// <c>LIBMTP_error_number_t LIBMTP_Detect_Raw_Devices(LIBMTP_raw_device_t **, int *);</c>(libmtp.h:839)
    /// 返回 0(<c>LIBMTP_ERROR_NONE</c>)表示成功;5 为无设备,其余见 <see cref="MtpError"/>。
    /// 列表由库分配,用完必须 <see cref="LIBMTP_FreeMemory"/>。
    /// </summary>
    [DllImport(LibraryName)]
    internal static extern int LIBMTP_Detect_Raw_Devices(out IntPtr devices, out int deviceCount);

    /// <summary>
    /// <c>LIBMTP_mtpdevice_t *LIBMTP_Open_Raw_Device_Uncached(LIBMTP_raw_device_t *);</c>(libmtp.h:842)
    /// 成功返回设备句柄,失败返回 NULL(错误细节在 errorstack 里)。
    /// </summary>
    [DllImport(LibraryName)]
    internal static extern IntPtr LIBMTP_Open_Raw_Device_Uncached(ref MtpRawDevice rawDevice);

    /// <summary>
    /// <c>int LIBMTP_Get_Storage(LIBMTP_mtpdevice_t *, int const);</c>(libmtp.h:880)
    /// **列目录前必须先调用**:它枚举设备上的存储区并填进设备内部链表;不调的话
    /// <see cref="LIBMTP_Get_Filelisting_With_Callback"/> 会返回空列表(libmtp 的 CLI 工具
    /// 与示例都遵循这个顺序)。第二个参数是排序方式,见 <see cref="StorageSort"/>
    /// </summary>
    [DllImport(LibraryName)]
    internal static extern int LIBMTP_Get_Storage(IntPtr device, int sortBy);

    /// <summary><c>LIBMTP_STORAGE_SORTBY_*</c>(libmtp.h:876 起)</summary>
    internal static class StorageSort {
        public const int NotSorted = 0;
        public const int MaxFreeSpace = 1;
        public const int MaxSpace = 2;
    }

    /// <summary>
    /// <c>void LIBMTP_Release_Device(LIBMTP_mtpdevice_t*);</c>(libmtp.h:852)
    /// </summary>
    [DllImport(LibraryName)]
    internal static extern void LIBMTP_Release_Device(IntPtr device);

    /// <summary><c>char *LIBMTP_Get_Modelname(LIBMTP_mtpdevice_t*);</c>(libmtp.h:856) 返回 UTF-8,勿释放。</summary>
    [DllImport(LibraryName)]
    internal static extern IntPtr LIBMTP_Get_Modelname(IntPtr device);

    /// <summary><c>char *LIBMTP_Get_Friendlyname(LIBMTP_mtpdevice_t*);</c>(libmtp.h:859) 返回 UTF-8,勿释放。</summary>
    [DllImport(LibraryName)]
    internal static extern IntPtr LIBMTP_Get_Friendlyname(IntPtr device);

    /// <summary><c>void LIBMTP_Dump_Errorstack(LIBMTP_mtpdevice_t*);</c>(libmtp.h:872) 打到 stderr,仅供诊断。</summary>
    [DllImport(LibraryName)]
    internal static extern void LIBMTP_Dump_Errorstack(IntPtr device);

    /// <summary><c>void LIBMTP_FreeMemory(void *);</c>(libmtp.h:874)</summary>
    [DllImport(LibraryName)]
    internal static extern void LIBMTP_FreeMemory(IntPtr memory);

    /// <summary>
    /// <c>LIBMTP_file_t *LIBMTP_Get_Files_And_Folders(LIBMTP_mtpdevice_t *, uint32_t const storageId, uint32_t const parentId);</c>
    /// (libmtp.h:925) —— **列一层目录的正确入口**,也是官方 <c>mtp-files</c> 用的那个。
    ///
    /// 两个参数都不是随便传的(真机实测):
    /// <list type="bullet">
    ///   <item><b>根一层</b>:storageId 传 <see cref="FilesAndFoldersRoot"/>(= 所有存储区)、
    ///         parentId 同样传 <see cref="FilesAndFoldersRoot"/>;</item>
    ///   <item><b>子目录</b>:storageId 必须传**真实的存储区 id**(从根一层的任意条目读 <c>storage_id</c>,
    ///         本机 Kindle 上是 65537)。子目录再传 0xFFFFFFFF 会得到
    ///         <c>PTP Layer error 02fe: PTP Data Expected</c> —— 每一层都失败,而且会把设备惹到掉线。</item>
    /// </list>
    ///
    /// 返回值是链表(含目录);每个节点都要用 <see cref="LIBMTP_destroy_file_t"/> 释放。
    /// </summary>
    [DllImport(LibraryName)]
    internal static extern IntPtr LIBMTP_Get_Files_And_Folders(IntPtr device, uint storageId, uint parentId);

    /// <summary>
    /// <c>void LIBMTP_destroy_file_t(LIBMTP_file_t*);</c>(libmtp.h:917) —— 逐节点释放
    /// <see cref="LIBMTP_Get_Files_And_Folders"/> 返回的链表。
    ///
    /// 必须用它,而不是自己 <see cref="LIBMTP_FreeMemory"/> 一个节点:前者会连带释放该节点的
    /// filename 字符串;手写版只释放节点本身,文件名就泄漏了(初版就是这么写的,被 clang 的
    /// "did you mean LIBMTP_destroy_file_t" 纠正过来)。
    /// </summary>
    [DllImport(LibraryName)]
    internal static extern void LIBMTP_destroy_file_t(IntPtr file);

    /// <summary>
    /// <c>int LIBMTP_Get_File_To_File(LIBMTP_mtpdevice_t*, uint32_t, char const * const, LIBMTP_progressfunc_t const, void const * const);</c>(libmtp.h:933)
    /// 返回 0 为成功,非 0 为出错码(见 errorstack)。
    /// </summary>
    [DllImport(LibraryName)]
    internal static extern int LIBMTP_Get_File_To_File(
        IntPtr device, uint fileId, [MarshalAs(UnmanagedType.LPUTF8Str)] string targetPath,
        ProgressCallback? callback, IntPtr data);

    /// <summary>
    /// <c>int LIBMTP_Send_File_From_File(LIBMTP_mtpdevice_t *, char const * const, LIBMTP_file_t * const, LIBMTP_progressfunc_t const, void const * const);</c>(libmtp.h:946)
    /// 第三个参数提供目标元数据(storage_id / parent_id / filename / filesize / filetype),返回 0 为成功。
    /// </summary>
    [DllImport(LibraryName)]
    internal static extern int LIBMTP_Send_File_From_File(
        IntPtr device, [MarshalAs(UnmanagedType.LPUTF8Str)] string sourcePath, ref MtpFile fileData,
        ProgressCallback? callback, IntPtr data);

    /// <summary><c>int LIBMTP_Delete_Object(LIBMTP_mtpdevice_t *, uint32_t);</c>(libmtp.h:1061)</summary>
    [DllImport(LibraryName)]
    internal static extern int LIBMTP_Delete_Object(IntPtr device, uint objectId);

    /// <summary>
    /// <c>#define LIBMTP_FILES_AND_FOLDERS_ROOT 0xffffffff</c>(libmtp.h:923)。
    /// 列根一层时 storageId 与 parentId 都传它。
    /// </summary>
    internal const uint FilesAndFoldersRoot = 0xFFFFFFFF;

    /// <summary>
    /// <c>DEVICE_FLAG_FORCE_RESET_ON_CLOSE</c>(device-flags.h:276)= 关闭会话时复位 USB 口。
    ///
    /// 本应用**主动清掉这一位**(见 <see cref="MtpDeviceSession"/>):它对我们这台 Kindle 的副作用是灾难性的 ——
    /// 每次关闭会话都复位 USB 口 → 设备离开总线 → **要物理重插才能再用**,
    /// 于是"先导入、再写回"这种两段式操作根本做不完。
    /// </summary>
    internal const uint DeviceFlagForceResetOnClose = 0x10000000;

    /// <summary>
    /// 顶层对象**回报**的 parent_id。注意与查询参数不是一回事:查询根一层要传
    /// <see cref="FilesAndFoldersRoot"/>,而设备回给我们的顶层条目 parent_id 是 0(真机实测),
    /// 只有子条目才等于父目录的 item_id。判定"这一条是不是顶层"要用这个值。
    /// </summary>
    internal const uint TopLevelReportedParentId = 0;

    // ————————————————————— 结构体(布局必须与 C ABI 逐字段一致) —————————————————————

    /// <summary><c>struct LIBMTP_device_entry_struct</c>(libmtp.h:440 附近)—— 64 位下 40 字节。</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct MtpDeviceEntry {
        public IntPtr Vendor;       // char* vendor
        public ushort VendorId;     // uint16_t vendor_id
        public IntPtr Product;      // char* product
        public ushort ProductId;    // uint16_t product_id
        public uint DeviceFlags;    // uint32_t device_flags
    }

    /// <summary><c>struct LIBMTP_raw_device_struct</c>(libmtp.h:445 附近)—— 64 位下 48 字节。</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct MtpRawDevice {
        public MtpDeviceEntry DeviceEntry;
        public uint BusLocation;    // uint32_t bus_location
        public byte Devnum;         // uint8_t devnum
    }

    /// <summary>
    /// <c>struct LIBMTP_file_struct</c>(libmtp.h:694)—— 64 位下 56 字节。
    /// 字段顺序/宽度错位不会报错,只会读到垃圾值,故有单测按 ABI 断言尺寸。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct MtpFile {
        public uint ItemId;             // uint32_t item_id
        public uint ParentId;           // uint32_t parent_id
        public uint StorageId;          // uint32_t storage_id
        public IntPtr Filename;         // char* filename(UTF-8)
        public ulong FileSize;          // uint64_t filesize
        public long ModificationDate;   // time_t modification_date
        public int FileType;            // LIBMTP_filetype_t filetype(枚举 → int)
        public IntPtr Next;             // LIBMTP_file_t* next
    }

    /// <summary>
    /// <c>LIBMTP_progressfunc_t</c>(libmtp.h:483)。注意参数是 <c>uint64_t</c> —— 老版本曾是 uint32_t,
    /// 宽度写错会让回调读到垃圾值。返回值非 0 会中断传输。
    /// </summary>
    internal delegate int ProgressCallback(ulong sent, ulong total, IntPtr data);

    // ————————————————————— 常量与辅助 —————————————————————

    /// <summary><c>LIBMTP_filetype_t</c> 里本应用用得到的取值(枚举按顺序从 0 起)。</summary>
    internal static class FileType {
        public const int Folder = 0;
        public const int Text = 27;
        public const int Unknown = 44;
    }

    /// <summary><c>LIBMTP_error_number_t</c>(libmtp.h:448)本应用需要辨别的取值。</summary>
    internal static class MtpError {
        public const int None = 0;
        public const int General = 1;
        public const int PtpLayer = 2;
        public const int UsbLayer = 3;
        public const int MemoryAllocation = 4;
        public const int NoDeviceAttached = 5;
        public const int StorageFull = 6;
        public const int Connecting = 7;
        public const int Cancelled = 8;

        public static string Describe(int code) => code switch {
            None => "无错误",
            General => "一般错误",
            PtpLayer => "PTP 层错误",
            UsbLayer => "USB 层错误",
            MemoryAllocation => "内存分配失败",
            NoDeviceAttached => "没有连接 MTP 设备",
            StorageFull => "设备存储已满",
            Connecting => "连接设备失败",
            Cancelled => "操作被取消",
            _ => $"未知错误码 {code}"
        };
    }

    /// <summary>把 <c>char*</c>(UTF-8)读成托管字符串;空指针返回空串。</summary>
    internal static string ReadUtf8(IntPtr pointer) =>
        pointer == IntPtr.Zero ? string.Empty : Marshal.PtrToStringUTF8(pointer) ?? string.Empty;

    /// <summary>
    /// 释放 <see cref="LIBMTP_Get_Files_And_Folders"/> 返回的一层链表。
    /// 逐节点用 <see cref="LIBMTP_destroy_file_t"/>(它会连带释放该节点的 filename),
    /// 先把 next 取出来再释放当前节点,免得读了已释放的内存。
    /// </summary>
    internal static void DestroyFileLevel(IntPtr head) {
        var node = head;
        while (node != IntPtr.Zero) {
            var next = Marshal.PtrToStructure<MtpFile>(node).Next;
            LIBMTP_destroy_file_t(node);
            node = next;
        }
    }

    /// <summary>把一层链表读成托管条目列表,读完即释放。</summary>
    internal static List<MtpEntry> ReadFileLevel(IntPtr head) {
        var entries = new List<MtpEntry>();
        if (head == IntPtr.Zero) {
            return entries;
        }

        try {
            var node = head;
            while (node != IntPtr.Zero) {
                var file = Marshal.PtrToStructure<MtpFile>(node);
                entries.Add(new MtpEntry(
                    file.ItemId, file.ParentId, file.StorageId,
                    ReadUtf8(file.Filename), file.FileSize, file.FileType));
                node = file.Next;
            }
        } finally {
            DestroyFileLevel(head);
        }

        return entries;
    }
}
