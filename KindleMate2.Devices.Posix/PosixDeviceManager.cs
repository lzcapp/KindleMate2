using KindleMate2.Application.Models;
using KindleMate2.Application.Services;
using KindleMate2.Shared;
using KindleMate2.Shared.Constants;
using KindleMate2.Shared.Diagnostics;
using KindleMate2.Shared.Entities;
using KindleMate2.Shared.Threading;

namespace KindleMate2.Devices.Posix;

/// <summary>
/// POSIX 平台(macOS 与 Linux)**共用**的 Kindle 设备检测与同步实现:扫候选挂载点 + 内容特征判定 +
/// 定时轮询监听,外加经 libmtp 的 MTP 支持。接口 <see cref="IDeviceManager"/> 定义在 Application 层,
/// 各平台项目(macOS / Linux)各出一个薄壳传入自己的挂载点根。
///
/// 之所以共享:USB 那半边两端逻辑完全一样,只有"卷挂在哪里"不同 ——
/// macOS 都在 <c>/Volumes</c> 下,而 Linux 随发行版/桌面环境有 <c>/media/&lt;用户&gt;</c>、
/// <c>/run/media/&lt;用户&gt;</c>、<c>/mnt</c> 几种。复制两份实现只会让修一处忘一处。
///
/// 与 <c>KindleMate2.Devices.Windows.DeviceManager</c> 的对应关系(语义一致,手段不同):
/// ① 发现:Windows 靠 <c>DriveInfo.GetDrives()</c> 筛可移动盘;POSIX 没有盘符概念 —— Kindle 在
///    「USB 大容量存储模式」下由系统挂载到某个目录,故按候选根枚举子目录。
/// ② 判定:两端都以「卷内存在 documents/My Clippings.txt」为唯一依据。POSIX 上尤其不能靠卷名猜:
///    macOS 的 /Volumes 下混着系统卷、恢复卷与磁盘映像挂载卷(Macintosh HD、Recovery、*.dmg),
///    Linux 的 /media 下也可能有别的 U 盘;而 .NET 在 Unix 上对 <c>DriveType</c> 的取值并不稳定。
/// ③ 监听:Windows 靠 WMI 事件;这里改为定时轮询挂载点快照(<see cref="DefaultPollInterval"/>),
///    发现变化后再走一段防抖 <see cref="DefaultDebounceInterval"/>,与 Windows 实现的防抖时长一致 ——
///    挂载刚出现时文件系统可能尚未就绪,立刻上报会得到「时连时断」的抖动。
/// ④ MTP:走 <see cref="MtpDeviceSession"/>(libmtp),macOS 与 Linux 同一套代码 ——
///    2024 年及以后发布的 Kindle 只支持 MTP,在 POSIX 上既不会挂载为磁盘,也没有 WPD 那种系统级支持。
/// </summary>
public class PosixDeviceManager : IDeviceManager {
    /// <summary>从设备取回的文件数(My Clippings.txt + vocab.db),用于按「第几个文件」上报进度。</summary>
    private const int DeviceFileCount = 2;


    /// <summary>轮询间隔。挂载/卸载是低频事件,遍历几个卷目录的开销可忽略。</summary>
    public static readonly TimeSpan DefaultPollInterval = TimeSpan.FromSeconds(2);

    /// <summary>状态变化后的防抖时长,与 Windows 实现取同一值。</summary>
    public static readonly TimeSpan DefaultDebounceInterval = TimeSpan.FromMilliseconds(2500);

    private readonly string _versionFilePath;
    /// <summary>候选挂载点根目录。macOS 只有 <c>/Volumes</c>;Linux 有 <c>/media/&lt;用户&gt;</c>、
    /// <c>/run/media/&lt;用户&gt;</c>、<c>/mnt</c> 等几种(发行版/桌面环境不同),所以要按列表依次扫。</summary>
    private readonly IReadOnlyList<string> _volumeRoots;
    private readonly TimeSpan _pollInterval;
    private readonly bool _detectMtpDevices;

