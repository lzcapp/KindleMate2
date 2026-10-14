using System.Globalization;
using Microsoft.Data.Sqlite;
using Xunit;
using KindleMate2.Application.Services;
using KindleMate2.Application.Services.KM2DB;
using KindleMate2.Domain.Interfaces.KM2DB;
using KindleMate2.Infrastructure.Helpers;
using KindleMate2.Infrastructure.Repositories.KM2DB;
using KindleMate2.Shared;
using KindleMate2.Shared.Constants;
using Km2LookupRepository = KindleMate2.Infrastructure.Repositories.KM2DB.LookupRepository;
using Km2VocabRepository = KindleMate2.Infrastructure.Repositories.KM2DB.VocabRepository;

namespace KindleMate2.Tests;

/// <summary>
/// 导入链路健壮性回归(源库 vocab.db 逐表降级 / 时间戳越界防护 / 跳过计数可见 /
/// 源库只读打开)。真实仓储 + 真实服务,只把源库造成"缺表缺列 / 脏时间戳"的坏样子。
/// </summary>
public sealed class ImportHardeningTests : IDisposable {
    private readonly string _dir;
    private readonly string _targetDb;
    private readonly string _targetCs;

    public ImportHardeningTests() {
        _dir = Path.Combine(Path.GetTempPath(), "km2-import-hardening-" + Guid.NewGuid().ToString("N")[..10]);
        Directory.CreateDirectory(_dir);
        _targetDb = Path.Combine(_dir, "KM2.dat");
        Assert.True(DatabaseHelper.CreateDatabase(_targetDb, out var ex), "CreateDatabase failed: " + ex.Message);
        _targetCs = DatabaseHelper.GetConnectionString(_targetDb);
    }

    public void Dispose() {
        try { Directory.Delete(_dir, true); } catch { /* best effort */ }
    }

    // ————————————————————————— ① 源库只读打开 —————————————————————————

    /// <summary>
    /// 源库(设备 vocab.db)必须以 <c>ReadOnly</c> 打开:它是设备写就的数据源,可写打开会在
    /// 只读挂载 / 被占用时失败,还可能生成 journal 污染设备盘。
    /// 双档断言:连接串的模式,以及对该连接发起的写操作确实被拒。
    /// </summary>
    [Fact]
    public void SourceConnection_IsReadOnly_AndRejectsWrites() {
        var sourceDb = CreateSourceDb("ro-vocab.db", WordsDdl);

        var connectionString = VocabDatabaseServiceFactory.BuildSourceConnectionString(sourceDb);
        Assert.Equal(SqliteOpenMode.ReadOnly, new SqliteConnectionStringBuilder(connectionString).Mode);

        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "CREATE TABLE probe (x INTEGER)";
        Assert.Throws<SqliteException>(() => cmd.ExecuteNonQuery());
    }

    // ————————————————————————— ② 缺表逐表降级 —————————————————————————

    /// <summary>缺 LOOKUPS / BOOK_INFO:标注词表(WORDS)照常导入,不整份失败,跳过的表计入结果。</summary>
    [Fact]
    public void MissingLookupAndBookInfoTables_WordsStillImport_RestDegraded() {
        var sourceDb = CreateSourceDb("only-words.db", WordsDdl,
            "INSERT INTO WORDS (id, word, timestamp) VALUES ('en:apple', 'apple', 1735689600000)");

        var result = ImportWords(sourceDb, out var ok);

        Assert.True(ok, "缺表不应导致整份导入失败");
        Assert.Single(new Km2VocabRepository(_targetCs).GetAll());
        Assert.Empty(new Km2LookupRepository(_targetCs).GetAll());
        Assert.Equal("2", result[AppConstants.SkippedTableCount]); // LOOKUPS + BOOK_INFO
        Assert.Equal("0", result[AppConstants.SkippedTimestampCount]);
    }

    /// <summary>缺 WORDS(必需表):查询记录(LOOKUPS)照常导入 —— 逐表降级,互不连坐。</summary>
    [Fact]
    public void MissingWordsTable_LookupsStillImport() {
        var sourceDb = CreateSourceDb("only-lookups.db", LookupsDdl,
            "INSERT INTO LOOKUPS (id, word_key, usage, timestamp) VALUES ('l1', 'en:apple', 'u', 1735689600000)");

        var result = ImportWords(sourceDb, out var ok);

        Assert.True(ok, "缺 WORDS 不应导致整份导入失败");
        Assert.Empty(new Km2VocabRepository(_targetCs).GetAll());
        Assert.Single(new Km2LookupRepository(_targetCs).GetAll());
        Assert.Equal("2", result[AppConstants.SkippedTableCount]); // WORDS + BOOK_INFO
    }

