using Microsoft.Data.Sqlite;
using Xunit;
using KindleMate2.Infrastructure.Helpers;

namespace KindleMate2.Tests;

/// <summary>
/// schema 版本机制(阶段 0)的用例 —— <c>PRAGMA user_version</c> 作为**权威**版本号、
/// <c>schema_migration</c> 表作为审计日志。
///
/// <para>
/// 阶段 0 只建立版本号这个机制,**不动任何现有表结构**;所以这里不测任何业务表,
/// 只测版本号本身的三件事:① 新库一出生就是当前版本;② 老库(v0)能被升上来;
/// ③ **幂等** —— 每次启动都会调用,不能越调越多(审计表尤其不能一行变两行)。
/// </para>
/// <para>
/// 一条关键前提(已在实现里钉死,这里用用例守护):<c>PRAGMA user_version</c> **随事务回滚**。
/// 所以迁移必须把「设版本号」放在事务的**最后一步**、与建表写审计一起原子提交 ——
/// <see cref="MigrateSchemaIfNeeded_LeavesTableAndVersionConsistent"/> 就是在守这条:
/// 若实现写成"先提交事务、再单独设 PRAGMA",表与版本就会不同步,该用例会红。
/// </para>
/// </summary>
public sealed class SchemaVersionTests : IDisposable {
    private readonly string _dir;
    private readonly string _db;

    public SchemaVersionTests() {
        _dir = Path.Combine(Path.GetTempPath(), "km2-schema-version-" + Guid.NewGuid().ToString("N")[..10]);
        Directory.CreateDirectory(_dir);

        _db = Path.Combine(_dir, "KM2.dat");
        Assert.True(DatabaseHelper.CreateDatabase(_db, out var ex), "CreateDatabase failed: " + ex.Message);
    }

    public void Dispose() {
        try {
            // 刻意**不**在这里清 SQLite 连接池:那是进程级 API,会和并行跑的其他测试类互撞。
            // 残留目录由 TestTempCleanup 在进程退出时统一清扫。
            Directory.Delete(_dir, true);
        } catch { /* best effort */ }
    }

    // ————————————————————————— 新建库 —————————————————————————

    /// <summary>新建的库由建库脚本直接标记为当前版本,并留下一条审计行。</summary>
    [Fact]
    public void NewDatabase_IsAtCurrentVersion_WithOneAuditRow() {
        Assert.Equal(DatabaseHelper.CurrentSchemaVersion, DatabaseHelper.GetSchemaVersion(_db));
        Assert.Equal(1, AuditRowCount(_db));
        Assert.Equal(DatabaseHelper.CurrentSchemaVersion, AuditVersion(_db));
    }

    // ————————————————————————— 老库升级 —————————————————————————

    /// <summary>
    /// 老库(阶段 0 之前建的库):手动把版本压回 0 并清空审计表 → 迁移应把它升到当前版本。
    /// 先断言前置条件(确实是 0) —— 否则可能因为"本来就是当前版本"而假通过。
    /// </summary>
    [Fact]
    public void LegacyDatabase_IsUpgradedToCurrentVersion() {
        MakeLegacy(_db);
        Assert.Equal(0, DatabaseHelper.GetSchemaVersion(_db));
        Assert.Equal(0, AuditRowCount(_db));

        DatabaseHelper.MigrateSchemaIfNeeded(_db);

        Assert.Equal(DatabaseHelper.CurrentSchemaVersion, DatabaseHelper.GetSchemaVersion(_db));
        Assert.Equal(1, AuditRowCount(_db));
        Assert.Equal(DatabaseHelper.CurrentSchemaVersion, AuditVersion(_db));
    }

    /// <summary>
    /// 更贴近阶段 0 之前的老库:连 <c>schema_migration</c> 表都不存在。迁移要能把它建出来
    /// 并标记版本 —— 这是"老库首次跑到新版本程序"的真实形态。
    /// </summary>
    [Fact]
    public void LegacyDatabase_WithoutAuditTable_IsUpgraded() {
        Exec(_db, "DROP TABLE IF EXISTS [schema_migration];");
        Exec(_db, "PRAGMA user_version = 0;");
        Assert.False(TableExists(_db, "schema_migration"));
        Assert.Equal(0, DatabaseHelper.GetSchemaVersion(_db));

        DatabaseHelper.MigrateSchemaIfNeeded(_db);

        Assert.True(TableExists(_db, "schema_migration"));
        Assert.Equal(DatabaseHelper.CurrentSchemaVersion, DatabaseHelper.GetSchemaVersion(_db));
        Assert.Equal(1, AuditRowCount(_db));
    }

    // ————————————————————————— 幂等 —————————————————————————

