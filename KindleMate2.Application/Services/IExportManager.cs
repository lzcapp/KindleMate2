namespace KindleMate2.Application.Services;

public interface IExportManager {
    bool ExportClippingsToMarkdown(string bookName = "");
    bool ExportVocabsToMarkdown(string word = "");
    bool ExportClippingsToCsv();
    Task<bool> ExportVocabsToCsvAsync(CancellationToken cancellationToken = default);
    bool BackupClippings(out Exception? exception);
    bool ExportOriginalClippings(string path, string fileName, out Exception? exception);
    void SyncToKindle();
    void BackupDatabase();
}
