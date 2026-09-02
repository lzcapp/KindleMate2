namespace KindleMate2.Application.Services;

public interface IImportManager {
    string Import(string kindleClippingsPath, string kindleWordsPath);
    string ImportKindleClippings(string clippingsPath);
    string ImportKindleWords(string kindleWordsPath);
    string ImportKmDatabase(string filePath);
}
