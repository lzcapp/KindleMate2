using Xunit;
using KindleMate2.Application.Services;
using KindleMate2.Application.Services.KM2DB;
using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Domain.Interfaces.KM2DB;
using KindleMate2.Infrastructure.Helpers;
using KindleMate2.Infrastructure.Repositories.KM2DB;

namespace KindleMate2.Tests;

/// <summary>
/// <see cref="ExportManager"/> 的 **public 导出方法**整链路用例 —— 补上一批 review 点名的空白。
/// </summary>
/// <remarks>
/// <para>
/// 此前导出测试**全部**打在 <c>internal static</c> 的纯函数 writer 上
/// (<see cref="JsonExportTests"/> / <see cref="CsvExportTests"/> / <see cref="ObsidianExportTests"/>):
/// 它们喂固定 DTO、直接调 writer,于是「建目录 → 落盘到 <c>Exports/</c> → try-catch 记日志并返回
/// false」这条**外壳**一行都没测过。
/// </para>
/// <para>
/// 这里刻意走**真实仓储 + 真实服务 + 真临时库**(与 <see cref="VocabStemExportTests"/> 同构):
/// 只有真写一次文件,才能证明"文件真的落在 <c>&lt;programPath&gt;/Exports/</c> 下且非空",
/// 而不是只证明了 writer 被调用。设备用 <see cref="NullDeviceManager"/>(这几条路根本不碰设备),
/// 全程不联网、不碰真机。
/// </para>
/// </remarks>
public sealed class ExportManagerPublicTests : IDisposable {
    private const string ExportsDirName = "Exports";

    private readonly string _dir;
    private readonly IClippingRepository _clippingRepo;
    private readonly ILookupRepository _lookupRepo;
    private readonly IVocabRepository _vocabRepo;
    private readonly IOriginalClippingLineRepository _originalRepo;
    private readonly string _programPath;
    private readonly ExportManager _export;

    public ExportManagerPublicTests() {
        _dir = Path.Combine(Path.GetTempPath(), "km2-pubexport-" + Guid.NewGuid().ToString("N")[..10]);
        Directory.CreateDirectory(_dir);
        var db = Path.Combine(_dir, "KM2.dat");
        Assert.True(DatabaseHelper.CreateDatabase(db, out var ex), "CreateDatabase failed: " + ex.Message);

        var cs = DatabaseHelper.GetConnectionString(db);
        _clippingRepo = new ClippingRepository(cs);
        _lookupRepo = new LookupRepository(cs);
        _vocabRepo = new VocabRepository(cs);
        _originalRepo = new OriginalClippingLineRepository(cs);

        // programPath 指向一个**刻意不预建**的子目录:导出方法要自己把 <programPath>/Exports/ 建出来,
        // 这正好覆盖"目录不存在时自动创建"这条路径(见各成功用例开头的 Directory.Exists 前置断言)。
        _programPath = Path.Combine(_dir, "Work");
        _export = NewManager(_programPath);
    }

    public void Dispose() {
        try {
            // 不在这里清 SQLite 连接池(进程级 API,会与并行测试互撞);残留目录由 TestTempCleanup 兜底。
            Directory.Delete(_dir, true);
        } catch { /* best effort */ }
    }

    private string ExportsDir => Path.Combine(_programPath, ExportsDirName);

    /// <summary>用同一套服务、但换一个 <paramref name="programPath"/> 组装一个 manager(供自动创建 / 失败用例)。</summary>
    private ExportManager NewManager(string programPath) => new(
        new ClippingService(_clippingRepo),
        new LookupService(_lookupRepo),
        new VocabService(_vocabRepo),
        new OriginalClippingLineService(_originalRepo),
        new NullDeviceManager(),
        programPath, Path.Combine(_dir, "Backups"), Path.Combine(_dir, "Temp"));

    // ————————————————————— 成功路径:产物真的落在 Exports/ 下 —————————————————————

    [Fact]
    public void ExportClippingsToJson_WritesNonEmptyFileUnderExports() {
        SeedClipping("k1", "专注力是稀缺资源。", "深度工作");
        Assert.False(Directory.Exists(ExportsDir));   // 前置:目录还不存在,证明是导出方法自己建的

        Assert.True(_export.ExportClippingsToJson());

        Assert.True(Directory.Exists(ExportsDir));
        var path = Path.Combine(ExportsDir, "Clippings.json");
        AssertNonEmptyFile(path);
        // 内容真的是那本书 —— 防止"写了个空壳 JSON 也算过"。
        Assert.Contains("深度工作", File.ReadAllText(path), StringComparison.Ordinal);
    }

    [Fact]
    public void ExportVocabsToJson_WritesNonEmptyFileUnderExports() {
        SeedLookup("en:apple", "an apple a day");
        Assert.False(Directory.Exists(ExportsDir));

        Assert.True(_export.ExportVocabsToJson());

        var path = Path.Combine(ExportsDir, "Vocabs.json");
        AssertNonEmptyFile(path);
        Assert.Contains("apple", File.ReadAllText(path), StringComparison.Ordinal);
    }

