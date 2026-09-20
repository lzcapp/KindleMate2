using Microsoft.Data.Sqlite;
using Xunit;
using KindleMate2.Application.Services.KM2DB;
using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Domain.Interfaces.KM2DB;
using KindleMate2.Infrastructure.Helpers;
using KindleMate2.Infrastructure.Repositories.KM2DB;

namespace KindleMate2.Tests;

/// <summary>
/// 导出路径的用例。导出是唯一"把数据带出应用"的出口，此前同样没有测试覆盖。
/// 这里钉住的是**产物形态**（生成哪些文件、文件名从哪来、内容是否真的含书摘），
/// 而不是 markdown 的排版细节 —— 排版改动不该轻易让测试红。
/// </summary>
public sealed class MarkdownExportTests : IDisposable {
    private readonly string _dir;
    private readonly string _clippingRepoPath;
    private readonly IClippingRepository _clippingRepo;
    private readonly ILookupRepository _lookupRepo;
    private readonly ClippingService _clippingService;
    private readonly LookupService _lookupService;

    public MarkdownExportTests() {
        _dir = Path.Combine(Path.GetTempPath(), "km2-export-tests-" + Guid.NewGuid().ToString("N")[..10]);
        Directory.CreateDirectory(_dir);

        _clippingRepoPath = Path.Combine(_dir, "KM2.dat");
        Assert.True(DatabaseHelper.CreateDatabase(_clippingRepoPath, out var ex), "CreateDatabase failed: " + ex.Message);

        var cs = DatabaseHelper.GetConnectionString(_clippingRepoPath);
        _clippingRepo = new ClippingRepository(cs);
        _lookupRepo = new LookupRepository(cs);
        _clippingService = new ClippingService(_clippingRepo);
        _lookupService = new LookupService(_lookupRepo);
    }

    public void Dispose() {
        try {
            SqliteConnection.ClearAllPools();
            Directory.Delete(_dir, true);
        } catch { /* best effort */ }
    }

    // ————————————————————————— 标注导出 —————————————————————————

    [Fact]
    public void ClippingsToMarkdown_WritesMarkdownHtmlAndStylesheet() {
        SeedClipping("k1", "专注力是稀缺资源。", book: "深度工作");
        var outDir = Path.Combine(_dir, "out");

        Assert.True(_clippingService.ClippingsToMarkdown(outDir));

        Assert.Single(Directory.GetFiles(outDir, "*.md"));
        Assert.Single(Directory.GetFiles(outDir, "*.html"));
        // 样式表是 markdown 的配套产物 —— HTML 里引用了它,缺了会样式全丢。
        // 断言"存在 .css"而不是具体文件名,免得实现改个常量名就把测试弄红。
        Assert.NotEmpty(Directory.GetFiles(outDir, "*.css"));
    }

    [Fact]
    public void ClippingsToMarkdown_ContentContainsTheHighlightText() {
        const string content = "忙碌不等于高效。";
        SeedClipping("k1", content, book: "深度工作");
        var outDir = Path.Combine(_dir, "out");

        Assert.True(_clippingService.ClippingsToMarkdown(outDir));

        var md = File.ReadAllText(Directory.GetFiles(outDir, "*.md").Single());
        Assert.Contains(content, md);
        Assert.Contains("深度工作", md);
    }

    [Fact]
    public void ClippingsToMarkdown_ForOneBook_UsesBookNameAsFileName() {
        SeedClipping("k1", "内容一", book: "深度工作");
        SeedClipping("k2", "内容二", book: "人类简史");
        var outDir = Path.Combine(_dir, "out");

        Assert.True(_clippingService.ClippingsToMarkdown(outDir, "人类简史"));

        // 单本导出时文件名取自书名;只应产出一个 md,而不是所有书混在一起
        var mdFiles = Directory.GetFiles(outDir, "*.md");
        Assert.Single(mdFiles);
        Assert.Contains("人类简史", Path.GetFileNameWithoutExtension(mdFiles[0]));
        var md = File.ReadAllText(mdFiles[0]);
        Assert.Contains("内容二", md);
        Assert.DoesNotContain("内容一", md);
    }

