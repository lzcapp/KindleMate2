using Microsoft.Data.Sqlite;
using Xunit;
using KindleMate2.Application.Services.KM2DB;
using KindleMate2.Infrastructure.Helpers;
using KindleMate2.Infrastructure.Repositories.KM2DB;

namespace KindleMate2.Tests;

/// <summary>
/// Tests for importing a KMate (kmate.io) km3.dat database into the current KM2 database
/// via <see cref="KmateDatabaseService"/> / <see cref="KmateSourceReader"/>.
/// The km3 source database is synthesized with its real-world schema shape
/// (extra columns, INTEGER vocab.id, TEXT colorRGB, no settings table).
/// </summary>
public sealed class KmateImportTests : IDisposable {
    private readonly string _dir;

    public KmateImportTests() {
        _dir = Path.Combine(Path.GetTempPath(), "kmate-import-tests-" + Guid.NewGuid().ToString("N")[..10]);
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

    /// <summary>Creates an empty KMate-shaped km3 database and returns its path.</summary>
    private string NewKmateDb(string name) {
        var path = Path.Combine(_dir, name);
        using (var conn = new SqliteConnection($"Data Source={path};Mode=ReadWriteCreate;")) {
            conn.Open();
            foreach (var ddl in KmateSampleDdl) {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = ddl;
                cmd.ExecuteNonQuery();
            }
        }
        return path;
    }

    private static readonly string[] KmateSampleDdl = [
        """
        CREATE TABLE clippings (
            key TEXT PRIMARY KEY, content TEXT, bookname TEXT, authorname TEXT,
            brieftype INTEGER, clippingdate TEXT, read INT DEFAULT 0, tag TEXT,
            sync INT DEFAULT 0, newbookname TEXT, colorRGB TEXT DEFAULT '',
            pagenumber INT DEFAULT 0, source TEXT DEFAULT 'Kindle',
            note_parent_key TEXT, ck_synced INT DEFAULT 0, created_at TEXT, updated_at TEXT)
        """,
        """
        CREATE TABLE original_clipping_lines (
            key TEXT PRIMARY KEY, line1 TEXT, line2 TEXT, line3 TEXT, line4 TEXT, line5 TEXT,
            created_at TEXT, updated_at TEXT)
        """,
        """
        CREATE TABLE lookups (
            id INTEGER PRIMARY KEY AUTOINCREMENT, word_key TEXT, usage TEXT, title TEXT,
            authors TEXT, timestamp TEXT, ck_synced INT DEFAULT 0)
        """,
        """
        CREATE TABLE vocab (
            id INTEGER PRIMARY KEY AUTOINCREMENT, word_key TEXT, word TEXT, stem TEXT,
            category INTEGER DEFAULT 0, translation TEXT, translation_plain TEXT,
            dict_source TEXT, timestamp TEXT, frequency INT DEFAULT 0, sync INT DEFAULT 0,
            ck_synced INT DEFAULT 0, colorRGB TEXT DEFAULT '', last_reviewed TEXT,
            review_count INT DEFAULT 0, word_level INT DEFAULT 0, created_at TEXT, updated_at TEXT)
        """,
        "CREATE TABLE books (id INTEGER PRIMARY KEY, title TEXT, author TEXT)",
        "CREATE TABLE tags_clippings (id INTEGER PRIMARY KEY, name TEXT)",
        "CREATE TABLE pending_deletions (record_name TEXT PRIMARY KEY, record_type TEXT)"
    ];

    private static void Execute(SqliteConnection conn, string sql) {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    [Fact]
    public void ImportKmateDatabase_MapsClippingsLinesLookupsVocab_AndSkipsOrphansAndEmptyContent() {
        var targetDb = NewDb("kt-target.db");
        var kmateDb = NewKmateDb("kt-kmate.db");

        using (var conn = new SqliteConnection($"Data Source={kmateDb};Mode=ReadWrite;")) {
            conn.Open();
            // Two valid clippings (highlight + note) with matching raw lines…
            Execute(conn, "INSERT INTO clippings (key, content, bookname, authorname, brieftype, clippingdate, colorRGB, pagenumber, source) VALUES ('a1b2c3d4e5f60718293a4b5c6d7e8f90', 'first highlight', 'Book A', 'Author A', 0, '2026-08-20 10:00:00', '', 12, 'Kindle')");
            Execute(conn, "INSERT INTO clippings (key, content, bookname, authorname, brieftype, clippingdate, colorRGB, pagenumber, source) VALUES ('b2c3d4e5f60718293a4b5c6d7e8f90a1', 'a note text', 'Book A', 'Author A', 1, '2026-08-20 10:05:00', '', 12, 'Kindle')");
            // …an empty-content row (skipped by the service)…
            Execute(conn, "INSERT INTO clippings (key, content, bookname, brieftype, clippingdate, source) VALUES ('c3d4e5f60718293a4b5c6d7e8f90a1b2', '', 'Book A', 0, '2026-08-20 10:10:00', 'Kindle')");
            // …one orphan line whose key no longer exists in clippings (KMate cleanup residue)…
            Execute(conn, "INSERT INTO original_clipping_lines (key, line1) VALUES ('ffffffffffffffffffffffffffffffff', 'orphan line')");
            Execute(conn, "INSERT INTO original_clipping_lines (key, line1, line2, line3) VALUES ('a1b2c3d4e5f60718293a4b5c6d7e8f90', 'Book A (Author A)', '', 'first highlight')");
            Execute(conn, "INSERT INTO original_clipping_lines (key, line1) VALUES ('b2c3d4e5f60718293a4b5c6d7e8f90a1', 'Book A (Author A)')");
            // lookups (zh: prefix on word_key) and vocab (INTEGER id identity)
            Execute(conn, "INSERT INTO lookups (word_key, usage, title, authors, timestamp) VALUES ('zh:测试', 'usage one', 'Book A', 'Author A', '2026-08-20 10:00:00')");
            Execute(conn, "INSERT INTO lookups (word_key, usage, title, authors, timestamp) VALUES ('en:apple', 'usage two', 'Book A', 'Author A', '2026-08-20 10:01:00')");
            Execute(conn, "INSERT INTO vocab (word_key, word, stem, category, translation, timestamp, frequency, colorRGB) VALUES ('zh:测试', '测试', '测', 0, 'test', '2026-08-20 10:00:00', 1, '-1')");
            Execute(conn, "INSERT INTO vocab (word_key, word, stem, category, translation, timestamp, frequency, colorRGB) VALUES ('en:apple', 'apple', 'appl', 0, '', '2026-08-20 10:01:00', 2, '')");
        }

        var targetClipRepo = new ClippingRepository(DatabaseHelper.GetConnectionString(targetDb));
        var targetLineRepo = new OriginalClippingLineRepository(DatabaseHelper.GetConnectionString(targetDb));
        var targetLookupRepo = new LookupRepository(DatabaseHelper.GetConnectionString(targetDb));
        var targetVocabRepo = new VocabRepository(DatabaseHelper.GetConnectionString(targetDb));
        var svc = new KmateDatabaseService(targetClipRepo, targetLookupRepo, targetLineRepo, targetVocabRepo, kmateDb);

        Assert.True(svc.ImportFromKmateDatabase(), "import should succeed");

        Assert.Equal(2, targetClipRepo.GetAll().Count); // empty-content row skipped
        Assert.Equal(2, targetLineRepo.GetAllKeys().Count); // orphan line filtered
        Assert.Equal(2, targetLookupRepo.GetAll().Count);
        Assert.Equal(2, targetVocabRepo.GetAll().Count);

        // vocab id must be the word_key-derived TEXT id, not the source INTEGER id
        Assert.NotNull(targetVocabRepo.GetAll().SingleOrDefault(v => v.Id == "zh:测试"));
        Assert.NotNull(targetVocabRepo.GetAll().SingleOrDefault(v => v.Id == "en:apple"));
        // colorRGB/category normalization applied
        var apple = targetVocabRepo.GetAll().Single(v => v.Id == "en:apple");
        Assert.Equal(-1L, apple.ColorRgb);
    }

    [Fact]
    public void ImportKmateDatabase_CrossBookSameContent_KeepsBothRows() {
        var targetDb = NewDb("kt-crossbook.db");
        var kmateDb = NewKmateDb("kt-crossbook-kmate.db");

        using (var conn = new SqliteConnection($"Data Source={kmateDb};Mode=ReadWrite;")) {
            conn.Open();
            // Identical sentence highlighted in TWO different books: both are distinct highlights
            // and must be kept (dedup scope is per book+author+content, not global content).
            Execute(conn, "INSERT INTO clippings (key, content, bookname, authorname, brieftype, clippingdate, source) VALUES ('aaaa1111222233334444555566667777', 'a shared quote', 'Book A', 'Author A', 0, '2026-08-21 09:00:00', 'Kindle')");
            Execute(conn, "INSERT INTO clippings (key, content, bookname, authorname, brieftype, clippingdate, source) VALUES ('bbbb1111222233334444555566667777', 'a shared quote', 'Book B', 'Author B', 0, '2026-08-21 09:00:05', 'Kindle')");
            // …and a genuine duplicate within the SAME book (same key, same content) stays skipped.
            Execute(conn, "INSERT INTO clippings (key, content, bookname, authorname, brieftype, clippingdate, source) VALUES ('cccc1111222233334444555566667777', 'unique line', 'Book A', 'Author A', 0, '2026-08-21 09:00:10', 'Kindle')");
            Execute(conn, "INSERT INTO original_clipping_lines (key, line1) VALUES ('aaaa1111222233334444555566667777', 'Book A (Author A)')");
            Execute(conn, "INSERT INTO original_clipping_lines (key, line1) VALUES ('bbbb1111222233334444555566667777', 'Book B (Author B)')");
            Execute(conn, "INSERT INTO original_clipping_lines (key, line1) VALUES ('cccc1111222233334444555566667777', 'Book A (Author A)')");
        }

        var clipRepo = new ClippingRepository(DatabaseHelper.GetConnectionString(targetDb));
        var lineRepo = new OriginalClippingLineRepository(DatabaseHelper.GetConnectionString(targetDb));
        var svc = new KmateDatabaseService(clipRepo,
            new LookupRepository(DatabaseHelper.GetConnectionString(targetDb)),
            lineRepo,
            new VocabRepository(DatabaseHelper.GetConnectionString(targetDb)),
            kmateDb);

        Assert.True(svc.ImportFromKmateDatabase());
        Assert.Equal(3, clipRepo.GetAll().Count); // cross-book same content kept (2) + unique (1)
        Assert.Equal(3, lineRepo.GetAllKeys().Count);

        Assert.True(svc.ImportFromKmateDatabase()); // idempotent
        Assert.Equal(3, clipRepo.GetAll().Count);
    }

    [Fact]
    public void ImportKmateDatabase_SecondRun_IsIdempotent() {
        var targetDb = NewDb("kt-target2.db");
        var kmateDb = NewKmateDb("kt-kmate2.db");

        using (var conn = new SqliteConnection($"Data Source={kmateDb};Mode=ReadWrite;")) {
            conn.Open();
            Execute(conn, "INSERT INTO clippings (key, content, bookname, authorname, brieftype, clippingdate, source) VALUES ('11112222333344445555666677778888', 'same highlight', 'Book X', 'Author X', 0, '2026-08-21 09:00:00', 'Kindle')");
            Execute(conn, "INSERT INTO original_clipping_lines (key, line1) VALUES ('11112222333344445555666677778888', 'Book X (Author X)')");
            Execute(conn, "INSERT INTO lookups (word_key, usage, title, timestamp) VALUES ('en:word', 'usage', 'Book X', '2026-08-21 09:00:00')");
            Execute(conn, "INSERT INTO vocab (word_key, word, translation, timestamp, frequency) VALUES ('en:word', 'word', '', '2026-08-21 09:00:00', 1)");
        }

        var clipRepo = new ClippingRepository(DatabaseHelper.GetConnectionString(targetDb));
        var lineRepo = new OriginalClippingLineRepository(DatabaseHelper.GetConnectionString(targetDb));
        var lookupRepo = new LookupRepository(DatabaseHelper.GetConnectionString(targetDb));
        var vocabRepo = new VocabRepository(DatabaseHelper.GetConnectionString(targetDb));
        var svc = new KmateDatabaseService(clipRepo, lookupRepo, lineRepo, vocabRepo, kmateDb);

        Assert.True(svc.ImportFromKmateDatabase());
        Assert.Single(clipRepo.GetAll());
        Assert.Single(vocabRepo.GetAll());

        Assert.True(svc.ImportFromKmateDatabase());
        Assert.Single(clipRepo.GetAll());
        Assert.Single(lineRepo.GetAllKeys());
        Assert.Single(lookupRepo.GetAll());
        Assert.Single(vocabRepo.GetAll());
    }
}
