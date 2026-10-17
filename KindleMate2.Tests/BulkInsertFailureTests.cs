using Microsoft.Data.Sqlite;
using Xunit;
using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Infrastructure.Helpers;
using KindleMate2.Infrastructure.Repositories.KM2DB;

namespace KindleMate2.Tests;

/// <summary>
/// 回归:批量插入的「逐条降级」兜底只应跳过**可跳过**的失败(重复主键 key / key 为空),
/// 磁盘满、库被锁等系统性问题必须向上抛,不能伪装成「导入成功、只少几条」。
/// </summary>
public sealed class BulkInsertFailureTests : IDisposable {
    private readonly string _dir;

    public BulkInsertFailureTests() {
        _dir = Path.Combine(Path.GetTempPath(), "km2-tests-" + Guid.NewGuid().ToString("N")[..10]);
        Directory.CreateDirectory(_dir);
    }

    public void Dispose() {
        try { Directory.Delete(_dir, true); } catch { /* best effort */ }
    }

    [Theory]
    [InlineData(19, true)]   // SQLITE_CONSTRAINT —— 重复 key(真实异常的 SqliteErrorCode 一般就是主码)
    [InlineData(2067, true)] // SQLITE_CONSTRAINT_UNIQUE(19 | 8<<8)—— 防御性:容忍扩展码混进该属性
    [InlineData(1555, true)] // SQLITE_CONSTRAINT_PRIMARYKEY(19 | 6<<8)—— 同上
    [InlineData(5, false)]   // SQLITE_BUSY —— 库被锁,不得吞
    [InlineData(13, false)]  // SQLITE_FULL —— 磁盘满,不得吞
    [InlineData(10, false)]  // SQLITE_IOERR
    public void IsSkippableInsertFailure_Policy(int code, bool expected) {
        var ex = new SqliteException("err", code);
        Assert.Equal(expected, DatabaseHelper.IsSkippableInsertFailure(ex));
    }

    /// <summary>
    /// 收窄后的策略:只有 SQLite 约束冲突可跳过。**任意 <see cref="InvalidOperationException"/>
    /// 不再可跳过** —— 旧实现范围过宽,会把真实故障吞成"导入成功、只少几条"。
    /// "数据非法(空 key)"改由各仓储的逐条降级循环**显式跳过**(见下方 NullKey 用例)。
    /// </summary>
    [Fact]
    public void IsSkippableInsertFailure_OnlyConstraintConflicts() {
        Assert.False(DatabaseHelper.IsSkippableInsertFailure(new InvalidOperationException()));
        Assert.False(DatabaseHelper.IsSkippableInsertFailure(new IOException("disk full")));
        Assert.False(DatabaseHelper.IsSkippableInsertFailure(new OutOfMemoryException()));
    }

    [Fact]
    public void Add_DuplicateKey_SkipsDupAndKeepsOthers() {
        var path = Path.Combine(_dir, "dup.db");
        Assert.True(DatabaseHelper.CreateDatabase(path, out var ex), ex.Message);
        var repo = new ClippingRepository(DatabaseHelper.GetConnectionString(path));

        repo.Add(new Clipping { Key = "k1", Content = "existing" });

        // 快路径整批事务会撞 k1 回滚 → 降级逐条:k2 进、k1 跳过
        var inserted = repo.Add(new List<Clipping> {
            new() { Key = "k1", Content = "dup" },
            new() { Key = "k2", Content = "new" }
        });

        Assert.Equal(1, inserted);
        Assert.NotNull(repo.GetByKey("k2"));
    }

    [Fact]
    public void Add_NullKey_IsSkippedAndDoesNotAbortBatch() {
        var path = Path.Combine(_dir, "nullkey.db");
        Assert.True(DatabaseHelper.CreateDatabase(path, out var ex), ex.Message);
        var repo = new ClippingRepository(DatabaseHelper.GetConnectionString(path));

        var inserted = repo.Add(new List<Clipping> {
            new() { Key = null!, Content = "no key" },
            new() { Key = "ok", Content = "valid" }
        });

        Assert.Equal(1, inserted);
        Assert.NotNull(repo.GetByKey("ok"));
    }

    // ——————————————————— lookups / vocab 的逐条降级(与 clippings 对称) ———————————————————

