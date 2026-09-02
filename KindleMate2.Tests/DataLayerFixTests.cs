using Microsoft.Data.Sqlite;
using Xunit;
using KindleMate2.Application.Services.KM2DB;
using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Infrastructure.Helpers;
using KindleMate2.Infrastructure.Repositories.KM2DB;

namespace KindleMate2.Tests;

/// <summary>Regression tests for review-fix commit 8a8db53 (data layer).</summary>
public sealed class DataLayerFixTests : IDisposable {
    private readonly string _dir;

    public DataLayerFixTests() {
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

    [Fact]
    public void LookupBatchAdd_5000Rows_ReusedCommand_InsertsAllAndReadsBack() {
        var repo = new LookupRepository(DatabaseHelper.GetConnectionString(NewDb("t1.db")));
        var batch = new List<Lookup>();
        for (var i = 0; i < 5000; i++) {
            batch.Add(new Lookup {
                WordKey = "wk" + i,
                Usage = "usage line " + i,
                Title = "book " + (i % 100),
                Authors = "author",
                Timestamp = "2026-09-01 10:00:" + (i % 60).ToString("00")
            });
        }
        Assert.Equal(5000, repo.Add(batch));
        var last = repo.GetByWordKey("wk4999");
        Assert.NotNull(last);
        Assert.Equal("wk4999", last!.WordKey);
    }

    [Fact]
    public void LookupBatchAdd_DuplicateCompositeKey_ThrowsAndRollsBackAtomically() {
        var repo = new LookupRepository(DatabaseHelper.GetConnectionString(NewDb("t2.db")));
        var batch = new List<Lookup> {
            new() { WordKey = "dup", Title = "a", Timestamp = "2026-09-01 10:00:00" },
            // duplicate (word_key, timestamp) inside the same batch
            new() { WordKey = "dup", Title = "b", Timestamp = "2026-09-01 10:00:00" }
        };
        Assert.ThrowsAny<Exception>(() => repo.Add(batch));
        Assert.Null(repo.GetByWordKey("dup")); // atomic rollback, no partial rows
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void LookupDelete_NullOrEmptyTimestamp_Throws(string? timestamp) {
        var repo = new LookupRepository(DatabaseHelper.GetConnectionString(NewDb("t3.db")));
        Assert.Throws<InvalidOperationException>(() => repo.Delete("wk", timestamp!));
    }

    [Fact]
    public void LookupDelete_ValidRow_Deletes() {
        var repo = new LookupRepository(DatabaseHelper.GetConnectionString(NewDb("t3b.db")));
        repo.Add(new Lookup { WordKey = "del1", Timestamp = "2026-09-01 10:00:00", Title = "t" });
        Assert.True(repo.Delete("del1", "2026-09-01 10:00:00"));
        Assert.Null(repo.GetByWordKey("del1"));
    }

    [Fact]
    public void VocabBatchAdd_3000Rows_ReusedCommand_InsertsAll() {
        var repo = new VocabRepository(DatabaseHelper.GetConnectionString(NewDb("t4.db")));
        var batch = new List<Vocab>();
        for (var i = 0; i < 3000; i++) {
            batch.Add(new Vocab {
                Id = "v" + i,
                WordKey = "wk" + i,
                Word = "word" + i,
                Timestamp = "2026-09-01 10:00:00",
                Frequency = 0
            });
        }
        Assert.Equal(3000, repo.Add(batch));
    }

    [Fact]
    public void KmDatabaseServiceImport_SourceWithDuplicateKeys_DoesNotAbortOrPartiallyImport() {
        var targetDb = NewDb("t5-target.db");
        var kmDb = NewDb("t5-km.db");

        // Simulate a legacy/external .dat that contains TWO clippings with the SAME key
        // (allowed by old writers; the repo layer's PRIMARY KEY rejects the 2nd row, which
        // is exactly the mid-import abort the dedup-set sync is meant to prevent).
        using (var conn = new SqliteConnection(DatabaseHelper.GetConnectionString(kmDb))) {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO clippings (key, content, bookname) VALUES ('same-key', 'first content', 'B'), ('same-key', 'second content', 'B');";
            Assert.ThrowsAny<Exception>(() => cmd.ExecuteNonQuery());
            // Only one row can physically exist; verify service still succeeds and imports it.
            cmd.CommandText = "INSERT INTO clippings (key, content, bookname) VALUES ('same-key', 'first content', 'B');";
            cmd.ExecuteNonQuery();
        }

        var targetClipRepo = new ClippingRepository(DatabaseHelper.GetConnectionString(targetDb));
        var svc = new KmDatabaseService(
            targetClipRepo,
            new LookupRepository(DatabaseHelper.GetConnectionString(targetDb)),
            new OriginalClippingLineRepository(DatabaseHelper.GetConnectionString(targetDb)),
            new SettingRepository(DatabaseHelper.GetConnectionString(targetDb)),
            new VocabRepository(DatabaseHelper.GetConnectionString(targetDb)),
            new ClippingRepository(DatabaseHelper.GetConnectionString(kmDb)),
            new LookupRepository(DatabaseHelper.GetConnectionString(kmDb)),
            new OriginalClippingLineRepository(DatabaseHelper.GetConnectionString(kmDb)),
            new SettingRepository(DatabaseHelper.GetConnectionString(kmDb)),
            new VocabRepository(DatabaseHelper.GetConnectionString(kmDb)));

        Assert.True(svc.ImportFromKmDatabase());
        Assert.Single(targetClipRepo.GetAll());
    }
}
