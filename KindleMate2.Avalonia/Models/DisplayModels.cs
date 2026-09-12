using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using KindleMate2.Domain.Entities.KM2DB;

namespace KindleMate2.Avalonia.Models;

/// <summary>标注类型分组,驱动标签配色(class 绑定用)。</summary>
public enum TypeKind {
    None,
    Highlight,
    Note,
    Bookmark,
    Cut
}

/// <summary>左栏导航项(书籍或生词,含「全部」占位项)。</summary>
public sealed class NavItem {
    public required string Key { get; init; }
    public required string Name { get; init; }
    public int Count { get; init; }
    public bool IsAll { get; init; }

    public string CountText => Count.ToString("N0", CultureInfo.InvariantCulture);

    public override string ToString() => Name;
}

/// <summary>
/// 主列表项(内容优先):主行放正文,元信息行放 书名 · 位置 · 附加 · 类型标签 · 时间。
/// 同时携带详情面板所需的源实体,避免二次查找。
/// </summary>
public sealed class ListItem {
    public required string Key { get; init; }
    public string Primary { get; init; } = string.Empty;
    public string Book { get; init; } = string.Empty;
    public string Place { get; init; } = string.Empty;
    public string Extra { get; init; } = string.Empty;
    public string Time { get; init; } = string.Empty;
    public string TypeText { get; init; } = string.Empty;
    public TypeKind Kind { get; init; } = TypeKind.None;

    public bool HasBook => Book.Length > 0;
    public bool HasPlace => Place.Length > 0;
    public bool HasExtra => Extra.Length > 0;
    public bool HasTime => Time.Length > 0;
    public bool HasType => Kind != TypeKind.None && TypeText.Length > 0;

    /// <summary>元信息行:书名 · 位置 · 附加(空项自动省略)。</summary>
    public string MetaText => string.Join(" · ", new[] { Book, Place, Extra }.Where(s => s.Length > 0));
    public bool HasMeta => MetaText.Length > 0;

    public bool IsHighlight => Kind == TypeKind.Highlight;
    public bool IsNote => Kind == TypeKind.Note;
    public bool IsBookmark => Kind == TypeKind.Bookmark;
    public bool IsCut => Kind == TypeKind.Cut;

    /// <summary>标注项来源(生词项为 null)。</summary>
    public Clipping? Clipping { get; init; }

    /// <summary>生词项来源(标注项为 null)。</summary>
    public Lookup? Lookup { get; init; }

    /// <summary>生词项对应的词条键(用于词干/词频与聚合)。</summary>
    public string LookupWordKey { get; init; } = string.Empty;

    public override string ToString() => Primary;
}

/// <summary>详情面板数据(不可变,选中项变化时整体替换)。</summary>
public sealed class DetailModel {
    public static readonly DetailModel Empty = new();

    public bool HasSelection { get; init; }
    public bool HasNoSelection => !HasSelection;
    public string Title { get; init; } = string.Empty;
    public string Subtitle { get; init; } = string.Empty;
    public string TypeText { get; init; } = string.Empty;
    public TypeKind Kind { get; init; } = TypeKind.None;
    public bool HasType => Kind != TypeKind.None && TypeText.Length > 0;
    public string Time { get; init; } = string.Empty;
    public bool HasTime => Time.Length > 0;

    public bool HasQuote { get; init; }
    public string QuoteLabel { get; init; } = string.Empty;
    public string Quote { get; init; } = string.Empty;

    public bool HasNote { get; init; }
    public string NoteLabel { get; init; } = string.Empty;
    public string Note { get; init; } = string.Empty;

    public bool HasBody { get; init; }
    public string Body { get; init; } = string.Empty;

    public bool IsHighlight => Kind == TypeKind.Highlight;
    public bool IsNote => Kind == TypeKind.Note;
    public bool IsBookmark => Kind == TypeKind.Bookmark;
    public bool IsCut => Kind == TypeKind.Cut;
}

/// <summary>标注类型文本与分组映射(与 Domain 的 BriefType 对齐)。</summary>
public static class TypeTextMap {
    public static (string Text, TypeKind Kind) Of(long? briefType) => briefType switch {
        (long)BriefType.Highlight => ("划线", TypeKind.Highlight),
        (long)BriefType.Note => ("笔记", TypeKind.Note),
        (long)BriefType.Bookmark => ("书签", TypeKind.Bookmark),
        (long)BriefType.Cut => ("剪切", TypeKind.Cut),
        _ => (string.Empty, TypeKind.None)
    };

    public static IReadOnlyList<string> SearchTypes { get; } =
        new[] { "全部", "书籍", "作者", "内容", "笔记" };
}
