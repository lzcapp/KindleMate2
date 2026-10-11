using Microsoft.Data.Sqlite;
using Xunit;
using KindleMate2.Application.Services.KM2DB;
using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Infrastructure.Helpers;
using KindleMate2.Infrastructure.Repositories.KM2DB;

namespace KindleMate2.Tests;

/// <summary>
/// 软删除 + 批次溯源(就地演进,2026-10-11)的回归。
///
/// <para>
/// 修的缺陷:删除 vocab/lookups 曾是硬 <c>DELETE</c>,而导入判重只读"活表"——
/// 删过的词再导一次会被当成新行插回来(复活)。现在删除置 <c>deleted_at</c>(行留着占住
/// 唯一键),判重读 <c>GetAllIncludingDeleted</c>,批量插入用 <c>INSERT OR IGNORE</c>
/// 让数据库约束当最终裁判。
/// </para>
/// <para>
/// 另一半约束:扁平 KM 导入会拿同一套仓储读**旧格式源库**(软删除改造前的 .dat,没有
/// deleted_at / origin_batch 两列),读取 SQL 必须按源库实际 schema 分支。
/// </para>
/// </summary>
public sealed class SoftDeleteImportTests : IDisposable {
    private readonly string _dir;

    public SoftDeleteImportTests() {
        _dir = Path.Combine(Path.GetTempPath(), "km2-tests-" + Guid.NewGuid().ToString("N")[..10]);
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

    /// <summary>造一个**旧格式**扁平库(软删除改造前的 schema:无 deleted_at / origin_batch)。</summary>
    private string NewLegacyDb(string name) {
        var path = Path.Combine(_dir, name);
        // 文件尚不存在,得用 ReadWriteCreate(仓库的 GetConnectionString 是 ReadWrite,只开已有文件)
        using var conn = new SqliteConnection($"Data Source={path};Mode=ReadWriteCreate;");
        conn.Open();
        Exec(conn, "CREATE TABLE clippings (key TEXT PRIMARY KEY NOT NULL UNIQUE, content TEXT DEFAULT(''), bookname TEXT DEFAULT(''), authorname TEXT, brieftype INTEGER, clippingtypelocation TEXT, clippingdate TEXT, read INT DEFAULT(0), clipping_importdate TEXT, tag TEXT, sync INT DEFAULT(0), newbookname TEXT, colorRGB INTEGER DEFAULT(-1), pagenumber INT DEFAULT(0))");
        Exec(conn, "CREATE TABLE original_clipping_lines (key TEXT PRIMARY KEY NOT NULL UNIQUE, line1 TEXT, line2 TEXT, line3 TEXT, line4 TEXT, line5 TEXT)");
        Exec(conn, "CREATE TABLE settings (name TEXT PRIMARY KEY UNIQUE, value TEXT)");
        Exec(conn, "CREATE TABLE lookups (word_key TEXT, usage TEXT, title TEXT, authors TEXT, timestamp TEXT, UNIQUE (word_key, timestamp))");
        Exec(conn, "CREATE TABLE vocab (id TEXT PRIMARY KEY NOT NULL UNIQUE, word_key TEXT, word TEXT NOT NULL, stem TEXT, category INTEGER DEFAULT '0', translation TEXT, timestamp TEXT, frequency INT DEFAULT(0), sync INT DEFAULT(0), colorRGB INTEGER DEFAULT(-1))");
        return path;
    }

    private static void Exec(SqliteConnection conn, string sql) {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static KmDatabaseService NewService(string targetDb, string kmDb) => new(
        new ClippingRepository(DatabaseHelper.GetConnectionString(targetDb)),
        new LookupRepository(DatabaseHelper.GetConnectionString(targetDb)),
        new OriginalClippingLineRepository(DatabaseHelper.GetConnectionString(targetDb)),
        new SettingRepository(DatabaseHelper.GetConnectionString(targetDb)),
        new VocabRepository(DatabaseHelper.GetConnectionString(targetDb)),
        new ClippingRepository(DatabaseHelper.GetConnectionString(kmDb)),
        new LookupRepository(DatabaseHelper.GetConnectionString(kmDb)),
        new OriginalClippingLineRepository(DatabaseHelper.GetConnectionString(kmDb)),
        new SettingRepository(DatabaseHelper.GetConnectionString(kmDb)),
        new VocabRepository(DatabaseHelper.GetConnectionString(kmDb)));

    /// <summary>往源库塞一份生词 + 查询记录(两词两行)。</summary>
    private static void SeedSource(SqliteConnection conn) {
        Exec(conn, "INSERT INTO lookups (word_key, usage, title, authors, timestamp) VALUES " +
                   "('en:apple', 'u1', 'Book', 'A', '2026-01-01 10:00:00'), " +
                   "('en:banana', 'u2', 'Book', 'A', '2026-01-02 10:00:00');");
        Exec(conn, "INSERT INTO vocab (id, word_key, word, stem, timestamp) VALUES " +
                   "('apple2026-01-01 10:00:00', 'en:apple', 'apple', 'appl', '2026-01-01 10:00:00'), " +
                   "('banana2026-01-02 10:00:00', 'en:banana', 'banana', 'banan', '2026-01-02 10:00:00');");
    }

    // ————————————————————————— 核心:删过再导不复活 —————————————————————————

    /// <summary>
    /// 缺陷本体:导入 → 删词(软删) → 再导同一份源库 → 被删的词**不得**回来,
    /// 而未删的词也不该因此受影响。
    /// </summary>
    [Fact]
    public void Import_DeleteWord_Reimport_DoesNotResurrect() {
        var targetDb = NewDb("target.db");
        var kmDb = NewDb("km.db");
        using (var conn = new SqliteConnection(DatabaseHelper.GetConnectionString(kmDb))) {
            conn.Open();
            SeedSource(conn);
        }

        var svc = NewService(targetDb, kmDb);
        Assert.True(svc.ImportFromKmDatabase());

        var targetConnStr = DatabaseHelper.GetConnectionString(targetDb);
        var vocabRepo = new VocabRepository(targetConnStr);
        var lookupRepo = new LookupRepository(targetConnStr);
        Assert.Equal(2, vocabRepo.GetCount());
        Assert.Equal(2, lookupRepo.GetCount());

        // 删掉 apple(生词条 + 它的查询记录)
        Assert.True(vocabRepo.DeleteByWordKey("en:apple"));
        Assert.True(lookupRepo.Delete("en:apple"));
        Assert.Equal(1, vocabRepo.GetCount());
        Assert.Equal(1, lookupRepo.GetCount());

        // 再导一次同一份源库:被删的 apple 不复活
        Assert.True(svc.ImportFromKmDatabase());
        Assert.Equal(1, vocabRepo.GetCount());
        Assert.Equal(1, lookupRepo.GetCount());
        Assert.DoesNotContain(vocabRepo.GetAll(), v => v.WordKey == "en:apple");
        Assert.DoesNotContain(lookupRepo.GetAll(), l => l.WordKey == "en:apple");

        // tombstone 还在(行留着占住唯一键),计数含已删行时应是 2
        Assert.Equal(2, vocabRepo.GetAllIncludingDeleted().Count);
        Assert.Equal(2, lookupRepo.GetAllIncludingDeleted().Count);
    }

    /// <summary>
    /// 软删只挡**同一条**记录:同一词的新时间戳是新身份,仍应导入 —— 否则删一次就等于
    /// 把这个词永久拉黑。
    /// </summary>
    [Fact]
    public void Import_DeleteLookup_Reimport_SameWordNewTimestamp_StillImports() {
        var targetDb = NewDb("target2.db");
        var kmDb = NewDb("km2.db");
        using (var conn = new SqliteConnection(DatabaseHelper.GetConnectionString(kmDb))) {
            conn.Open();
            SeedSource(conn);
        }

        var svc = NewService(targetDb, kmDb);
        Assert.True(svc.ImportFromKmDatabase());

        var lookupRepo = new LookupRepository(DatabaseHelper.GetConnectionString(targetDb));
        Assert.True(lookupRepo.Delete("en:apple", "2026-01-01 10:00:00"));
        Assert.Equal(1, lookupRepo.GetCount());

        // 源库补一条同词、不同时间戳的查询
        using (var conn = new SqliteConnection(DatabaseHelper.GetConnectionString(kmDb))) {
            conn.Open();
            Exec(conn, "INSERT INTO lookups (word_key, usage, title, authors, timestamp) VALUES " +
                       "('en:apple', 'u9', 'Book', 'A', '2026-03-03 09:00:00');");
        }

        Assert.True(svc.ImportFromKmDatabase());
        // 旧时间戳不复活,新时间戳进来了 → 活行 2 条
        Assert.Equal(2, lookupRepo.GetCount());
        Assert.Equal(3, lookupRepo.GetAllIncludingDeleted().Count);
    }

    /// <summary>
    /// 「清空全部数据」路径用真删(DeleteAll):先备份再清空,语义是"我要全新的库",
    /// 留 tombstone 反而会挡住随后的重新导入 —— 这条守卫该语义。
    /// </summary>
    [Fact]
    public void DeleteAll_ClearsTombstones_AndAllowsFullReimport() {
        var targetDb = NewDb("target3.db");
        var kmDb = NewDb("km3.db");
        using (var conn = new SqliteConnection(DatabaseHelper.GetConnectionString(kmDb))) {
            conn.Open();
            SeedSource(conn);
        }

        var svc = NewService(targetDb, kmDb);
        Assert.True(svc.ImportFromKmDatabase());

        var targetConnStr = DatabaseHelper.GetConnectionString(targetDb);
        var vocabRepo = new VocabRepository(targetConnStr);
        var lookupRepo = new LookupRepository(targetConnStr);
        Assert.True(vocabRepo.DeleteByWordKey("en:apple"));
        Assert.True(lookupRepo.Delete("en:apple"));
        Assert.True(vocabRepo.DeleteAll());
        Assert.True(lookupRepo.DeleteAll());

        Assert.Empty(vocabRepo.GetAllIncludingDeleted());
        Assert.Empty(lookupRepo.GetAllIncludingDeleted());

        // tombstone 已清,重新导入应满血回来
        Assert.True(svc.ImportFromKmDatabase());
        Assert.Equal(2, vocabRepo.GetCount());
        Assert.Equal(2, lookupRepo.GetCount());
    }

    // ————————————————————————— 判重下沉到唯一约束 —————————————————————————

    /// <summary>
    /// 批量插入的最终裁判是 UNIQUE:与 tombstone 撞键的行被 OR IGNORE 悄悄跳过,
    /// 返回值只计真正插入的行数(导入进度照这个数报)。
    /// </summary>
    [Fact]
    public void BatchAdd_RowConflictingWithTombstone_SkippedAndNotCounted() {
        var repo = new VocabRepository(DatabaseHelper.GetConnectionString(NewDb("batch.db")));

        var v1 = new Vocab { Id = "id1", WordKey = "en:apple", Word = "apple", Timestamp = "2026-01-01 10:00:00" };
        Assert.Equal(1, repo.Add(new List<Vocab> { v1 }));
        Assert.True(repo.Delete("id1")); // tombstone 占住主键 id1

        // 同 id 再插:撞主键 → 跳过;换 id(=同一词不同时间戳)→ 正常插入
        var dup = new Vocab { Id = "id1", WordKey = "en:apple", Word = "apple", Timestamp = "2026-01-01 10:00:00" };
        var fresh = new Vocab { Id = "id2", WordKey = "en:apple", Word = "apple", Timestamp = "2026-02-02 10:00:00" };
        Assert.Equal(1, repo.Add(new List<Vocab> { dup, fresh }));

        Assert.Equal(1, repo.GetCount()); // 只有 fresh 是活行
        Assert.Equal(2, repo.GetAllIncludingDeleted().Count); // id1 的 tombstone + fresh
    }

    // ————————————————————————— 迁移(老库补列) —————————————————————————

    /// <summary>
    /// 端到端「老用户升级」:造一个**上一版程序产出的真实库**(user_version=1、
    /// lookups 是老式单列 <c>UNIQUE(timestamp)</c>、vocab 无软删两列、没有 import_batches),
    /// 灌入数据后按启动顺序(MainWindowViewModel.PrepareDatabaseAsync 里的
    /// MigrateSchemaIfNeeded → MigrateLookupsSchemaIfNeeded → EnsureSoftDeleteSchemaIfNeeded →
    /// EnsureIndexesIfNeeded)跑一遍,验证:
    /// <list type="bullet">
    ///   <item>老数据一行不丢,且迁移后都是**活跃行**(deleted_at NULL);</item>
    ///   <item>lookups 被重建为 (word_key, timestamp) 唯一 —— 老式按 timestamp 唯一会把
    ///     同一秒查的两个不同词判成重复,这正是那次重建要修的;</item>
    ///   <item>软删除两列 + import_batches 表已补上,**软删与重导不复活在升级后的库上立即可用**;</item>
    ///   <item><c>user_version</c> 仍是 1(不升版)—— 逃生门:降级回旧版仍能打开;</item>
    ///   <item>整条链再跑一遍是 no-op(幂等),升级中断后重来也安全。</item>
    /// </list>
    /// </summary>
    [Fact]
    public void Upgrade_FromPreviousVersion_MigratesInPlace_AndSoftDeleteWorks() {
        var oldDb = NewOldVersionDb("upgrade.db");

        // 老库灌数据:一条标注、两条查询(其中一个 word_key 撞老式 timestamp 唯一约束的场景留作断言)、一个生词
        using (var conn = new SqliteConnection(DatabaseHelper.GetConnectionString(oldDb))) {
            conn.Open();
            Exec(conn, "INSERT INTO clippings (key, content, bookname) VALUES ('clip1', 'a highlight', 'Book A')");
            Exec(conn, "INSERT INTO lookups (word_key, usage, title, authors, timestamp) VALUES ('en:apple', 'u1', 'Book A', 'A', '2026-01-01 10:00:00')");
            Exec(conn, "INSERT INTO lookups (word_key, usage, title, authors, timestamp) VALUES ('en:banana', 'u2', 'Book A', 'A', '2026-01-01 10:00:05')");
            Exec(conn, "INSERT INTO vocab (id, word_key, word, stem, timestamp) VALUES ('apple2026-01-01 10:00:00', 'en:apple', 'apple', 'appl', '2026-01-01 10:00:00')");
            Exec(conn, "PRAGMA user_version = 1;");
        }

        // 按启动顺序跑完整迁移链(与 MainWindowViewModel.PrepareDatabaseAsync 一致)
        DatabaseHelper.MigrateSchemaIfNeeded(oldDb);
        DatabaseHelper.MigrateLookupsSchemaIfNeeded(oldDb);
        DatabaseHelper.EnsureSoftDeleteSchemaIfNeeded(oldDb);
        DatabaseHelper.EnsureIndexesIfNeeded(oldDb);

        var connStr = DatabaseHelper.GetConnectionString(oldDb);
        using (var conn = new SqliteConnection(connStr)) {
            conn.Open();
            // 软删两列 + import_batches 表已补
            Assert.True(DatabaseHelper.HasColumn(conn, "vocab", "deleted_at"));
            Assert.True(DatabaseHelper.HasColumn(conn, "vocab", "origin_batch"));
            Assert.True(DatabaseHelper.HasColumn(conn, "lookups", "deleted_at"));
            Assert.True(DatabaseHelper.HasColumn(conn, "lookups", "origin_batch"));
            using (var cmd = conn.CreateCommand()) {
                cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='import_batches';";
                Assert.Equal(1L, Convert.ToInt64(cmd.ExecuteScalar()));
            }
            // 逃生门:user_version 不升 —— 旧版程序打开时不会因前向保护拒绝
            using (var cmd = conn.CreateCommand()) {
                cmd.CommandText = "PRAGMA user_version;";
                Assert.Equal(1L, Convert.ToInt64(cmd.ExecuteScalar()));
            }
        }

        // 老数据一行不丢,且都是活跃行
        var vocabRepo = new VocabRepository(connStr);
        var lookupRepo = new LookupRepository(connStr);
        Assert.Equal(1, vocabRepo.GetCount());
        Assert.Equal(2, lookupRepo.GetCount());
        Assert.Single(vocabRepo.GetAllIncludingDeleted()); // 尚无 tombstone,与活行数一致
        Assert.Equal(2, lookupRepo.GetAllIncludingDeleted().Count);

        // lookups 已重建为 (word_key, timestamp) 唯一:现在能插入"同一秒、不同词"的两条 ——
        // 老式 UNIQUE(timestamp) 会把第二条判成重复拒掉,这正是那次重建要修的缺陷。
        using (var conn = new SqliteConnection(connStr)) {
            conn.Open();
            Exec(conn, "INSERT INTO lookups (word_key, usage, title, authors, timestamp) VALUES ('en:cherry', 'u3', 'Book A', 'A', '2026-01-01 10:00:00')");
        }
        Assert.Equal(3, lookupRepo.GetCount());

        // 软删除在升级后的库上立即可用
        Assert.True(vocabRepo.DeleteByWordKey("en:apple"));
        Assert.True(lookupRepo.Delete("en:apple"));
        Assert.Equal(0, vocabRepo.GetCount());
        Assert.Equal(2, lookupRepo.GetCount());

        // 重导不复活:拿老库自己当源再导一次(升级用户的常见操作),被删的 apple 不回来
        var svc = NewService(oldDb, oldDb);
        Assert.True(svc.ImportFromKmDatabase());
        Assert.Equal(0, vocabRepo.GetCount());
        Assert.DoesNotContain(lookupRepo.GetAll(), l => l.WordKey == "en:apple");

        // 整条链再跑一遍是 no-op:数据不动、不炸(升级中断后重来也安全)
        DatabaseHelper.MigrateSchemaIfNeeded(oldDb);
        DatabaseHelper.MigrateLookupsSchemaIfNeeded(oldDb);
        DatabaseHelper.EnsureSoftDeleteSchemaIfNeeded(oldDb);
        DatabaseHelper.EnsureIndexesIfNeeded(oldDb);
        Assert.Equal(0, vocabRepo.GetCount());
        Assert.Equal(2, lookupRepo.GetCount());
    }

    /// <summary>
    /// 造一个**上一版程序产出的真实库**:user_version=1(已过前向保护)、
    /// lookups 是老式单列 <c>UNIQUE(timestamp)</c>(触发重建)、vocab 无软删两列、
    /// 没有 import_batches 表。表结构照抄改造前的 <c>CreateDatabase</c>。
    /// </summary>
    private string NewOldVersionDb(string name) {
        var path = Path.Combine(_dir, name);
        using var conn = new SqliteConnection($"Data Source={path};Mode=ReadWriteCreate;");
        conn.Open();
        Exec(conn, "CREATE TABLE schema_migration (user_version INTEGER PRIMARY KEY, applied_at TEXT, description TEXT)");
        Exec(conn, "CREATE TABLE clippings (key TEXT PRIMARY KEY NOT NULL UNIQUE, content TEXT DEFAULT(''), bookname TEXT DEFAULT(''), authorname TEXT, brieftype INTEGER, clippingtypelocation TEXT, clippingdate TEXT, read INT DEFAULT(0), clipping_importdate TEXT, tag TEXT, sync INT DEFAULT(0), newbookname TEXT, colorRGB INTEGER DEFAULT(-1), pagenumber INT DEFAULT(0))");
        // ⚠️ 老式:UNIQUE 只在 timestamp 一列(新库是 (word_key, timestamp))
        Exec(conn, "CREATE TABLE lookups (word_key TEXT, usage TEXT, title TEXT, authors TEXT, timestamp TEXT, CONSTRAINT [uq_lookups_timestamp] UNIQUE ([timestamp]))");
        Exec(conn, "CREATE TABLE original_clipping_lines (key TEXT PRIMARY KEY NOT NULL UNIQUE, line1 TEXT, line2 TEXT, line3 TEXT, line4 TEXT, line5 TEXT)");
        Exec(conn, "CREATE TABLE settings (name TEXT PRIMARY KEY UNIQUE, value TEXT)");
        Exec(conn, "CREATE TABLE vocab (id TEXT PRIMARY KEY NOT NULL UNIQUE, word_key TEXT, word TEXT NOT NULL, stem TEXT, category INTEGER DEFAULT '0', translation TEXT, timestamp TEXT, frequency INT DEFAULT(0), sync INT DEFAULT(0), colorRGB INTEGER DEFAULT(-1))");
        return path;
    }

    /// <summary>
    /// 老库升级:EnsureSoftDeleteSchemaIfNeeded 给 vocab/lookups 补两列、建 import_batches,
    /// 已有行的 deleted_at/origin_batch 为 NULL(= 活行),且重复调用是 no-op(幂等)。
    /// </summary>
    [Fact]
    public void EnsureSoftDeleteSchema_AddsColumns_PreservesRows_AndIsIdempotent() {
        var legacy = NewLegacyDb("legacy-migrate.db");
        using (var conn = new SqliteConnection(DatabaseHelper.GetConnectionString(legacy))) {
            conn.Open();
            SeedSource(conn);
        }

        DatabaseHelper.EnsureSoftDeleteSchemaIfNeeded(legacy);
        DatabaseHelper.EnsureSoftDeleteSchemaIfNeeded(legacy); // 幂等:重复跑不炸

        using (var conn = new SqliteConnection(DatabaseHelper.GetConnectionString(legacy))) {
            conn.Open();
            Assert.True(DatabaseHelper.HasColumn(conn, "vocab", "deleted_at"));
            Assert.True(DatabaseHelper.HasColumn(conn, "vocab", "origin_batch"));
            Assert.True(DatabaseHelper.HasColumn(conn, "lookups", "deleted_at"));
            Assert.True(DatabaseHelper.HasColumn(conn, "lookups", "origin_batch"));
        }

        // 迁移后仓储读得到原有行,且视为活行
        var repo = new VocabRepository(DatabaseHelper.GetConnectionString(legacy));
        Assert.Equal(2, repo.GetCount());
        Assert.Equal(2, repo.GetAllIncludingDeleted().Count);
    }

    /// <summary>
    /// 旧格式源库(还没跑迁移的 .dat)必须仍可只读 —— 扁平 KM 导入拿同一套仓储读它,
    /// 读取 SQL 按源库实际 schema 分支,一条 no such column 都不能有。
    /// </summary>
    [Fact]
    public void LegacyFormatSource_WithoutNewColumns_IsStillReadableAndImportable() {
        var targetDb = NewDb("target4.db");
        var legacySource = NewLegacyDb("legacy-source.db");
        using (var conn = new SqliteConnection(DatabaseHelper.GetConnectionString(legacySource))) {
            conn.Open();
            SeedSource(conn);
        }

        // 源仓储直接读旧格式库:不炸,且行映射正常(尾部两列为 null)。
        // 刻意只走 GetAll/GetAllIncludingDeleted —— 这是生产里唯一会碰到旧源库的读路径
        // (扁平 KM 导入),其余方法(GetCount/GetByFuzzySearch 等)只在当前库上用,当前库
        // 启动时已迁移、必有新列。
        var sourceVocabRepo = new VocabRepository(DatabaseHelper.GetConnectionString(legacySource));
        Assert.Equal(2, sourceVocabRepo.GetAll().Count);
        Assert.Equal(2, sourceVocabRepo.GetAllIncludingDeleted().Count);
        Assert.Null(sourceVocabRepo.GetAll().First(v => v.WordKey == "en:apple").OriginBatch);

        // 走完整导入路径
        var svc = NewService(targetDb, legacySource);
        Assert.True(svc.ImportFromKmDatabase());
        var targetVocabRepo = new VocabRepository(DatabaseHelper.GetConnectionString(targetDb));
        var targetLookupRepo = new LookupRepository(DatabaseHelper.GetConnectionString(targetDb));
        Assert.Equal(2, targetVocabRepo.GetCount());
        Assert.Equal(2, targetLookupRepo.GetCount());
    }

    // ————————————————————————— 批次溯源 —————————————————————————

    /// <summary>
    /// 导入带 originBatch 时,插入的行打上 origin_batch,批次行的计数能回填并读回 ——
    /// 这是「这批词是哪次导进来的」的对账链路。
    /// </summary>
    [Fact]
    public void Import_WithOriginBatch_StampsRows_AndBatchRowRoundTrips() {
        var targetDb = NewDb("target5.db");
        var kmDb = NewDb("km5.db");
        using (var conn = new SqliteConnection(DatabaseHelper.GetConnectionString(kmDb))) {
            conn.Open();
            SeedSource(conn);
        }

        var targetConnStr = DatabaseHelper.GetConnectionString(targetDb);
        var batchRepo = new ImportBatchRepository(targetConnStr);
        var batchId = batchRepo.Add(new ImportBatch {
            SourceType = ImportBatch.SourceKm,
            SourcePath = kmDb,
            ImportedAt = "2026-10-11 12:00:00"
        });

        var svc = NewService(targetDb, kmDb);
        Assert.True(svc.ImportFromKmDatabase(null, batchId));

        var vocabRepo = new VocabRepository(targetConnStr);
        var lookupRepo = new LookupRepository(targetConnStr);
        Assert.All(vocabRepo.GetAll(), v => Assert.Equal(batchId, v.OriginBatch));
        Assert.All(lookupRepo.GetAll(), l => Assert.Equal(batchId, l.OriginBatch));

        Assert.True(batchRepo.UpdateCounts(batchId, 0, 4));
        var batch = Assert.Single(batchRepo.GetAll());
        Assert.Equal(ImportBatch.SourceKm, batch.SourceType);
        Assert.Equal(kmDb, batch.SourcePath);
        Assert.Equal(4, batch.InsertedCount);
    }

    /// <summary>不传 originBatch(旧调用方)时落 NULL —— 不能因为溯源缺省就把导入搞挂。</summary>
    [Fact]
    public void Import_WithoutOriginBatch_LeavesColumnNull() {
        var targetDb = NewDb("target6.db");
        var kmDb = NewDb("km6.db");
        using (var conn = new SqliteConnection(DatabaseHelper.GetConnectionString(kmDb))) {
            conn.Open();
            SeedSource(conn);
        }

        Assert.True(NewService(targetDb, kmDb).ImportFromKmDatabase());

        var vocabRepo = new VocabRepository(DatabaseHelper.GetConnectionString(targetDb));
        Assert.All(vocabRepo.GetAll(), v => Assert.Null(v.OriginBatch));
    }

    // ————————————————————————— 合并 word_key 搬 tombstone —————————————————————————

    /// <summary>
    /// MergeWordKey 搬行时连 tombstone 一起搬:软删除状态是行的事实,跟着词走 ——
    /// 否则合并后旧键下残留 tombstone,新键下的行又会被当成已删。
    /// </summary>
    [Fact]
    public void MergeWordKey_MovesTombstonesAlongWithLiveRows() {
        var repo = new LookupRepository(DatabaseHelper.GetConnectionString(NewDb("merge.db")));
        repo.Add(new List<Lookup> {
            new() { WordKey = "old", Usage = "live", Title = "t", Timestamp = "2026-01-01 10:00:00" },
            new() { WordKey = "old", Usage = "deleted", Title = "t", Timestamp = "2026-01-02 10:00:00" }
        });
        Assert.True(repo.Delete("old", "2026-01-02 10:00:00"));

        Assert.Equal(2, repo.MergeWordKey("old", "new"));

        Assert.DoesNotContain(repo.GetAllIncludingDeleted(), l => l.WordKey == "old");
        var moved = repo.GetAllIncludingDeleted().Where(l => l.WordKey == "new").ToList();
        Assert.Equal(2, moved.Count);
        Assert.Single(repo.GetAll()); // 只有活行露出来
    }
}
