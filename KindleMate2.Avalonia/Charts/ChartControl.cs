using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace KindleMate2.Avalonia.Charts;

public enum ChartKind {
    Bar,
    Line,

    /// <summary>
    /// 横向条形 —— 给排名类数据用(书名 / 单词这类长标签,竖排会互相重叠)。
    /// 与 <see cref="Bar"/> 共用同一套内边距、字号与圆角规范,只换朝向。
    /// </summary>
    HBar
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
}