    // ————————————————————————— ③ 缺列:必需缺则跳过,可选缺则补 NULL —————————————————————————

    /// <summary>WORDS 缺**必需列** timestamp:该表整表跳过,但 LOOKUPS 照常导入。</summary>
    [Fact]
    public void WordsMissingRequiredColumn_TableSkipped_OtherTablesStillImport() {
        var sourceDb = CreateSourceDb("words-no-ts.db",
            "CREATE TABLE WORDS (id TEXT, word TEXT, stem TEXT)",
            LookupsDdl,
            BookInfoDdl,
            "INSERT INTO WORDS (id, word, stem) VALUES ('en:apple', 'apple', 'appl')",
            "INSERT INTO LOOKUPS (id, word_key, usage, timestamp) VALUES ('l1', 'en:apple', 'u', 1735689600000)");

        var result = ImportWords(sourceDb, out var ok);

        Assert.True(ok, "缺必需列应只跳过该表,不整份失败");
        Assert.Empty(new Km2VocabRepository(_targetCs).GetAll());
        Assert.Single(new Km2LookupRepository(_targetCs).GetAll());
        Assert.Equal("1", result[AppConstants.SkippedTableCount]); // 只有 WORDS
    }

    /// <summary>WORDS 缺**可选列**(stem/lang/category/profileid):按实列构造 SELECT,词照常导入。</summary>
    [Fact]
    public void WordsMissingOptionalColumns_StillImport() {
        var sourceDb = CreateSourceDb("words-narrow.db",
            "CREATE TABLE WORDS (id TEXT, word TEXT, timestamp INTEGER)",
            LookupsDdl,
            BookInfoDdl,
            "INSERT INTO WORDS (id, word, timestamp) VALUES ('en:apple', 'apple', 1735689600000)");

        var result = ImportWords(sourceDb, out var ok);

        Assert.True(ok);
        var vocab = Assert.Single(new Km2VocabRepository(_targetCs).GetAll());
        Assert.Equal("apple", vocab.Word);
        Assert.Null(vocab.Stem); // 缺列补 NULL,而不是整表跳过
        Assert.Equal("0", result[AppConstants.SkippedTableCount]);
    }

    // ————————————————————————— ④ 时间戳越界防护 —————————————————————————

    /// <summary>
    /// 脏 unix 毫秒(0 / 负数 / long.MaxValue / NULL)不抛异常、按「无时间」处理并计数;
    /// 同批里的合法行照常导入。
    /// </summary>
    [Fact]
    public void DirtyUnixMilliseconds_DoNotThrow_CountedAndTreatedAsNoTime() {
        var sourceDb = CreateSourceDb("dirty-ts.db", WordsDdl, LookupsDdl, BookInfoDdl,
            "INSERT INTO WORDS (id, word, timestamp) VALUES " +
            "('en:ok', 'ok', 1735689600000), " +
            "('en:max', 'max', 9223372036854775807), " +
            "('en:neg', 'neg', -1000), " +
            "('en:zero', 'zero', 0), " +
            "('en:null', 'null', NULL)",
            "INSERT INTO LOOKUPS (id, word_key, usage, timestamp) VALUES " +
            "('l-ok', 'en:ok', 'u', 1735689600000), " +
            "('l-max', 'en:max', 'u', 9223372036854775807)");

        var result = ImportWords(sourceDb, out var ok);

        Assert.True(ok, "越界时间戳不应让整份导入抛异常");
        var vocab = Assert.Single(new Km2VocabRepository(_targetCs).GetAll());
        Assert.Equal("ok", vocab.Word);
        Assert.DoesNotContain("1970", vocab.Timestamp ?? string.Empty, StringComparison.Ordinal);
        Assert.Single(new Km2LookupRepository(_targetCs).GetAll());
        // 4 条 WORDS(max/neg/zero/null) + 1 条 LOOKUPS(max)
        Assert.Equal("5", result[AppConstants.SkippedTimestampCount]);
        Assert.Equal("0", result[AppConstants.SkippedTableCount]);
    }

    // ————————————————————————— ⑤ 跳过计数呈现给用户 —————————————————————————

    /// <summary>标注导入:日期解析失败的条目此前被静默丢弃,现在必须出现在成功提示里。</summary>
    [Fact]
    public void ImportKindleClippings_Message_ShowsDateParseSkipCount() {
        var path = Path.Combine(_dir, "My Clippings.txt");
        File.WriteAllText(path, """
Book (Author)
- Highlight | Page 5 | Added on Tuesday, May 20, 2025, 8:00:00 AM

some content
==========
Book (Author)
- Highlight | Page 6 | addedd on a totally unparseable date string

more content
==========
""");

        var message = NewClippingsManager().ImportKindleClippings(path);

        var expected = string.Format(CultureInfo.InvariantCulture, Strings.Import_Skipped_Clippings_Format, 1, 1, 0, 0);
        Assert.Contains(expected, message, StringComparison.Ordinal);
    }

