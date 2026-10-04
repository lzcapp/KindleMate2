using System.Text.Json;
using Xunit;
using KindleMate2.Application.Services;
using KindleMate2.Application.Services.KM2DB;
using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Domain.Interfaces.KM2DB;
using KindleMate2.Infrastructure.Helpers;
using KindleMate2.Infrastructure.Repositories.KM2DB;

namespace KindleMate2.Tests;

/// <summary>
/// 「stem 补齐」的口径用例 —— 钉住 <c>ExportManager.FillStems</c> 的保守语义,以及
/// **同一份库导出成 CSV 与 JSON 必须得到相同的 stem**。
///
/// <para>
/// 背景:stem(词干)只存在于 <c>vocab</c>(WORDS)表;查词记录表 <c>lookups</c> **没有** stem 列
/// (建表见 <c>DatabaseHelper</c>),<c>Lookup.Stem</c> 只是不落库的瞬时字段,读出来必为
/// <c>null</c>。所以导出时要按 <c>word_key</c> 从 WORDS 补齐。
/// </para>
/// <para>
/// 该补齐一度有两份实现且口径分叉:CSV 侧**无条件覆盖**、JSON 侧**仅空才补**。
/// 在「<c>Lookup.Stem</c> 必为空」的前提下两者输出相同,所以那处分叉**不可观测** ——
/// 也正因如此一直没人发现。现在两处统一走 <see cref="ExportManager.FillStems"/>,
/// 并由本用例把语义与「两格式一致」固化:任何一侧被改回内联/覆盖写法都会立刻变红。
/// </para>
/// </summary>
public sealed class VocabStemExportTests : IDisposable {
    private const string ExportsDirName = "Exports";

    private readonly string _dir;
    private readonly string _programPath;
    private readonly ILookupRepository _lookupRepo;
    private readonly IVocabRepository _vocabRepo;
    private readonly ExportManager _export;

    public VocabStemExportTests() {
        _dir = Path.Combine(Path.GetTempPath(), "km2-vocab-stem-" + Guid.NewGuid().ToString("N")[..10]);
        Directory.CreateDirectory(_dir);
        var db = Path.Combine(_dir, "KM2.dat");
        Assert.True(DatabaseHelper.CreateDatabase(db, out var ex), "CreateDatabase failed: " + ex.Message);

        // 与 Avalonia 壳的 DatabaseSession 同构,但按路径参数化(不依赖进程工作目录)
        var cs = DatabaseHelper.GetConnectionString(db);
        _lookupRepo = new LookupRepository(cs);
        _vocabRepo = new VocabRepository(cs);

        _programPath = Path.Combine(_dir, "Work");
        _export = new ExportManager(
            new ClippingService(new ClippingRepository(cs)),
            new LookupService(_lookupRepo),
            new VocabService(_vocabRepo),
            new OriginalClippingLineService(new OriginalClippingLineRepository(cs)),
            new NullDeviceManager(),   // CSV / JSON 这两条路根本不碰设备
            _programPath, Path.Combine(_dir, "Backups"), Path.Combine(_dir, "Temp"));
    }

    public void Dispose() {
        try {
            // 刻意不在这里清 SQLite 连接池(进程级 API,会与并行跑的其他测试类互撞);
            // 残留目录由 TestTempCleanup 在进程退出时统一清扫。
            Directory.Delete(_dir, true);
        } catch { /* best effort */ }
    }

    // ————————————————— 补齐语义(纯函数,不碰库) —————————————————

    /// <summary>值为空时按 WORDS 补齐 —— 这是「词形只存在 WORDS 表」的正常路径。</summary>
    [Fact]
    public void FillStems_FillsWhenEmpty() {
        var lookup = new Lookup { WordKey = "en:apple" };

        ExportManager.FillStems(new[] { lookup }, StemsByKey(("en:apple", "appl")));

        Assert.Equal("appl", lookup.Stem);
    }

    /// <summary>
    /// **保守语义**:已有非空 stem 时不覆盖。
    /// 这正是修复前 CSV 与 JSON 分叉的那一点 —— 旧 CSV 写的是无条件覆盖,会把这行抹成 WORDS 里的值。
    /// </summary>
    [Fact]
    public void FillStems_DoesNotOverwriteExistingStem() {
        var lookup = new Lookup { WordKey = "en:apple", Stem = "库里已有的值" };

        ExportManager.FillStems(new[] { lookup }, StemsByKey(("en:apple", "appl")));

        Assert.Equal("库里已有的值", lookup.Stem);
    }

