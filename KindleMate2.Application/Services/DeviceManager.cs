using System.Management;
using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Shared;
using KindleMate2.Shared.Constants;
using KindleMate2.Shared.Entities;
using KindleMate2.Infrastructure.Helpers;
using MediaDevices;

namespace KindleMate2.Application.Services;

/// <summary>
/// Manages Kindle device detection via USB and MTP, including connection monitoring.
/// </summary>
public class DeviceManager : IDeviceManager {
    private ManagementEventWatcher? _usbDeviceArrivalWatcher;
    private ManagementEventWatcher? _usbDeviceRemovalWatcher;
    private ManagementEventWatcher? _mtpDeviceArrivalWatcher;
    private ManagementEventWatcher? _mtpDeviceRemovalWatcher;

    private Device.Type _deviceType = Device.Type.Unknown;
    private string _driveLetter = string.Empty;
    private readonly string _versionFilePath;

    public Device.Type DeviceType => _deviceType;
    public string DriveLetter => _driveLetter;
    public bool IsConnected => !string.IsNullOrWhiteSpace(_driveLetter);

    public event Action<bool>? ConnectionChanged;

    public DeviceManager(string versionFilePath) {
        _versionFilePath = versionFilePath;
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
            }

            return isConnected;
            } catch (Exception ex) {
                Console.WriteLine($"[IsKindleConnected] {ex}");
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
        } catch {
            // Ignore version read errors
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

    private System.Threading.Timer? _debounceTimer;

    private void DeviceEventHandler(object sender) {
        if (_debounceTimer == null) {
            _debounceTimer = new System.Threading.Timer(OnDebounceTimerElapsed, null, 2500, System.Threading.Timeout.Infinite);
        } else {
            _debounceTimer.Change(2500, System.Threading.Timeout.Infinite);
        }
    }

    private void OnDebounceTimerElapsed(object? state) {
        try {
            IsKindleConnected();
            ConnectionChanged?.Invoke(IsConnected);
        } catch (Exception ex) {
            Console.WriteLine($"[HandleUsbDeviceEvent] {ex}");
        }
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
            Console.WriteLine(e);
            return false;
        }
    }

    private bool HandleMtpDevice() {
        try {
            var devices = MediaDevice.GetDevices().ToList();
            foreach (MediaDevice device in devices) {
                try {
                    device.Connect();
                    if (!device.FriendlyName.Contains(AppConstants.Kindle, StringComparison.InvariantCultureIgnoreCase) &&
                        !device.Model.Contains(AppConstants.Kindle, StringComparison.InvariantCultureIgnoreCase)) {
                        device.Disconnect();
                        continue;
                    }
                    _driveLetter = @"\Internal Storage\";
                    MediaDirectoryInfo? systemDir = device.GetDirectoryInfo(Path.Combine(_driveLetter, AppConstants.SystemPathName));
                    var files = systemDir.EnumerateFiles(AppConstants.VersionFileName);
                    var mediaFileInfos = files as MediaFileInfo[] ?? files.ToArray();
                    if (mediaFileInfos.Length == 0) {
                        device.Disconnect();
                        continue;
                    }
                    MediaFileInfo? file = mediaFileInfos[0];
                    using var memoryStream = new MemoryStream();
                    if (!device.IsConnected) {
                        device.Connect();
                    }
                    device.DownloadFile(file.FullName, memoryStream);
                    memoryStream.Position = 0;
                    using var reader = new StreamReader(memoryStream, leaveOpen: true);
                    reader.ReadToEnd(); // Version read for validation only
                    _deviceType = Device.Type.MTP;
                    device.Disconnect();
                    return true;
                } catch (Exception e) {
                    Console.WriteLine(e);
                }
            }
            return false;
        } catch (Exception e) {
            Console.WriteLine(e);
            return false;
        }
    }

    /// <summary>
    /// Copies files from the connected Kindle device to local backup paths.
    /// </summary>
    public bool ImportFilesFromDevice(string backupClippingsPath, string backupWordsPath, out Exception exception) {
        exception = new Exception();
        try {
            var documentPath = Path.Combine(_driveLetter, AppConstants.DocumentsPathName);
            var vocabularyPath = Path.Combine(_driveLetter, AppConstants.SystemPathName, AppConstants.VocabularyPathName);
            switch (_deviceType) {
                case Device.Type.USB: {
                    File.Copy(Path.Combine(documentPath, AppConstants.ClippingsFileName), backupClippingsPath);
                    File.Copy(Path.Combine(vocabularyPath, AppConstants.VocabFileName), backupWordsPath);
                    return true;
                }
                case Device.Type.MTP: {
                    var devices = MediaDevice.GetDevices().ToList();
                    var isPaired = false;
                    foreach (MediaDevice device in devices) {
                        device.Connect();
                        if (!device.FriendlyName.Contains(AppConstants.Kindle, StringComparison.InvariantCultureIgnoreCase) &&
                            !device.Model.Contains(AppConstants.Kindle, StringComparison.InvariantCultureIgnoreCase)) {
                            device.Disconnect();
                            continue;
                        }
                        ReadMtpFile(device, documentPath, AppConstants.ClippingsFileName, backupClippingsPath);
                        ReadMtpFile(device, vocabularyPath, AppConstants.VocabFileName, backupWordsPath);
                        device.Disconnect();
                        isPaired = true;
                        break;
                    }
                    return isPaired;
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
        var documentPath = Path.Combine(_driveLetter, AppConstants.DocumentsPathName);
        switch (_deviceType) {
            case Device.Type.USB:
                File.Copy(exportedFilePath, Path.Combine(documentPath, targetFileName), true);
                break;
            case Device.Type.MTP: {
                var devices = MediaDevice.GetDevices();
                using MediaDevice? device = devices.First(d =>
                    d.FriendlyName.Contains(AppConstants.Kindle, StringComparison.InvariantCultureIgnoreCase) ||
                    d.Model.Contains(AppConstants.Kindle, StringComparison.InvariantCultureIgnoreCase));
                device.Connect();
                using var sr = new StreamReader(exportedFilePath);
                device.DeleteFile(Path.Combine(documentPath, targetFileName));
                device.UploadFile(sr.BaseStream, Path.Combine(documentPath, targetFileName));
                device.Disconnect();
                break;
            }
        }
    }

    private static void ReadMtpFile(MediaDevice device, string path, string fileName, string filePath) {
        MediaDirectoryInfo? systemDir = device.GetDirectoryInfo(path);
        IEnumerable<MediaFileInfo> files = systemDir.EnumerateFiles(fileName);
        var fileInfos = files as MediaFileInfo[] ?? files.ToArray();
        if (fileInfos.Length == 0) {
            return;
        }
        MediaFileInfo file = fileInfos[0];
        using var memoryStream = new MemoryStream();
        device.DownloadFile(file.FullName, memoryStream);
        memoryStream.Position = 0;
        try {
            File.WriteAllBytes(filePath, memoryStream.ToArray());
        } catch (Exception ex) {
            Console.WriteLine(StringHelper.GetExceptionMessage(nameof(ReadMtpFile), ex));
        }
    }

    public void Dispose() {
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