    /// <summary>生词导入:逐表跳过与时间戳异常两个计数都要出现在成功提示里。</summary>
    [Fact]
    public void ImportKindleWords_Message_ShowsTableAndTimestampSkips() {
        var sourceDb = CreateSourceDb("words-msg.db", WordsDdl,
            "INSERT INTO WORDS (id, word, timestamp) VALUES " +
            "('en:ok', 'ok', 1735689600000), " +
            "('en:max', 'max', 9223372036854775807)");

        var message = NewWordsManager().ImportKindleWords(sourceDb);

        Assert.Contains(
            string.Format(CultureInfo.InvariantCulture, Strings.Import_Skipped_Tables_Format, 2),
            message, StringComparison.Ordinal);
        Assert.Contains(
            string.Format(CultureInfo.InvariantCulture, Strings.Import_Skipped_Timestamps_Format, 1),
            message, StringComparison.Ordinal);
    }

    // ————————————————————————— helpers —————————————————————————

    private static readonly string WordsDdl =
        "CREATE TABLE WORDS (id TEXT, word TEXT, stem TEXT, lang TEXT, category INTEGER, timestamp INTEGER, profileid TEXT)";

    private static readonly string LookupsDdl =
        "CREATE TABLE LOOKUPS (id TEXT, word_key TEXT, book_key TEXT, dict_key TEXT, pos TEXT, usage TEXT, timestamp INTEGER)";

    private static readonly string BookInfoDdl =
        "CREATE TABLE BOOK_INFO (id TEXT, asin TEXT, guid TEXT, lang TEXT, title TEXT, authors TEXT)";

    private string CreateSourceDb(string name, params string[] ddl) {
        var path = Path.Combine(_dir, name);
        using var connection = new SqliteConnection(DatabaseHelper.BuildConnectionString(path, SqliteOpenMode.ReadWriteCreate, sharedCache: false));
        connection.Open();
        foreach (var script in ddl) {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = script;
            cmd.ExecuteNonQuery();
        }
        return path;
    }

    /// <summary>经**真实工厂**创建服务(顺带覆盖源库 ReadOnly + 探测快照这条链路)。</summary>
    private Dictionary<string, string> ImportWords(string sourceDb, out bool ok) {
        var service = new VocabDatabaseServiceFactory(
            new Km2LookupRepository(_targetCs),
            new Km2VocabRepository(_targetCs)).Create(sourceDb);
        ok = service.ImportKindleWords(sourceDb, out var result);
        return result;
    }

    private ImportManager NewClippingsManager() => NewManager(new UnusedVocabFactory());

    private ImportManager NewWordsManager() => NewManager(
        new VocabDatabaseServiceFactory(new Km2LookupRepository(_targetCs), new Km2VocabRepository(_targetCs)));

    private ImportManager NewManager(IVocabDatabaseServiceFactory vocabFactory) {
        var clipRepo = new ClippingRepository(_targetCs);
        var lookupRepo = new Km2LookupRepository(_targetCs);
        var lineRepo = new OriginalClippingLineRepository(_targetCs);

        return new ImportManager(
            new Km2DatabaseService(clipRepo, lookupRepo, lineRepo, new SettingRepository(_targetCs), new Km2VocabRepository(_targetCs)),
            new ClippingService(clipRepo),
            new VocabService(new Km2VocabRepository(_targetCs)),
            new OriginalClippingLineService(lineRepo),
            new LookupService(lookupRepo),
            vocabFactory,
            new UnusedKmFactory(),
            new UnusedKmateFactory(),
            new ImportBatchRepository(_targetCs),
            _dir);
    }

    private sealed class UnusedVocabFactory : IVocabDatabaseServiceFactory {
        public VocabDatabaseService Create(string vocabDbPath) =>
            throw new NotSupportedException("本用例不走 vocab.db 导入");
    }

    private sealed class UnusedKmFactory : IKmDatabaseServiceFactory {
        public KmDatabaseService Create(string kmDbPath) =>
            throw new NotSupportedException("本用例不走平库导入");
    }

    private sealed class UnusedKmateFactory : IKmateDatabaseServiceFactory {
        public KmateDatabaseService Create(string km3DbPath) =>
            throw new NotSupportedException("本用例不走 KMate 导入");
    }
}
