using System.Text.Json;
using Xunit;
using KindleMate2.Application.Services;
using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Domain.Models.Export;
using KindleMate2.Shared.Constants;

namespace KindleMate2.Tests;

/// <summary>
/// JSON 导出(PR1)的纯逻辑:实体 → 中间模型 → 落盘。
/// 全部走 <c>ExportManager</c> 的 <c>internal static</c> writer 与
/// <see cref="ExportModelBuilder"/>,是静态纯函数(不碰库、不联网、不依赖设备),
/// 风格与 <see cref="CsvExportTests"/> 一致。
/// </summary>
/// <remarks>
/// 断言一律**反序列化回读**(而不是对文本做 Contains):键名、键的有无、转义是否还原,
/// 只有真的解析一遍才验证得到 —— 字符串匹配在转义写法变了的时候照样会绿。
/// </remarks>
public sealed class JsonExportTests : IDisposable {
    private readonly string _dir;

    public JsonExportTests() {
        _dir = Path.Combine(Path.GetTempPath(), "km2-json-" + Guid.NewGuid().ToString("N")[..10]);
        Directory.CreateDirectory(_dir);
    }

    public void Dispose() {
        try { Directory.Delete(_dir, true); } catch { /* best effort */ }
    }

    [Fact]
    public void WriteClippingsJson_EmptyInput_ProducesDocumentWithEmptyBooks() {
        var path = Path.Combine(_dir, "empty-clippings.json");

        ExportManager.WriteClippingsJson(new List<Clipping>(), path);

        var document = ReadClippings(path);
        Assert.Equal("1.0", document.Version);
        Assert.Equal("KindleMate2", document.Generator);
        // 时间戳要能被下游解析(带时区偏移),而不是一段看着像日期的字符串。
        Assert.True(DateTimeOffset.TryParse(document.ExportedAt, out _),
            $"exported_at 无法解析:{document.ExportedAt}");
        Assert.NotNull(document.Books);
        Assert.Empty(document.Books);
    }

    [Fact]
    public void WriteLookupsJson_EmptyInput_ProducesDocumentWithEmptyWords() {
        var path = Path.Combine(_dir, "empty-vocabs.json");

        ExportManager.WriteLookupsJson(new List<Lookup>(), path);

        var document = ReadVocabs(path);
        Assert.Equal("1.0", document.Version);
        Assert.Equal("KindleMate2", document.Generator);
        Assert.NotNull(document.Words);
        Assert.Empty(document.Words);
    }

    [Fact]
    public void BuildClippingsDocument_WritesTimestampInSchemaFormat() {
        // 显式给定时刻:格式必须与 schema 样例逐字一致(带偏移、不带毫秒)。
        var at = new DateTimeOffset(2026, 10, 3, 21, 34, 0, TimeSpan.FromHours(8));

        var document = ExportModelBuilder.BuildClippingsDocument(new List<Clipping>(), at);

        Assert.Equal("2026-10-03T21:34:00+08:00", document.ExportedAt);
    }

    [Fact]
    public void WriteClippingsJson_GroupsByBook_AndSkipsBlankContent() {
        var path = Path.Combine(_dir, "grouped.json");
        var clippings = new List<Clipping> {
            new() {
                Key = "k1", Content = "第一句", BookName = " 三体 ", AuthorName = "刘慈欣",
                BriefType = (long)BriefType.Highlight,
                ClippingTypeLocation = "Location 1", ClippingDate = "2026-01-01 10:00:00",
            },
            // 书名带前后空格:Trim 后应与上一本归为同一组,否则同一本书会裂成两组。
            new() { Key = "k2", Content = "第二句", BookName = "三体" },
            new() { Key = "k3", Content = "   " },   // 纯空白正文 → 跳过(与 CSV 同口径)
            new() { Key = "k4", Content = "没有书名的标注" },   // BookName 为 null → Unknown Book
        };

        ExportManager.WriteClippingsJson(clippings, path);

        var document = ReadClippings(path);
        Assert.Equal(2, document.Books.Count);   // 三体 + Unknown Book(空白那条已跳过)

        var book = document.Books[0];
        Assert.Equal("三体", book.Title);
        Assert.Equal("刘慈欣", book.Author);
        Assert.Equal(2, book.Entries.Count);
        Assert.Equal("Highlight", book.Entries[0].Type);
        // 可空字段退化成空串,而不是 null —— 下游只需处理一种"没有值"。
        Assert.Equal(string.Empty, book.Entries[1].Location);
        Assert.Equal(string.Empty, book.Entries[1].Date);

        Assert.Equal("Unknown Book", document.Books[1].Title);
        Assert.Single(document.Books[1].Entries);
    }

    [Fact]
    public void WriteClippingsJson_OmitsPageKeyWhenPageNumberIsNotPositive() {
        var path = Path.Combine(_dir, "pages.json");
        var clippings = new List<Clipping> {
            new() { Key = "k1", Content = "有页码", BookName = "B", PageNumber = 12 },
            new() { Key = "k2", Content = "库里无页码记 0", BookName = "B", PageNumber = 0 },
            new() { Key = "k3", Content = "负数页码无含义", BookName = "B", PageNumber = -3 },
        };

        ExportManager.WriteClippingsJson(clippings, path);

        // 键的**有无**只能看原始 JSON,反序列化成 DTO 后 null 与"键不存在"长得一样。
        using var root = JsonDocument.Parse(File.ReadAllBytes(path));
        var entries = root.RootElement.GetProperty("books")[0].GetProperty("entries");

        Assert.Equal(12, entries[0].GetProperty("page").GetInt32());
        Assert.False(entries[1].TryGetProperty("page", out _), "无页码(0)时 page 键应整个省略");
        Assert.False(entries[2].TryGetProperty("page", out _), "负数页码同样视为无页码");
    }

