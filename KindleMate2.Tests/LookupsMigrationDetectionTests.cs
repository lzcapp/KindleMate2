using Microsoft.Data.Sqlite;
using Xunit;
using KindleMate2.Infrastructure.Helpers;

namespace KindleMate2.Tests;

/// <summary>
/// 回归:<c>MigrateLookupsSchemaIfNeeded</c> 的 legacy 探测**不能在无法判定时静默返回 false**。
///
/// <para>
/// 旧实现把探测里的 <c>catch (Exception)</c> 一律当"无需迁移",于是 PRAGMA 一旦读不出来
/// (库损坏 / 非 SQLite / 被占用),legacy lookups(单列 <c>UNIQUE(timestamp)</c>)永远得不到重建,
/// 之后同秒多词插入会撞唯一约束失败且无任何告警。现在"无法判定"会抛出,由启动流程的
/// <c>MigrationWarning</c> 让用户知晓。
/// </para>
/// <para>
/// 另覆盖迁移建表的健壮性:残留同名草稿表 <c>lookups_migrated</c> 不得让迁移失败或串数据。
/// </para>
/// </summary>
public sealed class LookupsMigrationDetectionTests : IDisposable {
    private readonly string _dir;

    public LookupsMigrationDetectionTests() {
        _dir = Path.Combine(Path.GetTempPath(), "km2-lookups-migration-tests-" + Guid.NewGuid().ToString("N")[..10]);
        Directory.CreateDirectory(_dir);
    }

    public void Dispose() {
        try { Directory.Delete(_dir, true); } catch { /* best effort */ }
    }

    /// <summary>
    /// 库打不开 / 不是 SQLite → 探测无法判定 → 必须抛,而不是静默跳过迁移。
    /// 旧实现下这里**不会抛**(返回 false 当作无需迁移),本用例是那条缺陷的守卫。
    /// </summary>
    [Fact]
    public void UnreadableDatabase_ThrowsInsteadOfSilentlySkipping() {
        var bad = Path.Combine(_dir, "not-a-db.dat");
        // 足够长且不是 SQLite 头,确保 Open 成功、PRAGMA 阶段才报 "file is not a database"。
        File.WriteAllText(bad, new string('x', 512) + " definitely not a sqlite database file");

        var ex = Assert.Throws<InvalidOperationException>(() => DatabaseHelper.MigrateLookupsSchemaIfNeeded(bad));
        Assert.Contains("lookups schema", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 残留同名草稿表 <c>lookups_migrated</c> 时迁移仍须成功:先 DROP IF EXISTS 再建,
    /// 不把上一次被打断留下的旧草稿表当成新表用(否则会串数据 / 永久失败)。
    /// </summary>
    [Fact]
    public void Migration_WithLeftoverScratchTable_SucceedsAndPreservesData() {
        var db = Path.Combine(_dir, "legacy.dat");
        using (var conn = new SqliteConnection(DatabaseHelper.BuildConnectionString(db, SqliteOpenMode.ReadWriteCreate, sharedCache: true))) {
            conn.Open();
            Exec(conn, "CREATE TABLE lookups (word_key TEXT, usage TEXT, title TEXT, authors TEXT, timestamp TEXT, CONSTRAINT [uq_lookups_timestamp] UNIQUE ([timestamp]));");
            Exec(conn, "INSERT INTO lookups (word_key, usage, title, authors, timestamp) VALUES ('en:apple', 'u1', 'Book', 'A', '2026-01-01 10:00:00');");
            // 上一次迁移被打断留下的草稿表(内容与真实数据无关)
            Exec(conn, "CREATE TABLE lookups_migrated (word_key TEXT, usage TEXT, title TEXT, authors TEXT, timestamp TEXT, CONSTRAINT [uq_lookups_migrated_junk] UNIQUE ([timestamp]));");
            Exec(conn, "INSERT INTO lookups_migrated (word_key, timestamp) VALUES ('JUNK', '1999-01-01 00:00:00');");
        }

        DatabaseHelper.MigrateLookupsSchemaIfNeeded(db);

        using var conn2 = new SqliteConnection(DatabaseHelper.GetConnectionString(db));
        conn2.Open();
        // 原数据保留、草稿垃圾没串进来
        Assert.Equal(1, ScalarInt(conn2, "SELECT COUNT(*) FROM lookups;"));
        Assert.Equal("en:apple", ScalarString(conn2, "SELECT word_key FROM lookups LIMIT 1;"));
        // 已重建为 (word_key, timestamp) 唯一:同一秒、不同词可共存(老式 UNIQUE(timestamp) 会拒掉第二条)
        Exec(conn2, "INSERT INTO lookups (word_key, usage, title, authors, timestamp) VALUES ('en:banana', 'u2', 'Book', 'A', '2026-01-01 10:00:00');");
        Assert.Equal(2, ScalarInt(conn2, "SELECT COUNT(*) FROM lookups;"));
        // 草稿表名不再残留
        Assert.Equal(0, ScalarInt(conn2, "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='lookups_migrated';"));
    }

    private static void Exec(SqliteConnection conn, string sql) {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static int ScalarInt(SqliteConnection conn, string sql) {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    private static string? ScalarString(SqliteConnection conn, string sql) {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        return cmd.ExecuteScalar() as string;
    }
}
