using System.Text;
using Microsoft.Data.Sqlite;
using Xunit;
using KindleMate2.Infrastructure.Helpers;

namespace KindleMate2.Tests;

/// <summary>
/// <c>[vocab].[word_key]</c> 索引的用例 —— 建库脚本与老库迁移
/// (<see cref="DatabaseHelper.EnsureIndexesIfNeeded"/>)两条路径都要覆盖。
///
/// 这条索引不是"锦上添花":词频重算按 <c>word_key</c> 逐行回写 vocab,没有它每条 UPDATE 都要
/// 全表扫 vocab,整体退化成 O(vocab²)。实测(20000 lookups / 5000 vocab,同一事务内)
/// **无索引 576ms → 有索引 5ms**。所以这里除了"索引存在",还要钉住
/// <see cref="Index_IsUsableForWordKeyLookup"/>:存在但用不上等于没建。
/// </summary>
public sealed class VocabIndexTests : IDisposable {
    private const string IndexName = "ix_vocab_word_key";

    private readonly string _dir;
    private readonly string _db;

    public VocabIndexTests() {
        _dir = Path.Combine(Path.GetTempPath(), "km2-vocab-index-tests-" + Guid.NewGuid().ToString("N")[..10]);
        Directory.CreateDirectory(_dir);

        _db = Path.Combine(_dir, "KM2.dat");
        Assert.True(DatabaseHelper.CreateDatabase(_db, out var ex), "CreateDatabase failed: " + ex.Message);
    }

    public void Dispose() {
        try {
            // 与 ClippingsIndexTests 一致:刻意**不**清 SQLite 连接池 —— 那是进程级 API,
            // 会和并行跑的其他测试类互撞。残留目录由 TestTempCleanup 在进程退出时统一清扫。
            Directory.Delete(_dir, true);
        } catch { /* best effort */ }
    }

    // ————————————————————————— 新建库 —————————————————————————

    /// <summary>新建的库由建库脚本直接带上索引(脚本来源是 GetIndexScripts,与迁移路径同一份)。</summary>
    [Fact]
    public void NewDatabase_CarriesIndex_onWordKey() {
        Assert.True(IndexExists(_db, IndexName), "新建的库没有带上 " + IndexName);
        Assert.Equal(new[] { "word_key" }, IndexColumns(_db, IndexName));
    }

    // ————————————————————————— 老库迁移 —————————————————————————

    /// <summary>
    /// 老库(建库时脚本还没带这条索引)靠启动时的迁移补上。先手动删掉并确认删除生效 ——
    /// 否则下面的断言可能因为"索引本来就在"而假通过。
    /// </summary>
    [Fact]
    public void LegacyDatabase_GetsIndexFromEnsureIndexesIfNeeded() {
        Exec(_db, $"DROP INDEX IF EXISTS [{IndexName}];");
        Assert.False(IndexExists(_db, IndexName), "前置条件不成立:索引没被删掉,本用例无法证明迁移路径");

        DatabaseHelper.EnsureIndexesIfNeeded(_db);

        Assert.True(IndexExists(_db, IndexName));
        Assert.Equal(new[] { "word_key" }, IndexColumns(_db, IndexName));
    }

    /// <summary>
    /// 迁移路径改成遍历 <c>GetIndexScripts()</c> 之后,不能再出现"补了后一条、漏了前一条"。
    /// 这里把两条索引一起删掉,要求一次调用把**两条都**补回来。
    /// </summary>
    [Fact]
    public void EnsureIndexesIfNeeded_RestoresAllIndexes_NotJustTheLastOne() {
        Exec(_db, $"DROP INDEX IF EXISTS [{IndexName}];");
        Exec(_db, "DROP INDEX IF EXISTS [ix_clippings_book_page_date];");

        DatabaseHelper.EnsureIndexesIfNeeded(_db);

        Assert.True(IndexExists(_db, IndexName));
        Assert.True(IndexExists(_db, "ix_clippings_book_page_date"));
    }

    /// <summary>每次启动都会调用,必须幂等:不抛、不重复建。</summary>
    [Fact]
    public void EnsureIndexesIfNeeded_IsIdempotent() {
        Exec(_db, $"DROP INDEX IF EXISTS [{IndexName}];");

        DatabaseHelper.EnsureIndexesIfNeeded(_db);
        var afterFirst = IndexCount(_db, IndexName);
        DatabaseHelper.EnsureIndexesIfNeeded(_db);

        Assert.Equal(1, afterFirst);
        Assert.Equal(afterFirst, IndexCount(_db, IndexName));
    }

    // ————————————————————————— 索引确实被用上 —————————————————————————

    /// <summary>
    /// 索引存在不等于用得上。仓储里按 word_key 的等值查询(GetByWordKey / DeleteByWordKey /
    /// UpdateFrequencyByWordKey)都必须走它,而不是全表扫描。
    /// </summary>
    [Fact]
    public void Index_IsUsableForWordKeyLookup() {
        var select = QueryPlan(_db, "SELECT [id] FROM vocab WHERE word_key = 'en:apple';");
        Assert.Contains(IndexName, select);
        Assert.DoesNotContain("SCAN", select, StringComparison.OrdinalIgnoreCase);

        var delete = QueryPlan(_db, "DELETE FROM vocab WHERE word_key = 'en:apple';");
        Assert.Contains(IndexName, delete);
    }

    // ————————————————————————— helpers —————————————————————————

    private static void Exec(string dbPath, string sql) {
        using var conn = new SqliteConnection(DatabaseHelper.GetConnectionString(dbPath));
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static bool IndexExists(string dbPath, string name) => IndexCount(dbPath, name) > 0;

    private static int IndexCount(string dbPath, string name) {
        using var conn = new SqliteConnection(DatabaseHelper.GetConnectionString(dbPath));
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = @name;";
        cmd.Parameters.AddWithValue("@name", name);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    private static string[] IndexColumns(string dbPath, string name) {
        using var conn = new SqliteConnection(DatabaseHelper.GetConnectionString(dbPath));
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA index_info([{name}]);";
        var columns = new List<string>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) {
            // index_info 的列:seqno / cid / name
            columns.Add(reader.GetString(2));
        }
        return columns.ToArray();
    }

    private static string QueryPlan(string dbPath, string sql) {
        using var conn = new SqliteConnection(DatabaseHelper.GetConnectionString(dbPath));
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "EXPLAIN QUERY PLAN " + sql;
        var plan = new StringBuilder();
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) {
            // EXPLAIN QUERY PLAN 的第四列是给人看的描述文本
            plan.AppendLine(reader.GetValue(3)?.ToString());
        }
        return plan.ToString();
    }
}
