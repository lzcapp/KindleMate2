using Microsoft.Data.Sqlite;
using Xunit;
using KindleMate2.Application.Services.KM2DB;
using KindleMate2.Infrastructure.Helpers;
using KindleMate2.Infrastructure.Repositories.KM2DB;

namespace KindleMate2.Tests;

/// <summary>
/// 回归:导入原版 KM / KM2 扁平库时,lookups 的判重身份是 (word_key, timestamp) ——
/// 同一词可以出现在不同时间。之前只按 word_key 判重,会把同词不同时间的查询记录整批丢掉。
/// </summary>
public sealed class KmImportLookupDedupTests : IDisposable {
    private readonly string _dir;

    public KmImportLookupDedupTests() {
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

    [Fact]
    public void Import_SameWordDifferentTimestamps_KeepsAllLookups() {
        var targetDb = NewDb("target.db");
        var kmDb = NewDb("km.db");

        using (var conn = new SqliteConnection(DatabaseHelper.GetConnectionString(kmDb))) {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "INSERT INTO lookups (word_key, usage, title, authors, timestamp) VALUES " +
                "('en:word', 'u1', 'Book', 'A', '2026-01-01 10:00:00'), " +
                "('en:word', 'u2', 'Book', 'A', '2026-01-02 10:00:00');";
            cmd.ExecuteNonQuery();
        }

        var svc = NewService(targetDb, kmDb);
        Assert.True(svc.ImportFromKmDatabase());

        var targetRepo = new LookupRepository(DatabaseHelper.GetConnectionString(targetDb));
        var imported = targetRepo.GetAll().Where(l => l.WordKey == "en:word").ToList();
        Assert.Equal(2, imported.Count);

        // 幂等:同一源库再导一次不应重复。
        Assert.True(svc.ImportFromKmDatabase());
        Assert.Equal(2, targetRepo.GetAll().Count(l => l.WordKey == "en:word"));
    }
}
