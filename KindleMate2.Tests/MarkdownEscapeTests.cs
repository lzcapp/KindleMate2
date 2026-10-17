using Markdig;
using Xunit;
using KindleMate2.Infrastructure.Helpers;

namespace KindleMate2.Tests;

/// <summary>
/// <see cref="StringHelper.EscapeMarkdownContent"/> 的直接单测。
/// </summary>
/// <remarks>
/// <para>
/// 2026-10 审阅发现:原实现把有序列表的**首位数字**转义(<c>\1.</c>),而 CommonMark 的反斜杠转义
/// 只对 ASCII 标点有效 —— 数字前的反斜杠会原样渲染出来,导出产物里多一个可见 <c>\</c>。
/// 本文件的断言方式刻意不止看转义串:用仓库依赖的 Markdig(与生产 Markdown→HTML 导出**同一个**渲染器)
/// 把转义结果真的渲染一遍,再断言"渲染出来的字与原文逐字相同、且没有生成列表/标题等结构"。
/// </para>
/// <para>
/// 走真实渲染而不是只断言字符串:防止"看起来转义了、渲染出来却不是字面量"这类问题再次漏网
/// (本次缺陷正是只看转义串时看不出来的那类)。
/// </para>
/// </remarks>
public sealed class MarkdownEscapeTests {
    private static readonly MarkdownPipeline Pipeline =
        new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

    /// <summary>转义 + 渲染后,按行断言文本逐字还原,且整段渲染结果里没有残留的反斜杠。</summary>
    private static void AssertRendersAsLiteralText(string content) {
        var escaped = StringHelper.EscapeMarkdownContent(content);
        var plain = Markdown.ToPlainText(escaped, Pipeline).Replace("\r", "");

        foreach (var line in content.Replace("\r", "").Split('\n')) {
            if (line.Length == 0) {
                continue;
            }
            Assert.Contains(line, plain, StringComparison.Ordinal);
        }
        // 转义只应产生 Markdown 转义序列(渲染时被吃掉);正文本身不含反斜杠时,渲染结果里不该有反斜杠。
        if (!content.Contains('\\', StringComparison.Ordinal)) {
            Assert.DoesNotContain("\\", plain, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void OrderedList_EscapesDelimiterNotLeadingDigit() {
        // 反斜杠转义只对 ASCII 标点有效:必须转义 '.' / ')' 而不是数字。
        Assert.Equal("1\\. 第一条", StringHelper.EscapeMarkdownContent("1. 第一条"));
        Assert.Equal("12\\) 第十二条", StringHelper.EscapeMarkdownContent("12) 第十二条"));
        Assert.Equal("2026\\. 年计划", StringHelper.EscapeMarkdownContent("2026. 年计划"));
    }

    [Theory]
    [InlineData("1. 第一条")]
    [InlineData("12) 第十二条")]
    [InlineData("2026. 年计划")]
    [InlineData("1. 第一条\n2. 第二条\n普通行")]
    public void OrderedListLikeLines_RenderAsLiteralText_NotAsList(string content) {
        var escaped = StringHelper.EscapeMarkdownContent(content);
        var html = Markdown.ToHtml(escaped, Pipeline);

        Assert.DoesNotContain("<ol", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<li", html, StringComparison.OrdinalIgnoreCase);
        AssertRendersAsLiteralText(content);
    }

    [Theory]
    [InlineData("# 这不是标题")]
    [InlineData("> 引用段")]
    [InlineData("---")]
    [InlineData("***")]
    [InlineData("~~~csharp")]
    [InlineData("- 列表项")]
    [InlineData("甲 | 乙\n--- | ---")]
    [InlineData("<b>不是标签</b>")]
    [InlineData("a `b` c")]
    public void BlockOrInlineStructures_RenderAsLiteralText(string content) {
        var escaped = StringHelper.EscapeMarkdownContent(content);
        var html = Markdown.ToHtml(escaped, Pipeline);

        // 不该生成的结构元素(HTML 结构由内容决定正是本方法要消除的问题)。
        Assert.DoesNotContain("<h1", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<h2", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<blockquote", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<hr", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<table", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<code>", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<b>", html, StringComparison.OrdinalIgnoreCase);
        AssertRendersAsLiteralText(content);
    }

    [Fact]
    public void BlockMarkerEscaping_OnlyAppliesWithinThreeLeadingSpaces() {
        // CommonMark 的块级标记只允许至多 3 个前导空格;4 个以上就是缩进代码块,块内反斜杠不生效,
        // 再转义反而会把 '\' 渲染出来(已知限制)。故块级判定限制在 ≤3 空格。
        Assert.Equal("   \\# h", StringHelper.EscapeMarkdownContent("   # h"));
        Assert.Equal("    # 缩进代码块", StringHelper.EscapeMarkdownContent("    # 缩进代码块"));
    }

    [Fact]
    public void LinkReferenceDefinition_RendersAsLiteralText_InsteadOfBeingSwallowed() {
        // 行首 "[1]: url" 原样写入会被 Markdig 当链接引用定义解析 —— 整行**不出现在**渲染结果里(丢正文)。
        // 转义开头的 '[' 后它降级为普通文本(URL 部分仍会被 AutoLinks 自动链接,属既有行为,不在此列)。
        const string content = "[1]: https://example.com";
        var escaped = StringHelper.EscapeMarkdownContent(content);

        Assert.Equal("\\[1]: https://example.com", escaped);
        var html = Markdown.ToHtml(escaped, Pipeline);
        // 改动前:链接引用定义不渲染 → html 是空串。
        Assert.NotEmpty(html);
        Assert.Contains(content, Markdown.ToPlainText(escaped, Pipeline), StringComparison.Ordinal);
    }

    [Fact]
    public void InlineSpecialChars_RenderAsLiteralText() {
        const string content = @"甲 | 乙 <b>x</b> a`b` c\d";
        var escaped = StringHelper.EscapeMarkdownContent(content);
        var html = Markdown.ToHtml(escaped, Pipeline);

        Assert.DoesNotContain("<b>", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("&lt;b&gt;", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<code>", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(content, Markdown.ToPlainText(escaped, Pipeline), StringComparison.Ordinal);
    }

    [Fact]
    public void OrdinaryCjkText_IsNotEscaped() {
        // 正常中文标点/普通文本不做全标点转义,逐字不变(可读性)。
        const string content = "我们认为，专注是一种能力。他说：“真的吗？”——是的！";
        Assert.Equal(content, StringHelper.EscapeMarkdownContent(content));
    }
}
