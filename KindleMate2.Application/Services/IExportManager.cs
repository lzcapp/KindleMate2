namespace KindleMate2.Application.Services;

public interface IExportManager {
    bool ExportClippingsToMarkdown(string bookName = "");
    bool ExportVocabsToMarkdown(string word = "");
    bool ExportClippingsToCsv();
    /// <summary>导出生词 CSV。<paramref name="includeDefinitions"/> 为 true 时会**联网**查释义(把生词发给有道)。</summary>
    Task<bool> ExportVocabsToCsvAsync(bool includeDefinitions, CancellationToken cancellationToken = default);
    bool BackupClippings(out Exception? exception);
    bool ExportOriginalClippings(string path, string fileName, out Exception? exception);
    void SyncToKindle();
    void BackupDatabase();
}
