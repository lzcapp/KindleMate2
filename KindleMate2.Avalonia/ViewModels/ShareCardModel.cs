using KindleMate2.Avalonia.Models;

namespace KindleMate2.Avalonia.ViewModels;

/// <summary>
/// 分享卡片要显示的内容。
///
/// 内容口径是**用户 2026-09-22 定的**:只留「标注内容 + 书名 + 作者」——
/// 页数、日期这类信息一律舍去(分享图上没人看,还挤占正文的版面)。
/// 类型标签保留(划线/笔记/书签/摘抄):它是视觉识别,不算"信息",而且与应用里那四套 chip 配色同源。
/// </summary>
public sealed class ShareCardModel {
    /// <summary>标注正文 —— 卡片的主角。</summary>
    public string Quote { get; init; } = string.Empty;

    /// <summary>书名。</summary>
    public string BookName { get; init; } = string.Empty;

    /// <summary>作者(可能为空 —— 卡片上整行隐藏,不留空行)。</summary>
    public string AuthorName { get; init; } = string.Empty;

    /// <summary>类型标签文案(划线 / 笔记 / 书签 / 摘抄)。</summary>
    public string TypeText { get; init; } = string.Empty;

    /// <summary>类型 —— 决定标签用哪套 chip 配色。</summary>
    public TypeKind Kind { get; init; }

    public bool HasQuote => Quote.Length > 0;
    public bool HasBookName => BookName.Length > 0;
    public bool HasAuthor => AuthorName.Length > 0;
    public bool HasType => Kind != TypeKind.None && TypeText.Length > 0;

    public bool IsHighlight => Kind == TypeKind.Highlight;
    public bool IsNote => Kind == TypeKind.Note;
    public bool IsBookmark => Kind == TypeKind.Bookmark;
    public bool IsCut => Kind == TypeKind.Cut;
}
