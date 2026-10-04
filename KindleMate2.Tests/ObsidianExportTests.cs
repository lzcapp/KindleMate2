using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;
using KindleMate2.Application.Services;
using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Domain.Models.Export;

namespace KindleMate2.Tests;

/// <summary>
/// Obsidian 导出(PR2)的纯逻辑:中间模型 → vault 目录(index.md / Vocabs.md / Books/*.md)。
/// </summary>
/// <remarks>
/// 全部走 <see cref="ExportManager.WriteObsidianVault"/> 与 <see cref="ObsidianExportWriter"/>,
/// 是静态纯函数(不碰库、不联网、不依赖设备),风格与 <see cref="JsonExportTests"/> /
/// <see cref="CsvExportTests"/> 一致。
/// <para>
/// YAML 的断言一律**解析回读**(而不是对文本做 Contains):只有把 <c>key: "值"</c> 真的反转义一遍,
/// 才能证明"引号与反斜杠转义对了" —— 字符串匹配在转义写法变了的时候照样会绿。
/// </para>
/// </remarks>
public sealed class ObsidianExportTests : IDisposable {
    // 固定时刻:格式必须与 schema 样例逐字一致(带偏移、不带毫秒),故不能用 DateTimeOffset.Now。
    private static readonly DateTimeOffset FixedAt = new(2026, 10, 4, 20, 30, 0, TimeSpan.FromHours(8));
    private const string FixedAtText = "2026-10-04T20:30:00+08:00";

    private readonly string _dir;

    public ObsidianExportTests() {
        _dir = Path.Combine(Path.GetTempPath(), "km2-obsidian-" + Guid.NewGuid().ToString("N")[..10]);
        Directory.CreateDirectory(_dir);
    }

    public void Dispose() {
        try { Directory.Delete(_dir, true); } catch { /* best effort */ }
    }

    /// <summary>Exports 根目录下由 writer 自建的 Obsidian 子目录。</summary>
    private string VaultDir => Path.Combine(_dir, "Obsidian");
    private string BooksDir => Path.Combine(VaultDir, "Books");
    private string IndexPath => Path.Combine(VaultDir, "index.md");
    private string VocabsPath => Path.Combine(VaultDir, "Vocabs.md");

    // ————————————————————————— 结构 —————————————————————————

