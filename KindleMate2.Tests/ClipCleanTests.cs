using System.Text;
using Microsoft.Data.Sqlite;
using Xunit;
using KindleMate2.Application.Services.KM2DB;
using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Domain.Interfaces.KM2DB;
using KindleMate2.Infrastructure.Helpers;
using KindleMate2.Infrastructure.Repositories.KM2DB;
using KindleMate2.Shared.Constants;

namespace KindleMate2.Tests;

/// <summary>
/// 「清洗」功能在服务层的用例:导入自动清洗、只读预览、批量落库、以及**重建不会把清洗结果回退**。
///
/// 三个容易搞错的点,这里都用用例钉住了:
/// <list type="number">
/// <item><c>original_clipping_lines.line4</c> 在导入时写的是**清洗后**的 content(第 398 行同一个变量),
/// 所以库里**不保留清洗前的原文** —— 回头只有备份 + 改动清单。别以为能从 line4 "反清洗"。</item>
/// <item>「重建数据库」**只读** line4、不回写,所以老版本存量数据里的噪音会一直躺在 line4 里;
/// 但它重建出来的 content 是干净的,于是**手工清洗的结果不会因为重建而回退**。</item>
/// <item>「清洗」与「清理数据库」是两件事:后者判重/删空条目/VACUUM,动**行数**;
/// 清洗只改每条首尾标点,**一条都不删**(整条皆标点的还特意跳过,免得清成空条目)。</item>
/// </list>
/// </summary>
public sealed class ClipCleanTests : IDisposable {
    private readonly string _dir;
    private readonly string _db;
    private readonly IClippingRepository _clippingRepo;
    private readonly ILookupRepository _lookupRepo;
    private readonly IOriginalClippingLineRepository _originalRepo;
    private readonly ISettingRepository _settingRepo;
    private readonly IVocabRepository _vocabRepo;
    private readonly Km2DatabaseService _km2;

    public ClipCleanTests() {
        _dir = Path.Combine(Path.GetTempPath(), "km2-clipclean-tests-" + Guid.NewGuid().ToString("N")[..10]);
        Directory.CreateDirectory(_dir);
        _db = Path.Combine(_dir, "KM2.dat");
        Assert.True(DatabaseHelper.CreateDatabase(_db, out var ex), "CreateDatabase failed: " + ex.Message);

        var cs = DatabaseHelper.GetConnectionString(_db);
        _clippingRepo = new ClippingRepository(cs);
        _lookupRepo = new LookupRepository(cs);
        _originalRepo = new OriginalClippingLineRepository(cs);
        _settingRepo = new SettingRepository(cs);
        _vocabRepo = new VocabRepository(cs);

        _km2 = new Km2DatabaseService(_clippingRepo, _lookupRepo, _originalRepo, _settingRepo, _vocabRepo);
    }

    public void Dispose() {
        try {
            SqliteConnection.ClearAllPools();
            Directory.Delete(_dir, true);
        } catch { /* best effort */ }
    }

    // ————————————————————————— 导入即清洗 —————————————————————————

    /// <summary>
    /// 导入路径自动清洗。同时钉住一个**事实**:line4 拿到的也是清洗后的文本,
    /// 所以"库里还留着原文可以反清洗"这个想法是错的。
    /// </summary>
    [Fact]
    public void Import_CleansNoise_AndWritesCleanedTextIntoBothTables() {
        ImportOneClipping("。他走过去。");

        var clipping = Assert.Single(_clippingRepo.GetAll());
        Assert.Equal("他走过去。", clipping.Content);

        var line = Assert.Single(_originalRepo.GetAll());
        Assert.Equal("他走过去。", line.Line4);
    }

    /// <summary>导入结果的"首尾被修剪掉的条数"应当如实上报。</summary>
    [Fact]
    public void Import_ReportsCleanedCount() {
        var file = WriteClippingsFile("。他走过去。");

        Assert.True(_km2.ImportKindleClippings(file, out var result));

        Assert.Equal("1", result[AppConstants.TrimmedCount]);
    }

    /// <summary>整条只有标点的标注:留着原文入库(清下去会变成空条目,更糟)。</summary>
    [Fact]
    public void Import_AllPunctuationOnlyClipping_KeepsOriginalContent() {
        ImportOneClipping("。");

        var clipping = Assert.Single(_clippingRepo.GetAll());
        Assert.Equal("。", clipping.Content);
    }

    /// <summary>本来就干净的标注不该被记成"清洗过"。</summary>
    [Fact]
    public void Import_CleanClipping_IsNotCountedAsCleaned() {
        var file = WriteClippingsFile("他走过去。");

        Assert.True(_km2.ImportKindleClippings(file, out var result));

        Assert.Equal("0", result[AppConstants.TrimmedCount]);
        Assert.Equal("他走过去。", Assert.Single(_clippingRepo.GetAll()).Content);
    }

