using System.Text.Json.Serialization;

namespace KindleMate2.Domain.Models.Export;

/// <summary>
/// 导出中间模型:一本书(及其下的全部标注条目)。
/// </summary>
/// <remarks>
/// <c>author</c> 是**书级**字段,而实体里作者挂在每条标注上。同名书在库里可能只有部分
/// 条目带作者,故取组内**第一条非空**作者 —— 而不是取第一条(那条常常是空的)。
/// </remarks>
public sealed class ExportBook {
    /// <summary>书名;空书名导出为 <c>Unknown Book</c>,此处恒非空。</summary>
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>作者;库里没有时为空串(不写 null,免得下游还要判空)。</summary>
    [JsonPropertyName("author")]
    public string Author { get; set; } = string.Empty;

    /// <summary>本书下的标注条目。</summary>
    [JsonPropertyName("entries")]
    public List<ExportEntry> Entries { get; set; } = [];
}