    [Fact]
    public void ExportClippingsToCsv_WritesNonEmptyFileUnderExports() {
        SeedClipping("k1", "hello, world", "Book \"X\"");
        Assert.False(Directory.Exists(ExportsDir));

        Assert.True(_export.ExportClippingsToCsv());

        var path = Path.Combine(ExportsDir, "Clippings.csv");
        AssertNonEmptyFile(path);
        // CSV 带 UTF-8 BOM(给 Excel 认编码),故只断表头、不逐字节比。
        Assert.Contains("Content,Type,Book", File.ReadAllText(path), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExportVocabsToCsvAsync_WritesNonEmptyFileUnderExports() {
        SeedLookup("en:apple", "an apple a day");
        Assert.False(Directory.Exists(ExportsDir));

        // includeDefinitions=false:完全离线,不查释义 —— CI 不该联网、也不该把生词发给第三方。
        Assert.True(await _export.ExportVocabsToCsvAsync(includeDefinitions: false));

        var path = Path.Combine(ExportsDir, "Vocabs.csv");
        AssertNonEmptyFile(path);
        Assert.Contains("Word,Stem,Definition", File.ReadAllText(path), StringComparison.Ordinal);
    }

    [Fact]
    public void ExportObsidianVault_WritesVaultUnderExports() {
        SeedClipping("k1", "内容一", "三体");
        SeedLookup("en:apple", "an apple a day");
        Assert.False(Directory.Exists(ExportsDir));

        Assert.True(_export.ExportObsidianVault());

        // vault 建在 Exports/Obsidian/ 下:index.md / Vocabs.md 必在,书文件进 Books/。
        AssertNonEmptyFile(Path.Combine(ExportsDir, "Obsidian", "index.md"));
        AssertNonEmptyFile(Path.Combine(ExportsDir, "Obsidian", "Vocabs.md"));
        AssertNonEmptyFile(Path.Combine(ExportsDir, "Obsidian", "Books", "三体.md"));
    }

    // ————————————————————— 自动创建:programPath 本身都不存在 —————————————————————

    [Fact]
    public void Export_CreatesExportsDirectoryWhenWholeProgramPathIsMissing() {
        // programPath 指向一个**多级都不存在**的路径 —— 导出方法应把整条路径一次建出来,
        // 而不是要求调用方先备好目录(否则首次导出在干净安装上会直接失败)。
        var nested = Path.Combine(_dir, "deep", "a", "b");
        SeedClipping("k1", "内容一", "B");
        var export = NewManager(nested);

        Assert.True(export.ExportClippingsToJson());

        Assert.True(Directory.Exists(Path.Combine(nested, ExportsDirName)));
        AssertNonEmptyFile(Path.Combine(nested, ExportsDirName, "Clippings.json"));
    }

    // ————————————————————— 失败路径:异常必须被吞掉,只返回 false —————————————————————

    /// <summary>
    /// Exports 目录**建不出来**时(programPath 指向一个普通文件),每个 public 导出方法都必须
    /// 返回 <c>false</c>,且**不把异常泄漏**给调用方 —— 否则 UI 侧一次导出失败会直接把进程打崩。
    /// </summary>
    /// <remarks>
    /// 触发方式:把 <c>programPath</c> 指到一个**已存在的文件**,于是 <c>Directory.CreateDirectory</c>
    /// (在 <c>&lt;file&gt;/Exports</c> 上)必然抛异常。刻意用真实文件系统而非"会抛的替身":这样异常是
    /// 从**方法体内**抛出的、会走完整条 catch 路径,且不必为 5 个接口各造一套假实现。
    /// </remarks>
    [Theory]
    [InlineData("clippings-json")]
    [InlineData("vocabs-json")]
    [InlineData("clippings-csv")]
    [InlineData("vocabs-csv")]
    [InlineData("obsidian")]
    public async Task Export_WhenExportsDirectoryCannotBeCreated_ReturnsFalseWithoutThrowing(string kind) {
        var blocker = Path.Combine(_dir, "blocker");
        File.WriteAllText(blocker, "not a directory");   // 占位文件:它的子路径建不出来
        var export = NewManager(blocker);

        var ok = true;
        // 任何**泄漏出来**的异常都会被 Record.ExceptionAsync 捕获 → Assert.Null 变红。
        var thrown = await Record.ExceptionAsync(async () => ok = await RunExport(kind, export));

        Assert.Null(thrown);
        Assert.False(ok);
    }

    /// <summary>按用例名分派到对应的 public 导出方法(5 个里只有生词 CSV 是异步的)。</summary>
    private static async Task<bool> RunExport(string kind, ExportManager export) {
        switch (kind) {
            case "clippings-json": return export.ExportClippingsToJson();
            case "vocabs-json": return export.ExportVocabsToJson();
            case "clippings-csv": return export.ExportClippingsToCsv();
            case "vocabs-csv": return await export.ExportVocabsToCsvAsync(includeDefinitions: false);
            case "obsidian": return export.ExportObsidianVault();
            default: throw new ArgumentOutOfRangeException(nameof(kind), kind, "未知的导出口");
        }
    }

    // ————————————————————— helpers —————————————————————

    private void SeedClipping(string key, string content, string book) {
        Assert.True(_clippingRepo.Add(new Clipping {
            Key = key,
            Content = content,
            BookName = book,
            AuthorName = "作者",
            ClippingTypeLocation = "标注 位置 #1-1",
            ClippingDate = "2026-01-01 10:00:00",
            BriefType = (long)BriefType.Highlight,
        }), "插入标注夹具失败");
    }

    private void SeedLookup(string wordKey, string usage) {
        Assert.True(_lookupRepo.Add(new Lookup {
            WordKey = wordKey,
            Usage = usage,
            Title = "Some Book",
            Authors = "Some Author",
            Timestamp = "2026-01-01 10:00:00",
        }), "插入生词夹具失败");
    }

    /// <summary>断言文件存在且非空 —— 这是"真的落盘了"的最小证据。</summary>
    private static void AssertNonEmptyFile(string path) {
        Assert.True(File.Exists(path), $"产物不存在:{path}");
        Assert.True(new FileInfo(path).Length > 0, $"产物为空:{path}");
    }
}
