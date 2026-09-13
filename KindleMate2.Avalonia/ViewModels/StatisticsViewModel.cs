using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using KindleMate2.Avalonia.Charts;
using KindleMate2.Avalonia.Services;
using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Shared;

namespace KindleMate2.Avalonia.ViewModels;

/// <summary>
/// 统计页 VM:按日期 / 小时 / 周几三个维度聚合标注与生词数据,
/// 复用与主界面相同的 DatabaseSession(即"当前打开的库")。
/// </summary>
public sealed class StatisticsViewModel {
    public IReadOnlyList<ChartPoint> ClippingsByDate { get; private init; } = Array.Empty<ChartPoint>();
    public IReadOnlyList<ChartPoint> ClippingsByHour { get; private init; } = Array.Empty<ChartPoint>();
    public IReadOnlyList<ChartPoint> ClippingsByWeekday { get; private init; } = Array.Empty<ChartPoint>();
    public IReadOnlyList<ChartPoint> VocabsByDate { get; private init; } = Array.Empty<ChartPoint>();
    public IReadOnlyList<ChartPoint> VocabsByHour { get; private init; } = Array.Empty<ChartPoint>();
    public IReadOnlyList<ChartPoint> VocabsByWeekday { get; private init; } = Array.Empty<ChartPoint>();

    public bool HasVocabs { get; private init; }
    public string ClippingSummary { get; private init; } = string.Empty;
    public string VocabSummary { get; private init; } = string.Empty;
    public string Source { get; private init; } = string.Empty;
    public bool IsEmpty { get; private init; }

    public static StatisticsViewModel Load(DatabaseSession? session) {
        if (session == null) {
            return new StatisticsViewModel { IsEmpty = true, ClippingSummary = Strings.Ui_Status_NoDatabase };
        }

        var clippings = session.ClippingRepository.GetAll();
        var vocabs = session.VocabRepository.GetAll();

        var clippingDates = clippings.Select(c => ParseDate(c.ClippingDate)).Where(d => d.HasValue).Select(d => d!.Value).ToList();
        var vocabDates = vocabs.Select(v => ParseDate(v.Timestamp)).Where(d => d.HasValue).Select(d => d!.Value).ToList();

        var bookCount = clippings.Select(c => c.BookName).Where(b => !string.IsNullOrWhiteSpace(b)).Distinct().Count();
        var authorCount = clippings.Select(c => c.AuthorName).Where(a => !string.IsNullOrWhiteSpace(a)).Distinct().Count();
        var wordCount = vocabs.Select(v => v.Word).Where(w => !string.IsNullOrWhiteSpace(w)).Distinct().Count();
        var lookupCount = vocabs.Sum(v => v.Frequency ?? 0);

        return new StatisticsViewModel {
            IsEmpty = clippings.Count == 0 && vocabs.Count == 0,
            Source = session.DatabasePath,
            ClippingsByDate = ByDate(clippingDates),
            ClippingsByHour = ByHour(clippingDates),
            ClippingsByWeekday = ByWeekday(clippingDates),
            VocabsByDate = ByDate(vocabDates),
            VocabsByHour = ByHour(vocabDates),
            VocabsByWeekday = ByWeekday(vocabDates),
            HasVocabs = vocabs.Count > 0,
            ClippingSummary = clippingDates.Count > 0
                ? string.Format(CultureInfo.CurrentCulture, Strings.Ui_Stats_SummaryClippings,
                    SpanDays(clippingDates), clippings.Count, bookCount, authorCount)
                : string.Format(CultureInfo.CurrentCulture, Strings.Ui_Stats_SummaryClippingsNoSpan,
                    clippings.Count, bookCount, authorCount),
            VocabSummary = vocabDates.Count > 0
                ? string.Format(CultureInfo.CurrentCulture, Strings.Ui_Stats_SummaryVocab,
                    SpanDays(vocabDates), lookupCount, wordCount)
                : string.Format(CultureInfo.CurrentCulture, Strings.Ui_Stats_SummaryVocabNoSpan,
                    lookupCount, wordCount)
        };
    }

    private static int SpanDays(List<DateTime> dates) => dates.Count == 0 ? 0 : (dates.Max() - dates.Min()).Days;

    private static IReadOnlyList<ChartPoint> ByDate(List<DateTime> dates) =>
        dates.GroupBy(d => new DateTime(d.Year, d.Month, d.Day))
            .OrderBy(g => g.Key)
            .Select(g => new ChartPoint(g.Key.ToString("yyyy.MM.dd", CultureInfo.InvariantCulture), g.Count()))
            .ToList();

    private static IReadOnlyList<ChartPoint> ByHour(List<DateTime> dates) =>
        dates.GroupBy(d => d.Hour)
            .OrderBy(g => g.Key)
            .Select(g => new ChartPoint(g.Key.ToString("00", CultureInfo.InvariantCulture), g.Count()))
            .ToList();

    private static IReadOnlyList<ChartPoint> ByWeekday(List<DateTime> dates) {
        var names = CultureInfo.CurrentCulture.DateTimeFormat.AbbreviatedDayNames;
        return dates.GroupBy(d => (int)d.DayOfWeek)
            .OrderBy(g => g.Key)
            .Select(g => new ChartPoint(names[g.Key], g.Count()))
            .ToList();
    }

    private static DateTime? ParseDate(string? field) {
        if (string.IsNullOrWhiteSpace(field)) return null;
        if (DateTime.TryParse(field, CultureInfo.InvariantCulture, DateTimeStyles.None, out var invariant)) return invariant;
        return DateTime.TryParse(field, CultureInfo.CurrentCulture, DateTimeStyles.None, out var local) ? local : null;
    }
}