    [Fact]
    public void WriteVault_EmptyInput_ProducesIndexAndEmptyBooksDirectory() {
        ExportManager.WriteObsidianVault(new List<Clipping>(), new List<Lookup>(), _dir, FixedAt);

        // 空集合也要产出**合法**的 index.md(不能缺文件),并留下一个空的 Books/ 目录。
        Assert.True(File.Exists(IndexPath));
        Assert.True(File.Exists(VocabsPath));
        Assert.True(Directory.Exists(BooksDir));
        Assert.Empty(Directory.GetFiles(BooksDir));

        var index = File.ReadAllText(IndexPath);
        Assert.Contains("0 本书", index, StringComparison.Ordinal);
        Assert.Contains("0 条标注", index, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteVault_ProducesOneFilePerBook() {
        var clippings = new List<Clipping> {
            Clipping("k1", "内容一", book: "书一"),
            Clipping("k2", "内容二", book: "书二"),
            Clipping("k3", "内容三", book: "书三"),
        };

        ExportManager.WriteObsidianVault(clippings, new List<Lookup>(), _dir, FixedAt);

        Assert.Equal(3, Directory.GetFiles(BooksDir, "*.md").Length);
    }

    [Fact]
    public void WriteVault_BookFile_CarriesFrontmatterTitleAuthorAndCount() {
        var clippings = new List<Clipping> {
            Clipping("k1", "第一句", book: "三体", author: "刘慈欣", location: "Location 1", date: ""),
            Clipping("k2", "第二句", book: "三体", author: "刘慈欣", location: "Location 2", date: ""),
        };

        ExportManager.WriteObsidianVault(clippings, new List<Lookup>(), _dir, FixedAt);

        var md = File.ReadAllText(Path.Combine(BooksDir, "三体.md"));
        Assert.Equal("三体", ReadQuotedFrontmatterValue(md, "title"));
        Assert.Equal("刘慈欣", ReadQuotedFrontmatterValue(md, "author"));
        Assert.Equal("2", ReadFrontmatterLine(md, "clippings"));
        Assert.Equal("kindle", ReadFrontmatterLine(md, "source"));
        // 正文含两条,且用 --- 分隔(第一条与标题/作者之间只留空行)。
        Assert.Contains("第一句\n\n---\n\n**Location 2**", md, StringComparison.Ordinal);
    }

    // ————————————————————————— YAML 转义 —————————————————————————

    [Theory]
    [InlineData("Sapiens: A Brief History")]   // 冒号 + 空格:裸标量会被当成键值分隔
    [InlineData("#Tagged Book")]               // 行首 '#' :裸标量会被当成注释
    [InlineData("He said \"hi\"")]             // 内嵌双引号:必须转义,否则提前闭合标量
    [InlineData("line1\nline2")]               // 内嵌换行:双引号标量里必须写成 \n 字面量
    [InlineData("---")]                        // 三个连字符:裸写会被当成文档分隔符
    [InlineData("back\\slash")]                // 反斜杠:必须转义,否则与后续转义序列混淆
    public void WriteVault_EscapesYamlTitle_RoundTripsOriginalValue(string title) {
        var clippings = new List<Clipping> { Clipping("k", "正文", book: title, author: "作者") };

        ExportManager.WriteObsidianVault(clippings, new List<Lookup>(), _dir, FixedAt);

        var md = File.ReadAllText(Directory.GetFiles(BooksDir, "*.md").Single());
        // 先做**结构合法性**检查:标量本身必须是合法 YAML(见该 helper 的 remarks ——
        // 只靠下面的回读断言挡不住"少转义一个引号")。
        AssertValidYamlDoubleQuoted(ReadFrontmatterLine(md, "title"));
        // 书名经中间模型 Trim 后才写 frontmatter,故期望值同样 Trim;其余转义必须逐字还原。
        Assert.Equal(title.Trim(), ReadQuotedFrontmatterValue(md, "title"));
    }

    [Fact]
    public void WriteVault_EscapesYamlAuthor_RoundTripsOriginalValue() {
        // 作者**不经**中间模型 Trim(只有书名 Trim),故前后空格必须靠双引号标量原样保住。
        var clippings = new List<Clipping> { Clipping("k", "正文", book: "书", author: "  刘慈欣  ") };

        ExportManager.WriteObsidianVault(clippings, new List<Lookup>(), _dir, FixedAt);

        var md = File.ReadAllText(Directory.GetFiles(BooksDir, "*.md").Single());
        AssertValidYamlDoubleQuoted(ReadFrontmatterLine(md, "author"));
        Assert.Equal("  刘慈欣  ", ReadQuotedFrontmatterValue(md, "author"));
    }

    /// <summary>
    /// 断言一个 YAML 双引号标量**自身合法**:整体被双引号包住,内部每个 <c>"</c> 都被转义,
    /// 每个 <c>\</c> 都起一个合法转义序列(<c>\\</c> / <c>\"</c> / <c>\n</c> / <c>\r</c> / <c>\t</c>)。
    /// </summary>
    /// <remarks>
    /// 为什么不能只靠 <see cref="UnquoteYamlDoubleQuoted"/> 回读:那个反转义器对未转义的 <c>"</c>
    /// 是**宽容**的(掐头去尾取 body,内层引号原样留下),于是 writer 少转义一个引号时回读结果
    /// 照样正确 —— 2026-10-04 变异验证实测:把 <c>'"'</c> 的转义去掉,回读断言全绿、只有本检查
    /// 能抓住。故这里独立做一次**结构合法性**校验,不依赖 writer 的转义实现。
    /// <para>
    /// 刻意不引入真正的 YAML 解析器(如 YamlDotNet):为一条断言给测试工程加依赖不划算,
    /// 而这个结构检查足以覆盖"引号/反斜杠没转义"这一类缺陷。真要做严格 YAML 往返,
    /// 属于另一个量级的改动。
    /// </para>
    /// </remarks>
    private static void AssertValidYamlDoubleQuoted(string scalar) {
        Assert.StartsWith("\"", scalar, StringComparison.Ordinal);
        Assert.EndsWith("\"", scalar, StringComparison.Ordinal);
        Assert.True(scalar.Length >= 2, $"标量短于一对引号:{scalar}");

        var body = scalar[1..^1];
        for (var i = 0; i < body.Length; i++) {
            var c = body[i];
            Assert.True(c != '"', $"双引号标量里出现**未转义**的 '\"'(位置 {i}):{scalar}");
            if (c != '\\') {
                continue;
            }
            i++;
            Assert.True(i < body.Length, $"标量以孤立的反斜杠结尾:{scalar}");
            Assert.True(body[i] is '\\' or '"' or 'n' or 'r' or 't',
                $"非法转义序列 '\\{body[i]}':{scalar}");
        }
    }

    // ————————————————————————— 正文形态 —————————————————————————

    [Fact]
    public void WriteVault_OmitsPageSegmentWhenPageIsNull() {
        var clippings = new List<Clipping> {
            Clipping("k1", "有页码", book: "B", page: 12, location: "Location 3", date: ""),
            Clipping("k2", "无页码", book: "B", page: 0, location: "Location 4", date: ""),   // 库里"无页码"记 0
        };

        ExportManager.WriteObsidianVault(clippings, new List<Lookup>(), _dir, FixedAt);

        var md = File.ReadAllText(Path.Combine(BooksDir, "B.md"));
        Assert.Contains("**Location 3 · 第 12 页**", md, StringComparison.Ordinal);
        Assert.Contains("**Location 4**", md, StringComparison.Ordinal);
        // 全文只应出现一次「第 X 页」—— 无页码那条不能写成「第 0 页」。
        Assert.Single(Regex.Matches(md, @"第 \d+ 页"));
    }

    [Fact]
    public void WriteVault_OmitsAuthorQuoteWhenAuthorIsEmpty() {
        var clippings = new List<Clipping> { Clipping("k", "正文", book: "B", author: "") };

        ExportManager.WriteObsidianVault(clippings, new List<Lookup>(), _dir, FixedAt);

        var md = File.ReadAllText(Path.Combine(BooksDir, "B.md"));
        Assert.Equal("", ReadQuotedFrontmatterValue(md, "author"));
        // 正文里不该出现 "> 作者" 引用行(带前导换行的 '> ' 即为该行)。
        Assert.DoesNotContain("\n> ", md, StringComparison.Ordinal);
    }

    // ————————————————————————— 文件名消歧 —————————————————————————

    [Fact]
    public void WriteVault_DisambiguatesCollidingFileNames_AndLinksToActualFiles() {
        // "A/B" 与 "A:B" 净化后同为 "A_B" —— 两本不同的书撞在同一个文件名上。
        var clippings = new List<Clipping> {
            Clipping("k1", "内容一", book: "A/B", author: "X"),
            Clipping("k2", "内容二", book: "A:B", author: "Y"),
        };

        ExportManager.WriteObsidianVault(clippings, new List<Lookup>(), _dir, FixedAt);

        var files = Directory.GetFiles(BooksDir, "*.md")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => name != null)
            .Select(name => name!)
            .ToHashSet(StringComparer.Ordinal);
        Assert.Equal(2, files.Count);

        // 双链必须指向**实际生成的文件名**(去掉 .md),否则 Obsidian 里链接全断。
        var targets = ExtractWikilinkTargets(File.ReadAllText(IndexPath));
        Assert.Equal(files, targets);
        Assert.Contains("A_B", targets);
        Assert.Contains("Y - A_B", targets);
    }

    [Fact]
    public void WriteVault_Disambiguation_IsDeterministic() {
        var clippings = new List<Clipping> {
            Clipping("k1", "内容一", book: "A/B", author: "X"),
            Clipping("k2", "内容二", book: "A:B", author: "Y"),
        };

        // 同一份输入导出两次(到不同根目录),文件名集合必须逐字相同 —— 否则链接会随导出漂移。
        ExportManager.WriteObsidianVault(clippings, new List<Lookup>(), _dir, FixedAt);
        var first = Directory.GetFiles(BooksDir, "*.md").Select(Path.GetFileName).OrderBy(n => n).ToList();

        var second = Path.Combine(_dir, "again");
        Directory.CreateDirectory(second);
        ExportManager.WriteObsidianVault(clippings, new List<Lookup>(), second, FixedAt);
        var secondNames = Directory.GetFiles(Path.Combine(second, "Obsidian", "Books"), "*.md")
            .Select(Path.GetFileName).OrderBy(n => n).ToList();

        Assert.Equal(first, secondNames);
    }

    [Fact]
    public void WriteVault_EmptyBookName_FallsBackToUnknownBook() {
        var clippings = new List<Clipping> { Clipping("k", "没有书名的标注", book: null) };

        ExportManager.WriteObsidianVault(clippings, new List<Lookup>(), _dir, FixedAt);

        Assert.True(File.Exists(Path.Combine(BooksDir, "Unknown Book.md")));
        Assert.Contains("[[Unknown Book]]", File.ReadAllText(IndexPath), StringComparison.Ordinal);
    }

    // ————————————————————————— 生词 —————————————————————————

    [Fact]
    public void WriteVault_VocabsGroupedByBook_WithUnknownSourceFallback() {
        var lookups = new List<Lookup> {
            new() {
                WordKey = "en:apple", Stem = "appl", Usage = "an apple a day",
                Title = "Book X", Authors = "A", Timestamp = "2026-01-01 10:00:00",
            },
            new() { WordKey = "en:banana", Usage = "a banana", Title = null },   // 无来源书 → 未知来源
        };

        ExportManager.WriteObsidianVault(new List<Clipping>(), lookups, _dir, FixedAt);

        var md = File.ReadAllText(VocabsPath);
        Assert.Contains("## Book X", md, StringComparison.Ordinal);
        Assert.Contains("## 未知来源", md, StringComparison.Ordinal);
        Assert.Contains("apple", md, StringComparison.Ordinal);
        Assert.Contains("banana", md, StringComparison.Ordinal);
        Assert.Contains("an apple a day", md, StringComparison.Ordinal);
        // 词干 / 语言"有则显示"。
        Assert.Contains("词干 appl", md, StringComparison.Ordinal);
        Assert.Contains("语言 en", md, StringComparison.Ordinal);
    }

    // ————————————————————————— 落盘细节 —————————————————————————

    [Fact]
    public void WriteVault_ProducesFilesWithoutByteOrderMark() {
        var clippings = new List<Clipping> { Clipping("k", "正文", book: "B") };

        ExportManager.WriteObsidianVault(clippings, new List<Lookup>(), _dir, FixedAt);

        foreach (var path in Directory.GetFiles(VaultDir, "*.md", SearchOption.AllDirectories)) {
            // 读字节而不是文本:File.ReadAllText / StreamReader 会自动吃掉 BOM,那样断言就恒绿了。
            var bytes = File.ReadAllBytes(path);
            Assert.True(bytes.Length > 0);
            Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF,
                $"{Path.GetFileName(path)} 带了 UTF-8 BOM");
            // index.md / Vocabs.md 以 '#' 开头,书文件以 frontmatter 的 '-' 开头。
            Assert.True(bytes[0] == (byte)'#' || bytes[0] == (byte)'-');
        }
    }