    /// <summary>守连接状态(<see cref="_driveLetter"/> / <see cref="_deviceType"/>),与 Windows 实现同名同职责。</summary>
    private readonly object _lockObj = new object();

    /// <summary>守 <see cref="_reportedConnected"/>,与连接状态分开,避免上报路径持锁过久。</summary>
    private readonly object _reportedLockObj = new object();

    private Device.Type _deviceType = Device.Type.Unknown;
    private string _driveLetter = string.Empty;

    /// <summary>
    /// MTP 机型最近一次会话里读到的 <c>system/version.txt</c> 内容(USB 机型不用它,直接读卷)。
    /// 之所以缓存:版本是"顺手取"的,不该为它单独开一次会话;写的是字符串引用,赋值本身是原子的。
    /// </summary>
    private string _mtpVersionText = string.Empty;

    private CancellationTokenSource? _pollCts;
    private Task? _pollTask;

    /// <summary>
    /// 观测到变化后的防抖上报器。释放竞态(取消轮询不等待在飞的那一轮)统一由
    /// <see cref="DebouncedAction"/> 处理,与 Windows 实现共用同一份语义。
    /// </summary>
    private readonly DebouncedAction _debounce;

    /// <summary>
    /// 上一次「已上报」的连接状态,即 UI 当前知道的状态。防抖到期复检后与它不一致才上报。
    /// null 表示尚未上报过(首次探测不算变化,与 Windows 实现一致:启动时不触发事件)。
    /// </summary>
    private bool? _reportedConnected;

    /// <summary>
    /// 最近一次轮询**观测**到的状态。它单独存在是必需的:防抖只能由「观测值发生变化」启动,
    /// 不能用「观测值 ≠ 已上报值」来判断 —— 轮询间隔(2s)短于防抖窗口(2.5s),后者会让每一轮
    /// 都把防抖重置,计时器永远等不到到期,设备拔除后事件一次都不会上报(实测踩过)。
    /// </summary>
    private bool? _observedConnected;

    public Device.Type DeviceType => _deviceType;

    /// <summary>当前 Kindle 卷的挂载路径(如 <c>/Volumes/Kindle</c>);未连接时为空串。</summary>
    public string DriveLetter => _driveLetter;

    public bool IsConnected => !string.IsNullOrWhiteSpace(_driveLetter);

    public event Action<bool>? ConnectionChanged;

    /// <summary>
    /// 构造设备管理器。
    ///
    /// <paramref name="volumesRoot"/> 与两个间隔都可注入,默认值即生产配置 —— 注入点是**为测试留的**:
    /// 没有它,"扫哪些卷、变化后多久上报"这段逻辑只能靠改源码常量去验(此前正是用 sed 改副本跑的),
    /// 于是它无法进入 <c>KindleMate2.Tests</c>,也就无从回归。
    /// </summary>
    /// <param name="versionFilePath">设备侧 version.txt 的相对路径(相对卷根)。</param>
    /// <param name="volumeRoots">候选挂载点根目录(按顺序扫,取第一个命中)。</param>
    /// <param name="pollInterval">轮询间隔,默认 <see cref="DefaultPollInterval"/>。</param>
    /// <param name="debounceInterval">防抖时长,默认 <see cref="DefaultDebounceInterval"/>。</param>
    /// <param name="detectMtpDevices">
    /// 是否启用 MTP 机型支持(**检测 + 导入/写回**),默认开。
    /// 关掉它是**为了测试可确定性**:真机插着 Kindle 时,那些"没连接就该失败"的断言会被真实设备
    /// 影响(实测踩过:插着设备时 ImportFilesFromDevice 真的走进了 MTP 分支并成功),
    /// 而 CI 上没有设备 —— 同一个用例在两种环境给出不同结果是最糟的。
    /// </param>
    public PosixDeviceManager(string versionFilePath,
        IReadOnlyList<string> volumeRoots,
        TimeSpan? pollInterval = null,
        TimeSpan? debounceInterval = null,
        bool detectMtpDevices = true) {
        _versionFilePath = versionFilePath;
        _volumeRoots = volumeRoots;
        _pollInterval = pollInterval ?? DefaultPollInterval;
        _detectMtpDevices = detectMtpDevices;
        _debounce = new DebouncedAction(debounceInterval ?? DefaultDebounceInterval, OnDebounceElapsed);
    }

