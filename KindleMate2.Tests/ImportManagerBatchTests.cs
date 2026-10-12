using Microsoft.Data.Sqlite;
using Xunit;
using KindleMate2.Application.Services;
using KindleMate2.Application.Services.KM2DB;
using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Domain.Interfaces.KM2DB;
using KindleMate2.Infrastructure.Helpers;
using KindleMate2.Infrastructure.Repositories.KM2DB;

namespace KindleMate2.Tests;

/// <summary>
/// <see cref="ImportManager"/> 批次记账(import_batches)的回归 —— 审阅点名的三条
/// 「只有注释承诺、没有断言」的行为,外加一条端到端:
/// <list type="number">
///   <item><c>ImportKindleClippings</c> 建批次行但不打 origin_batch(标注侧只记总账);</item>
///   <item>导入失败时批次行留在 parsed=inserted=0(「尝试过但没进来」本身就是记录);</item>
///   <item><c>CompleteBatch</c> 回填异常只记日志不外抛;<c>BeginBatch</c> 建批次异常同样
///     不外抛(P2-2:溯源是辅助账本,写不进去就降级为无批次,不能反过来弄挂主导入);</item>
///   <item>成功导入后计数回填,vocab 行的 origin_batch 指回该批次。</item>
/// </list>
/// 全程真实仓储 + 真实导入服务(仅批次仓储可注入故障),不 mock 导入逻辑本身。
/// </summary>
public sealed class ImportManagerBatchTests : IDisposable {
    private readonly string _dir;
    private readonly string _targetDb;

    public ImportManagerBatchTests() {
        _dir = Path.Combine(Path.GetTempPath(), "km2-import-batch-tests-" + Guid.NewGuid().ToString("N")[..10]);
        Directory.CreateDirectory(_dir);
        _targetDb = Path.Combine(_dir, "KM2.dat");
        Assert.True(DatabaseHelper.CreateDatabase(_targetDb, out var ex), "CreateDatabase failed: " + ex.Message);
    }

    public void Dispose() {
        try { Directory.Delete(_dir, true); } catch { /* best effort */ }
    }

    // ————————————————————————— 用例 —————————————————————————

    /// <summary>
    /// ① 标注导入**建批次行**(import_batches 是「每次导入发生了什么」的总账,不分表),
    /// 但标注路径不写 origin_batch —— 本次没有导入任何 vocab/lookups,自然无行被打标。
    /// </summary>
    [Fact]
    public void ImportKindleClippings_RecordsBatch_ButStampsNoOriginBatch() {
        var batchRepo = new ImportBatchRepository(DatabaseHelper.GetConnectionString(_targetDb));
        var importer = NewManager(batchRepo);

        var message = importer.ImportKindleClippings(WriteClippingsFixture());

        Assert.False(string.IsNullOrWhiteSpace(message));
        var batch = Assert.Single(batchRepo.GetAll());
        Assert.Equal(ImportBatch.SourceKindleClippings, batch.SourceType);
        Assert.True(batch.ParsedCount > 0, "parsed 计数应回填");
        Assert.True(batch.InsertedCount > 0, "inserted 计数应回填");

        var targetConnStr = DatabaseHelper.GetConnectionString(_targetDb);
        Assert.Equal(0, new VocabRepository(targetConnStr).GetCount());
        Assert.Equal(0, new LookupRepository(targetConnStr).GetCount());
    }

    /// <summary>
    /// ② 导入失败(源库不存在)→ 批次行**已建**、计数留在 0/0 —— 不需要额外的状态列,
    /// 「尝试过但没进来」本身就是记录。
    /// </summary>
    [Fact]
    public void FailedImport_LeavesBatchRowAtZeroCounts() {
        var batchRepo = new ImportBatchRepository(DatabaseHelper.GetConnectionString(_targetDb));
        var importer = NewManager(batchRepo);

        var message = importer.ImportKmDatabase(Path.Combine(_dir, "no-such-source.dat"));

        Assert.Equal(string.Empty, message);
        var batch = Assert.Single(batchRepo.GetAll());
        Assert.Equal(ImportBatch.SourceKm, batch.SourceType);
        Assert.Equal(0, batch.ParsedCount);
        Assert.Equal(0, batch.InsertedCount);
    }

