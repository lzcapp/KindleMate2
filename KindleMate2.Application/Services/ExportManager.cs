using KindleMate2.Application.Services.KM2DB;
using KindleMate2.Infrastructure.Helpers;
using KindleMate2.Shared;
using KindleMate2.Shared.Constants;
using KindleMate2.Shared.Diagnostics;

namespace KindleMate2.Application.Services;

/// <summary>
/// Manages data export operations: Markdown export and sync back to Kindle device.
/// </summary>
public class ExportManager : IExportManager {
    private readonly IClippingService _clippingService;
    private readonly ILookupService _lookupService;
    private readonly IOriginalClippingLineService _originalClippingLineService;
    private readonly IDeviceManager _deviceManager;
    private readonly string _programPath;
    private readonly string _backupPath;
    private readonly string _tempPath;

    public ExportManager(IClippingService clippingService, ILookupService lookupService,
        IOriginalClippingLineService originalClippingLineService, IDeviceManager deviceManager,
        string programPath, string backupPath, string tempPath) {
        _clippingService = clippingService;
        _lookupService = lookupService;
        _originalClippingLineService = originalClippingLineService;
        _deviceManager = deviceManager;
        _programPath = programPath;
        _backupPath = backupPath;
        _tempPath = tempPath;
    }

    /// <summary>
    /// Exports clippings to Markdown format.
    /// </summary>
    public bool ExportClippingsToMarkdown(string bookName = "") {
        try {
            return _clippingService.ClippingsToMarkdown(Path.Combine(_programPath, AppConstants.ExportsPathName), bookName);
        } catch (Exception ex) {
            AppLog.Write($"[ClippingsToMarkdown] {ex}");
            return false;
        }
    }

    /// <summary>
    /// Exports vocabulary/lookups to Markdown format.
    /// </summary>
    public bool ExportVocabsToMarkdown(string word = "") {
        try {
            return _lookupService.LookupsToMarkdown(Path.Combine(_programPath, AppConstants.ExportsPathName), word);
        } catch (Exception ex) {
            AppLog.Write($"[VocabsToMarkdown] {ex}");
            return false;
        }
    }

    /// <summary>
    /// 把原始标注行导出到 Backups 目录，作为数据库备份之外的一份可读副本。
    /// </summary>
    /// <remarks>
    /// 文件名此前误用了 <see cref="AppConstants.DatabaseFileName"/>（"KM2.dat"），于是
    /// Backups 里会出现一个名为 KM2.dat 的**纯文本**文件：用 SQLite 打开报
    /// "file is not a database"，而且极易与真正的库备份（KM2_backup_&lt;时间戳&gt;.dat）
    /// 混淆 —— 用户很可能把它当数据库备份去恢复，然后打不开。此外它是固定名，每次
    /// 备份都覆盖上一份。
    /// 现改为与 <see cref="SyncToKindle"/> 一致的命名：MyClippings_&lt;时间戳&gt;.txt，
    /// 既表明这是标注文本而非数据库，也不再互相覆盖。
    /// </remarks>
    public bool BackupClippings(out Exception? exception) {
        var fileName = "MyClippings_" + DateTimeHelper.GetCurrentTimestamp() + FileExtension.TXT;
        return _originalClippingLineService.Export(_backupPath, fileName, out exception);
    }

    /// <summary>
    /// Exports original clippings to a specific path.
    /// </summary>
    public bool ExportOriginalClippings(string path, string fileName, out Exception? exception) {
        return _originalClippingLineService.Export(path, fileName, out exception);
    }

    /// <summary>
    /// Syncs clippings back to the connected Kindle device.
    /// </summary>
    public void SyncToKindle() {
        var backupClippingsPath = Path.Combine(_backupPath, "MyClippings_" + DateTimeHelper.GetCurrentTimestamp() + FileExtension.TXT);
        var backupWordsPath = Path.Combine(_backupPath, "vocab_" + DateTimeHelper.GetCurrentTimestamp() + FileExtension.DB);

        if (!Directory.Exists(_backupPath)) {
            Directory.CreateDirectory(_backupPath);
        }

        if (!_deviceManager.ImportFilesFromDevice(backupClippingsPath, backupWordsPath, out Exception? exception) ||
            !_originalClippingLineService.Export(_tempPath, AppConstants.ClippingsFileName, out exception)) {
            throw exception!;
        }

        var exportedClippingsPath = Path.Combine(_tempPath, AppConstants.ClippingsFileName);
        _deviceManager.SyncFileToDevice(exportedClippingsPath, AppConstants.ClippingsFileName);
    }

    /// <summary>
    /// Backs up the database file.
    /// </summary>
    public void BackupDatabase() {
        DatabaseHelper.BackupDatabase(_programPath, _backupPath, AppConstants.DatabaseFileName);
    }
}
