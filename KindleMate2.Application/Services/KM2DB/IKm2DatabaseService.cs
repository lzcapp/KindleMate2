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
    /// **只读**预演一次「维护数据库」:清洗会改哪些 + 清理会删哪些,不写任何东西。
    /// 清理部分是在**清洗后的投影**上判出来的 —— 清洗会把内容归一化、制造出新的重复项,
    /// 分开预演必然少报。执行顺序同样是先清洗后清理(见 <c>MaintainDatabaseAsync</c>)。
    /// </summary>
    bool ScanDatabaseMaintenance(out KindleMate2.Application.Models.DatabaseMaintenancePlan plan,
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
