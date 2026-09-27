namespace KindleMate2.Application.Services;

using KindleMate2.Application.Models;
using KindleMate2.Shared.Entities;
public interface IDeviceManager : IDisposable {
    Device.Type DeviceType { get; }
    string DriveLetter { get; }
    bool IsConnected { get; }
    event Action<bool>? ConnectionChanged;
    void StartWatching();
    bool IsKindleConnected();
    string GetKindleVersionText();
    /// <summary>
    /// 关于窗口「设备」段的数据:已连接返回摘要,未连接返回 null。
    /// 字段逐项可空(拿不到就留空,UI 不显示该行);与
    /// <see cref="GetKindleVersionText"/> 同一约束 —— <b>绝不开 MTP 会话</b>,只读卷文件与既有缓存。
    /// </summary>
    DeviceSummary? GetDeviceInfo();
    /// <summary>把设备上的 My Clippings.txt 与 vocab.db 取回本地。<paramref name="progress"/> 按「第几个文件」上报。</summary>
    bool ImportFilesFromDevice(string backupClippingsPath, string backupWordsPath, out Exception? exception,
        IProgress<KindleMate2.Application.Models.OperationProgress>? progress = null);
    void SyncFileToDevice(string exportedFilePath, string targetFileName);
}
