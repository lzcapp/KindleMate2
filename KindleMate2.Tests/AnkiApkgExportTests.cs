using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Xunit;
using KindleMate2.Application.Services;
using KindleMate2.Application.Services.KM2DB;
using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Infrastructure.Helpers;
using KindleMate2.Infrastructure.Repositories.KM2DB;

namespace KindleMate2.Tests;

/// <summary>
/// Anki 牌组导出(<c>.apkg</c>)的**结构性**验证。
/// </summary>
/// <remarks>
/// <para>
/// <b>为什么只能做到"结构性":</b>CI 里跑不了 Anki,因此"Anki 真的能导入"这件事无法自动验证。
/// 这里退而求其次,用一个**最小的独立读取器**(见 <see cref="ReadZip"/> / <see cref="OpenCollection"/>)
/// 把产物读回来逐项断言 —— 这能拿到的最强证据,而不是"文件存在"这种弱断言。
/// 仍然只有人工把 .apkg 拖进 Anki 才能确认的部分,见 <c>REPORT</c> 里列出的验证缺口。
/// </para>
/// <para>
/// 全部走 <see cref="ExportManager.WriteAnkiDeck"/> 与 <see cref="AnkiApkgWriter"/>,是静态纯函数
/// (不碰库、不联网、不依赖设备),风格与 <see cref="ObsidianExportTests"/> / <see cref="JsonExportTests"/> 一致。
/// </para>
/// </remarks>
public sealed class AnkiApkgExportTests : IDisposable {
    // 固定时刻:让"同一份输入导出两次 guid 逐字相同"这条断言真正可复现。
    private static readonly DateTimeOffset FixedAt = new(2026, 10, 4, 20, 30, 0, TimeSpan.FromHours(8));

    private readonly string _dir;

    public AnkiApkgExportTests() {
        _dir = Path.Combine(Path.GetTempPath(), "km2-anki-" + Guid.NewGuid().ToString("N")[..10]);
        Directory.CreateDirectory(_dir);
    }

    public void Dispose() {
        try { Directory.Delete(_dir, true); } catch { /* best effort */ }
    }

    // ————————————————————————— 结构:ZIP 条目 —————————————————————————

    [Fact]
    public void WriteAnkiDeck_ZipContainsCollectionAndMedia() {
        var path = Path.Combine(_dir, "deck.apkg");

        ExportManager.WriteAnkiDeck([Lookup("en:apple", usage: "an apple a day")], path, NoDefinitions, FixedAt);

        var entries = ReadZip(path);
        Assert.True(entries.ContainsKey("collection.anki2"), "缺少 collection.anki2 条目");
        Assert.True(entries.ContainsKey("media"), "缺少 media 条目");
        // 无媒体时 media 必须是合法 JSON 空对象(不是空文件 —— 空文件会让 Anki 解析失败)。
        Assert.Equal("{}", System.Text.Encoding.UTF8.GetString(entries["media"]));
    }

    // ————————————————————————— 结构:SQLite 表齐全 —————————————————————————

