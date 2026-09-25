using System.Management;
using KindleMate2.Application.Models;
using KindleMate2.Application.Services;
using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Shared;
using KindleMate2.Shared.Constants;
using KindleMate2.Shared.Entities;
using MediaDevices;
using KindleMate2.Shared.Diagnostics;
using KindleMate2.Shared.Threading;

namespace KindleMate2.Devices.Windows;

/// <summary>
/// Windows 专有的 Kindle 设备检测与同步:USB 盘符枚举 + MTP(MediaDevices)+ WMI 事件监听。
/// 接口 <see cref="IDeviceManager"/> 定义在 Application 层,本类是其 Windows 实现。
/// </summary>
public class DeviceManager : IDeviceManager {
    /// <summary>从设备取回的文件数(My Clippings.txt + vocab.db),用于按「第几个文件」上报进度。</summary>
    private const int DeviceFileCount = 2;

    private ManagementEventWatcher? _usbDeviceArrivalWatcher;
    private ManagementEventWatcher? _usbDeviceRemovalWatcher;
    private ManagementEventWatcher? _mtpDeviceArrivalWatcher;
    private ManagementEventWatcher? _mtpDeviceRemovalWatcher;

    private Device.Type _deviceType = Device.Type.Unknown;
    private string _driveLetter = string.Empty;
    private readonly string _versionFilePath;

    /// <summary>设备变化事件后的防抖上报器。WMI 事件可能在 <see cref="Dispose"/> 之后才到达,
    /// 释放竞态统一由 <see cref="DebouncedAction"/> 处理,与 POSIX 实现共用同一份语义。</summary>
    private readonly DebouncedAction _debounce;

    public Device.Type DeviceType => _deviceType;
    public string DriveLetter => _driveLetter;
    public bool IsConnected => !string.IsNullOrWhiteSpace(_driveLetter);

    public event Action<bool>? ConnectionChanged;

    /// <summary>状态变化后的防抖时长,与 POSIX 实现取同一值。</summary>
    private static readonly TimeSpan DebounceInterval = TimeSpan.FromMilliseconds(2500);

    public DeviceManager(string versionFilePath) {
        _versionFilePath = versionFilePath;
        _debounce = new DebouncedAction(DebounceInterval, OnDebounceElapsed);
    }

    public void StartWatching() {
        IsKindleConnected();

        const string usbCreationQuery = "SELECT * FROM Win32_DeviceChangeEvent WHERE EventType = 2";
        _usbDeviceArrivalWatcher = new ManagementEventWatcher(usbCreationQuery);
        _usbDeviceArrivalWatcher.EventArrived += UsbDeviceEventHandler;
        _usbDeviceArrivalWatcher.Start();

        const string usbDeletionQuery = "SELECT * FROM Win32_DeviceChangeEvent WHERE EventType = 3";
        _usbDeviceRemovalWatcher = new ManagementEventWatcher(usbDeletionQuery);
        _usbDeviceRemovalWatcher.EventArrived += DeviceRemovedEventHandler;
        _usbDeviceRemovalWatcher.Start();

        const string mtpCreationQuery = "SELECT * FROM __InstanceCreationEvent WITHIN 2 WHERE TargetInstance ISA 'Win32_PnPEntity'";
        _mtpDeviceArrivalWatcher = new ManagementEventWatcher(mtpCreationQuery);
        _mtpDeviceArrivalWatcher.EventArrived += MtpDeviceEventHandler;
        _mtpDeviceArrivalWatcher.Start();

        const string mtpDeletionQuery = "SELECT * FROM __InstanceDeletionEvent WITHIN 2 WHERE TargetInstance ISA 'Win32_PnPEntity'";
        _mtpDeviceRemovalWatcher = new ManagementEventWatcher(mtpDeletionQuery);
        _mtpDeviceRemovalWatcher.EventArrived += DeviceRemovedEventHandler;
        _mtpDeviceRemovalWatcher.Start();
    }

    private readonly object _lockObj = new object();

    public bool IsKindleConnected() {
        lock (_lockObj) {
            try {
                var isConnected = HandleUsbDevice();
            if (!isConnected) {
                isConnected = HandleMtpDevice();
            }

            if (!isConnected) {
                _driveLetter = string.Empty;
                _deviceType = Device.Type.Unknown;
            }

            return isConnected;
            } catch (Exception ex) {
                AppLog.Write($"[IsKindleConnected] {ex}");
                return false;
            }
        }
    }

