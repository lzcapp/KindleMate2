using Xunit;
using KindleMate2.Application.Services.KM2DB;
using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Domain.Interfaces.KM2DB;
using KindleMate2.Infrastructure.Helpers;
using KindleMate2.Infrastructure.Repositories.KM2DB;

namespace KindleMate2.Tests;

/// <summary>
/// 「删除查询记录后必须重算词频」的回归(本 PR 第 3 项)。
/// </summary>
/// <remarks>
/// <para>
/// 背景:<c>vocab.frequency</c> 是**落库值**,只有
/// <see cref="Km2DatabaseService.UpdateFrequency"/> 会回写;统计页的「查询次数」
/// (<c>StatisticsViewModel</c> 汇总 Frequency)、TopWords、频次分布都读它。删掉一条查询行后若不重算,
/// 统计会停在删除前的偏高值。
/// </para>
/// <para>
/// 触发重算的编排在 Avalonia 的 <c>MainWindowViewModel</c>(视图层,单测够不着 —— AGENTS.md 明确
/// "视图层行为单测够不着,靠 --smoke 探针覆盖"),故这里钉住**可测的服务层边界**:
/// <see cref="Km2DatabaseService.UpdateFrequency"/> 必须按**活跃**查询行重算 —— 软删除(tombstone)
/// 的行不得再计入。该断言同时守住一个真实风险:重算是读 <c>GetAll()</c>(活跃行)而非
/// <c>GetAllIncludingDeleted()</c>(含 tombstone),后者若被误用,删过的查询仍会被算进频次。
/// </para>
/// </remarks>
public sealed class LookupDeleteFrequencyTests : IDisposable {
    private readonly string _dir;
    private readonly string _dbPath;
    private readonly string _connectionString;
    private readonly ILookupRepository _lookupRepo;
    private readonly VocabRepository _vocabRepo;
    private readonly Km2DatabaseService _service;

    public LookupDeleteFrequencyTests() {
        _dir = Path.Combine(Path.GetTempPath(), "km2-freq-tests-" + Guid.NewGuid().ToString("N")[..10]);
        Directory.CreateDirectory(_dir);
        _dbPath = Path.Combine(_dir, "KM2.dat");
        Assert.True(DatabaseHelper.CreateDatabase(_dbPath, out var ex), "CreateDatabase failed: " + ex.Message);

        _connectionString = DatabaseHelper.GetConnectionString(_dbPath);
        _lookupRepo = new LookupRepository(_connectionString);
        _vocabRepo = new VocabRepository(_connectionString);
        _service = new Km2DatabaseService(
            new ClippingRepository(_connectionString),
            _lookupRepo,
            new OriginalClippingLineRepository(_connectionString),
            new SettingRepository(_connectionString),
            _vocabRepo);
    }

    public void Dispose() {
        try {
            // 不清 SQLite 连接池(进程级 API,会与并行测试互撞);残留目录由 TestTempCleanup 统一清扫。
            Directory.Delete(_dir, true);
        } catch { /* best effort */ }
    }

    [Fact]
    public void UpdateFrequency_AfterDeletingASingleLookupRow_DropsTheCount() {
        _vocabRepo.Add(new Vocab { Id = "v1", WordKey = "en:apple", Word = "apple", Timestamp = "t" });
        _lookupRepo.Add(new Lookup { WordKey = "en:apple", Usage = "u1", Timestamp = "2026-01-01 10:00:00" });
        _lookupRepo.Add(new Lookup { WordKey = "en:apple", Usage = "u2", Timestamp = "2026-01-02 10:00:00" });

        Assert.True(_service.UpdateFrequency());
        Assert.Equal(2, _vocabRepo.GetAll().Single().Frequency);

        // 这正是 UI「删除选中的查询」下发的底层操作:软删单条 (word_key, timestamp)。
        Assert.True(_lookupRepo.Delete("en:apple", "2026-01-02 10:00:00"));

        Assert.True(_service.UpdateFrequency());
        Assert.Equal(1, _vocabRepo.GetAll().Single().Frequency);

        // 被删的行是**软删**(仍留在表里占唯一键),故"重算按活跃行算"这一点必须成立。
        Assert.Equal(2, _lookupRepo.GetAllIncludingDeleted().Count);
    }

    [Fact]
    public void UpdateFrequency_AfterDeletingAllLookupsOfAWord_CountBecomesZero() {
        _vocabRepo.Add(new Vocab { Id = "v1", WordKey = "en:apple", Word = "apple", Timestamp = "t" });
        _lookupRepo.Add(new Lookup { WordKey = "en:apple", Usage = "u1", Timestamp = "2026-01-01 10:00:00" });
        _lookupRepo.Add(new Lookup { WordKey = "en:apple", Usage = "u2", Timestamp = "2026-01-02 10:00:00" });

        Assert.True(_service.UpdateFrequency());
        Assert.Equal(2, _vocabRepo.GetAll().Single().Frequency);

        // UI「删除某个词」下发的底层操作:软删该 word_key 下的全部查询行。
        Assert.True(_lookupRepo.Delete("en:apple"));

        Assert.True(_service.UpdateFrequency());
        Assert.Equal(0, _vocabRepo.GetAll().Single().Frequency);
    }

    [Fact]
    public void UpdateFrequency_LeavesUnrelatedWordCountsIntact() {
        _vocabRepo.Add(new Vocab { Id = "v1", WordKey = "en:apple", Word = "apple", Timestamp = "t" });
        _vocabRepo.Add(new Vocab { Id = "v2", WordKey = "en:banana", Word = "banana", Timestamp = "t" });
        _lookupRepo.Add(new Lookup { WordKey = "en:apple", Usage = "u1", Timestamp = "2026-01-01 10:00:00" });
        _lookupRepo.Add(new Lookup { WordKey = "en:banana", Usage = "u2", Timestamp = "2026-01-01 10:00:00" });
        _lookupRepo.Add(new Lookup { WordKey = "en:banana", Usage = "u3", Timestamp = "2026-01-02 10:00:00" });

        Assert.True(_lookupRepo.Delete("en:apple", "2026-01-01 10:00:00"));
        Assert.True(_service.UpdateFrequency());

        var byWord = _vocabRepo.GetAll().ToDictionary(v => v.Word, v => v.Frequency ?? 0);
        Assert.Equal(0, byWord["apple"]);
        Assert.Equal(2, byWord["banana"]);
    }
}