    /// <summary>③ 回填计数抛异常 → 只记日志不外抛,导入结果照常返回。</summary>
    [Fact]
    public void CompleteBatchFailure_DoesNotBreakImport() {
        var importer = NewManager(new ThrowingCountsRepository());

        var message = importer.ImportKindleClippings(WriteClippingsFixture());

        Assert.False(string.IsNullOrWhiteSpace(message));
    }

    /// <summary>
    /// ④ P2-2:建批次抛异常 → <c>BeginBatch</c> 兜底为 null,主导入照常进行
    /// (溯源降级为「无批次」)。
    /// </summary>
    [Fact]
    public void BeginBatchFailure_DoesNotBreakImport() {
        var importer = NewManager(new ThrowingAddRepository());

        var message = importer.ImportKindleClippings(WriteClippingsFixture());

        Assert.False(string.IsNullOrWhiteSpace(message));
    }

    /// <summary>
    /// ⑤ 端到端:走 <see cref="ImportManager.ImportKmDatabase"/> 成功导入后,批次计数回填,
    /// 且 vocab 行的 origin_batch 指回这一批 —— 把「ImportManager 开批次」与
    /// 「导入服务打标」两半记账真正接起来(此前两边各有测试、中间一环没人断言)。
    /// </summary>
    [Fact]
    public void SuccessfulFlatImport_BackfillsCounts_AndStampsOriginBatch() {
        var sourceDb = Path.Combine(_dir, "source.dat");
        Assert.True(DatabaseHelper.CreateDatabase(sourceDb, out var ex), "CreateDatabase failed: " + ex.Message);
        using (var conn = new SqliteConnection(DatabaseHelper.GetConnectionString(sourceDb))) {
            conn.Open();
            Exec(conn, "INSERT INTO lookups (word_key, usage, title, authors, timestamp) VALUES " +
                       "('en:apple', 'u1', 'Book', 'A', '2026-01-01 10:00:00'), " +
                       "('en:banana', 'u2', 'Book', 'A', '2026-01-02 10:00:00');");
            Exec(conn, "INSERT INTO vocab (id, word_key, word, stem, timestamp) VALUES " +
                       "('apple2026-01-01 10:00:00', 'en:apple', 'apple', 'appl', '2026-01-01 10:00:00'), " +
                       "('banana2026-01-02 10:00:00', 'en:banana', 'banana', 'banan', '2026-01-02 10:00:00');");
        }

        var batchRepo = new ImportBatchRepository(DatabaseHelper.GetConnectionString(_targetDb));
        var importer = NewManager(batchRepo);

        var message = importer.ImportKmDatabase(sourceDb);

        Assert.False(string.IsNullOrWhiteSpace(message));
        var batch = Assert.Single(batchRepo.GetAll());
        Assert.Equal(ImportBatch.SourceKm, batch.SourceType);
        // 扁平合并不区分「解析量 / 插入量」,parsed 记 0 —— 口径见 ImportManager 的注释
        Assert.Equal(0, batch.ParsedCount);
        Assert.Equal(4, batch.InsertedCount); // 2 vocab + 2 lookups

        var vocabRows = new VocabRepository(DatabaseHelper.GetConnectionString(_targetDb)).GetAllIncludingDeleted();
        Assert.Equal(2, vocabRows.Count);
        Assert.All(vocabRows, v => Assert.Equal(batch.Id, v.OriginBatch));
    }

    // ————————————————————————— 组装 —————————————————————————

