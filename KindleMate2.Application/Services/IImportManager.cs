namespace KindleMate2.Application.Services;

public interface IImportManager {
    string Import(string kindleClippingsPath, string kindleWordsPath);
    string ImportKindleClippings(string clippingsPath,
        IProgress<KindleMate2.Application.Models.OperationProgress>? progress = null);
    string ImportKindleWords(string kindleWordsPath);
    string ImportKmDatabase(string filePath);
    string ImportKmateDatabase(string filePath);
}