    [Fact]
    public void WriteAnkiDeck_SqliteHasAllStandardTables() {
        var path = Path.Combine(_dir, "tables.apkg");
        ExportManager.WriteAnkiDeck([Lookup("en:apple")], path, NoDefinitions, FixedAt);

        using var connection = OpenCollection(path);
        var tables = Query(connection, "SELECT name FROM sqlite_master WHERE type='table'")
            .Select(row => (string)row[0]!)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var expected in new[] { "col", "notes", "cards", "revlog", "graves" }) {
            Assert.Contains(expected, tables);
        }
    }

    // ————————————————————————— 结构:col 行 —————————————————————————

    [Fact]
    public void WriteAnkiDeck_CollectionRow_HasSingleRowWithSchemaVersion11() {
        var path = Path.Combine(_dir, "col.apkg");
        ExportManager.WriteAnkiDeck([Lookup("en:apple")], path, NoDefinitions, FixedAt);

        using var connection = OpenCollection(path);
        Assert.Equal(1L, Scalar<long>(connection, "SELECT COUNT(*) FROM col"));
        Assert.Equal(11L, Scalar<long>(connection, "SELECT ver FROM col"));
        Assert.Equal(1L, Scalar<long>(connection, "SELECT id FROM col"));

        // crt 必须是**秒**级(Anki 自身写的就是秒):应落在合理区间,而不是毫秒(13 位)。
        var crt = Scalar<long>(connection, "SELECT crt FROM col");
        Assert.InRange(crt, 1_000_000_000L, 100_000_000_000L);
    }

    [Theory]
    [InlineData("conf")]
    [InlineData("dconf")]
    [InlineData("tags")]
    [InlineData("models")]
    [InlineData("decks")]
    public void WriteAnkiDeck_CollectionJsonColumns_AreValidJson(string column) {
        var path = Path.Combine(_dir, "json-" + column + ".apkg");
        ExportManager.WriteAnkiDeck([Lookup("en:apple")], path, NoDefinitions, FixedAt);

        using var connection = OpenCollection(path);
        var text = Scalar<string>(connection, "SELECT " + column + " FROM col");

        // 只要求"能被解析":空 JSON({}/[])会让 Anki 报错,但那是**语义**问题,
        // 这里先钉住最基本的一条 —— 至少是合法 JSON。
        using var parsed = JsonDocument.Parse(text);
        Assert.NotEqual(JsonValueKind.Undefined, parsed.RootElement.ValueKind);
    }

    [Fact]
    public void WriteAnkiDeck_ModelsAndDecks_CarryOurNotetypeAndSingleDeck() {
        var path = Path.Combine(_dir, "defs.apkg");
        ExportManager.WriteAnkiDeck([Lookup("en:apple")], path, NoDefinitions, FixedAt);

        using var connection = OpenCollection(path);

        using var models = JsonDocument.Parse(Scalar<string>(connection, "SELECT models FROM col"));
        var model = Assert.Single(models.RootElement.EnumerateObject());
        // note type 的字段数必须与 note 的 flds 数一致(见下面的 note 断言)。
        Assert.Equal(AnkiApkgWriter.FieldNames.Length, model.Value.GetProperty("flds").GetArrayLength());
        Assert.Equal(1, model.Value.GetProperty("tmpls").GetArrayLength());
        Assert.Equal(AnkiApkgWriter.DeckName, model.Value.GetProperty("name").GetString());

        using var decks = JsonDocument.Parse(Scalar<string>(connection, "SELECT decks FROM col"));
        var deck = Assert.Single(decks.RootElement.EnumerateObject());
        Assert.Equal(AnkiApkgWriter.DeckName, deck.Value.GetProperty("name").GetString());
    }

    // ————————————————————————— 结构:note / card 数量与字段数 —————————————————————————

    [Fact]
    public void WriteAnkiDeck_NoteAndCardCountsMatchInput() {
        var path = Path.Combine(_dir, "counts.apkg");
        var lookups = new List<Lookup> {
            Lookup("en:apple", usage: "an apple a day", book: "Book A"),
            Lookup("en:banana", usage: "a banana", book: "Book A"),
            Lookup("en:cherry", usage: "a cherry", book: "Book B"),
        };

        ExportManager.WriteAnkiDeck(lookups, path, NoDefinitions, FixedAt);

        using var connection = OpenCollection(path);
        Assert.Equal(3L, Scalar<long>(connection, "SELECT COUNT(*) FROM notes"));
        Assert.Equal(3L, Scalar<long>(connection, "SELECT COUNT(*) FROM cards"));
        // 每张 card 的 nid 都必须指向一条真实存在的 note(悬空引用会让 Anki 导入后出现"孤儿卡片")。
        Assert.Equal(0L, Scalar<long>(connection,
            "SELECT COUNT(*) FROM cards WHERE nid NOT IN (SELECT id FROM notes)"));
    }

    [Fact]
    public void WriteAnkiDeck_NoteFieldCountMatchesTemplateFieldCount() {
        var path = Path.Combine(_dir, "fields.apkg");
        ExportManager.WriteAnkiDeck([Lookup("en:apple")], path, NoDefinitions, FixedAt);

        using var connection = OpenCollection(path);
        var flds = Scalar<string>(connection, "SELECT flds FROM notes");

        var count = flds.Split('\u001f').Length;
        Assert.Equal(AnkiApkgWriter.FieldNames.Length, count);
    }

    [Fact]
    public void WriteAnkiDeck_SortsByFirstField_AndChecksumsIt() {
        var path = Path.Combine(_dir, "sfld.apkg");
        ExportManager.WriteAnkiDeck([Lookup("en:apple")], path, NoDefinitions, FixedAt);

        using var connection = OpenCollection(path);
        // sfld = 首字段(排序用);csum = 首字段的校验和。csum 与 Anki 官方单测锚定值一致。
        Assert.Equal("apple", Scalar<string>(connection, "SELECT sfld FROM notes"));
        Assert.Equal(AnkiApkgWriter.FieldChecksum("apple"), Scalar<long>(connection, "SELECT csum FROM notes"));
    }

    // ————————————————————————— 结构:来源书 → tag —————————————————————————

    [Fact]
    public void WriteAnkiDeck_BookBecomesASingleTag_WithSpacesReplaced() {
        var path = Path.Combine(_dir, "tags.apkg");
        ExportManager.WriteAnkiDeck([Lookup("en:apple", book: "The Great Gatsby")], path, NoDefinitions, FixedAt);

        using var connection = OpenCollection(path);
        var tags = Scalar<string>(connection, "SELECT tags FROM notes");
        // 空格是 Anki 的 tag 分隔符 —— 书名里的空格必须折叠成 '_',否则会被拆成三个 tag。
        Assert.Equal(" The_Great_Gatsby ", tags);
    }

    // ————————————————————————— guid 确定性 —————————————————————————

    [Fact]
    public void WriteAnkiDeck_GuidIsDeterministic_AcrossTwoExports() {
        var lookups = new List<Lookup> {
            Lookup("en:apple", usage: "an apple a day", book: "A"),
            Lookup("en:banana", usage: "a banana", book: "B"),
        };
        var first = Path.Combine(_dir, "guid-1.apkg");
        var second = Path.Combine(_dir, "guid-2.apkg");

        // 同一份输入 + 同一时刻导出两次:guid 集合必须逐字相同,否则再次导入会产生重复卡片。
        ExportManager.WriteAnkiDeck(lookups, first, NoDefinitions, FixedAt);
        ExportManager.WriteAnkiDeck(lookups, second, NoDefinitions, FixedAt);

        var firstGuids = ReadGuids(first);
        var secondGuids = ReadGuids(second);
        Assert.Equal(firstGuids, secondGuids);
        Assert.Equal(2, firstGuids.Count);   // 且互不相同
    }

    [Fact]
    public void WriteAnkiDeck_GuidIsIndependentOfDefinitions() {
        var lookups = new List<Lookup> { Lookup("en:apple", usage: "an apple a day", book: "A") };
        var offline = Path.Combine(_dir, "offline.apkg");
        var online = Path.Combine(_dir, "online.apkg");

        // guid 不得掺入释义 —— 否则"是否联网查释义"会改变 guid,重复导入就防不住了。
        ExportManager.WriteAnkiDeck(lookups, offline, NoDefinitions, FixedAt);
        ExportManager.WriteAnkiDeck(lookups, online,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["apple"] = "苹果" }, FixedAt);

        Assert.Equal(ReadGuids(offline), ReadGuids(online));
    }

    [Fact]
    public void BuildGuid_IsStableAndUniqueForDistinctLookups() {
        var word = new Domain.Models.Export.ExportWord { Word = "apple", Language = "en", Timestamp = "2026-01-01 10:00:00" };
        var same = new Domain.Models.Export.ExportWord { Word = "apple", Language = "en", Timestamp = "2026-01-01 10:00:00" };
        var later = new Domain.Models.Export.ExportWord { Word = "apple", Language = "en", Timestamp = "2026-02-02 10:00:00" };

        Assert.Equal(AnkiApkgWriter.BuildGuid(word), AnkiApkgWriter.BuildGuid(same));
        // 同一个词在不同时间被查(库里是两行)→ 必须给出两个不同的 guid,否则会被 Anki 当成同一条 note。
        Assert.NotEqual(AnkiApkgWriter.BuildGuid(word), AnkiApkgWriter.BuildGuid(later));
    }

    // ————————————————————————— 空输入 —————————————————————————

    [Fact]
    public void WriteAnkiDeck_EmptyInput_ProducesValidDeckWithNoNotes() {
        var path = Path.Combine(_dir, "empty.apkg");

        ExportManager.WriteAnkiDeck([], path, NoDefinitions, FixedAt);

        var entries = ReadZip(path);
        Assert.True(entries.ContainsKey("collection.anki2"));

        using var connection = OpenCollection(path);
        Assert.Equal(0L, Scalar<long>(connection, "SELECT COUNT(*) FROM notes"));
        Assert.Equal(0L, Scalar<long>(connection, "SELECT COUNT(*) FROM cards"));
        Assert.Equal(11L, Scalar<long>(connection, "SELECT ver FROM col"));   // 空牌组也必须合法
    }

    // ————————————————————————— 释义字段 —————————————————————————

    [Fact]
    public void WriteAnkiDeck_Offline_LeavesDefinitionEmpty() {
        var path = Path.Combine(_dir, "no-def.apkg");

        // includeDefinitions=false 的等价物:传空释义字典 → Definition 字段留空。
        ExportManager.WriteAnkiDeck([Lookup("en:apple", usage: "an apple a day")], path, NoDefinitions, FixedAt);

        using var connection = OpenCollection(path);
        var fields = Scalar<string>(connection, "SELECT flds FROM notes").Split('\u001f');
        Assert.Equal("apple", fields[0]);
        Assert.Equal(string.Empty, fields[1]);   // Definition 留空,由用户自己填
    }

    [Fact]
    public void WriteAnkiDeck_WithDefinitions_FillsDefinitionField() {
        var path = Path.Combine(_dir, "with-def.apkg");
        var definitions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["apple"] = "n. 苹果" };

        ExportManager.WriteAnkiDeck([Lookup("en:apple", usage: "an apple a day")], path, definitions, FixedAt);

        using var connection = OpenCollection(path);
        var fields = Scalar<string>(connection, "SELECT flds FROM notes").Split('\u001f');
        Assert.Equal("n. 苹果", fields[1]);
    }

    // ————————————————————————— 字段校验和(与 Anki 官方锚定值比对) —————————————————————————

    [Theory]
    [InlineData("test", 2840236005L)]
    [InlineData("今日", 1464653051L)]
    public void FieldChecksum_MatchesAnkiReferenceValues(string text, long expected) {
        // 这两个值是 Anki 官方单测 rslib/src/notes/mod.rs 里 field_checksum 的锚定值;
        // 用它们比对,能证明我们的 SHA-1 → 前 4 字节 → 大端 u32 实现与 Anki 逐位一致。
        Assert.Equal(expected, AnkiApkgWriter.FieldChecksum(text));
    }

    // ————————————————————————— 端到端:ExportManager 离线路径(不联网) —————————————————————————

    [Fact]
    public async Task ExportVocabsToAnkiDeckAsync_Offline_WritesDeckWithoutNetwork() {
        // 走真实的 ExportManager(真库),验证 includeDefinitions=false 这条路径:
        // 它**不会**调用 LookupDefinitionsAsync(那条分支只在 includeDefinitions=true 时进),
        // 因此不构造 HttpClient、不发起任何请求;这里断言产物里 Definition 为空即可。
        var dbPath = Path.Combine(_dir, "KM2.dat");
        Assert.True(DatabaseHelper.CreateDatabase(dbPath, out var ex), "CreateDatabase failed: " + ex.Message);

        var cs = DatabaseHelper.GetConnectionString(dbPath);
        var lookupRepo = new LookupRepository(cs);
        Assert.True(lookupRepo.Add(new Lookup {
            WordKey = "en:apple", Usage = "an apple a day", Title = "Some Book",
            Authors = "Some Author", Timestamp = "2020-01-01 10:00:00",
        }), "插入 LOOKUPS 夹具失败");

        var programPath = Path.Combine(_dir, "Work");
        var manager = new ExportManager(
            new ClippingService(new ClippingRepository(cs)),
            new LookupService(lookupRepo),
            new VocabService(new VocabRepository(cs)),
            new OriginalClippingLineService(new OriginalClippingLineRepository(cs)),
            new NullDeviceManager(),
            programPath, Path.Combine(_dir, "Backups"), Path.Combine(_dir, "Temp"));

        Assert.True(await manager.ExportVocabsToAnkiDeckAsync(includeDefinitions: false));

        var apkg = Path.Combine(programPath, "Exports", AnkiApkgWriter.AnkiDeckFileName);
        Assert.True(File.Exists(apkg));

        using var connection = OpenCollection(apkg);
        Assert.Equal(1L, Scalar<long>(connection, "SELECT COUNT(*) FROM notes"));
        Assert.Equal(string.Empty, Scalar<string>(connection, "SELECT flds FROM notes").Split('\u001f')[1]);
    }

    // ————————————————————————— helpers —————————————————————————

    private static readonly IReadOnlyDictionary<string, string> NoDefinitions =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private static Lookup Lookup(string wordKey, string usage = "", string? book = "Book", string author = "Author") {
        return new Lookup {
            WordKey = wordKey,
            Usage = usage,
            Title = book,
            Authors = author,
            Timestamp = "2026-01-01 10:00:00",
        };
    }

    /// <summary>最小独立读取器:把 .apkg 当普通 ZIP 读,返回「条目名 → 字节」。</summary>
    private static Dictionary<string, byte[]> ReadZip(string apkgPath) {
        using var zip = ZipFile.OpenRead(apkgPath);
        var result = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var entry in zip.Entries) {
            using var stream = entry.Open();
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            result[entry.FullName] = buffer.ToArray();
        }
        return result;
    }

    /// <summary>把 ZIP 里的 <c>collection.anki2</c> 落成临时文件并用 SQLite 打开(最小独立读取器)。</summary>
    private SqliteConnection OpenCollection(string apkgPath) {
        var bytes = ReadZip(apkgPath)["collection.anki2"];
        var dbPath = Path.Combine(_dir, "read-" + Guid.NewGuid().ToString("N") + ".anki2");
        File.WriteAllBytes(dbPath, bytes);

        // Pooling=false:默认连接池会让"已归还"的连接继续持有文件句柄,妨碍 Dispose 时清理临时文件。
        var connectionString = new SqliteConnectionStringBuilder {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false,
        }.ToString();
        var connection = new SqliteConnection(connectionString);
        connection.Open();
        return connection;
    }

    private static List<object?[]> Query(SqliteConnection connection, string sql) {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        using var reader = command.ExecuteReader();
        var rows = new List<object?[]>();
        while (reader.Read()) {
            var row = new object?[reader.FieldCount];
            for (var i = 0; i < reader.FieldCount; i++) {
                row[i] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            }
            rows.Add(row);
        }
        return rows;
    }

    private static T Scalar<T>(SqliteConnection connection, string sql) {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        var value = command.ExecuteScalar();
        Assert.NotNull(value);
        return (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
    }

    private HashSet<string> ReadGuids(string apkgPath) {
        // 解包到本测试类的临时目录(而非系统临时目录根),这样 Dispose 能一并清掉。
        var dbPath = Path.Combine(_dir, "guids-" + Guid.NewGuid().ToString("N") + ".anki2");
        File.WriteAllBytes(dbPath, ReadZip(apkgPath)["collection.anki2"]);

        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false,
        }.ToString());
        connection.Open();
        return Query(connection, "SELECT guid FROM notes")
            .Select(row => (string)row[0]!)
            .ToHashSet(StringComparer.Ordinal);
    }
}
