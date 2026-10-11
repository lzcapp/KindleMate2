using System;
using System.Globalization;
using KindleMate2.Avalonia.Models;
using KindleMate2.Infrastructure.Helpers;

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

    /// <summary>标注主键里的**位置**(如 <c>113-113</c>)。只用于生成文件名,不上卡片。</summary>
    public string Location { get; init; } = string.Empty;

    /// <summary>
    /// 标注主键里的**时间**,已格式化成能进文件名的 <c>yyyyMMdd-HHmmss</c>。只用于文件名。
    /// </summary>
    public string ClippingTime { get; init; } = string.Empty;

    public bool HasQuote => Quote.Length > 0;
    public bool HasBookName => BookName.Length > 0;
    public bool HasAuthor => AuthorName.Length > 0;
    public bool HasType => Kind != TypeKind.None && TypeText.Length > 0;

    public bool IsHighlight => Kind == TypeKind.Highlight;
    public bool IsNote => Kind == TypeKind.Note;
    public bool IsBookmark => Kind == TypeKind.Bookmark;
    public bool IsCut => Kind == TypeKind.Cut;

    /// <summary>
    /// 建议的分享图文件名:<c>&lt;书名&gt;_&lt;位置&gt;_&lt;标注时间&gt;.png</c>
    ///
    /// **为什么要带位置与时间**:只用「书名 + 当天日期」的话,同一本书同一天的两条标注会给出
    /// **同一个建议名** —— 用户连存两张就会撞(或误覆盖)。而标注主键本身就是
    /// <c>标注日期|位置</c> 且**唯一**,从它拆出来的这两段拼进文件名,天然不会重复。
    /// 位置比"第 N 页"更精确(它是位置区间),所以用位置而不是页数。
    ///
    /// **为什么截断按"重要性"而不是从右往左砍**:位置与时间短,却是上面那条"天然不会重复"的
    /// 全部依据;书名可长。若按字符从尾部截断,书名一长就会把位置与时间**整段砍掉** —— 同书同日
    /// 的两条标注又退回同一个名字,连存两张就互相覆盖,与该方法存在的理由直接冲突。故截断
    /// **先整段保留位置与时间,再拿剩余预算截书名**(见 <see cref="StringHelper.ComposeTruncatedFileName"/>)。
    ///
    /// **上限按 UTF-8 字节而非字符**:全 emoji 的书名 80 个码点 = 320 字节,早已越过 255;
    /// 且按 <c>char</c> 截断会劈开代理对、产出非法 UTF-8。算式:主干 251 + <c>.png</c>(4) = 255 ≤ 255。
    ///
    /// 缺段时逐段跳过(而不是留出连续下划线);全空时退回调用方给的兜底前缀。
    /// </summary>
    public string SuggestedFileName(string fallbackPrefix) {
        var book = BookName.Length > 0 ? BookName : fallbackPrefix;
        // 时间那一段同样过清洗:键里的时间是 2017-06-11 06:57:28,冒号在 Windows 上非法。
        var time = ClippingTime.Length > 0
            ? ClippingTime
            : DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);

        // 位置 + 时间是"必须保留"的尾段;书名是唯一可截断的主段。截断与字节上限都在 helper 里,
        // 与 SanitizeFilename 共用同一份"按 UTF-8 字节在字符边界截断"的实现。
        return StringHelper.ComposeTruncatedFileName(
            head: book,
            tail: new[] { Location, time },
            extension: ".png",
            maxTotalUtf8Bytes: 255);
    }
}
