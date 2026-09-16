using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace KindleMate2.Avalonia.Charts;

public enum ChartKind {
    Bar,
    Line,

    /// <summary>
    /// 横向条形 —— 给排名类数据用(书名 / 单词这类长标签,竖排会互相重叠)。
    /// 与 <see cref="Bar"/> 共用同一套内边距、字号与圆角规范,只换朝向。
    /// </summary>
    HBar,

    /// <summary>
    /// 环形 —— 给构成占比用(3~5 类)。各扇区用唯一强调色的**透明度阶梯**区分,不引入第二色系;
    /// 右侧带图例(色块 + 标签 + 百分比),圆心显示合计。
    /// </summary>
    Donut,

    /// <summary>
    /// 年历热力图 —— 给「日期 × 密度」用。<see cref="Points"/> 的 Label 必须是 <c>yyyy-MM-dd</c>;
    /// 以数据最大日期为终点、向前 52 周绘制滚动一年,格子颜色按数值分五档取强调色透明度。
    /// </summary>
    Heatmap
}

/// <summary>图表数据点。</summary>
public sealed record ChartPoint(string Label, double Value);

/// <summary>
/// 轻量自绘图表(柱状 / 折线)。
/// Avalonia 无内置 Chart,为保持零第三方依赖,这里直接用 DrawingContext 绘制,
/// 配色走设计令牌,因此深/浅主题都能自动跟随。
/// </summary>
public sealed class ChartControl : Control {
    public static readonly StyledProperty<IReadOnlyList<ChartPoint>?> PointsProperty =
        AvaloniaProperty.Register<ChartControl, IReadOnlyList<ChartPoint>?>(nameof(Points));

    public static readonly StyledProperty<ChartKind> KindProperty =
        AvaloniaProperty.Register<ChartControl, ChartKind>(nameof(Kind));

    public static readonly StyledProperty<IBrush?> AccentProperty =
        AvaloniaProperty.Register<ChartControl, IBrush?>(nameof(Accent));

    public static readonly StyledProperty<IBrush?> AxisProperty =
        AvaloniaProperty.Register<ChartControl, IBrush?>(nameof(Axis));

    public static readonly StyledProperty<IBrush?> LabelProperty =
        AvaloniaProperty.Register<ChartControl, IBrush?>(nameof(Label));

    static ChartControl() {
        AffectsRender<ChartControl>(PointsProperty, KindProperty, AccentProperty, AxisProperty, LabelProperty);
    }