    // ————————————————————————— 只读预览 —————————————————————————

    /// <summary>预览必须在**不写任何东西**的前提下报出"会改哪些条、改成什么样"。</summary>
    [Fact]
    public void ScanClippingClean_IsReadOnly() {
        SeedClipping("k1", "。正文");
        SeedClipping("k2", "正文");

        Assert.True(_km2.ScanClippingClean(out var report));

        Assert.Equal(2, report.Scanned);
        Assert.Equal(1, report.ChangedCount);
        var change = Assert.Single(report.Changes);
        Assert.Equal("k1", change.Key);
        Assert.Equal("。正文", change.Before);
        Assert.Equal("正文", change.After);

        // 扫描本身不许动库
        Assert.Equal("。正文", ContentOf("k1"));
    }

    /// <summary>只读扫描也要如实报出"整条皆标点"的条数,否则用户看到的数字对不上。</summary>
    [Fact]
    public void ScanClippingClean_CountsAllPunctuationRows() {
        SeedClipping("k1", "。正文");
        SeedClipping("k2", "。");

        Assert.True(_km2.ScanClippingClean(out var report));

        Assert.Equal(2, report.Scanned);
        Assert.Equal(1, report.ChangedCount);
        Assert.Equal(1, report.AllPunctuationCount);
    }

    // ————————————————————————— 批量落库 —————————————————————————

    [Fact]
    public void CleanClippingTexts_WritesCleanedContent() {
        SeedClipping("k1", "。正文");
        SeedClipping("k2", "正文");

        Assert.True(_km2.CleanClippingTexts(out var report));

        Assert.Equal(1, report.ChangedCount);
        Assert.Equal("正文", ContentOf("k1"));
        Assert.Equal("正文", ContentOf("k2"));
    }

    /// <summary>
    /// <c>ClippingRepository.Update</c> 是**整行覆写**的,所以清洗遍里拿到的实体必须是完整读出来的;
    /// 一旦哪天改成"只构造出 Key + Content 就 Update",书名/作者会被静默清空 —— 这条用例就是拦这个的。
    /// </summary>
    [Fact]
    public void CleanClippingTexts_DoesNotBlankOtherFields() {
        SeedClipping("k1", "。正文", book: "书名", author: "作者");

        Assert.True(_km2.CleanClippingTexts(out _));

        var clipping = Assert.Single(_clippingRepo.GetAll());
        Assert.Equal("正文", clipping.Content);
        Assert.Equal("书名", clipping.BookName);
        Assert.Equal("作者", clipping.AuthorName);
    }

    /// <summary>洗完再扫,必须一条都不剩 —— 否则每次点都会"改"N 条,用户会以为没生效。</summary>
    [Fact]
    public void CleanClippingTexts_IsIdempotent() {
        SeedClipping("k1", "。正文");
        SeedClipping("k2", "正文（");

        Assert.True(_km2.CleanClippingTexts(out var first));
        Assert.Equal(2, first.ChangedCount);

        Assert.True(_km2.ScanClippingClean(out var second));
        Assert.Equal(0, second.ChangedCount);
        Assert.Empty(second.Changes);
    }

    /// <summary>没有需要清洗的时候是**正常结果**,不是失败;而且一条都不许删。</summary>
    [Fact]
    public void CleanClippingTexts_OnCleanData_ChangesNothingAndDeletesNoRows() {
        SeedClipping("k1", "正文一");
        SeedClipping("k2", "正文二");

        Assert.True(_km2.CleanClippingTexts(out var report));

        Assert.Equal(0, report.ChangedCount);
        Assert.Equal(2, report.Scanned);
        Assert.Equal(2, _clippingRepo.GetAll().Count);
    }

    /// <summary>
    /// 整条皆标点的条目**跳过不落库**,但行还在、内容原样 ——
    /// 清洗绝不能把一条标注变成空条目(那是"清理数据库"该管的事,不是这里)。
    /// </summary>
    [Fact]
    public void CleanClippingTexts_SkipsAllPunctuationRows() {
        SeedClipping("k1", "。正文");
        SeedClipping("k2", "。");

        Assert.True(_km2.CleanClippingTexts(out var report));

        Assert.Equal(1, report.ChangedCount);
        Assert.Equal(1, report.AllPunctuationCount);
        Assert.Equal(2, _clippingRepo.GetAll().Count);
        Assert.Equal("。", ContentOf("k2"));
    }

