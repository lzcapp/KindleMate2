using System.Text.RegularExpressions;
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
            // 刻意**不**在这里清 SQLite 连接池:那是进程级 API,会和并行跑的其他测试类互撞
            // (实测 ~1/6 概率报 ObjectDisposedException)。残留目录由 TestTempCleanup 在
            // 进程退出时统一清扫 —— 那里已无测试在跑,清池不会伤到谁。
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
    public void ClippingsToMarkdown_AllBooks_TableOfContentsLinksToRealHeadingIds() {
        // 这条用例守的是**第三方 TOC 扩展**那条路径,此前整个套件没有一条用例碰它
        // (全项目搜 "toc" 在测试目录下零命中),而它恰恰是 Markdig 升级时最容易坏的地方:
        //
        //   ① HTML 目录由 Leisn.MarkdigToc 生成(UseTableOfContent,见 ClippingService/LookupService);
        //   ② 该包 0.1.3 是 2021 年发布的,针对 Markdig **0.26.0** 编译;
        //   ③ 它自己的 nuspec 只写 `Markdig version="0.26.0"` —— NuGet 语义下这是"≥0.26.0、
        //      无上限",所以 Markdig 一路升到 1.4.0 会被**静默接受**,还原阶段连警告都没有;
        //   ④ 它的扩展方法偏偏挂在 `Markdig` 命名空间下(`Markdig.TocExtensions.UseTableOfContent`),
        //      源码上 `using Markdig;` 就能调 —— 完全看不出跨了包边界,也不会编译失败。
        //
        // 于是"升级后 260 例全绿"对这条路径不构成证据:坏掉的表现是**运行时不产出目录**
        // (或目录锚点对不上标题),而不是编译报错。这条用例把那层证据补上。
        //
        // 只断言"目录存在、且锚点能对上真实标题 id"这一结构性事实,不钉具体排版:
        // 将来换成手写目录、或插件改了引号风格,都不该让这条红。
        SeedClipping("k1", "内容一", book: "深度工作");
        SeedClipping("k2", "内容二", book: "人类简史");
        var outDir = Path.Combine(_dir, "out");

        // [TOC] 标记只在"全部书籍"分支写入(单本导出没有),所以这里刻意不传 bookName。
        Assert.True(_clippingService.ClippingsToMarkdown(outDir));

        var html = File.ReadAllText(Directory.GetFiles(outDir, "*.html").Single());

        // ① 目录容器在(插件被换掉/失效时最直接的信号)
        Assert.Contains("<nav", html, StringComparison.Ordinal);

        // ② 标题 id 与目录锚点都要有,且两者有交集 —— 即"目录真的链到了标题",而不是各写各的。
        //    引号两种风格都收:插件当前输出单引号(`href='#books'`),手写实现多半是双引号。
        var headingIds = ExtractCaptures(html, "<h[1-6] id=[\"']([^\"']+)[\"']");
        var tocTargets = ExtractCaptures(html, "<a href=[\"']#([^\"']+)[\"']");
        Assert.NotEmpty(headingIds);
        Assert.NotEmpty(tocTargets);
        Assert.NotEmpty(headingIds.Intersect(tocTargets));
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

    /// <summary>取出正则第一个捕获组的所有匹配值。</summary>
    private static HashSet<string> ExtractCaptures(string input, string pattern) =>
        Regex.Matches(input, pattern).Select(m => m.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
}