    /// <summary>
    /// 每次启动都会调用,必须幂等:连调两次,审计表**仍只有 1 行**(不是 2 行)、版本不变。
    /// </summary>
    [Fact]
    public void MigrateSchemaIfNeeded_IsIdempotent() {
        MakeLegacy(_db);

        DatabaseHelper.MigrateSchemaIfNeeded(_db);
        DatabaseHelper.MigrateSchemaIfNeeded(_db);

        Assert.Equal(DatabaseHelper.CurrentSchemaVersion, DatabaseHelper.GetSchemaVersion(_db));
        Assert.Equal(1, AuditRowCount(_db));
    }

    /// <summary>对已是当前版本的库调用,什么都不做(不新增审计行、不报错)。</summary>
    [Fact]
    public void MigrateSchemaIfNeeded_OnCurrentVersion_IsNoOp() {
        var before = AuditRowCount(_db);

        DatabaseHelper.MigrateSchemaIfNeeded(_db);

        Assert.Equal(DatabaseHelper.CurrentSchemaVersion, DatabaseHelper.GetSchemaVersion(_db));
        Assert.Equal(before, AuditRowCount(_db));
    }

    // ————————————————————————— 不降级 —————————————————————————

    /// <summary>
    /// 库版本**高于**本程序支持的版本时,DB 层只保证"不破坏" —— 版本保持不动。
    /// 拒绝打开是 VM 层的职责(<c>PrepareDatabaseAsync</c> 的前向保护),不在这里报错。
    /// </summary>
    [Fact]
    public void MigrateSchemaIfNeeded_DoesNotTouchNewerVersion() {
        var newer = DatabaseHelper.CurrentSchemaVersion + 1;
        Exec(_db, $"PRAGMA user_version = {newer};");

        DatabaseHelper.MigrateSchemaIfNeeded(_db);

        Assert.Equal(newer, DatabaseHelper.GetSchemaVersion(_db));
    }

    // ————————————————————————— 读版本要稳(不抛) —————————————————————————

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void GetSchemaVersion_BlankPath_ReturnsZero(string path) {
        Assert.Equal(0, DatabaseHelper.GetSchemaVersion(path));
    }

    [Fact]
    public void GetSchemaVersion_MissingFile_ReturnsZero() {
        Assert.Equal(0, DatabaseHelper.GetSchemaVersion(Path.Combine(_dir, "not-there.dat")));
    }

    /// <summary>
    /// 文件存在但**不是 SQLite**(例如用户手滑把别的文件改名成 .dat):返回 0 且**不抛** ——
    /// 损坏 / 非库文件交给后续流程按原版方式报错,不在这里引入新的崩溃点。
    /// </summary>
    [Fact]
    public void GetSchemaVersion_NonSqliteFile_ReturnsZeroWithoutThrowing() {
        var notADb = Path.Combine(_dir, "not-a-db.dat");
        File.WriteAllText(notADb, "this is definitely not a sqlite database file");

        Assert.Equal(0, DatabaseHelper.GetSchemaVersion(notADb));
    }

    // ————————————————————————— 事务原子性 —————————————————————————

    /// <summary>
    /// 迁移后「审计表存在」「版本号到位」「审计行就位」三者必须**同时**成立。
    /// 若实现写成"先提交建表、再单独设 <c>user_version</c>",或反过来,这条能捕捉到不一致。
    /// </summary>
    [Fact]
    public void MigrateSchemaIfNeeded_LeavesTableAndVersionConsistent() {
        MakeLegacy(_db);

        DatabaseHelper.MigrateSchemaIfNeeded(_db);

        Assert.True(TableExists(_db, "schema_migration"));
        Assert.Equal(DatabaseHelper.CurrentSchemaVersion, DatabaseHelper.GetSchemaVersion(_db));
        Assert.Equal(1, AuditRowCount(_db));
    }

    // ————————————————————————— helpers —————————————————————————

    /// <summary>把库伪装成阶段 0 之前的老库:版本压回 0、清空审计行。</summary>
    private static void MakeLegacy(string dbPath) {
        Exec(dbPath, "PRAGMA user_version = 0;");
        Exec(dbPath, "DELETE FROM [schema_migration];");
    }

    private static void Exec(string dbPath, string sql) {
        using var conn = new SqliteConnection(DatabaseHelper.GetConnectionString(dbPath));
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static int AuditRowCount(string dbPath) => Scalar(dbPath, "SELECT COUNT(*) FROM [schema_migration];");

    private static int AuditVersion(string dbPath) => Scalar(dbPath, "SELECT [version] FROM [schema_migration];");

    private static bool TableExists(string dbPath, string name) =>
        Scalar(dbPath, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = @name;",
            ("@name", name)) > 0;

    private static int Scalar(string dbPath, string sql, params (string Name, object Value)[] parameters) {
        using var conn = new SqliteConnection(DatabaseHelper.GetConnectionString(dbPath));
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in parameters) {
            cmd.Parameters.AddWithValue(name, value);
        }
        return Convert.ToInt32(cmd.ExecuteScalar());
    }
}
