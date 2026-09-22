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

    /// <summary>
    /// **只读**预览:算出清洗会改掉哪些条目,不写任何东西。确认框靠它拿到条数与样例。
    /// </summary>
    bool ScanClippingClean(out KindleMate2.Application.Models.ClippingCleanReport report,
        IProgress<KindleMate2.Application.Models.OperationProgress>? progress = null);

    /// <summary>
    /// 批量清洗已入库标注的**首尾标点**(Kindle 会把上一句的收尾标点一起划进标注)。
    /// 与 <see cref="CleanDatabase"/> 是两件事:那个动行数,这个一条都不删。
    /// </summary>
    bool CleanClippingTexts(out KindleMate2.Application.Models.ClippingCleanReport report,
        IProgress<KindleMate2.Application.Models.OperationProgress>? progress = null);

    bool IsDatabaseEmpty();
    bool DeleteAllData();
}