    /// <summary>
    /// 真实组装(与 DI 的差别只有两个用不到的工厂与可注入的批次仓储):
    /// 目标库固定为测试库,备份落 <c>_dir</c>,kmate/vocab.db 工厂本用例不会走到。
    /// </summary>
    private ImportManager NewManager(IImportBatchRepository batchRepo) {
        var targetConnStr = DatabaseHelper.GetConnectionString(_targetDb);
        var clipRepo = new ClippingRepository(targetConnStr);
        var lookupRepo = new LookupRepository(targetConnStr);
        var lineRepo = new OriginalClippingLineRepository(targetConnStr);
        var settingRepo = new SettingRepository(targetConnStr);
        var vocabRepo = new VocabRepository(targetConnStr);

        return new ImportManager(
            new Km2DatabaseService(clipRepo, lookupRepo, lineRepo, settingRepo, vocabRepo),
            new ClippingService(clipRepo),
            new VocabService(vocabRepo),
            new OriginalClippingLineService(lineRepo),
            new LookupService(lookupRepo),
            new UnusedVocabFactory(),
            new TestKmFactory(_targetDb),
            new UnusedKmateFactory(),
            batchRepo,
            _dir);
    }

    /// <summary>平库导入工厂:目标库固定为测试库,源库按传入路径(与 DI 版行为一致)。</summary>
    private sealed class TestKmFactory(string targetDbPath) : IKmDatabaseServiceFactory {
        public KmDatabaseService Create(string kmDbPath) => new(
            new ClippingRepository(DatabaseHelper.GetConnectionString(targetDbPath)),
            new LookupRepository(DatabaseHelper.GetConnectionString(targetDbPath)),
            new OriginalClippingLineRepository(DatabaseHelper.GetConnectionString(targetDbPath)),
            new SettingRepository(DatabaseHelper.GetConnectionString(targetDbPath)),
            new VocabRepository(DatabaseHelper.GetConnectionString(targetDbPath)),
            new ClippingRepository(DatabaseHelper.GetConnectionString(kmDbPath)),
            new LookupRepository(DatabaseHelper.GetConnectionString(kmDbPath)),
            new OriginalClippingLineRepository(DatabaseHelper.GetConnectionString(kmDbPath)),
            new SettingRepository(DatabaseHelper.GetConnectionString(kmDbPath)),
            new VocabRepository(DatabaseHelper.GetConnectionString(kmDbPath)));
    }

    private sealed class UnusedVocabFactory : IVocabDatabaseServiceFactory {
        public VocabDatabaseService Create(string vocabDbPath) =>
            throw new NotSupportedException("本用例不走 vocab.db 导入");
    }

    private sealed class UnusedKmateFactory : IKmateDatabaseServiceFactory {
        public KmateDatabaseService Create(string km3DbPath) =>
            throw new NotSupportedException("本用例不走 KMate 导入");
    }

    /// <summary>回填时抛异常 —— 钉住 CompleteBatch 只记日志不外抛。</summary>
    private sealed class ThrowingCountsRepository : IImportBatchRepository {
        public long Add(ImportBatch batch) => 1;
        public bool UpdateCounts(long id, int parsedCount, int insertedCount) =>
            throw new InvalidOperationException("回填失败(用例注入)");
        public List<ImportBatch> GetAll() => [];
    }

    /// <summary>建批次时抛异常 —— 钉住 BeginBatch 兜底为 null(P2-2)。</summary>
    private sealed class ThrowingAddRepository : IImportBatchRepository {
        public long Add(ImportBatch batch) =>
            throw new InvalidOperationException("建批次失败(用例注入)");
        public bool UpdateCounts(long id, int parsedCount, int insertedCount) => true;
        public List<ImportBatch> GetAll() => [];
    }

    // ————————————————————————— helpers —————————————————————————

    /// <summary>与 DataLayerFixTests 同形的 My Clippings 夹具(走真实解析路径)。</summary>
    private string WriteClippingsFixture() {
        var path = Path.Combine(_dir, "My Clippings.txt");
        File.WriteAllText(path, """
Book R (Author R)
- Highlight | Page 5 | Location 100-101 | Added on Thursday, January 1, 2026, 12:00:00 AM

a highlight
==========
""");
        return path;
    }

    private static void Exec(SqliteConnection conn, string sql) {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
