using System.Text;
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

    // ——————————————————————— 正文转义 / 编码 / 空导出(本 PR) ———————————————————————

    [Fact]
    public void ClippingsToMarkdown_EscapesContentThatWouldChangeDocumentStructure() {
        // 正文原样写入时:行首 '#' 会变成标题、'|' 会拆表格、裸 HTML 会被 Markdig 透传、
        // 行首有序列表会变成 <ol> —— 导出的排版因此取决于标注内容。这条钉住"正文一律按字面量渲染"。
        const string content = "# 这不是标题\n甲 | 乙\n<b>不是标签</b>\n1. 有序列表行";
        SeedClipping("k1", content, book: "深度工作");
        var outDir = Path.Combine(_dir, "out");

        Assert.True(_clippingService.ClippingsToMarkdown(outDir));

        var mdPath = Directory.GetFiles(outDir, "*.md").Single();
        var md = File.ReadAllText(mdPath);
        // ① 行首 '#' 被转义;② '|' 被转义(与 Obsidian 表格单元格同写法);③ 裸 HTML 被转义;
        // ④ 有序列表只转义分隔符(数字前的反斜杠不是 CommonMark 转义,会渲染出来)。
        Assert.Contains(@"\# 这不是标题", md, StringComparison.Ordinal);
        Assert.Contains(@"甲 \| 乙", md, StringComparison.Ordinal);
        Assert.Contains(@"\<b>不是标签\</b>", md, StringComparison.Ordinal);
        Assert.Contains(@"1\. 有序列表行", md, StringComparison.Ordinal);
        Assert.DoesNotContain(@"\1.", md, StringComparison.Ordinal);

        // 生成 HTML 时:正文的 '#' 不得成为标题,'<b>' 不得被当成标签透传,有序列表不得成 <ol>。
        var html = File.ReadAllText(Directory.GetFiles(outDir, "*.html").Single());
        var headings = Regex.Matches(html, "<h[1-6][^>]*>(.*?)</h[1-6]>", RegexOptions.Singleline)
            .Select(m => m.Groups[1].Value);
        Assert.DoesNotContain(headings, h => h.Contains("这不是标题", StringComparison.Ordinal));
        Assert.Contains("&lt;b&gt;不是标签&lt;/b&gt;", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<ol", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("1. 有序列表行", html, StringComparison.Ordinal);
    }

    [Fact]
    public void ClippingsToMarkdown_MarkdownHasNoByteOrderMark() {
        SeedClipping("k1", "内容", book: "深度工作");
        var outDir = Path.Combine(_dir, "out");

        Assert.True(_clippingService.ClippingsToMarkdown(outDir));

        // 读字节而不是文本:ReadAllText / StreamReader 会自动吃掉 BOM,那样断言恒绿。
        var bytes = File.ReadAllBytes(Directory.GetFiles(outDir, "*.md").Single());
        Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF,
            "Markdown 带了 UTF-8 BOM");
        // 首字符是标题的 '#' —— 进一步证明没有 BOM 占位。
        Assert.Equal((byte)'#', bytes[0]);
        // .html 是同一批产物里的另一份(2026-10 审阅:原来只改了 .md,编码不一致),同样不得带 BOM。
        var htmlBytes = File.ReadAllBytes(Directory.GetFiles(outDir, "*.html").Single());
        Assert.False(htmlBytes.Length >= 3 && htmlBytes[0] == 0xEF && htmlBytes[1] == 0xBB && htmlBytes[2] == 0xBF,
            "HTML 带了 UTF-8 BOM");
        Assert.Equal((byte)'<', htmlBytes[0]);
    }

    [Fact]
    public void LookupsToMarkdown_MarkdownHasNoByteOrderMark() {
        SeedLookup("en:apple", "apple");
        var outDir = Path.Combine(_dir, "vocab-out");

        Assert.True(_lookupService.LookupsToMarkdown(outDir));

        var bytes = File.ReadAllBytes(Directory.GetFiles(outDir, "*.md").Single());
        Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF,
            "Markdown 带了 UTF-8 BOM");
        Assert.Equal((byte)'#', bytes[0]);
        var htmlBytes = File.ReadAllBytes(Directory.GetFiles(outDir, "*.html").Single());
        Assert.False(htmlBytes.Length >= 3 && htmlBytes[0] == 0xEF && htmlBytes[1] == 0xBB && htmlBytes[2] == 0xBF,
            "HTML 带了 UTF-8 BOM");
        Assert.Equal((byte)'<', htmlBytes[0]);
    }

    [Fact]
    public void ClippingsToMarkdown_NonexistentBook_ReturnsFalseAndWritesNothing() {
        // 指定一本不存在的书:此前会写出一个只有标题的空文件并报成功。
        SeedClipping("k1", "内容一", book: "深度工作");
        var outDir = Path.Combine(_dir, "out");

        Assert.False(_clippingService.ClippingsToMarkdown(outDir, "并不存在的书"));

        Assert.False(Directory.Exists(outDir) && Directory.GetFiles(outDir, "*.md").Length > 0,
            "不存在的书不该产出任何 Markdown 文件");
    }

    [Fact]
    public void LookupsToMarkdown_NonexistentWord_ReturnsFalseAndWritesNothing() {
        SeedLookup("en:apple", "apple");
        var outDir = Path.Combine(_dir, "vocab-out");

        Assert.False(_lookupService.LookupsToMarkdown(outDir, "banana"));

        Assert.False(Directory.Exists(outDir) && Directory.GetFiles(outDir, "*.md").Length > 0,
            "不存在的词不该产出任何 Markdown 文件");
    }

    [Fact]
    public void ClippingsToMarkdown_ExistingBookWhoseEntriesHaveNoLocation_StillSucceeds() {
        // 反例保护:书**有条目**(只是位置为空,故 BuildMarkdownWithClippings 会跳过正文)仍属
        // 合法导出 —— 守卫必须只拒"该书一条都没有",不能把这种"合法但正文为空"的导出也拒掉。
        _clippingRepo.Add(new Clipping {
            Key = "k-nolocation",
            Content = "有内容但位置为空",
            BookName = "无位置的书",
            AuthorName = "作者",
            ClippingTypeLocation = string.Empty,
        });
        var outDir = Path.Combine(_dir, "out");

        Assert.True(_clippingService.ClippingsToMarkdown(outDir, "无位置的书"));
        Assert.Single(Directory.GetFiles(outDir, "*.md"));
    }

    [Fact]
    public void ClippingsToMarkdown_VeryLongBookName_WholePathStaysWithinByteLimit() {
        // 单段文件名有 240 字节上限,但"目录 + 文件名 + 扩展名"叠加后另有一层整路径上限。
        var longBook = new string('长', 400);   // 1200 字节,远超单段上限
        SeedClipping("k1", "内容", book: longBook);
        var outDir = Path.Combine(_dir, "out");

        Assert.True(_clippingService.ClippingsToMarkdown(outDir, longBook));

        var mdPath = Directory.GetFiles(outDir, "*.md").Single();
        Assert.True(Encoding.UTF8.GetByteCount(mdPath) <= StringHelper.DefaultMaxPathUtf8Bytes,
            $"整路径 {Encoding.UTF8.GetByteCount(mdPath)} 字节,超过 {StringHelper.DefaultMaxPathUtf8Bytes}");
    }

    [Fact]
    public void ClippingsToMarkdown_TwoDifferentVeryLongBookNames_BothFilesSurvive() {
        // 回归(2026-10 审阅):整路径截断若发生在撞名判定**之后**,两个不同但共享长前缀的超长书名
        // 会截断成同一个文件名,第二次单书导出静默覆盖第一次(allocator 的存在意义正是防这个)。
        // 现在 allocator 先按整路径预算截断、再拿最终名字做消歧,且序号本身不会被截掉。
        var prefix = new string('长', 120);
        var bookA = prefix + "（上册）";
        var bookB = prefix + "（下册）";
        SeedClipping("k1", "上册的内容", book: bookA, author: "");
        SeedClipping("k2", "下册的内容", book: bookB, author: "");
        var outDir = Path.Combine(_dir, "out");

        Assert.True(_clippingService.ClippingsToMarkdown(outDir, bookA));
        Assert.True(_clippingService.ClippingsToMarkdown(outDir, bookB));

        var mdFiles = Directory.GetFiles(outDir, "*.md");
        Assert.Equal(2, mdFiles.Length);
        var all = string.Concat(mdFiles.Select(File.ReadAllText));
        Assert.Contains("上册的内容", all, StringComparison.Ordinal);
        Assert.Contains("下册的内容", all, StringComparison.Ordinal);
        // 两个身份行都在:证明各自产物都还在(谁都没被覆盖)。
        Assert.Contains("## 📖 " + bookA, all, StringComparison.Ordinal);
        Assert.Contains("## 📖 " + bookB, all, StringComparison.Ordinal);
        // 整路径仍在字节预算内。
        Assert.All(mdFiles, f => Assert.True(
            Encoding.UTF8.GetByteCount(f) <= StringHelper.DefaultMaxPathUtf8Bytes,
            $"整路径 {Encoding.UTF8.GetByteCount(f)} 字节,超过 {StringHelper.DefaultMaxPathUtf8Bytes}:{f}"));
        Assert.Equal(2, Directory.GetFiles(outDir, "*.html").Length);
    }

    // ————————————————————————— 撞名消歧(PR-A) —————————————————————————

    [Fact]
    public void ClippingsToMarkdown_CollidingBookNames_BothSurvive_FirstNotOverwritten() {
        // 「A/B」与「A:B」净化后同为 A_B;此前第二次会**静默覆盖**第一次(.md 与 .html 一起丢)。
        SeedClipping("k1", "甲的内容", book: "A/B");
        SeedClipping("k2", "乙的内容", book: "A:B");
        var outDir = Path.Combine(_dir, "out");

        Assert.True(_clippingService.ClippingsToMarkdown(outDir, "A/B"));
        Assert.True(_clippingService.ClippingsToMarkdown(outDir, "A:B"));

        var mdFiles = Directory.GetFiles(outDir, "*.md");
        Assert.Equal(2, mdFiles.Length);
        var all = string.Concat(mdFiles.Select(File.ReadAllText));
        // 第一本的内容必须仍在(没被第二本覆盖)。
        Assert.Contains("甲的内容", all);
        Assert.Contains("乙的内容", all);
        // .html 与 .md 同基名,故也应各有一份。
        Assert.Equal(2, Directory.GetFiles(outDir, "*.html").Length);
    }

    [Fact]
    public void ClippingsToMarkdown_SameBookTwice_ReusesFile_NoNumberedSuffix() {
        SeedClipping("k1", "内容一", book: "深度工作");
        var outDir = Path.Combine(_dir, "out");

        Assert.True(_clippingService.ClippingsToMarkdown(outDir, "深度工作"));
        Assert.True(_clippingService.ClippingsToMarkdown(outDir, "深度工作"));

        // 重复导出同一本书 → 幂等覆盖,不堆出 (2)。
        var mdFiles = Directory.GetFiles(outDir, "*.md");
        Assert.Single(mdFiles);
        Assert.DoesNotContain("(", Path.GetFileNameWithoutExtension(mdFiles[0]));
    }

    [Fact]
    public void LookupsToMarkdown_CollidingWords_BothSurvive_FirstNotOverwritten() {
        // 生词侧同一问题:Word 由 WordKey 去掉语言前缀得到,「A/B」与「A:B」净化后同为 A_B。
        SeedLookup("en:A/B", "AAA");   // Lookup.Word = "A/B"
        SeedLookup("en:A:B", "BBB");   // Lookup.Word = "A:B"
        var outDir = Path.Combine(_dir, "vocab-out");

        Assert.True(_lookupService.LookupsToMarkdown(outDir, "A/B"));
        Assert.True(_lookupService.LookupsToMarkdown(outDir, "A:B"));

        var mdFiles = Directory.GetFiles(outDir, "*.md");
        Assert.Equal(2, mdFiles.Length);
        var all = string.Concat(mdFiles.Select(File.ReadAllText));
        Assert.Contains("usage of AAA", all);
        Assert.Contains("usage of BBB", all);
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