    [Fact]
    public void WriteVault_UsesSchemaTimestampFormat() {
        var clippings = new List<Clipping> { Clipping("k", "正文", book: "B") };

        ExportManager.WriteObsidianVault(clippings, new List<Lookup>(), _dir, FixedAt);

        Assert.Contains(FixedAtText, File.ReadAllText(IndexPath), StringComparison.Ordinal);
        Assert.Equal(FixedAtText,
            ReadFrontmatterLine(File.ReadAllText(Path.Combine(BooksDir, "B.md")), "exported_at"));
        // 顺带钉住"格式就是 schema 那一个":带偏移、不带毫秒。
        Assert.Equal(FixedAtText, FixedAt.ToString(ExportSchema.TimestampFormat, CultureInfo.InvariantCulture));
    }

    // ————————————————————————— helpers —————————————————————————

    private static Clipping Clipping(string key, string content, string? book = "Book",
        string author = "作者", int? page = null, string location = "Location 1",
        string date = "2026-01-01 10:00:00") {
        return new Clipping {
            Key = key,
            Content = content,
            BookName = book,
            AuthorName = author,
            PageNumber = page,
            ClippingTypeLocation = location,
            ClippingDate = date,
            BriefType = (long)BriefType.Highlight,
        };
    }

    /// <summary>取 frontmatter 里某键的**原始**值(不做引号反转义)。</summary>
    private static string ReadFrontmatterLine(string markdown, string key) {
        var lines = markdown.Split('\n');
        Assert.Equal("---", lines[0]);
        var prefix = key + ": ";
        for (var i = 1; i < lines.Length; i++) {
            if (lines[i] == "---") {
                break;
            }
            if (lines[i].StartsWith(prefix, StringComparison.Ordinal)) {
                return lines[i][prefix.Length..];
            }
        }
        throw new InvalidOperationException($"frontmatter 缺键 {key}");
    }

