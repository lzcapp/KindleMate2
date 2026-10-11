using KindleMate2.Domain.Entities.KM2DB;

namespace KindleMate2.Domain.Interfaces.KM2DB {
    /// <summary>
    /// 导入批次留底(「就地演进」第一步,2026-10-11)。
    /// 每次导入开始时记一行,vocab/lookups 的 origin_batch 指回它。
    /// </summary>
    public interface IImportBatchRepository {
        /// <summary>记一次导入的开始,返回自增 id(供 origin_batch 回指)。</summary>
        long Add(ImportBatch batch);

        /// <summary>回填一次导入的计数(解析多少、真正插入多少)。</summary>
        bool UpdateCounts(long id, int parsedCount, int insertedCount);

        List<ImportBatch> GetAll();
    }
}
