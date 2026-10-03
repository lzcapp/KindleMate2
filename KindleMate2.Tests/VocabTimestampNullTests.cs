using Microsoft.Data.Sqlite;
using Xunit;
using KindleMate2.Application.Services.KM2DB;
using KindleMate2.Infrastructure.Helpers;
using KindleMate2.Infrastructure.Repositories.VocabDB;
using Km2LookupRepository = KindleMate2.Infrastructure.Repositories.KM2DB.LookupRepository;
using Km2VocabRepository = KindleMate2.Infrastructure.Repositories.KM2DB.VocabRepository;
using VocabLookupRepository = KindleMate2.Infrastructure.Repositories.VocabDB.LookupRepository;

namespace KindleMate2.Tests;

/// <summary>
/// 回归:导入 vocab.db 时,时间戳**无效**的生词/查询记录应当**跳过**。
///
/// 两档都覆盖:
/// <list type="number">
/// <item><b>NULL</b> —— 之前 <c>Word.Timestamp</c>/<c>Lookup.Timestamp</c> 的 setter 把 null
/// 强转成 0,于是 `if (timestamp == null) continue` 永远不触发,NULL 行被当成 1970-01-01 导进来。</item>
/// <item><b>0</b> —— 源库 schema 里 <c>timestamp</c> 是 <c>INTEGER DEFAULT 0</c>(不是 NULL),
/// 所以"没有时间"的行读出来是 0。只判 null 同样会把它们按 1970-01-01 导入。
/// 这一档在真机上未观察到(实测最小值是 2025-08),属防御性覆盖。</item>
/// </list>
/// </summary>
public sealed class VocabTimestampNullTests : IDisposable {
    private readonly string _dir;

    public VocabTimestampNullTests() {
        _dir = Path.Combine(Path.GetTempPath(), "km2-tests-" + Guid.NewGuid().ToString("N")[..10]);
        Directory.CreateDirectory(_dir);
    }

    public void Dispose() {
        try { Directory.Delete(_dir, true); } catch { /* best effort */ }
    }

    [Fact]
    public void ImportWords_InvalidTimestampRows_AreSkippedInsteadOfBecoming1970() {
        var targetDb = Path.Combine(_dir, "target.db");
        Assert.True(DatabaseHelper.CreateDatabase(targetDb, out var ex), ex.Message);
        var targetCs = DatabaseHelper.GetConnectionString(targetDb);

        var sourceDb = Path.Combine(_dir, "vocab.db");
        var sourceCs = DatabaseHelper.BuildConnectionString(sourceDb, SqliteOpenMode.ReadWriteCreate, sharedCache: true);
        using (var conn = new SqliteConnection(sourceCs)) {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE WORDS (id TEXT, word TEXT, stem TEXT, lang TEXT, category INTEGER, timestamp INTEGER, profileid TEXT);
                CREATE TABLE LOOKUPS (id TEXT, word_key TEXT, book_key TEXT, dict_key TEXT, pos TEXT, usage TEXT, timestamp INTEGER);
                CREATE TABLE BOOK_INFO (id TEXT, asin TEXT, guid TEXT, lang TEXT, title TEXT, authors TEXT);
                INSERT INTO WORDS (id, word, timestamp) VALUES
                    ('w1', 'apple', NULL),
                    ('w2', 'banana', 1735689600000),
                    ('w3', 'cherry', 0);
                INSERT INTO LOOKUPS (id, word_key, usage, timestamp) VALUES
                    ('l1', 'en:apple', 'u', NULL),
                    ('l2', 'en:banana', 'u', 1735689600000),
                    ('l3', 'en:cherry', 'u', 0);
                """;
            cmd.ExecuteNonQuery();
        }

        var service = new VocabDatabaseService(
            new BookInfoRepository(sourceCs),
            new VocabLookupRepository(sourceCs),
            new WordRepository(sourceCs),
            new Km2LookupRepository(targetCs),
            new Km2VocabRepository(targetCs));

        Assert.True(service.ImportKindleWords(sourceDb, out _));

        // 只有带有效时间戳的 banana 被导入;apple(NULL)与 cherry(0)都不出现(尤其不是 1970)。
        // Assert.Single 同时钉住两档 —— 多出任何一条都会失败。
        var vocabs = new Km2VocabRepository(targetCs).GetAll();
        var vocab = Assert.Single(vocabs);
        Assert.Equal("banana", vocab.Word);
        Assert.DoesNotContain("1970", vocab.Timestamp ?? string.Empty, StringComparison.Ordinal);

        var lookups = new Km2LookupRepository(targetCs).GetAll();
        var lookup = Assert.Single(lookups);
        Assert.Equal("en:banana", lookup.WordKey);
    }
}
