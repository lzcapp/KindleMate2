using System.Text;
using Xunit;
using KindleMate2.Application.Services;
using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Domain.Models.Export;
using KindleMate2.Infrastructure.Helpers;

namespace KindleMate2.Tests;

/// <summary>
/// 导出文件名分配(<see cref="ExportFileNameAllocator"/>)的纯逻辑。
/// </summary>
/// <remarks>
/// 全部是静态纯函数(不碰库、不联网、不依赖设备),风格与 <see cref="ObsidianExportTests"/> /
/// <see cref="SanitizeFilenameTests"/> 一致。
/// <para>
/// 回归背景:既有的「按书导出 Markdown」路径此前**没有任何撞名处理** —— 书名净化后同名(如
/// <c>A/B</c> 与 <c>A:B</c> 都净化成 <c>A_B</c>)时,后导的那本会**静默覆盖**先导的那本。
/// 本文件用"往临时目录真写真读"的方式复现真实导出,断言前一本**没被覆盖**。
/// </para>
/// </remarks>
public sealed class ExportFileNameAllocatorTests : IDisposable {
    private readonly string _dir;

    public ExportFileNameAllocatorTests() {
        _dir = Path.Combine(Path.GetTempPath(), "km2-fname-" + Guid.NewGuid().ToString("N")[..10]);
        Directory.CreateDirectory(_dir);
    }

    public void Dispose() {
        try { Directory.Delete(_dir, true); } catch { /* best effort */ }
    }

    // ————————————————————————— 批量场景(Obsidian) —————————————————————————

