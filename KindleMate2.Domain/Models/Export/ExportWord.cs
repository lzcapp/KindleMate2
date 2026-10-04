using System.Text.Json.Serialization;

namespace KindleMate2.Domain.Models.Export;

/// <summary>
/// 导出中间模型:一条生词(来自 <c>vocab.db</c> 的 <c>LOOKUPS</c>)。
/// </summary>
/// <remarks>
/// 字段口径与既有 CSV 导出(<c>ExportManager.WriteLookupsCsv</c>)保持一致:
/// 可空字段退化成空串,<c>usage</c> 同样把换行占位符还原成真正的换行。
/// </remarks>
public sealed class ExportWord {
    /// <summary>单词本身(<c>WordKey</c> 去掉 <c>lang:</c> 前缀);空词在转换阶段已跳过。</summary>
    [JsonPropertyName("word")]
    public string Word { get; set; } = string.Empty;

    /// <summary>词形(stem);库里没有时为空串。</summary>
    [JsonPropertyName("stem")]
    public string Stem { get; set; } = string.Empty;

    /// <summary>语言代码(<c>en:apple</c> → <c>en</c>);<c>WordKey</c> 无前缀时为空串。</summary>
    [JsonPropertyName("language")]
    public string Language { get; set; } = string.Empty;

    /// <summary>
    /// 原句。库里把换行存成 <see cref="Shared.Constants.AppConstants.SpaceForNewLine"/> 占位符
    /// (因为单行的 <c>LOOKUPS</c> 存不下换行),导出时还原成真正的换行 —— 不还原的话
    /// 下游拿到的句子里会夹着一串意义不明的全角空格。
    /// </summary>
    [JsonPropertyName("usage")]
    public string Usage { get; set; } = string.Empty;

    /// <summary>出自哪本书;无则空串。</summary>
    [JsonPropertyName("book")]
    public string Book { get; set; } = string.Empty;

    /// <summary>该书作者;无则空串。</summary>
    [JsonPropertyName("author")]
    public string Author { get; set; } = string.Empty;

    /// <summary>查词时间原文(库里存的是字符串,不做解析);无则空串。</summary>
    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = string.Empty;
}
