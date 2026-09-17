namespace KindleMate2.Application.Services.KM2DB;

public interface IKm2DatabaseService {
    bool ImportKindleClippings(string clippingsPath, out Dictionary<string, string> result,
        IProgress<KindleMate2.Application.Models.OperationProgress>? progress = null);
    bool RebuildDatabase(out Dictionary<string, string> result);
    bool UpdateFrequency(IProgress<KindleMate2.Application.Models.OperationProgress>? progress = null);
    // —— 回收站(原版无此概念;语义与"已删除 N 条"统计口径一致) ——
    List<KindleMate2.Domain.Entities.KM2DB.OriginalClippingLine> GetDeletedOriginalLines();
    bool RestoreFromOriginalLine(KindleMate2.Domain.Entities.KM2DB.OriginalClippingLine originalLine);
    int PurgeDeletedOriginalLines(IEnumerable<string> keys);

    bool CleanDatabase(string databaseFilePath, out Dictionary<string, string> result,
        IProgress<KindleMate2.Application.Models.OperationProgress>? progress = null);
    bool IsDatabaseEmpty();
    bool DeleteAllData();
}
