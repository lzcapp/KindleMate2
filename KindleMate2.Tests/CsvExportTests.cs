using Xunit;
using KindleMate2.Application.Services;
using KindleMate2.Domain.Entities.KM2DB;

namespace KindleMate2.Tests;

/// <summary>
/// CSV 导出(#8)的纯逻辑:RFC4180 转义、word_key 语言前缀、以及两个落盘 writer。
/// 都是静态纯函数(不碰库),所以直接调用;这也让 #113 原本"没有测试"的缺口补上。
/// </summary>
public sealed class CsvExportTests : IDisposable {
    private readonly string _dir;

    public CsvExportTests() {
        _dir = Path.Combine(Path.GetTempPath(), "km2-csv-" + Guid.NewGuid().ToString("N")[..10]);
        Directory.CreateDirectory(_dir);
    }

    public void Dispose() {
        try { Directory.Delete(_dir, true); } catch { /* best effort */ }
    }

    [Theory]
    [InlineData("plain", "plain")]
    [InlineData("a,b", "\"a,b\"")]
    [InlineData("say \"hi\"", "\"say \"\"hi\"\"\"")]   // 引号加倍
    [InlineData("line1\nline2", "\"line1\nline2\"")]   // LF 触发加引号
    [InlineData("cr\rhere", "\"cr\rhere\"")]           // CR 触发加引号
    public void EscapeCsv_FollowsRfc4180(string input, string expected) {
        Assert.Equal(expected, ExportManager.EscapeCsv(input));
    }

    [Theory]
    [InlineData("en:word", "en")]
    [InlineData("zh:词", "zh")]
    [InlineData("word", "")]        // 无前缀
    [InlineData(":word", "")]       // 冒号在开头,不算语言前缀
    [InlineData("", "")]
    [InlineData(null, "")]
    public void LanguageOfWordKey_ReadsPrefix(string? wordKey, string expected) {
        Assert.Equal(expected, ExportManager.LanguageOfWordKey(wordKey));
    }

    [Fact]
    public void WriteClippingsCsv_EscapesFieldsAndSkipsEmptyContent() {
        var path = Path.Combine(_dir, "clippings.csv");
        var clippings = new List<Clipping> {
            new() {
                Key = "k1", Content = "hello, world", BookName = "Book \"X\"", AuthorName = "A",
                PageNumber = 12, ClippingTypeLocation = "Location 3", ClippingDate = "2026-01-01 10:00:00",
                BriefType = (long)BriefType.Highlight,
            },
            new() { Key = "k2", Content = "   " },   // 纯空白内容 → 跳过
        };

        ExportManager.WriteClippingsCsv(clippings, path);

        var lines = File.ReadAllLines(path);   // StreamReader 会自动去掉 UTF-8 BOM
        Assert.Equal("Content,Type,Book,Author,Page,Location,Date", lines[0]);
        Assert.Equal(2, lines.Length);         // 表头 + 1 行(空白那行被跳过)
        Assert.Contains("\"hello, world\"", lines[1]);
        Assert.Contains("\"Book \"\"X\"\"\"", lines[1]);
        Assert.Contains("Highlight", lines[1]);
        Assert.Contains("12", lines[1]);
    }

    [Fact]
    public void WriteLookupsCsv_WritesDefinitionLanguageAndStem() {
        var path = Path.Combine(_dir, "vocabs.csv");
        var lookups = new List<Lookup> {
            new() {
                WordKey = "en:apple", Stem = "appl", Usage = "an apple a day",
                Title = "Book", Authors = "A", Timestamp = "2026-01-01 10:00:00",
            },
            new() { WordKey = "en:", Usage = "" },   // Word 为空 → 跳过
        };
        var definitions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
            ["apple"] = "n. 苹果;n. 苹果树",
        };

        ExportManager.WriteLookupsCsv(lookups, path, definitions);

        var lines = File.ReadAllLines(path);   // StreamReader 会自动去掉 UTF-8 BOM,并忽略末尾换行
        Assert.Equal("Word,Stem,Definition,Usage,Book,Author,Language,Timestamp", lines[0]);
        Assert.Equal(2, lines.Length);   // 表头 + 1 行(空 Word 跳过)

        var data = lines[1];
        Assert.Contains("apple", data);
        Assert.Contains("appl", data);
        Assert.Contains("n. 苹果;n. 苹果树", data);
        Assert.Contains("an apple a day", data);
        Assert.Contains(",en,", data);   // Language 列
    }
}
