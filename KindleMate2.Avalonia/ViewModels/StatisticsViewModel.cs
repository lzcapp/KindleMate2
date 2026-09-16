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

    // —— 以下四个维度是**有意扩展**:原版 FrmStatistics 只有「按日期 / 按小时 / 按星期」
    // 三个维度(tabPageBooks + tabPageVocabs 各三张 Chart)。用户 2026-09-17 明确要求
    // 统计图表更丰富,故按工程判断补齐;形态与配色规范见 StatisticsWindow.axaml 顶部注释。

    /// <summary>标注类型构成(标注 / 笔记 / 书签)。标签复用既有文案键,顺序按 <see cref="BriefType"/> 数值。</summary>
    public IReadOnlyList<ChartPoint> ClippingsByType { get; private init; } = Array.Empty<ChartPoint>();

    /// <summary>标注最多的书籍(前 <see cref="TopBooksTake"/> 本),供横向条形。</summary>
    public IReadOnlyList<ChartPoint> TopBooks { get; private init; } = Array.Empty<ChartPoint>();

    /// <summary>查询最多的生词(前 <see cref="TopWordsTake"/> 个,按 Frequency 求和),供横向条形。</summary>
    public IReadOnlyList<ChartPoint> TopWords { get; private init; } = Array.Empty<ChartPoint>();

    /// <summary>
    /// 生词页的构成图:查询频次分布(1 次 / 2-3 次 / 4-10 次 / 11 次以上)。
    /// 原本打算用 <c>Vocab.Category</c> 做「词汇类别构成」,但该字段是类别 id(long?)而非名称,
    /// 直接拿来当图表标签没有可读性,故改用语义明确的频次分桶。
    /// </summary>
    public IReadOnlyList<ChartPoint> VocabFrequencyBuckets { get; private init; } = Array.Empty<ChartPoint>();

    /// <summary>
    /// 年历热力图数据。Label **固定**用不变文化的 <c>yyyy-MM-dd</c> ——
    /// <c>ChartControl</c> 的 Heatmap 分支按这个格式解析日期,改格式会让热力图画不出来。
    /// </summary>
    public IReadOnlyList<ChartPoint> ClippingsCalendar { get; private init; } = Array.Empty<ChartPoint>();

    /// <inheritdoc cref="ClippingsCalendar"/>
    public IReadOnlyList<ChartPoint> VocabsCalendar { get; private init; } = Array.Empty<ChartPoint>();

    // 排名类图表的项数上限:横向条形再多就挤得看不清了。
    private const int TopBooksTake = 10;
    private const int TopWordsTake = 20;

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
            ClippingsByType = ByType(clippings),
            TopBooks = TopBooksOf(clippings),
            VocabsByDate = ByDate(vocabDates),
            VocabsByHour = ByHour(vocabDates),
            VocabsByWeekday = ByWeekday(vocabDates),
            VocabFrequencyBuckets = ByFrequencyBuckets(vocabs),
            ClippingsCalendar = ByCalendarDay(clippingDates),
            VocabsCalendar = ByCalendarDay(vocabDates),
            TopWords = TopWordsOf(vocabs),
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

    /// <summary>标注类型构成。只列已归类的三种(标注 / 笔记 / 书签),顺序按 BriefType 数值。</summary>
    private static IReadOnlyList<ChartPoint> ByType(IEnumerable<Clipping> clippings) {
        var counts = new Dictionary<int, int>();
        foreach (var clipping in clippings) {
            if (clipping.BriefType is not { } briefType) continue;
            var type = (int)briefType;
            counts[type] = counts.TryGetValue(type, out var current) ? current + 1 : 1;
        }
        return new[] {
                (Type: (int)BriefType.Highlight, Label: Strings.Clippings),
                (Type: (int)BriefType.Note, Label: Strings.Note),
                (Type: (int)BriefType.Bookmark, Label: Strings.Ui_Type_Bookmark)
            }
            .Where(item => counts.TryGetValue(item.Type, out var count) && count > 0)
            .Select(item => new ChartPoint(item.Label, counts[item.Type]))
            .ToList();
    }

    /// <summary>标注最多的书籍。并列时按书名排序,保证结果稳定(不随字典遍历顺序抖动)。</summary>
    private static IReadOnlyList<ChartPoint> TopBooksOf(IEnumerable<Clipping> clippings) =>
        clippings.Where(c => !string.IsNullOrWhiteSpace(c.BookName))
            .GroupBy(c => c.BookName!, StringComparer.Ordinal)
            .Select(g => new ChartPoint(g.Key, g.Count()))
            .OrderByDescending(p => p.Value)
            .ThenBy(p => p.Label, StringComparer.CurrentCulture)
            .Take(TopBooksTake)
            .ToList();

    /// <summary>查询最多的生词(按 Frequency 累计)。</summary>
    private static IReadOnlyList<ChartPoint> TopWordsOf(IEnumerable<Vocab> vocabs) =>
        vocabs.Where(v => !string.IsNullOrWhiteSpace(v.Word))
            .GroupBy(v => v.Word!, StringComparer.Ordinal)
            .Select(g => new ChartPoint(g.Key, g.Sum(v => v.Frequency ?? 0)))
            .Where(p => p.Value > 0)
            .OrderByDescending(p => p.Value)
            .ThenBy(p => p.Label, StringComparer.CurrentCulture)
            .Take(TopWordsTake)
            .ToList();

    /// <summary>查询频次分布:把每个词的查询次数落进四个桶。零次(从未查询)不计入。</summary>
    private static IReadOnlyList<ChartPoint> ByFrequencyBuckets(IEnumerable<Vocab> vocabs) {
        var buckets = new int[4];
        foreach (var vocab in vocabs) {
            var frequency = vocab.Frequency ?? 0;
            if (frequency <= 0) continue;
            buckets[frequency == 1 ? 0 : frequency <= 3 ? 1 : frequency <= 10 ? 2 : 3]++;
        }

        // 桶标签用区间写法,配合卡片标题「查询次数分布」自解释
        var labels = new[] { "1", "2–3", "4–10", "11+" };
        var result = new List<ChartPoint>(labels.Length);
        for (var i = 0; i < labels.Length; i++) {
            if (buckets[i] > 0) result.Add(new ChartPoint(labels[i], buckets[i]));
        }
        return result;
    }

    /// <summary>
    /// 年历热力图数据:按日聚合。标签**必须**是不变文化的 <c>yyyy-MM-dd</c> ——
    /// 热力图那一侧按这个格式反解日期,换成 CurrentCulture 的写法(某些区域会用别的分隔符)就会画不出来。
    /// </summary>
    private static IReadOnlyList<ChartPoint> ByCalendarDay(IEnumerable<DateTime> dates) =>
        dates.GroupBy(d => new DateTime(d.Year, d.Month, d.Day))
            .OrderBy(g => g.Key)
            .Select(g => new ChartPoint(g.Key.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), g.Count()))
            .ToList();

    private static DateTime? ParseDate(string? field) {
        if (string.IsNullOrWhiteSpace(field)) return null;
        if (DateTime.TryParse(field, CultureInfo.InvariantCulture, DateTimeStyles.None, out var invariant)) return invariant;
        return DateTime.TryParse(field, CultureInfo.CurrentCulture, DateTimeStyles.None, out var local) ? local : null;
    }
}
