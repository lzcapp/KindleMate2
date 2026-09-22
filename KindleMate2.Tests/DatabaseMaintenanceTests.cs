using System.Globalization;
using Microsoft.Data.Sqlite;
using Xunit;
using KindleMate2.Application.Models;
using KindleMate2.Application.Services.KM2DB;
using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Domain.Interfaces.KM2DB;
using KindleMate2.Infrastructure.Helpers;
using KindleMate2.Infrastructure.Repositories.KM2DB;
using KindleMate2.Shared.Constants;

namespace KindleMate2.Tests;

/// <summary>
/// 「维护数据库」的只读预演(<c>ScanDatabaseMaintenance</c>)。
///
/// 这个功能把两件事合成一个入口:清洗(改首尾标点,**不删行**)→ 清理(删空条目 + 判重 + VACUUM)。
/// 合并本身不难,难的是**预演必须说准** —— 因为清洗会把内容**归一化**:
/// 两条原本"只差一个首部标点"的标注,清洗之后内容**完全相同**,于是双双成为清理眼里的重复项。
/// 而判重规则是"内容相同则**两行都删**"(不是留一条,见
/// <c>DataLayerFixTests.CleanDatabase_ExactAndNestedDuplicates_MatchesLegacySemantics</c>)。
///
/// 所以预演**必须先在内存里把清洗结果投影出来、再在投影上判重**。
/// 少了这一步,用户看到的是"删 1 条",执行时却删了 2 条 —— 所见与所做对不上,
/// 而这恰恰是唯一不可逆的部分。本组用例就是钉这件事。
/// </summary>
public sealed class DatabaseMaintenanceTests : IDisposable {
    private readonly string _dir;
    private readonly string _db;
    private readonly IClippingRepository _clippingRepo;
    private readonly Km2DatabaseService _km2;

    public DatabaseMaintenanceTests() {
        _dir = Path.Combine(Path.GetTempPath(), "km2-maintenance-tests-" + Guid.NewGuid().ToString("N")[..10]);
        Directory.CreateDirectory(_dir);
        _db = Path.Combine(_dir, "KM2.dat");
        Assert.True(DatabaseHelper.CreateDatabase(_db, out var ex), "CreateDatabase failed: " + ex.Message);

        var cs = DatabaseHelper.GetConnectionString(_db);
        _clippingRepo = new ClippingRepository(cs);
        _km2 = new Km2DatabaseService(
            _clippingRepo,
            new LookupRepository(cs),
            new OriginalClippingLineRepository(cs),
            new SettingRepository(cs),
            new VocabRepository(cs));
    }

    public void Dispose() {
        try {
            // 刻意**不**在这里清 SQLite 连接池:那是进程级 API,会和并行跑的其他测试类互撞
            // (实测 ~1/6 概率报 ObjectDisposedException)。残留目录由 TestTempCleanup 在
            // 进程退出时统一清扫 —— 那里已无测试在跑,清池不会伤到谁。
            Directory.Delete(_dir, true);
        } catch { /* best effort */ }
    }

    // ————————————————————— 核心:清洗制造出来的重复项 —————————————————————

    /// <summary>
    /// **本组最要紧的一条**。「。他走了。」与「他走了。」清洗前内容不同,清洗后**完全相同**,
    /// 于是两条都成了重复项 —— 预演必须报 2 条,而且一条都不许动库。
    /// </summary>
    [Fact]
    public void Scan_ReportsDuplicatesThatOnlyExistAfterCleaning() {
        SeedClipping("k1", "。他走了。");
        SeedClipping("k2", "他走了。");

        Assert.True(_km2.ScanDatabaseMaintenance(out var plan));

        // 清洗:只有 k1 的首部「。」会被去掉
        Assert.Equal(2, plan.Cleaning.Scanned);
        Assert.Equal(1, plan.Cleaning.ChangedCount);
        var change = Assert.Single(plan.Cleaning.Changes);
        Assert.Equal("k1", change.Key);
        Assert.Equal("。他走了。", change.Before);
        Assert.Equal("他走了。", change.After);

        // 清理:投影之后两条内容相同 ⇒ 两条都算重复
        Assert.Equal(2, plan.Cleanup.DuplicatedCount);
        Assert.Equal(0, plan.Cleanup.EmptyCount);
        Assert.Equal(
            new[] { "k1", "k2" },
            plan.Cleanup.Removals.Select(r => r.Key).OrderBy(k => k, StringComparer.Ordinal).ToArray());
        Assert.All(plan.Cleanup.Removals, r => Assert.Equal(DatabaseCleanRemovalReason.Duplicated, r.Reason));

        // 只读:一条都不许动
        Assert.Equal("。他走了。", ContentOf("k1"));
        Assert.Equal(2, _clippingRepo.GetAll().Count);
    }

