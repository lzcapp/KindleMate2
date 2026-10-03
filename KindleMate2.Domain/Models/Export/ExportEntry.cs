using System.Text.Json.Serialization;

namespace KindleMate2.Domain.Models.Export;

/// <summary>
/// 导出中间模型:一条标注。
/// </summary>
/// <remarks>
/// 字段口径与既有 CSV 导出(<c>ExportManager.WriteClippingsCsv</c>)保持一致:
/// 可空实体字段一律退化成空串而不是 null,<c>page</c> 是唯一的例外(见下)。
/// 这样下游只需要处理一种"没有值"的表示。
/// </remarks>
public sealed class ExportEntry {
    /// <summary>标注正文。实体里 <c>Content</c> 永不 null,空白条目在转换阶段已跳过。</summary>
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 类型文本(<c>Highlight</c> / <c>Note</c> / <c>Bookmark</c> / <c>Cut</c> …)。
    /// 非法或未识别的 <c>BriefType</c> 一律输出**空串**(与 CSV 同口径:既不回退成数字,
    /// 也不编造一个枚举名,免得下游把它当成合法类型去分支)。
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>位置(如 <c>Location 123</c>);无则空串。</summary>
    [JsonPropertyName("location")]
    public string Location { get; set; } = string.Empty;

    /// <summary>
    /// 页码。仅当实体 <c>PageNumber</c> 为正数时才有值 —— 库里"没有页码"存的是 0,
    /// 若照写会变成 <c>"page": 0</c>,下游分不清"第 0 页"和"没有页码",
    /// 故无效页码是 null,配合 <see cref="JsonIgnoreCondition.WhenWritingNull"/>
    /// **整个键被省略**(而不是写成 null)。
    /// </summary>
    [JsonPropertyName("page")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Page { get; set; }

    /// <summary>标注日期原文(库里存的是字符串,不做解析;无则空串)。</summary>
    [JsonPropertyName("date")]
    public string Date { get; set; } = string.Empty;
}
