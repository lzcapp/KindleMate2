namespace KindleMate2.Application.Services;

using KindleMate2.Shared.Entities;
public interface IDeviceManager : IDisposable {
    Device.Type DeviceType { get; }
    string DriveLetter { get; }
    bool IsConnected { get; }
    event Action<bool>? ConnectionChanged;
    void StartWatching();
    bool IsKindleConnected();
    string GetKindleVersionText();
    /// <summary>把设备上的 My Clippings.txt 与 vocab.db 取回本地。<paramref name="progress"/> 按「第几个文件」上报。</summary>
    bool ImportFilesFromDevice(string backupClippingsPath, string backupWordsPath, out Exception? exception,
        IProgress<KindleMate2.Application.Models.OperationProgress>? progress = null);
    void SyncFileToDevice(string exportedFilePath, string targetFileName);
}
