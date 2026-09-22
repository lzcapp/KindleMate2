using System;
using System.Collections.Generic;
using System.Text;
using KindleMate2.Application.Models;
using KindleMate2.Shared.Clippings;

namespace KindleMate2.Avalonia.ViewModels;

/// <summary>
/// 「清洗标注文本」预览里的一行:改前 / 改后两行,以及被清洗掉的标点。
///
/// 这一层只负责**拼装**:哪几个字算噪音、正文怎么截,全由
/// <see cref="ClippingCleanDiff"/> 与 <see cref="ElidedText"/> 决定 ——
/// 那两块在 Shared 里、有单测(测试工程不引用 Avalonia,逻辑留在这儿就钉不住)。
/// </summary>
public sealed class ClippingCleanPreviewRow {

    /// <summary>正文的显示上限:超出就首尾各留一段、中间省略。</summary>
    public const int DefaultCoreChars = 80;

    public ClippingCleanPreviewRow(ClippingCleanChange change, int coreChars = DefaultCoreChars) {
        ArgumentNullException.ThrowIfNull(change);

        // 先压成单行再算差异:换行会把一行撑成两行,而预览只为核对首尾标点,
        // 完整原文照样在改动清单里。压平之后再算差异,保证「改前 = 噪音 + 正文」自洽。
        var before = Flatten(change.Before);
        var after = Flatten(change.After);

        var diff = ClippingCleanDiff.Compute(before, after);
        // 只截**中间正文**:首尾的噪音一个字符都不能被截掉,那正是要看的东西。
        var core = ElidedText.From(diff.Core, coreChars);

        LeadingNoise = diff.LeadingNoise;
        Head = core.Head;
        Tail = core.Tail;
        TrailingNoise = diff.TrailingNoise;

        (ClippingDate, Location) = SplitKey(change.Key);
        BookName = change.BookName ?? string.Empty;
        FullKey = change.Key ?? string.Empty;
        Source = BuildSource(BookName, Location, ClippingDate);
    }

    /// <summary>首部被清洗掉的字符(视图层用删除线高亮)。</summary>
    public string LeadingNoise { get; }

    /// <summary>正文前缀;被省略时**已含省略号**。</summary>
    public string Head { get; }

    /// <summary>正文后缀;未被省略时为空串。</summary>
    public string Tail { get; }

    /// <summary>尾部被清洗掉的字符(视图层用删除线高亮)。</summary>
    public string TrailingNoise { get; }

    /// <summary>书名 —— 一行里最能定位"这是哪条"的信息。</summary>
    public string BookName { get; }

    /// <summary>标注日期(主键里 <c>|</c> 之前的那半)。</summary>
    public string ClippingDate { get; }

    /// <summary>标注位置(主键里 <c>|</c> 之后的那半)。</summary>
    public string Location { get; }

    /// <summary>行首显示用的一行摘要:<c>书名 · 位置</c>,空的部分自动省掉。</summary>
    public string Source { get; }

    /// <summary>悬停显示完整主键(<c>标注日期|位置</c>),行里放不下但排查时用得上。</summary>
    public string FullKey { get; }

    /// <summary>
    /// 行首摘要:优先「书名 · 位置」,缺哪个就用另一个兜底 —— 三个都空时退回完整主键,
    /// 总之**不许出现一行没有任何定位信息的条目**。
    /// </summary>
    private static string BuildSource(string bookName, string location, string clippingDate) {
        var parts = new List<string>(2);
        if (bookName.Length > 0) parts.Add(bookName);
        if (location.Length > 0) parts.Add(location);
        if (parts.Count == 0 && clippingDate.Length > 0) parts.Add(clippingDate);
        return string.Join(" · ", parts);
    }

    /// <summary>
    /// 主键形如 <c>yyyy-MM-dd HH:mm:ss|位置</c>(见 <c>KM2DatabaseService</c> 的拼装处)。
    /// 按**第一个** <c>|</c> 切:位置本身可能带 <c>|</c>。
    /// </summary>
    private static (string ClippingDate, string Location) SplitKey(string? key) {
        var value = key ?? string.Empty;
        var separator = value.IndexOf('|', StringComparison.Ordinal);
        if (separator < 0) {
            return (value, string.Empty);
        }
        return (value[..separator], value[(separator + 1)..]);
    }

    /// <summary>
    /// 压成单行:换行 / 制表 / 连续空白一律折叠成**一个空格**,且首尾的空白也保留一个。
    ///
    /// 首尾那一个空格不能顺手 Trim 掉 —— 清洗的 <c>Trim</c> 也会拿掉纯空白噪音,
    /// 若在这里先删了,那条改动在预览里就会显示成"什么都没改"。
    /// (视图给噪音加了底色,所以一个空格也是看得见的。)
    /// </summary>
    private static string Flatten(string? text) {
        if (string.IsNullOrEmpty(text)) {
            return string.Empty;
        }

        var builder = new StringBuilder(text.Length);
        var pendingSpace = false;
        foreach (var ch in text) {
            if (ch is ' ' or '\t' or '\r' or '\n' or '\u3000') {
                pendingSpace = true;
                continue;
            }
            if (pendingSpace) {
                builder.Append(' ');
                pendingSpace = false;
            }
            builder.Append(ch);
        }
        if (pendingSpace) {
            builder.Append(' ');
        }
        return builder.ToString();
    }
}