    public string GetKindleVersionText() {
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

    private void UsbDeviceEventHandler(object sender, EventArrivedEventArgs e) {
        DeviceEventHandler(sender);
    }

    private void MtpDeviceEventHandler(object sender, EventArrivedEventArgs e) {
        DeviceEventHandler(sender);
    }

    private void DeviceRemovedEventHandler(object sender, EventArrivedEventArgs e) {
        DeviceEventHandler(sender);
    }

    /// <summary>收到任一设备变化事件:重置防抖,静默后复检一次状态。释放后为空操作。</summary>
    private void DeviceEventHandler(object sender) {
        _debounce.Schedule();
    }

    private void OnDebounceElapsed() {
        IsKindleConnected();
        ConnectionChanged?.Invoke(IsConnected);
    }

    private bool HandleUsbDevice() {
        try {
            var drives = DriveInfo.GetDrives();
            foreach (DriveInfo drive in drives) {
                if (drive.DriveType != DriveType.Removable) {
                    continue;
                }
                var documentsDir = Path.Combine(drive.Name, AppConstants.DocumentsPathName);
                if (!Directory.Exists(documentsDir)) {
                    continue;
                }
                var clippingsPath = Path.Combine(documentsDir, AppConstants.ClippingsFileName);
                if (!File.Exists(clippingsPath)) {
                    continue;
                }

                _driveLetter = drive.Name;
                _deviceType = Device.Type.USB;
                return true;
            }
            return false;
        } catch (Exception e) {
            AppLog.Write(e);
            return false;
        }
    }

    private bool HandleMtpDevice() {
        try {
            var device = FindKindleDevice();
            if (device == null) {
                return false;
            }

            try {
                device.Connect();
                _driveLetter = @"\Internal Storage\";
                MediaDirectoryInfo? systemDir = device.GetDirectoryInfo(Path.Combine(_driveLetter, AppConstants.SystemPathName));
                var files = systemDir.EnumerateFiles(AppConstants.VersionFileName);
                var mediaFileInfos = files as MediaFileInfo[] ?? files.ToArray();
                if (mediaFileInfos.Length == 0) {
                    return false;
                }
                MediaFileInfo? file = mediaFileInfos[0];
                using var memoryStream = new MemoryStream();
                device.DownloadFile(file.FullName, memoryStream);
                memoryStream.Position = 0;
                using var reader = new StreamReader(memoryStream, leaveOpen: true);
                reader.ReadToEnd(); // Version read for validation only
                _deviceType = Device.Type.MTP;
                return true;
            } catch (Exception e) {
                AppLog.Write(e);
                // Reset partial state if _driveLetter was set before failure
                if (string.Equals(_driveLetter, @"\Internal Storage\", StringComparison.Ordinal)) {
                    _driveLetter = string.Empty;
                }
                return false;
            } finally {
                try { device.Disconnect(); } catch { /* best effort */ }
                // MediaDevice 构造即启动事件线程,只有 Dispose 才停 —— 只 Disconnect 会让线程 +
                // WPD 会话在 8 秒一次的探测里持续堆积(写回路径 SyncFileToDevice 早有正解)。
                device.Dispose();
            }
        } catch (Exception e) {
            AppLog.Write(e);
            return false;
        }
    }

    /// <summary>
    /// Copies files from the connected Kindle device to local backup paths.
    /// <paramref name="progress"/> 按「第几个文件」上报(共 <see cref="DeviceFileCount"/> 个):
    /// 两个文件都是整文件传输,几千条标注 / 几千词的大库上这一步本身就有可感知耗时。
    /// </summary>
    public bool ImportFilesFromDevice(string backupClippingsPath, string backupWordsPath, out Exception? exception,
        IProgress<OperationProgress>? progress = null) {
        exception = null;
        try {
            var documentPath = Path.Combine(_driveLetter, AppConstants.DocumentsPathName);
            var vocabularyPath = Path.Combine(_driveLetter, AppConstants.SystemPathName, AppConstants.VocabularyPathName);
            switch (_deviceType) {
                case Device.Type.USB: {
                    // 拔出后 _deviceType 若还停在 USB,Path.Combine("", "documents") 会退化成
                    // 相对路径,误读/误写本机工作目录里的文件。这里用 IsConnected 兜底。
                    if (!IsConnected) {
                        throw new Exception(Strings.Kindle_Connect_Failed);
                    }
                    progress?.Report(new OperationProgress(OperationStage.ReadingFile, 0, DeviceFileCount));
                    File.Copy(Path.Combine(documentPath, AppConstants.ClippingsFileName), backupClippingsPath);
                    progress?.Report(new OperationProgress(OperationStage.ReadingFile, 1, DeviceFileCount));
                    File.Copy(Path.Combine(vocabularyPath, AppConstants.VocabFileName), backupWordsPath);
                    progress?.Report(new OperationProgress(OperationStage.ReadingFile, DeviceFileCount, DeviceFileCount));
                    return true;
                }
                case Device.Type.MTP: {
                    var device = FindKindleDevice();
                    if (device == null) {
                        throw new Exception(Strings.Device_Mtp_Connect_Failed);
                    }
                    try {
                        device.Connect();
                        progress?.Report(new OperationProgress(OperationStage.ReadingFile, 0, DeviceFileCount));
                        if (!ReadMtpFile(device, documentPath, AppConstants.ClippingsFileName, backupClippingsPath)) {
                            throw new Exception(Strings.Device_Clippings_Not_Found);
                        }
                        progress?.Report(new OperationProgress(OperationStage.ReadingFile, 1, DeviceFileCount));
                        // vocab.db 尽力而为:没查过词/老固件没有这个库,不该让整个导入失败。
                        if (!ReadMtpFile(device, vocabularyPath, AppConstants.VocabFileName, backupWordsPath)) {
                            AppLog.Write("[DeviceManager] MTP: 设备上没有 vocab.db,跳过生词本");
                        }
                        progress?.Report(new OperationProgress(OperationStage.ReadingFile, DeviceFileCount, DeviceFileCount));
                        return true;
                    } finally {
                        try { device.Disconnect(); } catch { /* best effort */ }
                        // 同 HandleMtpDevice:不 Dispose 每次导入都会漏掉一个事件线程。
                        device.Dispose();
                    }
                }
                case Device.Type.Unknown:
                default: {
                    throw new Exception(Strings.Kindle_Connect_Failed);
                }
            }
        } catch (Exception e) {
            exception = e;
            return false;
        }
    }

    /// <summary>
    /// Syncs exported clippings file back to the connected Kindle device.
    /// </summary>
    public void SyncFileToDevice(string exportedFilePath, string targetFileName) {
        if (!IsConnected) {
            throw new Exception(Strings.Kindle_Connect_Failed);
        }
        var documentPath = Path.Combine(_driveLetter, AppConstants.DocumentsPathName);
        switch (_deviceType) {
            case Device.Type.USB:
                File.Copy(exportedFilePath, Path.Combine(documentPath, targetFileName), true);
                break;
            case Device.Type.MTP: {
                var device = FindKindleDevice();
                if (device == null) {
                    throw new Exception(Strings.Kindle_Connect_Failed);
                }
                try {
                    device.Connect();
                    var targetPath = Path.Combine(documentPath, targetFileName);
                    SyncFileToDeviceViaMtp(device, documentPath, targetFileName, targetPath, exportedFilePath);
                } finally {
                    if (device is { IsConnected: true }) {
                        device.Disconnect();
                    }
                    device.Dispose();
                }
                break;
            }
        }
    }

    /// <summary>
    /// Find the first Kindle MTP device without connecting to every device.
    /// FriendlyName / Model are available before Connect().
    /// </summary>
    private static MediaDevice? FindKindleDevice() {
        return MediaDeviceManager.Instance.GetDevices()?
            .FirstOrDefault(d =>
                d.FriendlyName?.Contains(AppConstants.Kindle, StringComparison.InvariantCultureIgnoreCase) == true ||
                d.Model?.Contains(AppConstants.Kindle, StringComparison.InvariantCultureIgnoreCase) == true);
    }

    private static bool ReadMtpFile(MediaDevice device, string path, string fileName, string filePath) {
        MediaDirectoryInfo? dir = device.GetDirectoryInfo(path);
        if (dir == null) {
            return false;
        }
        IEnumerable<MediaFileInfo> files = dir.EnumerateFiles(fileName);
        var fileInfos = files as MediaFileInfo[] ?? files.ToArray();
        if (fileInfos.Length == 0) {
            return false;
        }
        MediaFileInfo file = fileInfos[0];
        using var memoryStream = new MemoryStream();
        device.DownloadFile(file.FullName, memoryStream);
        memoryStream.Position = 0;
        // 失败(传输中断/写盘失败)向上抛,由调用方记入 exception —— 不能再像以前那样
        // 吞掉写盘异常,让「备份根本没写成」也返回成功。
        File.WriteAllBytes(filePath, memoryStream.ToArray());
        return true;
    }

    /// <summary>
    /// 经 MTP 把文件写回设备(覆盖 <c>documents/</c> 下同名文件)。MTP 没有原子替换,
    /// 「删旧 → 传新」中间有个窗口:删成功、传失败 → 设备上就没有该文件了,而应用重试
    /// 第一步是「从设备导入」,缺文件即无法自愈。所以先下载现有文件当回滚点,传失败就恢复。
    /// </summary>
    private static void SyncFileToDeviceViaMtp(MediaDevice device, string documentPath,
        string targetFileName, string targetPath, string exportedFilePath) {
        var rollbackPath = Path.Combine(Path.GetTempPath(), "km2-mtp-rollback-" + Guid.NewGuid().ToString("N") + ".tmp");
        var hasRollback = TryDownloadRollback(device, documentPath, targetFileName, rollbackPath);

        var keepRollback = false;
        try {
            try { device.DeleteFile(targetPath); } catch { /* 首次同步可能没有该文件 */ }

            try {
                using var fileStream = File.OpenRead(exportedFilePath);
                device.UploadFile(fileStream, targetPath);
            } catch {
                // 上传失败:把设备上的原文件恢复回去。恢复也失败时,保留本地回滚副本。
                if (hasRollback) {
                    keepRollback = !RestoreRollback(device, rollbackPath, targetPath);
                }
                throw;
            }
        } finally {
            if (!keepRollback) {
                try { File.Delete(rollbackPath); } catch { /* 临时文件删不掉不影响结果 */ }
            }
        }
    }

    /// <summary>上传前把设备上现有文件下载到本地当回滚点。文件不存在返回 false(无需回滚);读不出来则抛出。</summary>
    private static bool TryDownloadRollback(MediaDevice device, string documentPath, string fileName, string rollbackPath) {
        try {
            MediaDirectoryInfo? dir = device.GetDirectoryInfo(documentPath);
            if (dir == null) {
                return false;
            }
            IEnumerable<MediaFileInfo> files = dir.EnumerateFiles(fileName);
            var fileInfos = files as MediaFileInfo[] ?? files.ToArray();
            if (fileInfos.Length == 0) {
                return false;
            }
            using var fs = File.Create(rollbackPath);
            device.DownloadFile(fileInfos[0].FullName, fs);
            return true;
        } catch (Exception ex) {
            // 连回滚点都拿不到就先别删 —— 宁可这次写回不成功,也不让设备处于删了却传不上的中间态。
            AppLog.Write($"[DeviceManager] MTP: 无法为设备上现有文件创建回滚点,放弃本次写回:{ex}");
            try { File.Delete(rollbackPath); } catch { /* best effort */ }
            throw new Exception(Strings.Device_Mtp_Sync_Failed);
        }
    }

    /// <summary>上传失败后把回滚点传回设备。true = 已恢复(可删本地副本);false = 恢复也失败(必须保留副本)。</summary>
    private static bool RestoreRollback(MediaDevice device, string rollbackPath, string targetPath) {
        try {
            using var rollbackStream = File.OpenRead(rollbackPath);
            device.UploadFile(rollbackStream, targetPath);
            AppLog.Write("[DeviceManager] MTP: 上传失败,已把设备上的原文件恢复回去");
            return true;
        } catch (Exception ex) {
            AppLog.Write($"[DeviceManager] MTP: 上传失败后恢复也失败,设备上可能已无该文件。原始副本保留在:{rollbackPath} ({ex})");
            return false;
        }
    }

    public void Dispose() {
        // 幂等,并挡住 Dispose 之后才到达的 WMI 事件(可能与 Stop/Dispose watcher 并发)。
        _debounce.Dispose();

        _usbDeviceArrivalWatcher?.Stop();
        _usbDeviceArrivalWatcher?.Dispose();
        _usbDeviceRemovalWatcher?.Stop();
        _usbDeviceRemovalWatcher?.Dispose();
        _mtpDeviceArrivalWatcher?.Stop();
        _mtpDeviceArrivalWatcher?.Dispose();
        _mtpDeviceRemovalWatcher?.Stop();
        _mtpDeviceRemovalWatcher?.Dispose();
    }
}
