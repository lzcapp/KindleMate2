namespace KindleMate2.Application.Services;

public interface IExportManager {
    bool ExportClippingsToMarkdown(string bookName = "");
    bool ExportVocabsToMarkdown(string word = "");
    bool BackupClippings(out Exception exception);
    bool ExportOriginalClippings(string path, string fileName, out Exception exception);
    void SyncToKindle();
    void BackupDatabase();
}