    /// <summary>预览与实际改动**必须同源**:看到的和改掉的不可能对不上。</summary>
    [Fact]
    public void ScanAndClean_ReportTheSameChanges() {
        SeedClipping("k1", "。甲");
        SeedClipping("k2", "乙");
        SeedClipping("k3", "丙（");
        SeedClipping("k4", "。");
        SeedClipping("k5", "丁");

        Assert.True(_km2.ScanClippingClean(out var scan));
        Assert.True(_km2.CleanClippingTexts(out var clean));

        Assert.Equal(scan.ChangedCount, clean.ChangedCount);
        Assert.Equal(scan.AllPunctuationCount, clean.AllPunctuationCount);
        Assert.Equal(
            scan.Changes.OrderBy(c => c.Key).Select(c => $"{c.Key}:{c.Before}->{c.After}"),
            clean.Changes.OrderBy(c => c.Key).Select(c => $"{c.Key}:{c.Before}->{c.After}"));
    }

    // ————————————————————————— 重建不许把清洗结果回退 —————————————————————————

    /// <summary>
    /// 老版本(或清洗功能上线前)导入的存量数据:content 与 line4 里都还带着噪音。
    /// 「重建数据库」拿 line4 再走一遍解析路径,出来的 content 应当是干净的。
    /// </summary>
    [Fact]
    public void Rebuild_CleansLegacyNoiseFromOriginalLine() {
        var key = ImportOneClipping("正文");
        SimulateLegacyNoisyRow(key, "。正文");

        Assert.True(_km2.RebuildDatabase(out _));

        Assert.Equal("正文", ContentOf(key));
        // 重建**只读** line4、不回写,所以噪音还躺在那里 —— 无害,但别以为它被修掉了。
        Assert.Equal("。正文", _originalRepo.GetByKey(key)!.Line4);
    }

    /// <summary>
    /// **本功能最关键的一条回归**:手工清洗只改 clippings.content、不动 line4;
    /// 而重建又从 line4 重新解析。若哪天清洗被从 <c>HandleClippings</c> 里挪走,
    /// 用户手工清洗过的库会在下一次"重建数据库"时**静默退回**带噪音的样子。
    /// </summary>
    [Fact]
    public void ManualClean_IsNotRevertedByRebuild() {
        var key = ImportOneClipping("正文");
        SimulateLegacyNoisyRow(key, "。正文");

        Assert.True(_km2.CleanClippingTexts(out var report));
        Assert.Equal(1, report.ChangedCount);
        Assert.Equal("正文", ContentOf(key));

        Assert.True(_km2.RebuildDatabase(out _));

        Assert.Equal("正文", ContentOf(key));
    }

    // ————————————————————————— helpers —————————————————————————

    /// <summary>把一条已导入的标注改造成"清洗功能上线前"的样子:两张表里都带噪音。</summary>
    private void SimulateLegacyNoisyRow(string key, string noisyContent) {
        var clipping = Assert.Single(_clippingRepo.GetAll());
        clipping.Content = noisyContent;
        Assert.True(_clippingRepo.Update(clipping));

        var line = _originalRepo.GetByKey(key) ?? throw new InvalidOperationException("原始行不存在");
        line.Line4 = noisyContent;
        Assert.True(_originalRepo.Update(line));
    }

    private string ContentOf(string key) =>
        _clippingRepo.GetAll().Single(c => c.Key == key).Content ?? string.Empty;

    /// <summary>走**真实导入路径**造一条标注并返回它的 key(两张表靠同一个 key 关联)。</summary>
    private string ImportOneClipping(string content, string book = "Book", string author = "Author") {
        var file = WriteClippingsFile(content, book, author);
        Assert.True(_km2.ImportKindleClippings(file, out _), "导入夹具失败");
        return Assert.Single(_clippingRepo.GetAll()).Key;
    }

    private string WriteClippingsFile(string content, string book = "Book", string author = "Author") {
        var file = Path.Combine(_dir, $"My Clippings {Guid.NewGuid().ToString("N")[..6]}.txt");
        var text = new StringBuilder()
            .AppendLine($"{book} ({author})")
            .AppendLine("- 您在第 1 页（位置 #1-1）的标注 | 添加于 2020年1月1日星期三 上午 10:00:00")
            .AppendLine()
            .AppendLine(content)
            .AppendLine("==========")
            .ToString();
        // 与 Kindle 导出的文件一致:UTF-8 **无 BOM**
        File.WriteAllText(file, text, new UTF8Encoding(false));
        return file;
    }

    /// <summary>直接往 clippings 写一条(不涉及 import/重建的用例用它更省事)。</summary>
    private void SeedClipping(string key, string content, string book = "Book", string author = "Author") {
        _clippingRepo.Add(new Clipping {
            Key = key,
            Content = content,
            BookName = book,
            AuthorName = author,
            ClippingTypeLocation = "标注 位置 #1-1",
        });
    }
}
