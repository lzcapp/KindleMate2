namespace KindleMate2.Domain.Entities.KM2DB {
    /// <summary>
    /// 一次导入的留底记录(「就地演进」第一步,2026-10-11)。
    /// 每次导入开始时写一行,vocab/lookups 的 origin_batch 指回它的 Id。
    /// </summary>
    /// <remarks>
    /// 用途:①事后对账"这批词是哪次导进来的"(合并 KM/KMate 库后尤其有用);
    /// ②将来做「撤销本次导入」的抓手(按 origin_batch 软删即可)。
    /// 刻意**只记账,不参与判重** —— 判重仍然只认 (词, 时间戳),导入来源不影响身份。
    /// </remarks>
    public class ImportBatch {
        /// <summary>来源类型常量。值会落库,改动即破坏既有数据的可读性 —— 只增不改。</summary>
        public const string SourceKindleClippings = "kindle-clippings";
        public const string SourceKindleVocab = "kindle-vocab";
        public const string SourceKm = "km";
        public const string SourceKm2 = "km2";
        public const string SourceKmate = "kmate";

        public long? Id { get; set; }
        public required string SourceType { get; set; }
        public string? SourcePath { get; set; }
        public required string ImportedAt { get; set; }
        public int ParsedCount { get; set; }
        public int InsertedCount { get; set; }
    }
}
