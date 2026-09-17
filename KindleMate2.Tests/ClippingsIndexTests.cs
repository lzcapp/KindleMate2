using System.Text;
using Microsoft.Data.Sqlite;
using Xunit;
using KindleMate2.Infrastructure.Helpers;

namespace KindleMate2.Tests;

/// <summary>
/// <c>[clippings]</c> 查询索引的用例 —— 建库脚本与老库迁移(<see cref="DatabaseHelper.EnsureIndexesIfNeeded"/>)
/// 两条路径都要覆盖。
///
/// 这组用例的重点不只是"索引存在",而是钉住三件容易悄悄失效的事:
/// ① **列顺序**(bookname, pagenumber, clippingdate)——顺序错了索引照样存在,但主列表的
/// <c>ORDER BY</c> 就再也用不上它;② 迁移路径真的跑到了(先确认索引不在,再要求它出现);
/// ③ 幂等 —— 每次启动都会调用,不能越调越多。
/// </summary>
public sealed class ClippingsIndexTests : IDisposable {
    private const string IndexName = "ix_clippings_book_page_date";

    private readonly string _dir;
    private readonly string _db;

    public ClippingsIndexTests() {
        _dir = Path.Combine(Path.GetTempPath(), "km2-index-tests-" + Guid.NewGuid().ToString("N")[..10]);
        Directory.CreateDirectory(_dir);

        _db = Path.Combine(_dir, "KM2.dat");
        Assert.True(DatabaseHelper.CreateDatabase(_db, out var ex), "CreateDatabase failed: " + ex.Message);
    }

    public void Dispose() {
        try {
            // 连接池会让已归还连接的句柄继续存活,不清池在 Windows 上删不掉临时目录。
            SqliteConnection.ClearAllPools();
            Directory.Delete(_dir, true);
        } catch { /* best effort */ }
    }

    // ————————————————————————— 新建库 —————————————————————————

    /// <summary>新建的库由建库脚本直接带上索引,列顺序必须与主列表排序一致。</summary>
    [Fact]
    public void NewDatabase_CarriesIndex_withExpectedColumnOrder() {
        Assert.Equal(new[] { "bookname", "pagenumber", "clippingdate" }, IndexColumns(_db, IndexName));
    }

    // ————————————————————————— 老库迁移 —————————————————————————

    /// <summary>
    /// 老库(建库时脚本还没带索引)靠启动时的迁移补上。先手动删掉索引并确认删除生效 ——
    /// 否则下面的断言可能因为"索引本来就在"而假通过。
    /// </summary>
    [Fact]
    public void LegacyDatabase_GetsIndexFromEnsureIndexesIfNeeded() {
        Exec(_db, $"DROP INDEX IF EXISTS [{IndexName}];");
        Assert.False(IndexExists(_db, IndexName), "前置条件不成立:索引没被删掉,本用例无法证明迁移路径");

        DatabaseHelper.EnsureIndexesIfNeeded(_db);

        Assert.True(IndexExists(_db, IndexName));
        Assert.Equal(new[] { "bookname", "pagenumber", "clippingdate" }, IndexColumns(_db, IndexName));
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

    /// <summary>空路径 / 不存在的库:默默返回,不能把启动流程炸掉。</summary>
    [Fact]
    public void EnsureIndexesIfNeeded_BlankOrMissingPath_DoesNotThrow() {
        DatabaseHelper.EnsureIndexesIfNeeded(string.Empty);
        DatabaseHelper.EnsureIndexesIfNeeded("   ");
        DatabaseHelper.EnsureIndexesIfNeeded(Path.Combine(_dir, "not-there.dat"));
    }

    // ————————————————————————— 索引确实被用上 —————————————————————————

    /// <summary>
    /// 索引存在不等于用得上。按书名取书摘(仓储里 6 处 <c>WHERE bookname = @bookname</c>)与
    /// 主列表排序都应命中它,而不是全表扫描 + 临时排序。
    /// </summary>
    [Fact]
    public void Index_IsUsableForBookLookupAndListOrdering() {
        var byBook = QueryPlan(_db, "SELECT [key] FROM clippings WHERE bookname = 'x';");
        Assert.Contains(IndexName, byBook);

        var ordered = QueryPlan(_db, "SELECT [key] FROM clippings ORDER BY bookname, pagenumber, clippingdate;");
        Assert.Contains(IndexName, ordered);
        Assert.DoesNotContain("TEMP B-TREE", ordered, StringComparison.OrdinalIgnoreCase);
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
