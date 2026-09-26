using Xunit;
using KindleMate2.Application.Services.KM2DB;
using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Infrastructure.Helpers;
using KindleMate2.Infrastructure.Repositories.KM2DB;

namespace KindleMate2.Tests;

/// <summary>
/// 回归:lookups 无主键,身份是 (word_key, timestamp)。<see cref="LookupRepository.Update"/>
/// 之前只按 word_key 定位,会把同词全部查询行一起改写、并把 timestamp 压成一个值,撞
/// UNIQUE(word_key, timestamp)。修复后按 (word_key, timestamp) 精确定位单行。
/// </summary>
public sealed class LookupUpdateTests : IDisposable {
    private readonly string _dir;

    public LookupUpdateTests() {
        _dir = Path.Combine(Path.GetTempPath(), "km2-tests-" + Guid.NewGuid().ToString("N")[..10]);
        Directory.CreateDirectory(_dir);
    }

    public void Dispose() {
        try { Directory.Delete(_dir, true); } catch { /* best effort */ }
    }

    [Fact]
    public void Update_SameWordDifferentTimestamps_UpdatesOnlyTargetRow() {
        var path = Path.Combine(_dir, "lookups.db");
        Assert.True(DatabaseHelper.CreateDatabase(path, out var ex), ex.Message);
        var repo = new LookupRepository(DatabaseHelper.GetConnectionString(path));

        repo.Add(new Lookup { WordKey = "en:word", Usage = "sentence A", Title = "Book", Authors = "A", Timestamp = "2026-01-01 10:00:00" });
        repo.Add(new Lookup { WordKey = "en:word", Usage = "sentence B", Title = "Book", Authors = "A", Timestamp = "2026-01-02 10:00:00" });

        var target = repo.GetByWordKey("en:word")!;
        target.Usage = "edited";
        target.Title = "New Book";
        Assert.True(repo.Update(target));

        var rows = repo.GetAll().Where(l => l.WordKey == "en:word").OrderBy(l => l.Timestamp).ToList();
        Assert.Equal(2, rows.Count);

        // 只有 timestamp 匹配的那一行被改;另一行原样保留,且两行 timestamp 不再被压成同一个值。
        var edited = rows.Single(r => r.Timestamp == "2026-01-01 10:00:00");
        var untouched = rows.Single(r => r.Timestamp == "2026-01-02 10:00:00");
        Assert.Equal("edited", edited.Usage);
        Assert.Equal("New Book", edited.Title);
        Assert.Equal("sentence B", untouched.Usage);
        Assert.Equal("Book", untouched.Title);
    }

    [Fact]
    public void RenameBook_WordWithMultipleLookups_DoesNotCollideOnUniqueConstraint() {
        var path = Path.Combine(_dir, "rename.db");
        Assert.True(DatabaseHelper.CreateDatabase(path, out var ex), ex.Message);
        var repo = new LookupRepository(DatabaseHelper.GetConnectionString(path));
        var service = new LookupService(repo);

        repo.Add(new Lookup { WordKey = "en:word", Usage = "sentence A", Title = "Old Book", Authors = "A", Timestamp = "2026-01-01 10:00:00" });
        repo.Add(new Lookup { WordKey = "en:word", Usage = "sentence B", Title = "Old Book", Authors = "A", Timestamp = "2026-01-02 10:00:00" });

        // 同书、同词、两条不同 timestamp 的记录:旧实现会在重命名时把两条压成一个 timestamp 并抛异常。
        Assert.True(service.RenameBook("Old Book", "New Book", "New Author"));

        var rows = repo.GetAll();
        Assert.Equal(2, rows.Count);
        Assert.All(rows, r => Assert.Equal("New Book", r.Title));
        Assert.All(rows, r => Assert.Equal("New Author", r.Authors));
        // 两个 timestamp 原样保留(未互相覆盖)
        Assert.Equal(2, rows.Select(r => r.Timestamp).Distinct().Count());
    }
}
