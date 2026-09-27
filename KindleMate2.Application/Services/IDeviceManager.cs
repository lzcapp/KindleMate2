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
    /// 字段逐项可空(拿不到就留空,UI 不显示该行);约束是**不额外开新会话** ——
    /// 连接与否复用既有探测(与状态栏同源;Windows 上该探测对 MTP 机型本就会连一次),
    /// 字段只取卷文件读取与既有缓存,不为这个 getter 单独开会话。
    /// </summary>
    DeviceSummary? GetDeviceInfo();
    /// <summary>把设备上的 My Clippings.txt 与 vocab.db 取回本地。<paramref name="progress"/> 按「第几个文件」上报。</summary>
    bool ImportFilesFromDevice(string backupClippingsPath, string backupWordsPath, out Exception? exception,
        IProgress<KindleMate2.Application.Models.OperationProgress>? progress = null);
    void SyncFileToDevice(string exportedFilePath, string targetFileName);
}
