namespace KindleMate2.Application.Services;

public interface IExportManager {
    bool ExportClippingsToMarkdown(string bookName = "");
    bool ExportVocabsToMarkdown(string word = "");
    bool ExportClippingsToCsv();
    /// <summary>导出生词 CSV。<paramref name="includeDefinitions"/> 为 true 时会**联网**查释义(把生词发给有道)。</summary>
    Task<bool> ExportVocabsToCsvAsync(bool includeDefinitions, CancellationToken cancellationToken = default);
    /// <summary>导出标注为 JSON(按书名分组),离线。</summary>
    bool ExportClippingsToJson();
    /// <summary>导出生词为 JSON,离线(不查释义)。</summary>
    bool ExportVocabsToJson();
    bool BackupClippings(out Exception? exception);
    bool ExportOriginalClippings(string path, string fileName, out Exception? exception);
    void SyncToKindle();
    void BackupDatabase();
}