    /// <summary>
    /// lookups 批量插入遇 UNIQUE 冲突 + 数据非法时**不再整批回滚**:冲突项被 OR IGNORE 跳过、
    /// 空 word_key 的行被显式跳过,合法行照常插入。旧实现下空 word_key 会让整批回滚,一条都进不去。
    /// </summary>
    [Fact]
    public void LookupBatchAdd_MixedDuplicateAndInvalid_SkipsBothAndKeepsValid() {
        var path = Path.Combine(_dir, "lookup-dup.db");
        Assert.True(DatabaseHelper.CreateDatabase(path, out var ex), ex.Message);
        var repo = new LookupRepository(DatabaseHelper.GetConnectionString(path));

        repo.Add(new Lookup { WordKey = "dup", Timestamp = "2026-09-01 10:00:00", Title = "orig" });

        var inserted = repo.Add(new List<Lookup> {
            new() { WordKey = "dup", Timestamp = "2026-09-01 10:00:00", Title = "dupe" }, // UNIQUE(word_key,timestamp) 冲突
            new() { WordKey = null!, Timestamp = "2026-09-01 10:00:01", Title = "bad" },  // 数据非法
            new() { WordKey = "new", Timestamp = "2026-09-01 10:00:02", Title = "good" }  // 正常
        });

        Assert.Equal(1, inserted);
        Assert.Equal("orig", repo.GetByWordKey("dup")!.Title); // 冲突项未覆盖既有行
        Assert.NotNull(repo.GetByWordKey("new"));
    }

    /// <summary>vocab 批量插入的同类降级:主键冲突 + 数据非法都跳过,合法行照常进。</summary>
    [Fact]
    public void VocabBatchAdd_MixedDuplicateAndInvalid_SkipsBothAndKeepsValid() {
        var path = Path.Combine(_dir, "vocab-dup.db");
        Assert.True(DatabaseHelper.CreateDatabase(path, out var ex), ex.Message);
        var repo = new VocabRepository(DatabaseHelper.GetConnectionString(path));

        repo.Add(new Vocab { Id = "v1", Word = "apple", WordKey = "en:apple" });

        var inserted = repo.Add(new List<Vocab> {
            new() { Id = "v1", Word = "apple2", WordKey = "en:apple2" },  // 主键 id 冲突
            new() { Id = null!, Word = "bad", WordKey = "en:bad" },      // 数据非法
            new() { Id = "v2", Word = "banana", WordKey = "en:banana" }  // 正常
        });

        Assert.Equal(1, inserted);
        Assert.Equal("apple", repo.GetById("v1")!.Word); // 冲突项未覆盖既有行
        Assert.NotNull(repo.GetById("v2"));
    }

    // ——————————————————— 系统性失败必须向上抛(两段式降级的核心承诺) ———————————————————

    /// <summary>
    /// 制造一个**确定性的非约束失败**用于验证"不吞系统性故障":把目标表删掉,后续 INSERT 会抛
    /// SQLITE_ERROR(1) —— 真实世界的对应物是磁盘满 / 只读库 / IO 错,它们同样不在可跳过集合里。
    /// (不用"只读库文件"模拟:同进程内 Microsoft.Data.Sqlite 的连接池会保留可写句柄,
    /// Windows 上只读属性对已池化路径无效,那样写出来的用例会假绿。)
    /// </summary>
    private static void DropTable(string dbPath, string table) {
        using var conn = new SqliteConnection(DatabaseHelper.GetConnectionString(dbPath));
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"DROP TABLE [{table}];";
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// lookups 批量插入遇到非可跳过失败时必须抛出来,不能吞成"导入成功、只少几条"。
    /// 快路径整批失败后进入逐条降级,降级循环的第一条仍失败 → 因不在可跳过集合内而向上抛。
    /// </summary>
    [Fact]
    public void LookupBatchAdd_SystemicFailure_PropagatesInsteadOfBeingSwallowed() {
        var path = Path.Combine(_dir, "lookup-failure.db");
        Assert.True(DatabaseHelper.CreateDatabase(path, out var ex), ex.Message);
        DropTable(path, "lookups");
        var repo = new LookupRepository(DatabaseHelper.GetConnectionString(path));

        var thrown = Assert.Throws<SqliteException>(() => repo.Add(new List<Lookup> {
            new() { WordKey = "en:apple", Timestamp = "2026-01-01 10:00:00" }
        }));
        Assert.False(DatabaseHelper.IsSkippableInsertFailure(thrown),
            "系统性失败(SQLite 错误 " + thrown.SqliteErrorCode + ")不得被当作可跳过");
    }

    /// <summary>vocab 批量插入的同一承诺:非可跳过的系统性失败必须向上抛。</summary>
    [Fact]
    public void VocabBatchAdd_SystemicFailure_PropagatesInsteadOfBeingSwallowed() {
        var path = Path.Combine(_dir, "vocab-failure.db");
        Assert.True(DatabaseHelper.CreateDatabase(path, out var ex), ex.Message);
        DropTable(path, "vocab");
        var repo = new VocabRepository(DatabaseHelper.GetConnectionString(path));

        var thrown = Assert.Throws<SqliteException>(() => repo.Add(new List<Vocab> {
            new() { Id = "v1", Word = "apple", WordKey = "en:apple" }
        }));
        Assert.False(DatabaseHelper.IsSkippableInsertFailure(thrown),
            "系统性失败(SQLite 错误 " + thrown.SqliteErrorCode + ")不得被当作可跳过");
    }
}
