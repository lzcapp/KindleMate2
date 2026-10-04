using System.Globalization;
using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Domain.Models.Export;
using KindleMate2.Shared.Constants;

namespace KindleMate2.Application.Services;

/// <summary>
/// 实体(<see cref="Clipping"/> / <see cref="Lookup"/>)→ 导出中间模型(DTO)的转换。
/// </summary>
/// <remarks>
/// <para>
/// 为什么单独抽一层而不直接在 writer 里拼:序列化格式只是**一种**落地方式。
/// 这份中间模型是后续 PR(Markdown frontmatter、Obsidian 模式、以及将来的服务集成)
/// 与 JSON 共用的**同一个源**,届时新增格式只需要新增一个 writer,不必再重写一遍
/// "空内容跳过 / 无效页码省略 / 换行占位符还原"这类口径。
/// </para>
/// <para>
/// 各条口径都刻意与既有 CSV 导出对齐(见各方法注释):同一份库导出成两种格式,
/// 却对"这条算不算""这个值有没有"给出两个答案,是最难排查的那类不一致。
/// </para>
/// </remarks>
internal static class ExportModelBuilder {
    /// <summary>
    /// 书名缺失时的兜底书名,与 <c>StringHelper.BuildMarkdownWithClippings</c> 的既有口径一致。
    /// </summary>
    internal const string UnknownBookName = "Unknown Book";

    /// <summary>
    /// 把标注实体转成标注文档。
    /// </summary>
    /// <param name="clippings">标注实体序列(通常来自 <c>IClippingService.GetAllClippings()</c>)。</param>
    /// <param name="exportedAt">导出时刻;不传则取当前时间(仅单测需要显式固定它)。</param>
    internal static ExportClippingsDocument BuildClippingsDocument(
        IEnumerable<Clipping> clippings, DateTimeOffset? exportedAt = null) {
        var document = new ExportClippingsDocument { ExportedAt = FormatTimestamp(exportedAt) };

        // 书名 → 组的索引,同时保留插入顺序(List):标注在库里的顺序本身就是阅读顺序,
        // 若改成按书名排序,JSON 的行序会与 Markdown / CSV 导出对不上,diff 时很难看。
        var books = new List<ExportBook>();
        var bookByTitle = new Dictionary<string, ExportBook>(StringComparer.Ordinal);

        foreach (var clipping in clippings) {
            // 空白正文直接跳过,与 WriteClippingsCsv 一致(库里存在正文为空的占位行)。
            if (string.IsNullOrWhiteSpace(clipping.Content)) {
                continue;
            }

            // Trim 后才做分组键:导入路径会让部分书名带前后空格,不 Trim 会把同一本书
            // 在 JSON 里裂成两组(Markdown 导出同样是 Trim 后写的标题)。
            var title = string.IsNullOrWhiteSpace(clipping.BookName)
                ? UnknownBookName
                : clipping.BookName.Trim();

            if (!bookByTitle.TryGetValue(title, out var book)) {
                book = new ExportBook { Title = title };
                bookByTitle.Add(title, book);
                books.Add(book);
            }

            var author = clipping.AuthorName ?? string.Empty;
            if (book.Author.Length == 0 && author.Length > 0) {
                book.Author = author;
            }

            book.Entries.Add(new ExportEntry {
                Content = clipping.Content,
                Type = BriefTypeText(clipping.BriefType),
                Location = clipping.ClippingTypeLocation ?? string.Empty,
                // PageNumber 为 0 / 负数都视作"没有页码":库里无页码存 0,而负数没有任何
                // 合法含义 —— 交给 ExportEntry.Page 的 WhenWritingNull 省略整个键。
                Page = clipping.PageNumber is > 0 ? clipping.PageNumber : null,
                Date = clipping.ClippingDate ?? string.Empty,
            });
        }

        document.Books = books;
        return document;
    }

    /// <summary>
    /// 把生词实体转成熟词文档。
    /// </summary>
    /// <param name="lookups">生词实体序列(通常来自 <c>ILookupService.GetAllLookups()</c>)。</param>
    /// <param name="exportedAt">导出时刻;不传则取当前时间。</param>
    internal static ExportVocabsDocument BuildVocabsDocument(
        IEnumerable<Lookup> lookups, DateTimeOffset? exportedAt = null) {
        var document = new ExportVocabsDocument { ExportedAt = FormatTimestamp(exportedAt) };

        var words = new List<ExportWord>();
        foreach (var lookup in lookups) {
            // 词为空则跳过,与 WriteLookupsCsv 一致(WordKey 为 null 或 "en:" 时取不到词)。
            var word = lookup.Word;
            if (string.IsNullOrWhiteSpace(word)) {
                continue;
            }

            words.Add(new ExportWord {
                Word = word,
                Stem = lookup.Stem ?? string.Empty,
                // 复用 CSV 那条已经单测过的实现,而不是再写一份前缀解析。
                Language = ExportManager.LanguageOfWordKey(lookup.WordKey),
                Usage = (lookup.Usage ?? string.Empty)
                    .Replace(AppConstants.SpaceForNewLine, Environment.NewLine, StringComparison.Ordinal),
                Book = lookup.Title ?? string.Empty,
                Author = lookup.Authors ?? string.Empty,
                Timestamp = lookup.Timestamp ?? string.Empty,
            });
        }

        document.Words = words;
        return document;
    }

    /// <summary>
    /// <c>BriefType</c> → 类型文本的判定,供 **CSV 与 JSON 两个导出 writer 共用**。
    /// </summary>
    /// <remarks>
    /// 输出的是**与界面语言无关的枚举名**(<c>Highlight</c> / <c>Note</c> / …),而不是本地化文案 ——
    /// 下游脚本 / Anki 按字段值解析,值随界面语言变化会直接破坏它们。UI 侧的
    /// <c>TypeTextMap.Of</c> 输出本地化文案并附带分组用的 <c>TypeKind</c>,**刻意不共用本方法**。
    /// </remarks>
    internal static string BriefTypeText(long? briefType) {
        return briefType is { } brief && Enum.IsDefined(typeof(BriefType), (int)brief)
            ? ((BriefType)brief).ToString()
            : string.Empty;
    }

    private static string FormatTimestamp(DateTimeOffset? exportedAt) {
        return (exportedAt ?? DateTimeOffset.Now).ToString(ExportSchema.TimestampFormat, CultureInfo.InvariantCulture);
    }
}
