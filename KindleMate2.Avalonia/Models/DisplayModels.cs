using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Controls.Documents;
using KindleMate2.Application.Services;
using KindleMate2.Avalonia.Services;
using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Shared;

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

    /// <summary>
    /// <see cref="Primary"/> 按生词切好的分段(见 <c>WordEmphasis</c>)。**默认空表** ——
    /// 空表 = 不做高亮,渲染出来与 <see cref="Primary"/> 整段一致。
    ///
    /// 这里存的是**纯数据**(哪一段命中),<c>InlineCollection</c> 由
    /// <see cref="PrimaryInlines"/> 现算 —— 这样 <c>BuildWordDomainItems</c> 无头可测。
    /// </summary>
    public IReadOnlyList<EmphasisSegment> PrimarySegments { get; init; } = Array.Empty<EmphasisSegment>();

    /// <summary>列表行正文的渲染形态(界面绑这个,不再绑 <see cref="Primary"/>)。</summary>
    public InlineCollection PrimaryInlines => EmphasisInlines.Build(PrimarySegments, Primary);

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

    /// <summary>
    /// 元信息行里**跟在书名后面**的那一截:位置 · 附加(空项自动省略)。
    ///
    /// 书名**不在这里** —— 它单独占一列、由那一列负责省略(见 <c>MainWindow.axaml</c> 的列表模板)。
    /// 早先这里是整串 <c>Book · Place · Extra</c>,书名一长就把后面全挤掉,而"第 798 页"
    /// 恰恰是比书名更该留住的信息(书名在左栏/详情面板都有)。
    /// </summary>
    public string MetaTail => string.Join(" · ", new[] { Place, Extra }.Where(s => s.Length > 0));

    public bool HasMetaTail => MetaTail.Length > 0;

    public bool IsHighlight => Kind == TypeKind.Highlight;
    public bool IsNote => Kind == TypeKind.Note;
    public bool IsBookmark => Kind == TypeKind.Bookmark;
    public bool IsCut => Kind == TypeKind.Cut;

    /// <summary>
    /// 分组标题行的文字(如「1 条查询」「20 条标注」)。**非空即代表这是一行分组标题** ——
    /// 它没有正文、没有详情,只负责把中栏切成两段(生词域:查询 / 标注)。
    ///
    /// 复用既有那两条计数文案、而不是新造「查询」「标注」标签:它们已带三种语言,
    /// 而且正是用户在右栏副标题里见过的措辞 —— 一眼认得出,不用重新学。
    /// </summary>
    public string SectionTitle { get; init; } = string.Empty;

    /// <summary>是否为分组标题行(不挂 Clipping / Lookup,详情为空)。</summary>
    public bool IsSectionHeader => SectionTitle.Length > 0;

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

    /// <summary>
    /// <see cref="Quote"/> 按生词切好的分段。笔记条目的引文是**原文划线**,
    /// 与被引的那条划线的正文是同一类东西 —— 正文高亮、引文不高亮,读起来像两个功能。
    /// </summary>
    public IReadOnlyList<EmphasisSegment> QuoteSegments { get; init; } = Array.Empty<EmphasisSegment>();

    /// <summary>引文的渲染形态(界面绑这个,不再绑 <see cref="Quote"/>)。</summary>
    public InlineCollection QuoteInlines => EmphasisInlines.Build(QuoteSegments, Quote);

    public bool HasNote { get; init; }
    public string NoteLabel { get; init; } = string.Empty;
    public string Note { get; init; } = string.Empty;

    /// <summary>
    /// <see cref="Note"/> 按生词切好的分段。笔记条目的**笔记正文**在中栏就是以
    /// <c>ListItem.Primary</c> 显示的同一串字(走 <c>ToListItem(Clipping)</c>),
    /// 那边已按生词加粗 —— 右栏不加就又成了"同一句话两种样子"。
    /// </summary>
    public IReadOnlyList<EmphasisSegment> NoteSegments { get; init; } = Array.Empty<EmphasisSegment>();

    /// <summary>笔记正文的渲染形态(界面绑这个,不再绑 <see cref="Note"/>)。</summary>
    public InlineCollection NoteInlines => EmphasisInlines.Build(NoteSegments, Note);

    public bool HasBody { get; init; }
    public string Body { get; init; } = string.Empty;

    /// <summary>
    /// <see cref="Body"/> 按生词切好的分段。生词详情的用法句里会把生词加粗 ——
    /// 中栏「标注」段加粗、右栏同一句话却不高亮,看上去就像高亮坏了,所以两边同源。
    ///
    /// 这里覆盖的**不止生词详情**:生词域里点中栏「标注」段的一条,右栏出的是那条**剪藏**
    /// 的详情(见 <c>BuildClippingDetail</c>)—— 中栏那行加了粗、右栏同一句话不加,
    /// 与上面正是同一个毛病(2026-09-24 用户发现)。
    /// </summary>
    public IReadOnlyList<EmphasisSegment> BodySegments { get; init; } = Array.Empty<EmphasisSegment>();

    /// <summary>正文的渲染形态(界面绑这个,不再绑 <see cref="Body"/>)。</summary>
    public InlineCollection BodyInlines => EmphasisInlines.Build(BodySegments, Body);

    /// <summary>
    /// 在线释义(生词详情专用)。**拿不到就不显示** —— 所以它由 <see cref="HasDefinition"/> 单独控制,
    /// 不并进 Body:Body 是 Kindle 记下的原句(离线数据),这一段是联网查来的,来源与可得性都不同。
    /// </summary>
    public bool HasDefinition { get; init; }
    public string DefinitionLabel { get; init; } = string.Empty;
    public string Definition { get; init; } = string.Empty;

    /// <summary>释义被截断了(只有百科会:实测「高要」414 字)⇒ 界面上给一个"展开全文"入口。
    /// **由是否截断决定,不由展开状态决定** —— 展开之后那个"收起"入口还得在。</summary>
    public bool HasDefinitionOverflow { get; init; }
    public string DefinitionToggleLabel { get; init; } = string.Empty;

    public bool IsHighlight => Kind == TypeKind.Highlight;
    public bool IsNote => Kind == TypeKind.Note;
    public bool IsBookmark => Kind == TypeKind.Bookmark;
    public bool IsCut => Kind == TypeKind.Cut;
}

/// <summary>标注类型文本与分组映射(与 Domain 的 BriefType 对齐;文案取自 Shared.Strings)。</summary>
public static class TypeTextMap {
    public static (string Text, TypeKind Kind) Of(long? briefType) => briefType switch {
        (long)BriefType.Highlight => (Strings.Ui_Type_Highlight, TypeKind.Highlight),
        (long)BriefType.Note => (Strings.Ui_Type_Note, TypeKind.Note),
        (long)BriefType.Bookmark => (Strings.Ui_Type_Bookmark, TypeKind.Bookmark),
        (long)BriefType.Cut => (Strings.Ui_Type_Cut, TypeKind.Cut),
        _ => (string.Empty, TypeKind.None)
    };

    /// <summary>搜索范围下拉项。索引与 <see cref="MatchClipping"/> 的判定顺序一致。</summary>
    public static IReadOnlyList<string> SearchTypes { get; } = new[] {
        Strings.Ui_Search_Type_All,
        Strings.Ui_Search_Type_Books,
        Strings.Ui_Search_Type_Author,
        Strings.Ui_Search_Type_Content,
        Strings.Ui_Search_Type_Note
    };
}
