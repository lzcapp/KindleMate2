using KindleMate2.Application.Services.KM2DB;
using KindleMate2.Infrastructure.Helpers;
using KindleMate2.Shared;
using KindleMate2.Shared.Constants;

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
            Console.WriteLine($"[ClippingsToMarkdown] {ex}");
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
            Console.WriteLine($"[VocabsToMarkdown] {ex}");
            return false;
        }
    }

    /// <summary>
    /// Exports original clippings for backup.
    /// </summary>
    public bool BackupClippings(out Exception exception) {
        return _originalClippingLineService.Export(_backupPath, AppConstants.DatabaseFileName, out exception);
    }

    /// <summary>
    /// Exports original clippings to a specific path.
    /// </summary>
    public bool ExportOriginalClippings(string path, string fileName, out Exception exception) {
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
            throw exception;
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
