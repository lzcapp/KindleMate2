using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Xunit;
using KindleMate2.Shared.Charts;

namespace KindleMate2.Tests;

/// <summary>
/// 年历热力图栅格的回归用例。
///
/// 需求背景:热力图原先只能画「以数据最大日期为终点向前 52 周」的滚动窗口,没有年份概念,
/// 用户无法回看往年。改成可取年份后,同一段算术开始有两种模式,而这段算术恰恰是
/// **最容易算错、又最难靠肉眼看出错**的地方 —— 少画一天、多画一天、错开一列,
/// 在几百个格子里几乎看不出来,只有把「某天落在第几列第几行」逐日断言才钉得住。
///
/// 因此这里的用例分两层:
///   1. 滚动模式逐一比照**重构前的原文**(<see cref="LegacyRollingWindow"/>,照抄旧实现);
///   2. 自然年模式对全年 365/366 天做「日期 → 列/行 → 日期」往返。
/// 第一层保证这次重构没有顺手改掉原有行为;第二层保证新功能本身成立。
/// </summary>
public sealed class CalendarHeatmapTests {
    // 贴近统计页真实卡片的比例:热力图整幅宽、高度只有百来像素
    private const double PadLeft = 6;
    private const double PadTop = 16;
    private const double PlotWidth = 868;
    private const double PlotHeight = 95;

    /// <summary>
    /// 重构**之前**的滚动窗口算法,照抄自 <c>ChartControl.DrawHeatmap</c> / <c>HitTest</c>。
    /// 拿它当对照原文,而不是把测试写成"再实现一遍看起来差不多的东西" ——
    /// 后者的偏差会和被测代码同步漂移,等于没测。
    /// </summary>
    private static (DateTime WindowStart, DateTime GridStart, DateTime End, int Weeks) LegacyRollingWindow(DateTime maxDate) {
        var end = maxDate.Date;
        var windowStart = end.AddDays(-(52 * 7 - 1));
        var gridStart = windowStart.AddDays(-(((int)windowStart.DayOfWeek + 6) % 7));
        var weeks = (int)((end - gridStart).TotalDays / 7) + 1;
        return (windowStart, gridStart, end, weeks);
    }

    private static CalendarHeatmapGrid Build(DateTime maxDate, int? year) =>
        CalendarHeatmap.BuildGrid(maxDate, year, PadLeft, PadTop, PlotWidth, PlotHeight);

    // —— 1. 日期标签:格式是生产端与解析端之间的契约 ——

    [Fact]
    public void YearsOf_ReturnsDescendingDistinctYears() {
        var labels = new[] { "2019-01-01", "2026-09-21", "2024-02-29", "2026-12-31", "2025-06-30" };

        var years = CalendarHeatmap.YearsOf(labels);

        Assert.Equal(new[] { 2026, 2025, 2024, 2019 }, years);
    }

    [Fact]
    public void YearsOf_IgnoresUnparsableLabels_IncludingNull() {
        // 同一套 ChartPoint 别的图表也在用:「按日期」那张的标签是 yyyy.MM.dd,
        // 万一把它错喂进来,应当一年都读不出(而不是读出脏年份)。
        var labels = new string?[] { "2026.09.21", "21/09/2026", "not-a-date", "", null, "2026-09-21" };

        var years = CalendarHeatmap.YearsOf(labels);

        Assert.Equal(new[] { 2026 }, years);
    }

    /// <summary>
    /// 解析必须不吃 <c>CurrentCulture</c>。换到日期分隔符为 '-' 之外的区域后,
    /// 若解析走的是当前区域而不是不变文化,整张热力图会一片空白 —— 而"图片突然空了"
    /// 很难反推回"某台机器的区域设置"。
    /// </summary>
    [Theory]
    [InlineData("en-GB")]   // 日期分隔符是 '/'
    [InlineData("de-DE")]   // 是 '.'
    [InlineData("zh-CN")]
    [InlineData("th-TH")]   // 非公历区域(佛历),年份本身会变
    public void TryParseDay_IsCultureIndependent(string cultureName) {
        var original = CultureInfo.CurrentCulture;
        try {
            CultureInfo.CurrentCulture = new CultureInfo(cultureName);

            Assert.True(CalendarHeatmap.TryParseDay("2026-09-21", out var date));
            Assert.Equal(new DateTime(2026, 9, 21), date);
            Assert.Equal("2026-09-21", CalendarHeatmap.FormatDay(new DateTime(2026, 9, 21)));
        } finally {
            CultureInfo.CurrentCulture = original;
        }
    }

