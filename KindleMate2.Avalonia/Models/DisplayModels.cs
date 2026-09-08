using KindleMate2.Domain.Entities.KM2DB;

namespace KindleMate2.Avalonia.Models;

/// <summary>书籍节点:书名 + 标注条数。Count = 0 时用于「全部」占位(若需要)。</summary>
public class BookItem {
    public required string Name { get; init; }
    public int Count { get; init; }
    public override string ToString() => Count > 0 ? $"{Name}  ({Count})" : Name;
}

/// <summary>生词节点(按词聚合)。</summary>
public class WordItem {
    public required string Word { get; init; }
    public int Count { get; init; }
    public override string ToString() => Count > 0 ? $"{Word}  ({Count})" : Word;
}

/// <summary>标注行(UI 展示层包装,补类型文本列)。</summary>
public class ClippingRow {
    public required string Key { get; init; }
    public string? BookName { get; init; }
    public string? AuthorName { get; init; }
    public int PageNumber { get; init; }
    public string? Location { get; init; }
    public string? ClippingDate { get; init; }
    public string? Content { get; init; }
    public string TypeText { get; init; } = string.Empty;

    public static ClippingRow From(Clipping c) => new() {
        Key = c.Key,
        BookName = c.BookName,
        AuthorName = c.AuthorName,
        PageNumber = c.PageNumber ?? 0,
        Location = c.ClippingTypeLocation,
        ClippingDate = c.ClippingDate,
        Content = c.Content,
        TypeText = (c.BriefType ?? (long)BriefType.Unknown) switch {
            (long)BriefType.Highlight => "划线",
            (long)BriefType.Note => "笔记",
            (long)BriefType.Bookmark => "书签",
            (long)BriefType.Cut => "剪切",
            (long)BriefType.Hide => "隐藏",
            _ => string.Empty
        }
    };
}

/// <summary>生词查询行(直接复用 Domain Lookup)。</summary>
public static class LookupRow {
    // Lookup 实体已含 Word/Usage/Title/Authors/Timestamp,UI 直接绑定即可。
}