    /// <summary>
    /// 反证 —— 不投影就会少报。同一份数据,直接对**清洗前**的行判重只得到 1 条
    /// (「他走了。」被更长的「。他走了。」包含),而真正执行时删的是 2 条。
    /// 与上一条成对:钉住"必须先在内存里投影"这件事本身。
    /// </summary>
    [Fact]
    public void CleanDatabase_WithoutProjectingCleaning_SeesOnlyOneDuplicate() {
        SeedClipping("k1", "。他走了。");
        SeedClipping("k2", "他走了。");

        Assert.True(_km2.CleanDatabase(string.Empty, out var result));

        Assert.Equal("1", result[AppConstants.DuplicatedCount]);
    }

    /// <summary>预演说的删除数 == 实际删除数。**一条不多、一条不少** —— 这是"先看后做"的全部意义。</summary>
    [Fact]
    public void Execute_DeletesExactlyWhatWasPreviewed() {
        SeedClipping("k1", "。他走了。");
        SeedClipping("k2", "他走了。");
        SeedClipping("k3", "无关内容");

        Assert.True(_km2.ScanDatabaseMaintenance(out var plan));
        var previewed = plan.Cleanup.Removals.Count;
        Assert.Equal(2, previewed);

        // 按合并后的固定顺序执行:先清洗,再清理
        Assert.True(_km2.CleanClippingTexts(out _));
        Assert.True(_km2.CleanDatabase(string.Empty, out var result));

        var deleted = Parse(result[AppConstants.DuplicatedCount]) + Parse(result[AppConstants.EmptyCount]);
        Assert.Equal(previewed, deleted);
        Assert.Equal("k3", Assert.Single(_clippingRepo.GetAll()).Key);
    }

    // ————————————————————————— 空条目 / 无事可做 —————————————————————————

    /// <summary>
    /// 空条目按「书名为空」识别。
    ///
    /// ⚠️ 这里**只**测书名为空这一半,因为另一半(内容为空)**当前走不到**:
    /// <c>ClippingRepository.GetAll()</c> 会把内容为空 / 全空白的行**直接跳过**
    /// (它同时是界面列表的数据源,空白行不该出现在列表里),而清理与预演都以 GetAll 为输入
    /// ⇒ 那种行既看不见、也删不掉。这是**既有行为**,不是本次合并引入的
    /// (详见 <c>ComputeDatabaseCleanPlan</c> 上的说明)。
    /// </summary>
    [Fact]
    public void Scan_ReportsRowsWithBlankBookNameAsEmpty() {
        SeedClipping("k1", "正常内容一");
        SeedClipping("k2", "另一条内容", book: "   ");   // 书名全空白 ⇒ 空条目

        Assert.True(_km2.ScanDatabaseMaintenance(out var plan));

        Assert.Equal(1, plan.Cleanup.EmptyCount);
        Assert.Equal(0, plan.Cleanup.DuplicatedCount);
        Assert.All(plan.Cleanup.Removals, r => Assert.Equal(DatabaseCleanRemovalReason.Empty, r.Reason));
    }

    /// <summary>本来就干净的数据:整体"无事可做"。视图靠 <c>HasWork</c> 区分"跑完了什么都没改"与失败。</summary>
    [Fact]
    public void Scan_ReportsNoWorkForCleanData() {
        SeedClipping("k1", "干净内容一");
        SeedClipping("k2", "干净内容二");

        Assert.True(_km2.ScanDatabaseMaintenance(out var plan));

        Assert.Equal(0, plan.Cleaning.ChangedCount);
        Assert.Empty(plan.Cleanup.Removals);
        Assert.False(plan.HasWork);
    }

    /// <summary>「整条皆标点」的条数要如实报出来,否则概要里的数字对不上。</summary>
    [Fact]
    public void Scan_CountsAllPunctuationRows() {
        SeedClipping("k1", "。正文");
        SeedClipping("k2", "。");

        Assert.True(_km2.ScanDatabaseMaintenance(out var plan));

        Assert.Equal(1, plan.Cleaning.ChangedCount);
        Assert.Equal(1, plan.Cleaning.AllPunctuationCount);
        // 整条皆标点的会被清洗**跳过**(清下去就成了空条目),所以它仍然原样留着
        Assert.Equal(0, plan.Cleanup.EmptyCount);
    }

    // ————————————————————————— 夹具 —————————————————————————

    private static int Parse(string value) => int.Parse(value, CultureInfo.InvariantCulture);

    private string ContentOf(string key) =>
        _clippingRepo.GetAll().Single(c => c.Key == key).Content ?? string.Empty;

    private void SeedClipping(string key, string content, string book = "Book") {
        _clippingRepo.Add(new Clipping {
            Key = key,
            Content = content,
            BookName = book,
            AuthorName = "Author",
            ClippingTypeLocation = "标注 位置 #1-1",
        });
    }
}
