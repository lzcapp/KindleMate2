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
    [InlineData(19, true)]  // SQLITE_CONSTRAINT —— 重复 key
    [InlineData(5, false)]  // SQLITE_BUSY —— 库被锁,不得吞
    [InlineData(13, false)] // SQLITE_FULL —— 磁盘满,不得吞
    [InlineData(10, false)] // SQLITE_IOERR
    public void IsSkippableInsertFailure_Policy(int code, bool expected) {
        var ex = new SqliteException("err", code);
        Assert.Equal(expected, DatabaseHelper.IsSkippableInsertFailure(ex));
    }

    [Fact]
    public void IsSkippableInsertFailure_InvalidDataAndSystemic() {
        Assert.True(DatabaseHelper.IsSkippableInsertFailure(new InvalidOperationException()));
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
}
