namespace KindleMate2.Application.Models;

/// <summary>一条被清洗掉的差异记录 —— 既用于确认框里的样例,也用于落盘的完整清单。</summary>
/// <param name="Key">标注主键(标注日期|位置)。</param>
/// <param name="BookName">书名,清单里用它定位是哪个本书。</param>
/// <param name="Before">清洗前的内容(用户当时看到的样子)。</param>
/// <param name="After">清洗后的内容。</param>
public sealed record ClippingCleanChange(string Key, string BookName, string Before, string After);

/// <summary>
/// 一次「清洗标注文本」的统计结果。
///
/// 与「清理数据库」(<c>CleanDatabase</c>)是**两件事**:那个清理做的是判重、删空条目、VACUUM,
/// 动的是行数;这个清洗只改每条标注首尾的标点,行数一条不变。
/// </summary>
public sealed class ClippingCleanReport {
    /// <summary>扫过的标注总数。</summary>
    public int Scanned { get; init; }

    /// <summary>真正改掉的条数。</summary>
    public int ChangedCount { get; init; }

    /// <summary>整条都是标点、因而**跳过未改**的条数。</summary>
    public int AllPunctuationCount { get; init; }

    /// <summary>逐条差异,顺序与库内扫描顺序一致。</summary>
    public IReadOnlyList<ClippingCleanChange> Changes { get; init; } = [];
}
