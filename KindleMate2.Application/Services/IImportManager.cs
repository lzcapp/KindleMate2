namespace KindleMate2.Application.Services;

public interface IImportManager {
    string Import(string kindleClippingsPath, string kindleWordsPath,
        IProgress<KindleMate2.Application.Models.OperationProgress>? progress = null);
    string ImportKindleClippings(string clippingsPath,
        IProgress<KindleMate2.Application.Models.OperationProgress>? progress = null);
    string ImportKindleWords(string kindleWordsPath);
    string ImportKmDatabase(string filePath);
    /// <summary>导入另一个 Kindle Mate 2 数据库(本程序自己的库格式)。</summary>
    string ImportKm2Database(string filePath);
    string ImportKmateDatabase(string filePath);
}
