using Xunit;
using KindleMate2.Application.Services.KM2DB;
using KindleMate2.Infrastructure.Helpers;
using KindleMate2.Infrastructure.Repositories.KM2DB;
using KindleMate2.Shared.Constants;

namespace KindleMate2.Tests;

/// <summary>
/// <c>My Clippings.txt</c> 解析健壮性回归(2026-10-14 审计修复):
/// <list type="number">
/// <item><b>分隔符识别不依赖上下文行</b> —— 正文可多行(<c>formats.md</c> §1.2/§1.5),
///   修复前判定要求分隔符「其上第 2 行是空行」,多行正文时该分隔符被漏掉 ⇒ 相邻两条被并成一条、
///   ParsedCount 退化、下一条的书名/正文混进上一条。</item>
/// <item><b>书名/作者只在两侧带空格的 <c> - </c> 处拆分</b> —— 修复前用裸 <c>-</c> 的
///   LastIndexOf,会把书名内的连字符(<c>Spider-Man</c>)误当作者分隔,写坏书名/作者。</item>
/// </list>
/// </summary>
public sealed class ClippingParseRobustnessTests : IDisposable {
    private readonly string _dir;

    public ClippingParseRobustnessTests() {
        _dir = Path.Combine(Path.GetTempPath(), "km2-clipping-parse-" + Guid.NewGuid().ToString("N")[..10]);
        Directory.CreateDirectory(_dir);
    }

    public void Dispose() {
        try { Directory.Delete(_dir, true); } catch { /* best effort */ }
    }

    private string NewDb(string name) {
        var path = Path.Combine(_dir, name);
        Assert.True(DatabaseHelper.CreateDatabase(path, out var ex), "CreateDatabase failed: " + ex.Message);
        return path;
    }

    private Km2DatabaseService NewService(string dbPath) {
        var connectionString = DatabaseHelper.GetConnectionString(dbPath);
        return new Km2DatabaseService(
            new ClippingRepository(connectionString),
            new LookupRepository(connectionString),
            new OriginalClippingLineRepository(connectionString),
            new SettingRepository(connectionString),
            new VocabRepository(connectionString));
    }

    /// <summary>
    /// 多行正文的分隔符必须被识别:第 1 条正文占两行,其后紧跟 <c>==========</c>,再接下一条。
    /// 修复前该分隔符因「其上第 2 行(content line 1)不是空行」被漏掉 ⇒ 只解析出 1 条,
    /// 且第 2 条的书名/正文被拼进第 1 条的正文里。修复后必须解析出 2 条。
    /// </summary>
    [Fact]
    public void ImportKindleClippings_MultiLineContent_KeepsEntriesSeparate() {
        var targetDb = NewDb("multiline.db");
        var path = Path.Combine(_dir, "My Clippings.txt");
        File.WriteAllText(path, """
Book A (Author A)
- Highlight | Page 5 | Added on Tuesday, May 20, 2025, 8:00:00 AM

content line 1
content line 2
==========
Book B (Author B)
- Highlight | Page 6 | Added on Tuesday, May 20, 2025, 8:00:00 AM

content B
==========
""");

        var clipRepo = new ClippingRepository(DatabaseHelper.GetConnectionString(targetDb));
        var svc = NewService(targetDb);

        Assert.True(svc.ImportKindleClippings(path, out var result));

        // 修复前 ParsedCount 退化成 "1"(多行正文条目的分隔符未被识别)
        Assert.Equal("2", result[AppConstants.ParsedCount]);
        Assert.Equal("2", result[AppConstants.InsertedCount]);

        var clippings = clipRepo.GetAll();
        Assert.Equal(2, clippings.Count);

        // 第 1 条:正文含两行,且不能混入分隔符或第 2 条的书名
        var first = clippings.Single(c => c.BookName == "Book A");
        Assert.Contains("content line 1", first.Content);
        Assert.Contains("content line 2", first.Content);
        Assert.DoesNotContain("Book B", first.Content);
        Assert.DoesNotContain("==========", first.Content);

        // 第 2 条:header 正确、正文干净
        var second = clippings.Single(c => c.BookName == "Book B");
        Assert.Contains("content B", second.Content);
        Assert.DoesNotContain("content line 1", second.Content);
    }

    /// <summary>
    /// 分隔符判定**刻意容忍非 10 个 <c>=</c>**:<c>formats.md</c> 多处标注「未实测」,
    /// 无法排除设备写出不同数量 <c>=</c> 的可能(原实现 <c>StartsWith("===")</c> 也是宽松的)。
    /// 这里用 9 个 <c>=</c> 的分隔符钉住该宽容性是**刻意约定、不是巧合** —— 两条须被正确拆开。
    /// </summary>
    [Fact]
    public void ImportKindleClippings_NonStandardDelimiterLength_StillSplitsEntries() {
        var targetDb = NewDb("short-delimiter.db");
        var path = Path.Combine(_dir, "My Clippings.txt");
        File.WriteAllText(path, """
Book C (Author C)
- Highlight | Page 7 | Added on Tuesday, May 20, 2025, 8:00:00 AM

content C
=========
Book D (Author D)
- Highlight | Page 8 | Added on Tuesday, May 20, 2025, 8:00:00 AM

content D
=========
""");

        var clipRepo = new ClippingRepository(DatabaseHelper.GetConnectionString(targetDb));
        var svc = NewService(targetDb);

        Assert.True(svc.ImportKindleClippings(path, out var result));

        Assert.Equal("2", result[AppConstants.ParsedCount]);
        Assert.Equal("2", result[AppConstants.InsertedCount]);

        var clippings = clipRepo.GetAll();
        Assert.Equal(2, clippings.Count);
        Assert.Contains("content C", clippings.Single(c => c.BookName == "Book C").Content);
        Assert.Contains("content D", clippings.Single(c => c.BookName == "Book D").Content);
    }

    /// <summary>
    /// 只有两侧带空格的 <c> - </c> 才拆分为「书名 / 作者」;书名内的裸连字符(Spider-Man)
    /// 不得被当成分隔符。修复前 <c>Spider-Man</c> 会被拆成 Title=Spider、Author=Man。
    /// </summary>
    [Theory]
    [InlineData("Spider-Man", "Spider-Man", "")]
    [InlineData("Zoo-ology", "Zoo-ology", "")]
    [InlineData("Some Book - Some Author", "Some Book", "Some Author")]
    [InlineData("Spider-Man - Stan Lee", "Spider-Man", "Stan Lee")]
    public void ParseTitleAndAuthor_OnlySplitsOnSpacedHyphen(string input, string expectedTitle, string expectedAuthor) {
        var header = MyClippingsHelper.ParseTitleAndAuthor(input);

        Assert.Equal(expectedTitle, header.Title);
        Assert.Equal(expectedAuthor, header.Author);
    }
}