    [Theory]
    [InlineData(0L, "Highlight")]
    [InlineData(1L, "Note")]
    [InlineData(3L, "Cut")]
    [InlineData(-2L, "Unknown")]   // BriefType.Unknown 是**已定义**的枚举值,照常输出
    [InlineData(99L, "")]          // 未定义 → 空串(不回退成数字,免得下游当合法类型分支)
    [InlineData(null, "")]         // 无值 → 空串
    public void WriteClippingsJson_MapsBriefTypeLikeCsv(long? briefType, string expected) {
        var path = Path.Combine(_dir, "type-" + (briefType?.ToString() ?? "null") + ".json");
        var clippings = new List<Clipping> {
            new() { Key = "k", Content = "c", BookName = "B", BriefType = briefType },
        };

        ExportManager.WriteClippingsJson(clippings, path);

        var document = ReadClippings(path);
        Assert.Equal(expected, document.Books[0].Entries[0].Type);
    }

    [Fact]
    public void WriteLookupsJson_RestoresNewLinePlaceholderAndSkipsEmptyWord() {
        var path = Path.Combine(_dir, "vocabs.json");
        var lookups = new List<Lookup> {
            new() {
                WordKey = "en:apple", Stem = "appl",
                Usage = "an apple a day" + AppConstants.SpaceForNewLine + "keeps the doctor away",
                Title = "Book", Authors = "A", Timestamp = "2026-01-01 10:00:00",
            },
            new() { WordKey = "en:", Usage = "取不到词" },   // Word 为空 → 跳过
            new() { WordKey = null, Usage = "连 key 都没有" },   // Word 为空 → 跳过
        };

        ExportManager.WriteLookupsJson(lookups, path);

        var document = ReadVocabs(path);
        var word = Assert.Single(document.Words);
        Assert.Equal("apple", word.Word);
        Assert.Equal("appl", word.Stem);
        Assert.Equal("en", word.Language);
        // 换行占位符必须还原成真正的换行,否则下游拿到的句子里夹着一串全角空格。
        Assert.Equal("an apple a day" + Environment.NewLine + "keeps the doctor away", word.Usage);
        Assert.Equal("Book", word.Book);
        Assert.Equal("A", word.Author);
        Assert.Equal("2026-01-01 10:00:00", word.Timestamp);
    }

    [Theory]
    [InlineData("en:word", "en")]
    [InlineData("word", "")]     // 无语言前缀
    public void WriteLookupsJson_ReadsLanguagePrefixFromWordKey(string wordKey, string expected) {
        var path = Path.Combine(_dir, "lang-" + wordKey.Replace(':', '_') + ".json");

        ExportManager.WriteLookupsJson(new List<Lookup> { new() { WordKey = wordKey, Usage = "u" } }, path);

        var document = ReadVocabs(path);
        Assert.Equal(expected, Assert.Single(document.Words).Language);
    }

    [Fact]
    public void WriteClippingsJson_RoundTripsSpecialCharacters() {
        var path = Path.Combine(_dir, "nasty.json");
        // 引号 / 反斜杠 / 换行 / 制表 / HTML 敏感字符 / 中文 / emoji 各来一份。
        var content = "引号 \" 反斜杠 \\ 换行 \n 制表 \t 尖括号 <&> 中文 emoji 😀";
        var clippings = new List<Clipping> {
            new() { Key = "k", Content = content, BookName = "书名 \"带引号\"", AuthorName = "作者 \\ 反斜杠" },
        };

        ExportManager.WriteClippingsJson(clippings, path);

        var document = ReadClippings(path);
        var book = Assert.Single(document.Books);
        Assert.Equal("书名 \"带引号\"", book.Title);
        Assert.Equal("作者 \\ 反斜杠", book.Author);
        // 转义正确 = 解析回来与原值逐字相同(而不是多了少了反斜杠)。
        Assert.Equal(content, book.Entries[0].Content);

        // 刻意用宽松编码器:中文按字面写,不写成 \uXXXX —— 否则整个文件没法读、也没法 diff。
        Assert.Contains("中文", File.ReadAllText(path), StringComparison.Ordinal);
    }

    [Fact]
    public void WriteJson_StartsWithoutByteOrderMark() {
        var clippingsPath = Path.Combine(_dir, "bom-clippings.json");
        var vocabsPath = Path.Combine(_dir, "bom-vocabs.json");

        ExportManager.WriteClippingsJson(
            new List<Clipping> { new() { Key = "k", Content = "内容" } }, clippingsPath);
        ExportManager.WriteLookupsJson(new List<Lookup>(), vocabsPath);

        foreach (var path in new[] { clippingsPath, vocabsPath }) {
            // 读字节而不是文本:File.ReadAllText / StreamReader 会自动吃掉 BOM,
            // 那样"有没有 BOM"这条断言就永远绿了。UTF-8 BOM = EF BB BF。
            var bytes = File.ReadAllBytes(path);
            Assert.True(bytes.Length > 0);
            Assert.Equal((byte)'{', bytes[0]);
            // JsonDocument.Parse 遇到 BOM 会直接抛,能解析过本身就是"无 BOM"的旁证。
            using var parsed = JsonDocument.Parse(bytes);
            Assert.Equal(JsonValueKind.Object, parsed.RootElement.ValueKind);
        }
    }

    private static ExportClippingsDocument ReadClippings(string path) {
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<ExportClippingsDocument>(stream)!;
    }

    private static ExportVocabsDocument ReadVocabs(string path) {
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<ExportVocabsDocument>(stream)!;
    }
}
