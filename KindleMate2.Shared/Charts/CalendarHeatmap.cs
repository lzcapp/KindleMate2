using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace KindleMate2.Shared.Charts;

/// <summary>
/// 年历热力图的栅格几何 —— 绘制与命中测试共用同一份计算。
///
/// 日期到「第几列 / 第几行」的换算一旦两处各写一遍就会漂移:画面在第 N 格,
/// 悬停却高亮到隔壁 —— 这类偏差肉眼看不出,只能靠单点定义 + 用例钉住。
/// </summary>
public readonly struct CalendarHeatmapGrid {
    /// <summary>首列所在周的周一。可能早于 <see cref="RangeStart"/>(自然年模式下最多早 6 天)。</summary>
    public DateTime GridStart { get; init; }

    /// <summary>允许着色的最早日期。自然年模式 = 当年 1 月 1 日;滚动模式 = 窗口左端。</summary>
    public DateTime RangeStart { get; init; }

    /// <summary>允许着色的最晚日期。自然年模式 = 当年 12 月 31 日;滚动模式 = 数据最大日期。</summary>
    public DateTime RangeEnd { get; init; }

    /// <summary>列数(一周一列)。自然年最多 53 列。</summary>
    public int Weeks { get; init; }

    /// <summary>格子边长(px)。列向与行向取同一个值,保证格子是正方形。</summary>
    public double Cell { get; init; }

    public double OriginX { get; init; }

    public double OriginY { get; init; }

    /// <summary>格子的最小可见边长;再小就糊成一片,不如不画。</summary>
    public const double MinCell = 2;

    public bool IsDrawable => Weeks > 0 && Cell >= MinCell;

    /// <summary>
    /// <paramref name="date"/> 是否落在可着色区间内。
    /// 首/末列里属于邻年的日子(自然年模式下最多各 6 天)为 false —— 既不画,也不响应悬停。
    /// </summary>
    public bool Contains(DateTime date) {
        var day = date.Date;
        return day >= RangeStart && day <= RangeEnd;
    }

    /// <summary>周一起算的行号:周一 = 0 … 周日 = 6。</summary>
    public static int RowOf(DateTime date) => ((int)date.DayOfWeek + 6) % 7;

    /// <summary>所在列。日期早于 <see cref="GridStart"/> 时给出负数,由调用方判界。</summary>
    public int ColumnOf(DateTime date) => (int)((date.Date - GridStart).TotalDays / 7);

    /// <summary>由列 / 行反解日期。列可超出 <see cref="Weeks"/>,同样由调用方判界。</summary>
    public DateTime DateAt(int column, int row) => GridStart.AddDays((column * 7) + row);
}

/// <summary>
/// 年历热力图共用的纯计算:日标签格式、可用年份、栅格区间。
///
/// 放在 Shared 而不是 Avalonia 层,是为了让回归用例不必引用 UI 程序集就能覆盖这段算术 ——
/// 「哪一天落在哪一格、哪一天该被判为越界」正是热力图最容易算错、又最难靠肉眼看出错的部分。
/// </summary>
public static class CalendarHeatmap {
    /// <summary>
    /// 日标签的**唯一**格式,固定不变文化。
    /// 生产端(<c>StatisticsViewModel.ByCalendarDay</c>)与解析端(<c>ChartControl</c>)必须一致:
    /// 换成 <c>CurrentCulture</c> 的写法后,某些区域会用别的分隔符,热力图会整片画不出来。
    /// </summary>
    public const string DayLabelFormat = "yyyy-MM-dd";

    /// <summary>滚动模式的窗口宽度:最近 52 周。热力图未指定年份时的原有行为。</summary>
    public const int RollingWeeks = 52;

    // 与 ChartControl 的绘图内边距配套:左侧留星期标签、顶部留月份标签。
    private const double WeekdayLabelWidth = 16;
    private const double MonthLabelHeight = 12;

    public static string FormatDay(DateTime date) => date.ToString(DayLabelFormat, CultureInfo.InvariantCulture);

    public static bool TryParseDay(string? label, out DateTime date) =>
        DateTime.TryParseExact(label, DayLabelFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    /// <summary>
    /// 日标签里出现过的年份,**降序**(最近的在前,下拉框默认选中第一项)。
    /// 解析不出来的标签直接忽略 —— 同一个 <c>ChartPoint</c> 类型别的图表也在用,标签格式各不相同。
    /// </summary>
    public static IReadOnlyList<int> YearsOf(IEnumerable<string?> dayLabels) {
        var years = new SortedSet<int>();
        foreach (var label in dayLabels) {
            if (TryParseDay(label, out var date)) years.Add(date.Year);
        }
        return years.Reverse().ToList();
    }

    /// <summary>
    /// 算栅格几何。<paramref name="year"/> 为 null(或超出 <see cref="DateTime"/> 可表示范围)时,
    /// 退回「以 <paramref name="maxDate"/> 为终点向前 <see cref="RollingWeeks"/> 周」的滚动窗口 ——
    /// 这是热力图原有行为,保留给不关心年份的调用方。
    /// </summary>
    public static CalendarHeatmapGrid BuildGrid(DateTime maxDate, int? year,
        double padLeft, double padTop, double plotWidth, double plotHeight) {
        // 先收敛成「合法年份 / null」两态,免得后面每个分支都要重复判一次范围
        var selected = year is > 0 and <= 9999 ? year : null;

        DateTime rangeStart;
        DateTime rangeEnd;
        if (selected.HasValue) {
            rangeStart = new DateTime(selected.Value, 1, 1);
            rangeEnd = new DateTime(selected.Value, 12, 31);
        } else {
            rangeEnd = maxDate.Date;
            rangeStart = rangeEnd.AddDays(-((RollingWeeks * 7) - 1));
        }

        // 左端回退到本周周一,让每一列都是完整的「周一 → 周日」
        var gridStart = rangeStart.AddDays(-CalendarHeatmapGrid.RowOf(rangeStart));
        var weeks = (int)((rangeEnd - gridStart).TotalDays / 7) + 1;

        return new CalendarHeatmapGrid {
            GridStart = gridStart,
            RangeStart = rangeStart,
            RangeEnd = rangeEnd,
            Weeks = weeks,
            Cell = Math.Min((plotWidth - WeekdayLabelWidth) / weeks, (plotHeight - MonthLabelHeight) / 7),
            OriginX = padLeft + WeekdayLabelWidth,
            OriginY = padTop + MonthLabelHeight
        };
    }
}
