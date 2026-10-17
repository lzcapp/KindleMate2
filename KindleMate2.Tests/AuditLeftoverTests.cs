using Xunit;
using KindleMate2.Application.Services.KM2DB;
using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Infrastructure.Helpers;
using KindleMate2.Infrastructure.Repositories.KM2DB;

namespace KindleMate2.Tests;

/// <summary>
/// 「审计遗留」低风险项的回归用例(PR chore/audit-leftovers):
/// <list type="number">
///   <item>
///     <b>跨书撞 key —— 已知限制,非期望行为</b>。<c>clippings.key = 标注日期 + "|" + 类型+位置</c>,
///     **不含书名**,而 <c>clippings.key</c> 是 <c>PRIMARY KEY NOT NULL UNIQUE</c>。于是
///     「两本不同的书 + 同一秒 + 同一位置 + 同一类型」会算出同一个 key,后到的那条在
///     <c>HandleClippings</c> 里被判为重复、**静默跳过**。Kindle 的 Location 是每本书各自编号,
///     跨书位置相同很常见。本用例把当前行为**钉死**:若将来审计 P2-14 被修(key 纳入书名),
///     本用例会失败 —— 届时应改为断言「导入 2 条」。
///   </item>
///   <item>
///     <b><c>UpdateFrequency</c> 对带空白 <c>word_key</c> 的词频</b>:统计侧 <c>GroupBy(Trim())</c>,
///     查表侧也必须同口径;并且回写要能定位到库里带空白的原始行(按主键 <c>id</c>,而非已 Trim 的
///     <c>word_key</c>)—— 否则 frequency 恒为 0。
///   </item>
///   <item>
///     <b>metadata 清洗</b>:只剥行首的项目符号 <c>"- "</c>,不做全局 <c>Replace("- ","")</c>;
///     且 <c>|</c> 前没有空格时不啃掉类型词/位置串的末字。
///   </item>
/// </list>
/// </summary>
public sealed class AuditLeftoverTests : IDisposable {
    private readonly string _dir;

    public AuditLeftoverTests() {
        _dir = Path.Combine(Path.GetTempPath(), "km2-audit-leftovers-" + Guid.NewGuid().ToString("N")[..10]);
        Directory.CreateDirectory(_dir);
    }

    public void Dispose() {
        try { Directory.Delete(_dir, true); } catch { /* best effort */ }
    }

    // ————————————————————————— B:跨书撞 key —————————————————————————

    /// <summary>
    /// 两本不同的书、**同一秒 + 同一位置 + 同一类型**:因为判重键不含书名,第二条被静默跳过。
    ///
    /// ⚠ 这**不是期望行为**,而是当前实现的**已知限制**(审计 P2-14)的忠实记录。此断言一旦变红,
    /// 说明判重键已经纳入书名 —— 那时请把它改成 <c>Assert.Equal(2, ...)</c>。
    /// </summary>
    [Fact]
    public void TwoDifferentBooks_SameSecondLocationAndType_CollideOnKey_SecondIsSilentlySkipped() {
        var (service, repo) = NewService("crossbook.db");
        var path = WriteClippings("crossbook.txt", """
Book R (Author R)
- Your Highlight on page 5 | Location 100-101 | Added on Thursday, January 1, 2026, 12:00:00 AM

first highlight
==========
Book S (Author S)
- Your Highlight on page 5 | Location 100-101 | Added on Thursday, January 1, 2026, 12:00:00 AM

second highlight
==========
""");

        Assert.True(service.ImportKindleClippings(path, out _));

        // 只进来 1 条(先到的那本)。第二条与第一条算出同一个 key,被判重复跳过。
        var stored = Assert.Single(repo.GetAll());
        Assert.Equal("Book R", stored.BookName);
        Assert.Equal("first highlight", stored.Content);
        // 两条不同书、不同正文,却共用一个 key —— 正是"key 不含书名"的直接体现。
        Assert.Equal("2026-01-01 00:00:00|Your Highlight on page 5 | Location 100-101", stored.Key);
    }

    /// <summary>
    /// 对照组:同样两本书,但**位置不同**(Kindle 的位置是每本书各自编号,这里刻意错开)——
    /// key 因此不同,两条都进库。这条用来证明上一条的"只进 1 条"确实源于 key 撞车,
    /// 而不是因为别的解析原因被丢弃。
    /// </summary>
    [Fact]
    public void TwoDifferentBooks_DifferentLocations_BothImported() {
        var (service, repo) = NewService("crossbook-ok.db");
        var path = WriteClippings("crossbook-ok.txt", """
Book R (Author R)
- Your Highlight on page 5 | Location 100-101 | Added on Thursday, January 1, 2026, 12:00:00 AM

first highlight
==========
Book S (Author S)
- Your Highlight on page 5 | Location 200-201 | Added on Thursday, January 1, 2026, 12:00:00 AM

second highlight
==========
""");

        Assert.True(service.ImportKindleClippings(path, out _));

        Assert.Equal(2, repo.GetAll().Count);
    }

    // ————————————————————————— C:UpdateFrequency 的 Trim 口径 —————————————————————————