    [Fact]
    public void AssignFileNames_Batch_CollidingTitles_GetDistinctDeterministicNames() {
        var books = new List<ExportBook> {
            new() { Title = "A/B", Author = "X" },
            new() { Title = "A:B", Author = "Y" },
        };

        var names = ExportFileNameAllocator.AssignFileNames(books);

        Assert.Equal(2, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal("A_B", names[0]);
        // 撞名后按候选序退化为「作者 - 书名」。
        Assert.Equal("Y - A_B", names[1]);
        // 确定性:同一输入再分配一次,结果逐字相同(不用随机数 / 时间戳消歧)。
        Assert.Equal(names, ExportFileNameAllocator.AssignFileNames(books));
    }

    // ————————————————————————— 单书场景(clippings 侧) —————————————————————————

    [Fact]
    public void AssignSingleFileName_CollidingBooks_ProduceTwoFiles_AndFirstIsNotOverwritten() {
        // 先导《A/B》,再导《A:B》—— 二者净化后同为 A_B。
        var first = WriteClippingExport("A/B", "甲的内容", author: "作者");
        var second = WriteClippingExport("A:B", "乙的内容", author: "作者");

        Assert.Equal("A_B", first);
        Assert.NotEqual(first, second);

        var files = Directory.GetFiles(_dir, "*.md");
        Assert.Equal(2, files.Length);
        // 第一本的内容必须**仍在**(没被第二本覆盖)。
        var all = string.Concat(files.Select(File.ReadAllText));
        Assert.Contains("甲的内容", all, StringComparison.Ordinal);
        Assert.Contains("乙的内容", all, StringComparison.Ordinal);
    }

    [Fact]
    public void AssignSingleFileName_SameBookTwice_ReusesFileName_NoNumberedSuffix() {
        var first = WriteClippingExport("深度工作", "内容一");
        var second = WriteClippingExport("深度工作", "内容二（更新后）");

        // 同一本书重复导出:复用同一个文件名(幂等覆盖),不堆出 (2)。
        Assert.Equal(first, second);
        var files = Directory.GetFiles(_dir, "*.md");
        Assert.Single(files);
        Assert.DoesNotContain("(", Path.GetFileNameWithoutExtension(files[0]));
        // 且内容为最新一次。
        Assert.Contains("内容二（更新后）", File.ReadAllText(files[0]), StringComparison.Ordinal);
    }

    [Fact]
    public void AssignSingleFileName_DifferentBookOnSameName_FallsBackToAuthorQualifiedName() {
        WriteClippingExport("A/B", "甲", author: "X");

        var second = ExportFileNameAllocator.AssignSingleFileName(_dir, "A:B", author: "Y");

        Assert.Equal("Y - A_B", second);
    }

    [Fact]
    public void AssignSingleFileName_AllCandidatesTaken_NumbersSequentially_AndReusesOwnNumberedName() {
        // 三本无作者的书,净化后同为 A_B;候选序只有一条,故依次落到 A_B / A_B (2) / A_B (3)。
        var first = WriteClippingExport("A/B", "一");
        var second = WriteClippingExport("A:B", "二");
        var third = WriteClippingExport("A|B", "三");

        Assert.Equal("A_B", first);
        Assert.Equal("A_B (2)", second);
        Assert.Equal("A_B (3)", third);
        Assert.Equal(3, Directory.GetFiles(_dir, "*.md").Length);

        // 重导第二本:必须复用它**自己的** A_B (2),而不是再往后堆 (4)。
        Assert.Equal("A_B (2)", ExportFileNameAllocator.AssignSingleFileName(_dir, "A:B"));
    }

    [Fact]
    public void AssignSingleFileName_ForeignFileWithoutIdentityLine_IsNeverOverwritten() {
        // 目录里已有一个同名文件,但**不是**我们的产物(没有 ## 📖 身份行)→ 绝不能复用 / 覆盖它。
        const string foreign = "用户自己的笔记,没有身份标题行";
        File.WriteAllText(Path.Combine(_dir, "A_B.md"), foreign);

        var name = ExportFileNameAllocator.AssignSingleFileName(_dir, "A/B");

        Assert.NotEqual("A_B", name);
        Assert.Equal(foreign, File.ReadAllText(Path.Combine(_dir, "A_B.md")));
    }

    [Fact]
    public void AssignSingleFileName_EmptyDirectory_ReturnsSanitizedTitle() {
        var name = ExportFileNameAllocator.AssignSingleFileName(_dir, "Sapiens: A Brief History");

        Assert.Equal("Sapiens_ A Brief History", name);
    }

    // ————————————————————————— 生词侧 —————————————————————————

    [Fact]
    public void AssignSingleFileName_CollidingWords_ProduceTwoFiles_AndFirstIsNotOverwritten() {
        // 生词没有作者 → 只有「净化(词)」一条候选序。
        // 注意 usage 里刻意**不含**词本身:BuildMarkdownWithLookups 会把正文中的词加粗包裹,含词的断言会失真。
        var first = WriteLookupExport("A/B", "第一条释义");
        var second = WriteLookupExport("A:B", "第二条释义");

        Assert.Equal("A_B", first);
        Assert.Equal("A_B (2)", second);

        var files = Directory.GetFiles(_dir, "*.md");
        Assert.Equal(2, files.Length);
        var all = string.Concat(files.Select(File.ReadAllText));
        Assert.Contains("第一条释义", all, StringComparison.Ordinal);
        Assert.Contains("第二条释义", all, StringComparison.Ordinal);
        // 两个身份行都在:证明各自的产物都还在,谁都没被覆盖。
        Assert.Contains("## 📖 A/B", all, StringComparison.Ordinal);
        Assert.Contains("## 📖 A:B", all, StringComparison.Ordinal);
    }

    [Fact]
    public void AssignSingleFileName_SameWordTwice_ReusesFileName() {
        var first = WriteLookupExport("apple", "an apple a day");

        var second = ExportFileNameAllocator.AssignSingleFileName(_dir, "apple");

        Assert.Equal(first, second);
        Assert.Single(Directory.GetFiles(_dir, "*.md"));
    }

    // ————————————————————————— helpers —————————————————————————

    /// <summary>模拟一次「按书导出 Markdown」:分配文件名 → 用与真实路径同一 helper 拼正文 → 落盘。</summary>
    private string WriteClippingExport(string bookName, string content, string? author = null) {
        var baseName = ExportFileNameAllocator.AssignSingleFileName(_dir, bookName, author);
        var markdown = new StringBuilder();
        markdown.AppendLine("# 📚 书");
        markdown.AppendLine();
        markdown.Append(StringHelper.BuildMarkdownWithClippings(new List<Clipping> {
            new() {
                Key = "k",
                BookName = bookName,
                AuthorName = author,
                Content = content,
                ClippingTypeLocation = "位置 #1",
            },
        }));
        File.WriteAllText(Path.Combine(_dir, baseName + ".md"), markdown.ToString());
        return baseName;
    }

    /// <summary>模拟一次「按词导出生词 Markdown」:身份行是 <c>## 📖 &lt;词&gt;</c>。</summary>
    private string WriteLookupExport(string word, string usage) {
        var baseName = ExportFileNameAllocator.AssignSingleFileName(_dir, word);
        var markdown = new StringBuilder();
        markdown.AppendLine("# 📚 生词");
        markdown.AppendLine();
        markdown.Append(StringHelper.BuildMarkdownWithLookups(new List<Lookup> {
            new() { WordKey = "en:" + word, Usage = usage, Title = "Book", Authors = "A", Timestamp = "t" },
        }));
        File.WriteAllText(Path.Combine(_dir, baseName + ".md"), markdown.ToString());
        return baseName;
    }
}