    [Fact]
    public void ClippingsToMarkdown_BookNameWithIllegalChars_StillProducesAFile() {
        // 书名里带路径分隔符 / 通配符时必须被 sanitize,否则会在写文件时炸掉
        SeedClipping("k1", "内容一", book: @"../../非法:书名*?");
        var outDir = Path.Combine(_dir, "out");

        Assert.True(_clippingService.ClippingsToMarkdown(outDir, @"../../非法:书名*?"));

        Assert.Single(Directory.GetFiles(outDir, "*.md"));
    }

    [Fact]
    public void ClippingsToMarkdown_BookNameWithIllegalChars_FileNameIsPortable() {
        // 上一条只要求"能产出文件" —— 在 macOS 上含 ':' '?' '*' 的名字也写得出来,所以它放过了这个缺陷。
        // 这条进一步要求产出的文件名**不含任何 Windows 非法字符**:导出目录常被拷到 Windows / SMB 共享,
        // 而净化用的字符集此前取自 Path.GetInvalidFileNameChars(),在 macOS 上只有 '\0' 与 '/'。
        const string book = "非法:书名*?<>|\"\\ 与 / 分隔符";
        SeedClipping("k1", "内容一", book: book);
        var outDir = Path.Combine(_dir, "out");

        Assert.True(_clippingService.ClippingsToMarkdown(outDir, book));

        var name = Path.GetFileNameWithoutExtension(Directory.GetFiles(outDir, "*.md").Single());
        Assert.DoesNotContain(name, c => "<>:\"/\\|?*".Contains(c) || char.IsControl(c));
        Assert.False(name.EndsWith('.'));
    }

    [Fact]
    public void ClippingsToMarkdown_OnEmptyDatabase_StillWritesFiles() {
        var outDir = Path.Combine(_dir, "out");

        Assert.True(_clippingService.ClippingsToMarkdown(outDir));

        Assert.Single(Directory.GetFiles(outDir, "*.md"));
    }

    [Fact]
    public void ClippingsToMarkdown_CreatesDirectoryWhenMissing() {
        SeedClipping("k1", "内容一");
        var nested = Path.Combine(_dir, "a", "b", "c");

        Assert.True(_clippingService.ClippingsToMarkdown(nested));

        Assert.True(Directory.Exists(nested));
    }

    // ————————————————————————— 生词导出 —————————————————————————

    [Fact]
    public void LookupsToMarkdown_WritesMarkdownFile() {
        SeedLookup("word-1", "apple");
        var outDir = Path.Combine(_dir, "vocab-out");
        Directory.CreateDirectory(outDir);

        Assert.True(_lookupService.LookupsToMarkdown(outDir));

        var mdFiles = Directory.GetFiles(outDir, "*.md");
        Assert.NotEmpty(mdFiles);
    }

    // ————————————————————————— helpers —————————————————————————

    private void SeedClipping(string key, string content, string book = "Book", string author = "Author") {
        _clippingRepo.Add(new Clipping {
            Key = key,
            Content = content,
            BookName = book,
            AuthorName = author,
            // 必须给 location:导出会**静默跳过** location 或 content 为空的条目
            // (StringHelper.BuildMarkdownWithClippings 里的 continue),夹具少了它就会
            // 得到一份只有标题、没有正文的 markdown。
            ClippingTypeLocation = "标注 位置 #1-1",
        });
    }

    private void SeedLookup(string wordKey, string word) {
        _lookupRepo.Add(new Lookup {
            WordKey = wordKey,
            Usage = $"usage of {word}",
            Title = "Some Book",
            Authors = "Some Author",
            Timestamp = "1",
        });
    }
}