    /// <summary>
    /// <c>word_key</c> 带前后空白时,词频必须照常统计出来(此处两条 usage 命中同一词键 → 2)。
    ///
    /// 注意:读取侧 <c>GetSafeString</c> 会把 <c>word_key</c> <c>Trim</c> 掉,所以
    /// <c>UpdateFrequency</c> 的"分组 vs 查表"两侧其实早已同口径 —— 真正会让这类行恒为 0 的是
    /// **回写**:旧实现用读回来的(已 Trim 的)<c>word_key</c> 做 <c>WHERE word_key = ?</c>,
    /// 匹配不到库里带空白的原始行。故回写改为按主键 <c>id</c> 定位
    /// (<c>VocabRepository.UpdateFrequencyByWordKey</c>)。
    /// </summary>
    [Fact]
    public void UpdateFrequency_WordKeyWithSurroundingWhitespace_IsStillCounted() {
        var path = Path.Combine(_dir, "freq.db");
        Assert.True(DatabaseHelper.CreateDatabase(path, out var ex), "CreateDatabase failed: " + ex.Message);
        var connectionString = DatabaseHelper.GetConnectionString(path);
        var vocabRepo = new VocabRepository(connectionString);
        var lookupRepo = new LookupRepository(connectionString);
        var service = new Km2DatabaseService(
            new ClippingRepository(connectionString),
            lookupRepo,
            new OriginalClippingLineRepository(connectionString),
            new SettingRepository(connectionString),
            vocabRepo);

        // 词键带前后空白 —— 正是修复前会漏算的形态。
        Assert.True(vocabRepo.Add(new Vocab { Id = "padded", WordKey = "  en:apple  ", Word = "apple" }));
        Assert.True(lookupRepo.Add(new Lookup { WordKey = "en:apple", Usage = "u1", Timestamp = "2026-01-01 10:00:00" }));
        Assert.True(lookupRepo.Add(new Lookup { WordKey = "en:apple", Usage = "u2", Timestamp = "2026-01-02 10:00:00" }));

        // 前置条件:两行 usage 确实进了库(否则下面的断言可能因"没数据"而假通过/假失败)。
        Assert.Equal(2, lookupRepo.GetAll().Count);
        Assert.Single(vocabRepo.GetAll());

        Assert.True(service.UpdateFrequency());

        var stored = Assert.Single(vocabRepo.GetAll());
        Assert.Equal(2, stored.Frequency ?? 0);
    }

    // ————————————————————————— D:metadata 清洗 —————————————————————————

    /// <summary>
    /// metadata 里出现行内的 <c>"- "</c>(如位置区间被写成 <c>100 - 101</c>)时,旧实现用
    /// 全局 <c>Replace("- ","")</c> 会把它一并删掉、篡改类型/位置串。修复后只剥行首项目符号,
    /// 行内的 <c>"- "</c> 原样保留。
    /// </summary>
    [Fact]
    public void Import_MetadataWithInnerHyphenSpace_OnlyStripsLeadingBullet() {
        var (service, repo) = NewService("inner-dash.db");
        var path = WriteClippings("inner-dash.txt", """
Book R (Author R)
- Your Highlight on page 5 | Location 100 - 101 | Added on Thursday, January 1, 2026, 12:00:00 AM

a highlight
==========
""");

        Assert.True(service.ImportKindleClippings(path, out _));

        var stored = Assert.Single(repo.GetAll());
        // 行首 "- " 被剥掉,但行内的 " - " 必须还在(旧实现会得到 "…Location 100101")。
        Assert.Equal("Your Highlight on page 5 | Location 100 - 101", stored.ClippingTypeLocation);
    }

    /// <summary>
    /// <c>|</c> 前**没有空格**时,旧实现按 <c>indexOf - 1</c> 取串会啃掉类型词/位置的末字
    /// (如 <c>…100-101</c> 变成 <c>…100-10</c>)。修复后保留完整末字。
    /// </summary>
    [Fact]
    public void Import_MetadataWithoutSpaceBeforePipe_DoesNotEatLastCharacter() {
        var (service, repo) = NewService("no-space-pipe.db");
        var path = WriteClippings("no-space-pipe.txt", """
Book R (Author R)
- Your Highlight on page 5|Location 100-101|Added on Thursday, January 1, 2026, 12:00:00 AM

a highlight
==========
""");

        Assert.True(service.ImportKindleClippings(path, out _));

        var stored = Assert.Single(repo.GetAll());
        Assert.Equal("Your Highlight on page 5|Location 100-101", stored.ClippingTypeLocation);
    }

    // ————————————————————————— helpers —————————————————————————

    private (Km2DatabaseService Service, ClippingRepository Repo) NewService(string name) {
        var path = Path.Combine(_dir, name);
        Assert.True(DatabaseHelper.CreateDatabase(path, out var ex), "CreateDatabase failed: " + ex.Message);
        var connectionString = DatabaseHelper.GetConnectionString(path);
        var repo = new ClippingRepository(connectionString);
        var service = new Km2DatabaseService(repo,
            new LookupRepository(connectionString),
            new OriginalClippingLineRepository(connectionString),
            new SettingRepository(connectionString),
            new VocabRepository(connectionString));
        return (service, repo);
    }

    private string WriteClippings(string name, string body) {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, body);
        return path;
    }
}