    /// <summary>无 <c>WordKey</c>、或 WORDS 里查不到该 key 时保持为空(不臆造)。</summary>
    [Fact]
    public void FillStems_LeavesUnmatchedRowsEmpty() {
        var noWordKey = new Lookup { WordKey = null };
        var notInVocab = new Lookup { WordKey = "en:banana" };

        ExportManager.FillStems(new[] { noWordKey, notInVocab }, StemsByKey(("en:apple", "appl")));

        Assert.Null(noWordKey.Stem);
        Assert.Null(notInVocab.Stem);
    }

    // ————————————————— 端到端:两种格式必须给出同一个 stem —————————————————

    /// <summary>WORDS 里有 stem 时,CSV 与 JSON 都要带上,且**两者相同**。</summary>
    [Fact]
    public async Task CsvAndJson_AgreeOnStem_WhenVocabHasStem() {
        SeedVocabAndLookup(stem: "appl");

        await _export.ExportVocabsToCsvAsync(includeDefinitions: false);
        Assert.True(_export.ExportVocabsToJson());

        var csvStem = ReadCsvStem();
        var jsonStem = ReadJsonStem();
        Assert.Equal("appl", csvStem);
        Assert.Equal(csvStem, jsonStem);
    }

    /// <summary>WORDS 里 stem 为空串时两格式同样一致(都为空),不出现一边空一边有值。</summary>
    [Fact]
    public async Task CsvAndJson_AgreeOnStem_WhenVocabStemIsEmpty() {
        SeedVocabAndLookup(stem: string.Empty);

        await _export.ExportVocabsToCsvAsync(includeDefinitions: false);
        Assert.True(_export.ExportVocabsToJson());

        var csvStem = ReadCsvStem();
        var jsonStem = ReadJsonStem();
        Assert.Equal(string.Empty, csvStem);
        Assert.Equal(csvStem, jsonStem);
    }

    // ————————————————— helpers —————————————————

    private static Dictionary<string, string> StemsByKey(params (string Key, string Stem)[] pairs) =>
        pairs.ToDictionary(p => p.Key, p => p.Stem, StringComparer.Ordinal);

    /// <summary>造一对同 <c>WordKey</c> 的 WORDS / LOOKUPS 行 —— 补齐就是靠这个 key 关联的。</summary>
    private void SeedVocabAndLookup(string stem) {
        Assert.True(_vocabRepo.Add(new Vocab {
            Id = "en:apple",
            WordKey = "en:apple",
            Word = "apple",
            Stem = stem
        }), "插入 WORDS 夹具失败");

        Assert.True(_lookupRepo.Add(new Lookup {
            WordKey = "en:apple",
            Usage = "an apple a day",
            Title = "Some Book",
            Authors = "Some Author",
            Timestamp = "2020-01-01 10:00:00"
        }), "插入 LOOKUPS 夹具失败");
    }

    private string ExportedPath(string fileName) => Path.Combine(_programPath, ExportsDirName, fileName);

    /// <summary>取 CSV 产物里那一行的 Stem 列(表头顺序见 <c>WriteLookupsCsv</c>)。</summary>
    private string ReadCsvStem() {
        var lines = File.ReadAllLines(ExportedPath("Vocabs.csv"));
        Assert.True(lines.Length >= 2, $"CSV 应至少有表头 + 1 行:实际 {lines.Length} 行");

        var header = lines[0].Split(',');
        Assert.Equal("Word", header[0]);
        Assert.Equal("Stem", header[1]);

        return lines[1].Split(',')[1];
    }

    /// <summary>取 JSON 产物里那条生词的 <c>stem</c>。</summary>
    private string ReadJsonStem() {
        using var doc = JsonDocument.Parse(File.ReadAllText(ExportedPath("Vocabs.json")));
        var words = doc.RootElement.GetProperty("words");
        Assert.Equal(1, words.GetArrayLength());

        return words[0].GetProperty("stem").GetString() ?? string.Empty;
    }
}
