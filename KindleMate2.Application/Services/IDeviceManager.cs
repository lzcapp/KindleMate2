namespace KindleMate2.Application.Services;

public interface IDeviceManager : IDisposable {
    Device.Type DeviceType { get; }
    string DriveLetter { get; }
    bool IsConnected { get; }
    event Action<bool>? ConnectionChanged;
    void StartWatching();
    bool IsKindleConnected();
    string GetKindleVersionText();
    bool ImportFilesFromDevice(string backupClippingsPath, string backupWordsPath, out Exception exception);
    void SyncFileToDevice(string exportedFilePath, string targetFileName);
}