    public void StartWatching() {
        // 先探一次并记为「已知状态」,这样启动瞬间不会平白上报一次变化。
        var connected = IsKindleConnected();
        lock (_reportedLockObj) {
            _observedConnected = connected;
            _reportedConnected = connected;
        }

        _pollCts = new CancellationTokenSource();
        _pollTask = Task.Run(() => PollLoopAsync(_pollCts.Token));
    }

    private async Task PollLoopAsync(CancellationToken token) {
        try {
            using var timer = new PeriodicTimer(_pollInterval);
            while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false)) {
                var connected = IsKindleConnected();
                lock (_reportedLockObj) {
                    // 只在观测值真正发生变化时启动防抖。切勿写成「与 _reportedConnected 不同」:
                    // 防抖窗口比轮询间隔长,那样每轮都会重置计时器,永远等不到到期。
                    if (_observedConnected == connected) {
                        continue;
                    }
                    _observedConnected = connected;
                }
                _debounce.Schedule();
            }
        } catch (OperationCanceledException) {
            // Dispose 取消轮询,属正常退出路径
        } catch (Exception ex) {
            AppLog.Write($"[PosixDeviceManager.PollLoop] {ex}");
        }
    }

    /// <summary>
    /// 防抖到期:复检一次真实状态,与「已上报」不同才发事件。释放后晚到的调度由
    /// <see cref="DebouncedAction"/> 挡掉,不会在这里补报。
    /// </summary>
    private void OnDebounceElapsed() {
        var connected = IsKindleConnected();
        lock (_reportedLockObj) {
            // 复检结果同样是「已观测」状态,避免下一轮轮询把它当成新变化再启动一次防抖。
            _observedConnected = connected;
            if (_reportedConnected == connected) {
                return;
            }
            _reportedConnected = connected;
        }
        // 事件在锁外触发:订阅方可能回到 UI 线程,持锁调用有死锁风险。
        ConnectionChanged?.Invoke(connected);
    }

    public bool IsKindleConnected() {
        lock (_lockObj) {
            try {
                // ① 先找 USB 大容量存储卷(2024 年以前的机型,卷挂在候选挂载点根下 —— macOS 是
                //    /Volumes,Linux 见各薄壳的 DefaultVolumeRoots)
                if (HandleUsbDevice()) {
                    return true;
                }

                // ② 再看 USB 上有没有 Amazon 的设备 —— 那是 MTP 机型(2024 年及以后)的正常状态。
                //    这里只做**只读枚举**(见 KindleUsbProbe):绝不能开 MTP 会话,
                //    本方法是被 2 秒轮询调用的:开 MTP 会话是"重"操作(libmtp 历史上还会在关闭时
                //    复位 USB 口把设备弄下线),轮询绝不能挂上去。
                _driveLetter = string.Empty;
                if (_detectMtpDevices && KindleUsbProbe.TryFindKindle(out var productId)) {
                    _deviceType = Device.Type.MTP;
                    AppLog.Write($"[IsKindleConnected] 检测到 Amazon USB 设备(PID=0x{productId:x4}),按 MTP 机型处理");
                    return true;
                }

                _deviceType = Device.Type.Unknown;
                return false;
            } catch (Exception ex) {
                AppLog.Write($"[IsKindleConnected] {ex}");
                return false;
            }
        }
    }

    public string GetKindleVersionText() {
        // MTP 机型没有卷路径,版本是从"最近一次会话"里缓存下来的 —— 见 ImportFilesViaMtp。
        // 之所以不在本方法里开会话:它可能被界面在任意时机调用,而每次会话都要重新打开设备,
        // 不能挂在一个看起来"只是读个字符串"的 getter 上。
        if (_deviceType == Device.Type.MTP) {
            return _mtpVersionText;
        }

        if (string.IsNullOrWhiteSpace(_driveLetter)) {
            return string.Empty;
        }

        var versionText = string.Empty;
        try {
            var kindleVersionPath = Path.Combine(_driveLetter, _versionFilePath);
            if (File.Exists(kindleVersionPath)) {
                using var reader = new StreamReader(kindleVersionPath);
                versionText = reader.ReadToEnd();
            }
        } catch (Exception ex) {
            AppLog.Write($"[GetKindleVersionText] {ex}");
        }
        return versionText;
    }

    private bool HandleUsbDevice() {
        try {
            // 快路径:已知的卷还在,且卷内特征文件也还在,不必重新遍历所有候选根。
            // 内容特征必须每次复查 —— 卷目录被拔掉后仍可能存在残留挂载点,
            // 只看目录存在会误报「已连接」(空挂载点)。
            if (IsKindleVolume(_driveLetter)) {
                _deviceType = Device.Type.USB;
                return true;
            }

            foreach (var volume in EnumerateVolumes()) {
                if (!IsKindleVolume(volume)) {
                    continue;
                }
                // 不保留末尾分隔符:与 Windows 的 "D:\" 不同,这里同时要用于状态栏文案
                // (Strings.Ui_Status_DeviceOnlineDrive),"/Volumes/Kindle" 更易读;
                // 而 Path.Combine 对带不带尾分隔符都能正确拼接。
                _driveLetter = volume;
                _deviceType = Device.Type.USB;
                return true;
            }

            return false;
        } catch (Exception ex) {
            AppLog.Write($"[HandleUsbDevice] {ex}");
            return false;
        }
    }

    /// <summary>
    /// 枚举候选卷。这里刻意不做任何猜测性过滤(按卷名、按卷数量、按 DriveType 都不可靠),
    /// 是否 Kindle 一律交给 <see cref="IsKindleVolume"/> 按内容判定。
    /// </summary>
    private IReadOnlyList<string> EnumerateVolumes() {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var volumes = new List<string>();

        foreach (var root in _volumeRoots) {
            try {
                if (!Directory.Exists(root)) {
                    continue;
                }

                foreach (var directory in Directory.GetDirectories(root)) {
                    // Linux 上多个候选根可能互相包含(如 /media 与 /media/<user>),去重免得同一卷扫两遍
                    if (seen.Add(directory)) {
                        volumes.Add(directory);
                    }
                }
            } catch (Exception ex) {
                // 枚举某个根失败(权限等)只能当作"这个根下没设备",不能因此让整个探测抛出去
                AppLog.Write($"[EnumerateVolumes] 跳过 '{root}':{ex.Message}");
            }
        }

        return volumes;
    }

    /// <summary>
    /// 内容特征判定:卷内存在 <c>documents/My Clippings.txt</c> 才算 Kindle 卷。
    /// 卷在枚举与检查之间被拔掉时会抛异常,那属于预期内的竞态,直接当作未命中,不写日志(否则每轮轮询刷屏)。
    /// </summary>
    private static bool IsKindleVolume(string volumeRoot) {
        if (string.IsNullOrWhiteSpace(volumeRoot) || !Directory.Exists(volumeRoot)) {
            return false;
        }

        try {
            var documentsDir = Path.Combine(volumeRoot, AppConstants.DocumentsPathName);
            return File.Exists(Path.Combine(documentsDir, AppConstants.ClippingsFileName));
        } catch {
            return false;
        }
    }

    /// <summary>
    /// Copies files from the connected Kindle device to local backup paths.
    /// <paramref name="progress"/> 按「第几个文件」上报(共 <see cref="DeviceFileCount"/> 个):
    /// 两个文件都是整文件传输,几千条标注 / 几千词的大库上这一步本身就有可感知耗时。
    ///
    /// 两条路径按机型自动选:
    /// <list type="number">
    ///   <item><b>USB 大容量存储</b> —— 2024 年以前发布的机型,卷挂在 /Volumes 下,直接拷贝;</item>
    ///   <item><b>MTP</b> —— 2024 年及以后(Paperwhite 12 代 / Colorsoft / Scribe 等)只支持 MTP,
    ///         走 <see cref="MtpDeviceSession"/>(libmtp)。</item>
    /// </list>
    /// </summary>
    public bool ImportFilesFromDevice(string backupClippingsPath, string backupWordsPath, out Exception? exception,
        IProgress<OperationProgress>? progress = null) {
        exception = null;
        try {
            if (_deviceType == Device.Type.USB && IsConnected) {
                var documentPath = Path.Combine(_driveLetter, AppConstants.DocumentsPathName);
                var vocabularyPath = Path.Combine(_driveLetter, AppConstants.SystemPathName, AppConstants.VocabularyPathName);

                progress?.Report(new OperationProgress(OperationStage.ReadingFile, 0, DeviceFileCount));
                File.Copy(Path.Combine(documentPath, AppConstants.ClippingsFileName), backupClippingsPath);
                progress?.Report(new OperationProgress(OperationStage.ReadingFile, 1, DeviceFileCount));
                File.Copy(Path.Combine(vocabularyPath, AppConstants.VocabFileName), backupWordsPath);
                progress?.Report(new OperationProgress(OperationStage.ReadingFile, DeviceFileCount, DeviceFileCount));
                return true;
            }

            if (!_detectMtpDevices) {
                // MTP 支持被关掉(测试/无 libusb 的环境):没有 USB 卷就是"没连接",
                // 不要再去碰真实设备总线,否则同一个用例在插着设备的机器与 CI 上结论不同。
                throw new Exception(Strings.Kindle_Connect_Failed);
            }

            return ImportFilesViaMtp(backupClippingsPath, backupWordsPath, out exception, progress);
        } catch (Exception e) {
            exception = e;
            return false;
        }
    }

    /// <summary>
    /// 经 MTP 取回两个文件。整个流程只开一次会话(开 → 列目录 → 下载两次 → 释放):
    /// MTP 是单会话协议,一次会话做完既快也少占设备(历史上每次会话还会复位 USB 口,现已解决,
    /// 见 <see cref="MtpDeviceSession"/>)。
    ///
    /// 语义上有意与 USB 路径有一处不同:**My Clippings.txt 必需,vocab.db 尽力而为**。
    /// 生词本缺失(没查过词、老固件没这个库)不该让整个导入失败。
    /// </summary>
    private bool ImportFilesViaMtp(string backupClippingsPath, string backupWordsPath,
        out Exception? exception, IProgress<OperationProgress>? progress) {
        exception = null;

        using var session = MtpDeviceSession.TryOpenKindle();
        if (session is null) {
            // libmtp 无法区分「没插」「没授权」「被别的 MTP 客户端占用」——三者都表现为探测不到设备,
            // 所以这里给一条把三种可能都列出来的人话提示(而不是笼统的"连接失败")。
            exception = new Exception(Strings.Device_Mtp_Connect_Failed);
            return false;
        }

        // 会话开成功就说明设备确实是 MTP —— 把类型落定。**不走这一步会留下状态不一致**:
        // GetKindleVersionText() 按类型分支(USB 读卷 / MTP 读缓存),类型还停在 Unknown 时就
        // 会返回空串,尽管版本已经读到了(实测踩过)。轮询那边也会独立把它设成 MTP,但操作路径
        // 不能指望别人先跑过。
        lock (_lockObj) {
            _deviceType = Device.Type.MTP;
            _driveLetter = string.Empty;
        }

        progress?.Report(new OperationProgress(OperationStage.ReadingFile, 0, DeviceFileCount));

        var clippings = session.FindByPath(AppConstants.DocumentsPathName, AppConstants.ClippingsFileName);
        if (clippings is not { } clippingsEntry) {
            exception = new Exception(Strings.Device_Clippings_Not_Found);
            return false;
        }

        if (!session.Download(clippingsEntry.ItemId, backupClippingsPath)) {
            exception = new Exception(string.Format(
                System.Globalization.CultureInfo.CurrentCulture,
                Strings.Device_Clippings_Read_Failed, AppConstants.ClippingsFileName));
            return false;
        }

        progress?.Report(new OperationProgress(OperationStage.ReadingFile, 1, DeviceFileCount));

        var vocabulary = session.FindByPath(
            AppConstants.SystemPathName, AppConstants.VocabularyPathName, AppConstants.VocabFileName);
        if (vocabulary is { } vocabEntry) {
            if (!session.Download(vocabEntry.ItemId, backupWordsPath)) {
                AppLog.Write("[DeviceManager] MTP: 生词本下载失败,继续(标注已取回)");
            }
        } else {
            AppLog.Write("[DeviceManager] MTP: 设备上没有 vocab.db,跳过生词本");
        }

        // 顺手读一下固件版本(27 字节):同一个会话里多一次请求,换来 GetKindleVersionText()
        // 在 MTP 机型上也能返回值。失败不算错,版本本来就是附带信息。
        var version = session.ReadFileText(
            AppConstants.SystemPathName, AppConstants.VersionFileName);
        if (!string.IsNullOrWhiteSpace(version)) {
            _mtpVersionText = version;
            AppLog.Write($"[DeviceManager] MTP: 设备固件版本 '{version}'");
        }

        progress?.Report(new OperationProgress(OperationStage.ReadingFile, DeviceFileCount, DeviceFileCount));
        return true;
    }

    /// <summary>
    /// Syncs exported clippings file back to the connected Kindle device.
    /// USB 卷上就是一次覆盖写;MTP 上走 <see cref="SyncFileToDeviceViaMtp"/>(见其注释里的安全替换说明)。
    /// </summary>
    public void SyncFileToDevice(string exportedFilePath, string targetFileName) {
        if (_deviceType == Device.Type.USB && IsConnected) {
            var documentPath = Path.Combine(_driveLetter, AppConstants.DocumentsPathName);
            File.Copy(exportedFilePath, Path.Combine(documentPath, targetFileName), true);
            return;
        }

        if (!_detectMtpDevices) {
            throw new Exception(Strings.Kindle_Connect_Failed);
        }

        SyncFileToDeviceViaMtp(exportedFilePath, targetFileName);
    }

    /// <summary>
    /// 经 MTP 把文件写回设备(只支持覆盖 <c>documents/</c> 下已存在的同名文件)。
    ///
    /// 为什么不能简单地"删掉再传":MTP 没有原子替换,而 libmtp 的 Send_File_From_File
    /// **只新建、不替换**同名文件(读了 libmtp.c 的实现确认)。于是"删旧 → 上传新"中间有个窗口:
    /// 删成功、传失败 → 设备上就没有 My Clippings.txt 了。**而这个后果比看起来严重**:
    /// 应用的重试流程第一步是「从设备导入」,设备上没有该文件就直接失败,用户无法自行重试。
    ///
    /// 所以这里先把它**下载到本地临时文件当回滚点**,再删、再传;上传失败就把它传回去恢复原状。
    /// 多一次同尺寸的传输,换取"任何一步失败都不会让设备处于无法自愈的状态"。
    /// </summary>
    private static void SyncFileToDeviceViaMtp(string exportedFilePath, string targetFileName) {
        using var session = MtpDeviceSession.TryOpenKindle();
        if (session is null) {
            throw new Exception(Strings.Device_Mtp_Connect_Failed);
        }

        ReplaceFileOnDevice(session, exportedFilePath, targetFileName);
    }

    /// <summary>
    /// 安全替换的本体,拆出来是为了**能被测试在既有会话上直接调用**(不必为验证回滚再开一次会话、
    /// 也不必让测试去构造一台设备)。生产路径见 <see cref="SyncFileToDeviceViaMtp"/>。
    /// </summary>
    internal static void ReplaceFileOnDevice(MtpDeviceSession session, string exportedFilePath, string targetFileName) {
        // 只覆盖设备上已有的文件:往设备新增任意文件不是本方法承诺的能力。
        var existing = session.FindByPath(AppConstants.DocumentsPathName, targetFileName);
        if (existing is not { } target) {
            throw new Exception(Strings.Device_Clippings_Not_Found);
        }

        var rollbackPath = exportedFilePath + ".device-rollback";
        var hasRollback = session.Download(target.ItemId, rollbackPath);
        if (!hasRollback) {
            // 连回滚点都拿不到就先别删 —— 宁可这次同步不成功,也不让设备处于无法恢复的中间态。
            AppLog.Write("[DeviceManager] MTP: 无法为设备上现有文件创建回滚点,放弃本次写回");
            throw new Exception(Strings.Device_Mtp_Sync_Failed);
        }

        // 只有当"恢复也失败、设备上确实少了这个文件"时才保留回滚副本 ——
        // 那种情况下它是用户唯一的原始内容来源,绝不能在 finally 里被删掉。
        var keepRollback = false;
        try {
            if (!session.Delete(target.ItemId)) {
                throw new Exception(Strings.Device_Mtp_Sync_Failed);
            }

            // 沿用原文件的类型与父目录,避免在设备上换一个"陌生人"。
            if (!session.Upload(exportedFilePath, targetFileName, target.ParentId, session.StorageId, target.FileType)) {
                keepRollback = !RestoreRollback(session, rollbackPath, targetFileName, target);
                throw new Exception(Strings.Device_Mtp_Sync_Failed);
            }
        } finally {
            if (!keepRollback) {
                try {
                    File.Delete(rollbackPath);
                } catch {
                    // 临时文件删不掉不影响结果
                }
            }
        }
    }

    /// <summary>
    /// 上传失败时把回滚点传回设备。
    /// 返回 true 表示**已恢复**(调用方可以删掉本地回滚副本);false 表示恢复也失败了,
    /// 此时本地那份原始内容必须保留。
    /// </summary>
    private static bool RestoreRollback(MtpDeviceSession session, string rollbackPath, string targetFileName, MtpEntry target) {
        if (session.Upload(rollbackPath, targetFileName, target.ParentId, session.StorageId, target.FileType)) {
            AppLog.Write("[DeviceManager] MTP: 上传失败,已把设备上的原文件恢复回去");
            return true;
        }

        AppLog.Write($"[DeviceManager] MTP: 上传失败后**恢复也失败**,设备上已无 '{targetFileName}'。" +
                     $"原始内容的本地副本保留在:{rollbackPath}");
        return false;
    }

    public void Dispose() {
        // 幂等,并挡住在飞轮询那一轮的补报(取消轮询不等待它)—— 语义见 DebouncedAction。
        _debounce.Dispose();

        var cts = _pollCts;
        var pollTask = _pollTask;
        _pollCts = null;
        _pollTask = null;

        if (cts != null) {
            try {
                cts.Cancel();
            } catch (ObjectDisposedException) {
                // 已释放,无需处理
            }

            if (pollTask is { IsCompleted: false }) {
                // 不在 Dispose 里阻塞等待:轮询体只读 /Volumes,取消后至多再跑完一轮(那一轮的
                // 防抖调度已被 DebouncedAction 挡住,不会再上报)。
                // 但要等它结束再释放 cts —— 回调仍持有注册时释放会抛 ObjectDisposedException。
                pollTask.ContinueWith(_ => cts.Dispose(), CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            } else {
                cts.Dispose();
            }
        }
    }
}