    // —— 2. 滚动模式:不得因这次重构而位移 ——

    [Theory]
    [InlineData("2026-09-21")]  // 周一
    [InlineData("2026-09-20")]  // 周日
    [InlineData("2026-01-01")]
    [InlineData("2024-02-29")]  // 闰日
    [InlineData("2025-12-31")]
    [InlineData("2016-05-15")]
    public void BuildGrid_WithoutYear_KeepsLegacyRollingWindow(string maxDateText) {
        var maxDate = DateTime.ParseExact(maxDateText, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var legacy = LegacyRollingWindow(maxDate);

        var grid = Build(maxDate, null);

        Assert.Equal(legacy.GridStart, grid.GridStart);
        Assert.Equal(legacy.WindowStart, grid.RangeStart);
        Assert.Equal(legacy.End, grid.RangeEnd);
        Assert.Equal(legacy.Weeks, grid.Weeks);
    }

    [Fact]
    public void BuildGrid_WithoutYear_StillEndsAtMaxDate() {
        var maxDate = new DateTime(2026, 9, 21);

        var grid = Build(maxDate, null);

        Assert.True(grid.Contains(maxDate));
        Assert.False(grid.Contains(maxDate.AddDays(1)));
        Assert.Equal(0, CalendarHeatmapGrid.RowOf(grid.GridStart));  // 首列是周一
    }

    // —— 3. 自然年模式:整个自然年,一天不多一天不少 ——

    [Theory]
    [InlineData(2026)]  // 1/1 是周四 → 首列落在上一年 12 月
    [InlineData(2025)]  // 1/1 是周三
    [InlineData(2024)]  // 闰年,1/1 是周一
    [InlineData(2023)]  // 1/1 是周日 → 首列整列都属于上一年
    [InlineData(2021)]  // 1/1 是周五
    public void BuildGrid_NaturalYear_CoversExactlyThatYear(int year) {
        var first = new DateTime(year, 1, 1);
        var last = new DateTime(year, 12, 31);

        var grid = Build(last, year);

        Assert.Equal(first, grid.RangeStart);
        Assert.Equal(last, grid.RangeEnd);
        Assert.Equal(0, CalendarHeatmapGrid.RowOf(grid.GridStart));
        Assert.InRange((first - grid.GridStart).Days, 0, 6);          // 首列最多回退 6 天
        Assert.InRange(grid.Weeks, 52, 53);                           // 自然年最多 53 列
        Assert.True(grid.IsDrawable);
        Assert.True(grid.Contains(first));
        Assert.True(grid.Contains(last));
        Assert.False(grid.Contains(first.AddDays(-1)));
        Assert.False(grid.Contains(last.AddDays(1)));
    }

    /// <summary>
    /// 全年逐日的往返:日期 → 列/行 → 日期必须回到原处,且列始终落在 [0, Weeks) 内。
    /// 这条是整份用例里最有价值的一条 —— 它同时钉住了列宽、周一为行 0、以及自然年边界。
    /// </summary>
    [Theory]
    [InlineData(2026)]
    [InlineData(2024)]
    [InlineData(2023)]
    public void BuildGrid_NaturalYear_RoundTripsEveryDayOfYear(int year) {
        var grid = Build(new DateTime(year, 12, 31), year);

        for (var day = new DateTime(year, 1, 1); day.Year == year; day = day.AddDays(1)) {
            var column = grid.ColumnOf(day);
            var row = CalendarHeatmapGrid.RowOf(day);

            Assert.InRange(column, 0, grid.Weeks - 1);
            Assert.InRange(row, 0, 6);
            Assert.Equal(day, grid.DateAt(column, row));
        }
    }

    /// <summary>首/末列里属于邻年的格子既不画也不响应悬停,故不能算"包含"。</summary>
    [Fact]
    public void BuildGrid_NaturalYear_ExcludesAdjacentYearCells() {
        // 2026-01-01 是周四 → 首列从 2025-12-29(周一)起
        var grid = Build(new DateTime(2026, 12, 31), 2026);

        Assert.Equal(new DateTime(2025, 12, 29), grid.GridStart);
        Assert.False(grid.Contains(new DateTime(2025, 12, 29)));
        Assert.False(grid.Contains(new DateTime(2025, 12, 31)));
        Assert.True(grid.Contains(new DateTime(2026, 1, 1)));

        // 越界的格子仍然有确定的坐标(供命中测试判界),只是不该被画出来
        Assert.Equal(0, grid.ColumnOf(new DateTime(2025, 12, 29)));
        Assert.Equal(new DateTime(2025, 12, 29), grid.DateAt(0, 0));
    }

    [Fact]
    public void BuildGrid_NaturalYear_OutOfRangeYearFallsBackToRolling() {
        var maxDate = new DateTime(2026, 9, 21);
        var rolling = Build(maxDate, null);

        foreach (var bogus in new int?[] { 0, -1, 10000, int.MinValue, int.MaxValue }) {
            var grid = Build(maxDate, bogus);

            Assert.Equal(rolling.GridStart, grid.GridStart);
            Assert.Equal(rolling.RangeEnd, grid.RangeEnd);
            Assert.Equal(rolling.Weeks, grid.Weeks);
        }
    }

    /// <summary>年份参数必须真的改变区间 —— 否则「可选往年」就是个摆设。</summary>
    [Fact]
    public void BuildGrid_YearParameterActuallyChangesTheWindow() {
        var maxDate = new DateTime(2026, 9, 21);

        var rolling = Build(maxDate, null);
        var thisYear = Build(maxDate, 2026);
        var lastYear = Build(maxDate, 2025);

        Assert.NotEqual(rolling.GridStart, thisYear.GridStart);
        Assert.Equal(new DateTime(2026, 1, 1), thisYear.RangeStart);
        Assert.Equal(new DateTime(2025, 1, 1), lastYear.RangeStart);
        Assert.False(thisYear.Contains(new DateTime(2025, 12, 31)));  // 往年在自己的那一年里
        Assert.True(lastYear.Contains(new DateTime(2025, 12, 31)));
    }

    // —— 4. 几何:格子铺满绘图区、不溢出,且横向纵向各自独立 ——

    [Theory]
    [InlineData(2026)]
    [InlineData(null)]
    public void BuildGrid_FitsInsidePlotArea(int? year) {
        var grid = Build(new DateTime(2026, 9, 21), year);

        Assert.True(grid.IsDrawable);
        Assert.True(grid.OriginX + grid.Weeks * grid.CellWidth <= PadLeft + PlotWidth + 0.5,
            $"横向溢出:右边界 {grid.OriginX + grid.Weeks * grid.CellWidth} > {PadLeft + PlotWidth}");
        Assert.True(grid.OriginY + 7 * grid.CellHeight <= PadTop + PlotHeight + 0.5,
            $"纵向溢出:下边界 {grid.OriginY + 7 * grid.CellHeight} > {PadTop + PlotHeight}");
        Assert.Equal(PadLeft + 16, grid.OriginX);   // 左侧让给星期标签
        Assert.Equal(PadTop + 12, grid.OriginY);    // 顶部让给月份标签
    }

    /// <summary>
    /// 横向必须铺满:重构前横向纵向共用一个 <c>Cell = Min(宽, 高)</c>,
    /// 高度是瓶颈时 52 周只占左边三分之一,右边一大片空白。
    /// </summary>
    [Theory]
    [InlineData(2026)]
    [InlineData(null)]
    public void BuildGrid_FillsPlotAreaHorizontally(int? year) {
        var grid = Build(new DateTime(2026, 9, 21), year);

        Assert.Equal(PadLeft + PlotWidth, grid.OriginX + grid.Weeks * grid.CellWidth, 1);
    }

    /// <summary>纵向同理必须铺满:否则格子挤在顶上、底部留白。</summary>
    [Theory]
    [InlineData(2026)]
    [InlineData(null)]
    public void BuildGrid_FillsPlotAreaVertically(int? year) {
        var grid = Build(new DateTime(2026, 9, 21), year);

        Assert.Equal(PadTop + PlotHeight, grid.OriginY + 7 * grid.CellHeight, 1);
    }

    /// <summary>
    /// 窗口拉高:格子该跟着变高,横向尺度不受影响 —— 这就是「拉伸适配窗口」本身。
    /// 若哪天又退回单值 Cell,这条会红。
    /// </summary>
    [Fact]
    public void BuildGrid_TallerCard_KeepsWidthAndGrowsHeight() {
        var maxDate = new DateTime(2026, 12, 31);
        var fixedHeight = CalendarHeatmap.BuildGrid(maxDate, 2026, PadLeft, PadTop, PlotWidth, PlotHeight);
        var tallerHeight = CalendarHeatmap.BuildGrid(maxDate, 2026, PadLeft, PadTop, PlotWidth, PlotHeight + 60);

        Assert.Equal(fixedHeight.CellWidth, tallerHeight.CellWidth);
        Assert.True(tallerHeight.CellHeight > fixedHeight.CellHeight,
            $"纵向没跟着拉伸:{tallerHeight.CellHeight} 应大于 {fixedHeight.CellHeight}");
    }

    /// <summary>窗口拉宽:反向成立 —— 高一格不变,横向变宽。</summary>
    [Fact]
    public void BuildGrid_WiderCard_KeepsHeightAndGrowsWidth() {
        var maxDate = new DateTime(2026, 12, 31);
        var normal = CalendarHeatmap.BuildGrid(maxDate, 2026, PadLeft, PadTop, PlotWidth, PlotHeight);
        var wider = CalendarHeatmap.BuildGrid(maxDate, 2026, PadLeft, PadTop, PlotWidth + 200, PlotHeight);

        Assert.Equal(normal.CellHeight, wider.CellHeight);
        Assert.True(wider.CellWidth > normal.CellWidth,
            $"横向没跟着拉伸:{wider.CellWidth} 应大于 {normal.CellWidth}");
    }

    /// <summary>
    /// 两轴尺度本就不同,格子不再正方形。这条把「不再取小值」这个决定钉在用例里:
    /// 一旦有人为了"看起来整齐"又把两轴绑回同一个值,断言立刻失败。
    /// </summary>
    [Fact]
    public void BuildGrid_AxesAreIndependent_NotForcedSquare() {
        var grid = Build(new DateTime(2026, 12, 31), 2026);

        Assert.NotEqual(grid.CellWidth, grid.CellHeight, 3);
    }

    [Fact]
    public void BuildGrid_PlotTooSmallToRead_IsNotDrawable() {
        // 高度只剩 20px:扣掉月份标签后一格不到 2px,画出来只是一条糊线
        var grid = CalendarHeatmap.BuildGrid(new DateTime(2026, 12, 31), 2026,
            PadLeft, PadTop, PlotWidth, 20);

        Assert.True(grid.CellHeight < CalendarHeatmapGrid.MinCell);
        Assert.False(grid.IsDrawable);
    }

    /// <summary>宽度不够时同样不可绘制 —— 两方向各判一次,不能只看纵向。</summary>
    [Fact]
    public void BuildGrid_PlotTooNarrow_IsNotDrawable() {
        // 宽度只剩 6px,全给星期标签,横向一格都摊不出来
        var grid = CalendarHeatmap.BuildGrid(new DateTime(2026, 12, 31), 2026,
            PadLeft, PadTop, 6, PlotHeight);

        Assert.True(grid.CellWidth < CalendarHeatmapGrid.MinCell);
        Assert.False(grid.IsDrawable);
    }

    [Fact]
    public void RowOf_MondayIsZeroThroughSundayIsSix() {
        var monday = new DateTime(2026, 9, 21);   // 周一
        Assert.Equal(DayOfWeek.Monday, monday.DayOfWeek);

        for (var offset = 0; offset < 7; offset++) {
            Assert.Equal(offset, CalendarHeatmapGrid.RowOf(monday.AddDays(offset)));
        }
    }

    [Fact]
    public void BuildGrid_NaturalYear_IsDrawableAtRealisticCardSize() {
        // 用例里的尺寸就是统计页热力图卡片的实际尺寸,免得"逻辑对但在真实尺寸下画不出来"
        var grid = Build(new DateTime(2026, 12, 31), 2026);

        Assert.True(grid.IsDrawable);
        // 53 列铺满 852px ≈ 16px/格;7 行铺满 83px ≈ 11.9px/格
        Assert.InRange(grid.CellWidth, 12, 20);
        Assert.InRange(grid.CellHeight, 8, 20);
    }

    private static IEnumerable<string> AllDaysOf(int year) {
        for (var day = new DateTime(year, 1, 1); day.Year == year; day = day.AddDays(1)) {
            yield return CalendarHeatmap.FormatDay(day);
        }
    }

    [Fact]
    public void YearsOf_HandlesFullYearOfLabels() {
        var years = CalendarHeatmap.YearsOf(AllDaysOf(2024).Concat(AllDaysOf(2026)));

        Assert.Equal(new[] { 2026, 2024 }, years);
    }
}