    public IReadOnlyList<ChartPoint>? Points {
        get => GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    public ChartKind Kind {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public IBrush? Accent {
        get => GetValue(AccentProperty);
        set => SetValue(AccentProperty, value);
    }

    public IBrush? Axis {
        get => GetValue(AxisProperty);
        set => SetValue(AxisProperty, value);
    }

    public IBrush? Label {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public override void Render(DrawingContext context) {
        var width = Bounds.Width;
        var height = Bounds.Height;
        if (width <= 2 || height <= 2) return;

        DrawCore(context, width, height);

        // 悬停浮层画在所有图形之上(命中测试见 HitTest)
        DrawHoverOverlay(context, width, height);
    }

    private void DrawCore(DrawingContext context, double width, double height) {

        var accent = Accent ?? Brushes.SteelBlue;
        var axis = Axis ?? Brushes.Gray;
        var labelBrush = Label ?? Brushes.Gray;

        const double padLeft = 6;
        const double padRight = 6;
        const double padTop = 16;
        const double padBottom = 20;

        var plotWidth = width - padLeft - padRight;
        var plotHeight = height - padTop - padBottom;
        if (plotWidth <= 1 || plotHeight <= 1) return;

        var baselineY = padTop + plotHeight;

        var points = Points;
        if (points == null || points.Count == 0) {
            DrawText(context, KindleMate2.Shared.Strings.Ui_Stats_Empty, padLeft, padTop, labelBrush);
            return;
        }

        // 横向条形自成一套:没有底部基线,改为在每条右端标数值(排名图的逐项数值才是主要信息)
        if (Kind == ChartKind.HBar) {
            DrawHorizontalBars(context, points, padLeft, padTop, plotWidth, plotHeight, accent, labelBrush);
            return;
        }

        // 环形不走直角坐标系:自带圆心 / 半径 / 图例布局
        if (Kind == ChartKind.Donut) {
            DrawDonut(context, points, width, height, accent, labelBrush);
            return;
        }

        // 年历热力图同样不走直角坐标系:格子按「周 × 星期」排布
        if (Kind == ChartKind.Heatmap) {
            DrawHeatmap(context, points, padLeft, padTop, plotWidth, plotHeight, accent, labelBrush);
            return;
        }

        context.DrawLine(new Pen(axis, 1), new Point(padLeft, baselineY), new Point(padLeft + plotWidth, baselineY));

        var max = 0d;
        foreach (var point in points) {
            if (point.Value > max) max = point.Value;
        }
        if (max <= 0) max = 1;

        // 峰值参考线 + 数值
        var peakY = baselineY - (max / max) * plotHeight;
        context.DrawLine(new Pen(axis, 0.5, new DashStyle(new double[] { 3, 3 }, 0)),
            new Point(padLeft, peakY), new Point(padLeft + plotWidth, peakY));
        DrawText(context, FormatValue(max), padLeft, peakY - 13, labelBrush);

        var step = plotWidth / points.Count;
        var labelEvery = Math.Max(1, (int)Math.Ceiling(points.Count / 7d));

        if (Kind == ChartKind.Bar) {
            var barWidth = Math.Max(1.5, step * 0.6);
            for (var i = 0; i < points.Count; i++) {
                var value = points[i].Value;
                var barHeight = value <= 0 ? 0 : Math.Max(1, value / max * plotHeight);
                var x = padLeft + step * i + (step - barWidth) / 2;
                context.DrawRectangle(accent, null,
                    new Rect(x, baselineY - barHeight, barWidth, barHeight), 2, 2);
                if (i % labelEvery == 0) {
                    DrawTextCentered(context, points[i].Label, x + barWidth / 2, baselineY + 4, labelBrush);
                }
            }
            return;
        }

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open()) {
            for (var i = 0; i < points.Count; i++) {
                var x = padLeft + step * i + step / 2;
                var y = baselineY - (points[i].Value <= 0 ? 0 : points[i].Value / max * plotHeight);
                if (i == 0) ctx.BeginFigure(new Point(x, y), false);
                else ctx.LineTo(new Point(x, y));
            }
        }
        context.DrawGeometry(null, new Pen(accent, 1.6), geometry);

        for (var i = 0; i < points.Count; i++) {
            var x = padLeft + step * i + step / 2;
            var y = baselineY - (points[i].Value <= 0 ? 0 : points[i].Value / max * plotHeight);
            context.DrawEllipse(accent, null, new Point(x, y), 1.8, 1.8);
            if (i % labelEvery == 0) {
                DrawTextCentered(context, points[i].Label, x, baselineY + 4, labelBrush);
            }
        }
    }

    /// <summary>环形 / 热力图共用的透明度阶梯 —— 只用一个强调色,靠 alpha 分档,不引入第二色系。</summary>
    private static readonly double[] RampAlpha = [1.0, 0.72, 0.48, 0.30, 0.18];

    /// <summary>热力图滚动窗口:最近 52 周。</summary>
    private const int HeatWeeks = 52;

    /// <summary>
    /// 环形图。扇区按 <see cref="RampAlpha"/> 取强调色透明度区分,右侧图例给出色块 / 标签 / 百分比,
    /// 圆心显示合计;卡片偏窄(宽 &lt; 220)时省略图例,优先保证环本身可读。
    /// </summary>
    private static void DrawDonut(DrawingContext context, IReadOnlyList<ChartPoint> points,
        double width, double height, IBrush accent, IBrush labelBrush) {
        var total = 0d;
        foreach (var point in points) {
            if (point.Value > 0) total += point.Value;
        }
        if (total <= 0) {
            DrawText(context, KindleMate2.Shared.Strings.Ui_Stats_Empty, 6, 6, labelBrush);
            return;
        }

        const double minLegendWidth = 220;
        var showLegend = width >= minLegendWidth;
        var legendWidth = showLegend ? Math.Min(width * 0.44, 150) : 0;
        var chartWidth = width - legendWidth;
        var radius = Math.Max(8, Math.Min(chartWidth - 16, height - 16) / 2);
        var center = new Point(chartWidth / 2, height / 2);
        var innerRadius = radius * 0.58;

        var startAngle = -Math.PI / 2;
        for (var i = 0; i < points.Count; i++) {
            var value = points[i].Value;
            if (value <= 0) continue;
            var sweep = value / total * Math.PI * 2;
            DrawRingSegment(context, new SolidColorBrush(AlphaOf(accent, i)), center, radius, innerRadius, startAngle, sweep);
            startAngle += sweep;
        }

        // 圆心合计
        var totalText = new FormattedText(FormatValue(total), CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, Typeface.Default, 16, labelBrush);
        context.DrawText(totalText,
            new Point(center.X - totalText.Width / 2, center.Y - totalText.Height / 2));

        if (!showLegend) return;

        // 右侧图例:色块 + 标签 + 百分比
        var legendX = chartWidth + 8;
        var rowHeight = Math.Min(18, height / Math.Max(1, points.Count));
        var legendTop = (height - rowHeight * points.Count) / 2;
        for (var i = 0; i < points.Count; i++) {
            var rowY = legendTop + rowHeight * i;
            context.DrawRectangle(new SolidColorBrush(AlphaOf(accent, i)), null,
                new Rect(legendX, rowY + rowHeight / 2 - 4, 8, 8), 2, 2);

            var percent = (points[i].Value / total * 100).ToString("0.#", CultureInfo.CurrentCulture) + "%";
            var percentText = FormatText(percent, labelBrush);
            var available = legendWidth - 12 - percentText.Width - 6;
            var labelText = FormatText(FitText(points[i].Label, available, labelBrush), labelBrush);
            var textY = rowY + rowHeight / 2 - labelText.Height / 2;

            context.DrawText(labelText, new Point(legendX + 12, textY));
            context.DrawText(percentText,
                new Point(legendX + legendWidth - percentText.Width, textY));
        }
    }

    /// <summary>
    /// 画一段环带(annulus sector)。整圆拆成两段半圆 —— 起终点重合时 ArcTo 会退化成空路径。
    /// </summary>
    private static void DrawRingSegment(DrawingContext context, IBrush brush, Point center,
        double radius, double innerRadius, double startAngle, double sweep) {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open()) {
            var outerStart = PointOnCircle(center, radius, startAngle);
            var outerEnd = PointOnCircle(center, radius, startAngle + sweep);
            var innerStart = PointOnCircle(center, innerRadius, startAngle);
            var innerEnd = PointOnCircle(center, innerRadius, startAngle + sweep);
            var full = sweep >= Math.PI * 2 - 1e-6;
            var isLarge = !full && sweep > Math.PI;

            ctx.BeginFigure(outerStart, true);
            if (full) {
                ctx.ArcTo(PointOnCircle(center, radius, startAngle + Math.PI), new Size(radius, radius), 0, false, SweepDirection.Clockwise);
                ctx.ArcTo(outerEnd, new Size(radius, radius), 0, false, SweepDirection.Clockwise);
                ctx.LineTo(innerEnd);
                ctx.ArcTo(PointOnCircle(center, innerRadius, startAngle + Math.PI), new Size(innerRadius, innerRadius), 0, false, SweepDirection.CounterClockwise);
                ctx.ArcTo(innerStart, new Size(innerRadius, innerRadius), 0, false, SweepDirection.CounterClockwise);
            } else {
                ctx.ArcTo(outerEnd, new Size(radius, radius), 0, isLarge, SweepDirection.Clockwise);
                ctx.LineTo(innerEnd);
                ctx.ArcTo(innerStart, new Size(innerRadius, innerRadius), 0, isLarge, SweepDirection.CounterClockwise);
            }
            ctx.EndFigure(true);
        }
        context.DrawGeometry(brush, null, geometry);
    }

    /// <summary>
    /// 年历热力图:以数据最大日期为终点向前 52 周的滚动窗口,列 = 周、行 = 星期(周一起)。
    /// 强调色分五档表示密度,顶部标月份、左侧隔行标星期。
    /// </summary>
    private static void DrawHeatmap(DrawingContext context, IReadOnlyList<ChartPoint> points,
        double padLeft, double padTop, double plotWidth, double plotHeight, IBrush accent, IBrush labelBrush) {
        var dated = ParseDated(points, out var maxDate);
        if (dated.Count == 0) {
            DrawText(context, KindleMate2.Shared.Strings.Ui_Stats_Empty, padLeft, padTop, labelBrush);
            return;
        }

        var end = maxDate.Date;
        var start = end.AddDays(-(HeatWeeks * 7 - 1));
        start = start.AddDays(-(((int)start.DayOfWeek + 6) % 7));
        var weeks = (int)((end - start).TotalDays / 7) + 1;

        const double monthLabelHeight = 12;
        const double weekdayLabelWidth = 16;
        var cell = Math.Min((plotWidth - weekdayLabelWidth) / weeks, (plotHeight - monthLabelHeight) / 7);
        if (cell < 2) return;
        var gap = Math.Max(1, cell * 0.14);

        var max = 0d;
        foreach (var item in dated) {
            if (item.Value > max) max = item.Value;
        }
        if (max <= 0) max = 1;

        var originX = padLeft + weekdayLabelWidth;
        var originY = padTop + monthLabelHeight;

        // 左侧星期标签:隔行标一次,避免糊成一片
        var dayNames = CultureInfo.CurrentCulture.DateTimeFormat.AbbreviatedDayNames;
        for (var row = 0; row < 7; row += 2) {
            DrawText(context, dayNames[(row + 1) % 7], padLeft, originY + row * cell + cell / 2 - 6, labelBrush);
        }

        // 顶部月份标签:该周含有 1 号就标一次
        var monthNames = CultureInfo.CurrentCulture.DateTimeFormat.AbbreviatedMonthNames;
        var lastMonth = -1;
        for (var col = 0; col < weeks; col++) {
            var weekStart = start.AddDays(col * 7);
            for (var day = 0; day < 7; day++) {
                var date = weekStart.AddDays(day);
                if (date.Day != 1 || date.Month == lastMonth) continue;
                lastMonth = date.Month;
                DrawText(context, monthNames[date.Month - 1], originX + col * cell, padTop, labelBrush);
                break;
            }
        }

        foreach (var (date, value) in dated) {
            if (date < start || date > end) continue;
            var col = (int)((date - start).TotalDays / 7);
            var row = ((int)date.DayOfWeek + 6) % 7;
            var rect = new Rect(
                originX + col * cell + gap / 2,
                originY + row * cell + gap / 2,
                Math.Max(1, cell - gap),
                Math.Max(1, cell - gap));
            context.DrawRectangle(new SolidColorBrush(AlphaOf(accent, DensityLevel(value, max))), null, rect, 1.5, 1.5);
        }
    }

    /// <summary>把 <c>yyyy-MM-dd</c> 标签解析成日期;同时给出最大日期作为热力图右端。</summary>
    private static List<(DateTime Date, double Value)> ParseDated(IReadOnlyList<ChartPoint> points, out DateTime maxDate) {
        var dated = new List<(DateTime, double)>(points.Count);
        maxDate = DateTime.MinValue;
        foreach (var point in points) {
            if (!DateTime.TryParseExact(point.Label, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var date)) {
                continue;
            }
            dated.Add((date.Date, point.Value));
            if (date > maxDate) maxDate = date;
        }
        return dated;
    }

    /// <summary>密度分档:0 = 最深,4 = 最淡(含空值)。</summary>
    private static int DensityLevel(double value, double max) {
        if (value <= 0) return RampAlpha.Length - 1;
        var ratio = value / max;
        return ratio > 0.75 ? 0 : ratio > 0.5 ? 1 : ratio > 0.25 ? 2 : 3;
    }

    private static Point PointOnCircle(Point center, double radius, double angle) =>
        new(center.X + radius * Math.Cos(angle), center.Y + radius * Math.Sin(angle));

    private static Color ColorOf(IBrush brush) =>
        brush is ISolidColorBrush solid ? solid.Color : Colors.SteelBlue;

    /// <summary>取强调色的第 <paramref name="index"/> 档透明度。</summary>
    private static Color AlphaOf(IBrush brush, int index) {
        var color = ColorOf(brush);
        var alpha = RampAlpha[Math.Clamp(index, 0, RampAlpha.Length - 1)];
        return Color.FromArgb((byte)(alpha * 255), color.R, color.G, color.B);
    }

    /// <summary>
    /// 横向条形。左侧标签区宽度按最长标签实测(上限为绘图区的 40%),超宽标签截断加省略号;
    /// 数值统一标在条右端。行高随项数自适应,故项数越多条越细,永远不会溢出绘图区。
    /// </summary>
    private static void DrawHorizontalBars(DrawingContext context, IReadOnlyList<ChartPoint> points,
        double padLeft, double padTop, double plotWidth, double plotHeight, IBrush accent, IBrush labelBrush) {
        var max = 0d;
        foreach (var point in points) {
            if (point.Value > max) max = point.Value;
        }
        if (max <= 0) max = 1;

        var widestLabel = 0d;
        foreach (var point in points) {
            var measured = FormatText(point.Label, labelBrush).Width;
            if (measured > widestLabel) widestLabel = measured;
        }

        const double labelGap = 6;
        const double valueArea = 36;
        var labelArea = Math.Min(widestLabel, plotWidth * 0.4);
        var barArea = plotWidth - labelArea - labelGap - valueArea;
        if (barArea <= 8) return;

        var rowHeight = plotHeight / points.Count;
        var barHeight = Math.Max(2, Math.Min(rowHeight * 0.62, 18));
        var barX = padLeft + labelArea + labelGap;

        for (var i = 0; i < points.Count; i++) {
            var centerY = padTop + rowHeight * i + rowHeight / 2;
            var value = points[i].Value;
            var barWidth = value <= 0 ? 0 : Math.Max(1, value / max * barArea);

            context.DrawRectangle(accent, null,
                new Rect(barX, centerY - barHeight / 2, barWidth, barHeight), 2, 2);

            var labelText = FormatText(FitText(points[i].Label, labelArea, labelBrush), labelBrush);
            context.DrawText(labelText,
                new Point(padLeft + labelArea - labelText.Width, centerY - labelText.Height / 2));

            var valueText = FormatText(FormatValue(value), labelBrush);
            context.DrawText(valueText, new Point(barX + barWidth + 6, centerY - valueText.Height / 2));
        }
    }

    private static string FormatValue(double value) =>
        value >= 1000 ? value.ToString("N0", CultureInfo.CurrentCulture) : value.ToString("0.#", CultureInfo.CurrentCulture);

    /// <summary>图表统一字号 10.5pt。</summary>
    private static FormattedText FormatText(string text, IBrush brush) =>
        new(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, 10.5, brush);

    /// <summary>按可用宽度截断(超宽加省略号)。逐字符回退 —— 标签都是短串,这点开销可忽略。</summary>
    private static string FitText(string text, double maxWidth, IBrush brush) {
        if (FormatText(text, brush).Width <= maxWidth) return text;

        const string ellipsis = "…";
        var available = maxWidth - FormatText(ellipsis, brush).Width;
        if (available <= 0) return ellipsis;

        for (var length = text.Length - 1; length > 0; length--) {
            if (FormatText(text[..length], brush).Width <= available) {
                return text[..length] + ellipsis;
            }
        }
        return ellipsis;
    }

    private static void DrawText(DrawingContext context, string text, double x, double y, IBrush brush) {
        context.DrawText(FormatText(text, brush), new Point(x, y));
    }

    private static void DrawTextCentered(DrawingContext context, string text, double centerX, double y, IBrush brush) {
        var formatted = FormatText(text, brush);
        context.DrawText(formatted, new Point(centerX - formatted.Width / 2, y));
    }

    // —— 悬停提示 ——
    //
    // 图表此前只能靠眼睛估数值。这里做最小的命中测试 + 自绘浮层:不引入 ToolTip 控件(它有自己的
    // 定位与主题样式,会和自绘图表的观感打架),浮层直接用强调色底 + 深色字,两种主题下都清晰。

    private int _hoverIndex = -1;
    private Point _hoverPosition;

    protected override void OnPointerMoved(PointerEventArgs e) {
        base.OnPointerMoved(e);
        var position = e.GetPosition(this);
        var index = HitTest(position);
        if (index == _hoverIndex && index < 0) return;
        _hoverIndex = index;
        _hoverPosition = position;
        InvalidateVisual();
    }

    protected override void OnPointerExited(PointerEventArgs e) {
        base.OnPointerExited(e);
        if (_hoverIndex < 0) return;
        _hoverIndex = -1;
        InvalidateVisual();
    }

    /// <summary>命中测试:按当前形态把指针位置换算成数据下标,未命中返回 -1。</summary>
    private int HitTest(Point position) {
        var points = Points;
        if (points == null || points.Count == 0) return -1;

        const double padLeft = 6;
        const double padRight = 6;
        const double padTop = 16;
        const double padBottom = 20;
        var width = Bounds.Width;
        var height = Bounds.Height;
        var plotWidth = width - padLeft - padRight;
        var plotHeight = height - padTop - padBottom;
        if (plotWidth <= 1 || plotHeight <= 1) return -1;

        switch (Kind) {
            case ChartKind.Bar:
            case ChartKind.Line: {
                if (position.X < padLeft || position.X > padLeft + plotWidth) return -1;
                var index = (int)((position.X - padLeft) / (plotWidth / points.Count));
                return index >= 0 && index < points.Count ? index : -1;
            }
            case ChartKind.HBar: {
                if (position.Y < padTop || position.Y > padTop + plotHeight) return -1;
                var index = (int)((position.Y - padTop) / (plotHeight / points.Count));
                return index >= 0 && index < points.Count ? index : -1;
            }
            case ChartKind.Donut: {
                var showLegend = width >= 220;
                var chartWidth = width - (showLegend ? Math.Min(width * 0.44, 150) : 0);
                var radius = Math.Max(8, Math.Min(chartWidth - 16, height - 16) / 2);
                var dx = position.X - chartWidth / 2;
                var dy = position.Y - height / 2;
                var distance = Math.Sqrt(dx * dx + dy * dy);
                if (distance > radius || distance < radius * 0.58) return -1;

                var total = 0d;
                foreach (var point in points) {
                    if (point.Value > 0) total += point.Value;
                }
                if (total <= 0) return -1;

                // 与绘制同一起点(-90°)与同向累加
                var fromStart = (Math.Atan2(dy, dx) + Math.PI / 2 + Math.PI * 2) % (Math.PI * 2);
                var accumulated = 0d;
                for (var i = 0; i < points.Count; i++) {
                    if (points[i].Value <= 0) continue;
                    accumulated += points[i].Value / total * Math.PI * 2;
                    if (fromStart <= accumulated) return i;
                }
                return -1;
            }
            case ChartKind.Heatmap: {
                var dated = ParseDated(points, out var maxDate);
                if (dated.Count == 0) return -1;

                var end = maxDate.Date;
                var start = end.AddDays(-(HeatWeeks * 7 - 1));
                start = start.AddDays(-(((int)start.DayOfWeek + 6) % 7));
                var weeks = (int)((end - start).TotalDays / 7) + 1;
                var cell = Math.Min((plotWidth - 16) / weeks, (plotHeight - 12) / 7);
                if (cell < 2) return -1;

                var col = (int)((position.X - (padLeft + 16)) / cell);
                var row = (int)((position.Y - (padTop + 12)) / cell);
                if (col < 0 || col >= weeks || row < 0 || row >= 7) return -1;

                var date = start.AddDays(col * 7 + row);
                for (var i = 0; i < points.Count; i++) {
                    if (string.Equals(points[i].Label, date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), StringComparison.Ordinal)) {
                        return i;
                    }
                }
                return -1;
            }
            default:
                return -1;
        }
    }

    private void DrawHoverOverlay(DrawingContext context, double width, double height) {
        var points = Points;
        if (_hoverIndex < 0 || points == null || _hoverIndex >= points.Count) return;

        var point = points[_hoverIndex];
        // 强调色是固定的浅紫(#7B8CFF),所以浮层文字用深色即可,深浅主题下都够对比
        var text = FormatText(point.Label + "  " + FormatValue(point.Value), Brushes.Black);
        const double paddingX = 8;
        const double paddingY = 5;
        var boxWidth = text.Width + paddingX * 2;
        var boxHeight = text.Height + paddingY * 2;
        var x = Math.Clamp(_hoverPosition.X + 12, 0, Math.Max(0, width - boxWidth));
        var y = Math.Clamp(_hoverPosition.Y + 12, 0, Math.Max(0, height - boxHeight));

        context.DrawRectangle(Accent ?? Brushes.SteelBlue, null, new Rect(x, y, boxWidth, boxHeight), 6, 6);
        context.DrawText(text, new Point(x + paddingX, y + paddingY));
    }
}
