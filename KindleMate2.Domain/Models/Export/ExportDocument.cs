using System.Text.Json.Serialization;

namespace KindleMate2.Domain.Models.Export;

/// <summary>
/// JSON 导出的**顶层信封常量**。
/// </summary>
/// <remarks>
/// <c>version</c> 是**文档格式**的版本(不是程序版本,程序版本来自 git tag):
/// 下游脚本据此判断能不能吃这份文件,将来加字段/改结构时靠它区分,而不是让人去猜。
/// 首次发布即为 <c>1.0</c>,字段只增不改。
/// </remarks>
public static class ExportSchema {
    /// <summary>文档格式版本。</summary>
    public const string Version = "1.0";

    /// <summary>生成这份文件的程序名,便于下游判断来源。</summary>
    public const string Generator = "KindleMate2";

    /// <summary>
    /// <c>exported_at</c> 的格式:本地时间带时区偏移(<c>2026-10-03T21:34:00+08:00</c>)。
    /// 刻意不用 <c>"O"</c>——它会带出 7 位小数秒(毫秒全 0 时也照写),对人工阅读是噪音;
    /// 但偏移量刻意保留:标注时间本身跨时区,丢掉偏移就等于丢信息。
    /// </summary>
    public const string TimestampFormat = "yyyy-MM-ddTHH:mm:sszzz";
}

/// <summary>
/// 标注 JSON 的顶层文档(<c>Clippings.json</c>)。
/// </summary>
public sealed class ExportClippingsDocument {
    /// <summary>文档格式版本。</summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = ExportSchema.Version;

    /// <summary>生成者标识。</summary>
    [JsonPropertyName("generator")]
    public string Generator { get; set; } = ExportSchema.Generator;

    /// <summary>导出时刻(本地时间 + 偏移)。</summary>
    [JsonPropertyName("exported_at")]
    public string ExportedAt { get; set; } = string.Empty;

    /// <summary>按书名分组后的标注集合;无数据时为空数组(不是 null)。</summary>
    [JsonPropertyName("books")]
    public List<ExportBook> Books { get; set; } = [];
}

/// <summary>
/// 生词 JSON 的顶层文档(<c>Vocabs.json</c>)。
/// </summary>
public sealed class ExportVocabsDocument {
    /// <summary>文档格式版本。</summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = ExportSchema.Version;

    /// <summary>生成者标识。</summary>
    [JsonPropertyName("generator")]
    public string Generator { get; set; } = ExportSchema.Generator;

    /// <summary>导出时刻(本地时间 + 偏移)。</summary>
    [JsonPropertyName("exported_at")]
    public string ExportedAt { get; set; } = string.Empty;

    /// <summary>生词集合;无数据时为空数组(不是 null)。</summary>
    [JsonPropertyName("words")]
    public List<ExportWord> Words { get; set; } = [];
}
