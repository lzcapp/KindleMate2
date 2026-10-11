using Microsoft.Data.Sqlite;
using Xunit;
using KindleMate2.Infrastructure.Helpers;

namespace KindleMate2.Tests;

/// <summary>
/// 结构完整性检查(<see cref="DatabaseHelper.TryDescribeMissingTables"/>)的用例。
///
/// <para>
/// 这个检查只有一个用途:**库打不开时把「结构损坏」这一种成因识别出来**,好让界面给用户一条
/// 看得懂、能照着做的提示(而不是 <c>SQLite Error 1: 'no such table: vocab'.</c>)。它**只读**,
/// 不补表、不动版本号 —— 补出来的是空表、原数据已丢,要不要修由用户决定。
/// </para>
/// <para>
/// 期望表名取自建表脚本本身(不另维护一份列表),所以下面那条"全空库应报出全部表"的用例同时是
/// **漂移守卫**:将来新增一张表时它会红,逼着人回来看解析是否仍然成立。
/// </para>
/// </summary>
public sealed class SchemaIntegrityTests : IDisposable {
    private readonly string _dir;
    private readonly string _db;

    /// <summary>建表脚本里声明的全部表,按脚本顺序 —— 与 <c>DatabaseHelper.GetTableCreationScripts()</c> 一致。</summary>
    private static readonly string[] AllExpectedTables = [
        "clippings", "lookups", "original_clipping_lines", "settings", "vocab",
    ];

    public SchemaIntegrityTests() {
        _dir = Path.Combine(Path.GetTempPath(), "km2-schema-integrity-tests-" + Guid.NewGuid().ToString("N")[..10]);
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

    // ————————————————————————— 正常库 —————————————————————————

    [Fact]
    public void HealthyDatabase_ReportsNothingMissing() {
        Assert.False(DatabaseHelper.TryDescribeMissingTables(_db, out var missing));
        Assert.Empty(missing);
    }

    // ————————————————————————— 缺表 —————————————————————————

    /// <summary>
    /// 核心场景:库结构被破坏(某张业务表没了)。这正是 issue #148 描述的那条路径 ——
    /// 版本号可能仍说"已是最新",于是没有任何环节会去补它。
    /// </summary>
    [Fact]
    public void DroppedTable_IsReported() {
        Exec(_db, "DROP TABLE [vocab];");

        Assert.True(DatabaseHelper.TryDescribeMissingTables(_db, out var missing));
        Assert.Equal(["vocab"], missing.ToArray());
    }

    [Fact]
    public void MultipleDroppedTables_AreReportedInScriptOrder() {
        Exec(_db, "DROP TABLE [settings];");
        Exec(_db, "DROP TABLE [clippings];");

        Assert.True(DatabaseHelper.TryDescribeMissingTables(_db, out var missing));
        // 顺序按建表脚本(不是 DROP 的顺序),这样提示里的表名稳定、便于比对。
        Assert.Equal(["clippings", "settings"], missing.ToArray());
    }

    /// <summary>
    /// 全空库应报出**建表脚本声明的每一张表** —— 既是基本行为,也是**漂移守卫**:
    /// 将来给 <c>GetTableCreationScripts()</c> 添一张表,这条会红,从而提醒更新
    /// <see cref="AllExpectedTables"/> 并复核解析逻辑。
    /// </summary>
    [Fact]
    public void EmptyDatabase_ReportsEveryExpectedTable() {
        foreach (var name in AllExpectedTables) {
            Exec(_db, $"DROP TABLE IF EXISTS [{name}];");
        }

        Assert.True(DatabaseHelper.TryDescribeMissingTables(_db, out var missing));
        Assert.Equal(AllExpectedTables, missing.ToArray());
    }

    /// <summary>
    /// 索引缺失**不算**结构损坏:索引由 <c>EnsureIndexesIfNeeded</c> 每次启动补齐,
    /// 不该把"库能正常打开"误报成结构损坏。
    /// </summary>
    [Fact]
    public void MissingIndex_IsNotReportedAsStructuralDamage() {
        Exec(_db, "DROP INDEX IF EXISTS [ix_vocab_word_key];");

        Assert.False(DatabaseHelper.TryDescribeMissingTables(_db, out var missing));
        Assert.Empty(missing);
    }

    // ————————————————————————— 读不出时不硬猜 —————————————————————————

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankPath_ReturnsFalse(string path) {
        Assert.False(DatabaseHelper.TryDescribeMissingTables(path, out var missing));
        Assert.Empty(missing);
    }

    [Fact]
    public void MissingFile_ReturnsFalse() {
        Assert.False(DatabaseHelper.TryDescribeMissingTables(Path.Combine(_dir, "not-there.dat"), out _));
    }

    /// <summary>
    /// 文件存在但**不是 SQLite**:识别不了就不硬猜 —— 返回 false,让上层沿用原来的报错,
    /// 而不是把"读不出"误报成"缺表"。
    /// </summary>
    [Fact]
    public void NonSqliteFile_ReturnsFalseWithoutThrowing() {
        var notADb = Path.Combine(_dir, "not-a-db.dat");
        File.WriteAllText(notADb, "this is definitely not a sqlite database file");

        Assert.False(DatabaseHelper.TryDescribeMissingTables(notADb, out var missing));
        Assert.Empty(missing);
    }

    // ————————————————————————— helpers —————————————————————————

    private static void Exec(string dbPath, string sql) {
        using var conn = new SqliteConnection(DatabaseHelper.GetConnectionString(dbPath));
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