    /// <summary>取 frontmatter 里某键的**双引号标量**值并反转义回原值。</summary>
    private static string ReadQuotedFrontmatterValue(string markdown, string key) =>
        UnquoteYamlDoubleQuoted(ReadFrontmatterLine(markdown, key));

    /// <summary>把 <c>"…"</c> 形式的 YAML 双引号标量反转义回原字符串(与 writer 的转义互逆)。</summary>
    private static string UnquoteYamlDoubleQuoted(string scalar) {
        Assert.StartsWith("\"", scalar, StringComparison.Ordinal);
        Assert.EndsWith("\"", scalar, StringComparison.Ordinal);

        var body = scalar[1..^1];
        var sb = new StringBuilder(body.Length);
        for (var i = 0; i < body.Length; i++) {
            var c = body[i];
            if (c != '\\') {
                sb.Append(c);
                continue;
            }
            i++;
            sb.Append(body[i] switch {
                'n' => '\n',
                'r' => '\r',
                't' => '\t',
                '\\' => '\\',
                '"' => '"',
                _ => body[i],
            });
        }
        return sb.ToString();
    }

    /// <summary>抽出 index.md 里所有 <c>[[目标]]</c> / <c>[[目标|别名]]</c> 的**目标**部分。</summary>
    private static HashSet<string> ExtractWikilinkTargets(string markdown) =>
        Regex.Matches(markdown, @"\[\[([^\]|]+)(?:\|[^\]]*)?\]\]")
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);
}
