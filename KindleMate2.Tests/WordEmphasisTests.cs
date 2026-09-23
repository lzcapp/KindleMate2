using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;
using KindleMate2.Application.Services;

namespace KindleMate2.Tests;

/// <summary>
/// 生词高亮切分(纯逻辑,不碰界面)。
///
/// 这组用例的重心不在"能不能切出来",而在两件容易静默出错的事:
/// ① **拼接必须还原原文** —— 切分一旦吃掉/重复一个字符,用户看到的是被改过的正文,
///    而正文是要导出、要同步出去的;所以每条用例末尾都过一遍 <see cref="AssertLossless"/>。
/// ② **不命中也不能切坏** —— 没命中时返回的那一条必须与原串逐字相等,
///    否则"没做高亮"这个最常见的情形反而最容易出问题。
///
/// 匹配口径刻意与 <c>FindClippingsContainingWord</c> 一致(大小写不敏感、子串、
/// 单字词不参与),用例里把这三条各自钉住 —— 口径分叉会造出"列出来了却一个字没加粗"的行。
/// </summary>
public sealed class WordEmphasisTests {

    /// <summary>把各段接回去必须**逐字**等于原文。所有用例都拿它兜底。</summary>
    private static void AssertLossless(string text, IReadOnlyList<EmphasisSegment> segments) {
        Assert.Equal(text, string.Concat(segments.Select(s => s.Text)));
    }

    private static string MatchedText(string text, string? word) =>
        string.Concat(WordEmphasis.Split(text, word).Where(s => s.IsMatch).Select(s => s.Text));

    // ————————————————————————— 基本命中 —————————————————————————

    [Fact]
    public void Split_MarksTheWordInTheMiddle() {
        var segments = WordEmphasis.Split("I read beautiful things.", "beautiful");

        Assert.Equal(new[] {
            new EmphasisSegment("I read ", false),
            new EmphasisSegment("beautiful", true),
            new EmphasisSegment(" things.", false)
        }, segments);
        AssertLossless("I read beautiful things.", segments);
    }

    /// <summary>大小写不敏感,但**输出保留原文大小写** —— 生词条里常是大写开头,句子里不是,
    /// 按词条改写正文等于改了用户读到的句子。</summary>
    [Fact]
    public void Split_IsCaseInsensitiveButKeepsOriginalCasing() {
        var segments = WordEmphasis.Split("The Beautiful view.", "beautiful");

        Assert.Equal("Beautiful", Assert.Single(segments, s => s.IsMatch).Text);
        AssertLossless("The Beautiful view.", segments);
    }

    [Fact]
    public void Split_MatchesAtStartAndAtEnd() {
        var head = WordEmphasis.Split("beautiful day", "beautiful");
        Assert.True(head[0].IsMatch);
        Assert.Equal("beautiful", head[0].Text);

        var tail = WordEmphasis.Split("a beautiful", "beautiful");
        Assert.True(tail[^1].IsMatch);
        Assert.Equal("beautiful", tail[^1].Text);
    }

    /// <summary>出现多次要**全部**加粗,不是只加第一处。</summary>
    [Fact]
    public void Split_MarksEveryOccurrence() {
        var segments = WordEmphasis.Split("beautiful, more beautiful", "beautiful");

        Assert.Equal(2, segments.Count(s => s.IsMatch));
        Assert.Equal("beautifulbeautiful", MatchedText("beautiful, more beautiful", "beautiful"));
        AssertLossless("beautiful, more beautiful", segments);
    }

    /// <summary>子串命中(不要求整词):"run" 命中 "running" 的前三个字母。
    /// 这是**有意**的取舍 —— 单词在句子里几乎总是变形出现,要求整词就等于漏掉它们。</summary>
    [Fact]
    public void Split_MatchesSubstringsNotOnlyWholeWords() {
        var segments = WordEmphasis.Split("running fast", "run");

        Assert.Equal("run", segments[0].Text);
        Assert.True(segments[0].IsMatch);
        Assert.False(segments[1].IsMatch);
        AssertLossless("running fast", segments);
    }

    /// <summary>相邻命中不会被合并成一个长段,也不会重叠切。</summary>
    [Fact]
    public void Split_HandlesAdjacentOccurrencesWithoutOverlap() {
        var segments = WordEmphasis.Split("ababab", "ab");

        Assert.Equal(new[] {
            new EmphasisSegment("ab", true),
            new EmphasisSegment("ab", true),
            new EmphasisSegment("ab", true)
        }, segments);
        AssertLossless("ababab", segments);
    }

    /// <summary>重叠的可能(needle="aa", text="aaa")从左往右取,不回头 —— 剩下的 "a" 归入未命中段。</summary>
    [Fact]
    public void Split_DoesNotBacktrackOnOverlappingCandidates() {
        var segments = WordEmphasis.Split("aaa", "aa");

        Assert.Equal(new[] {
            new EmphasisSegment("aa", true),
            new EmphasisSegment("a", false)
        }, segments);
        AssertLossless("aaa", segments);
    }

    // ————————————————————————— 中文 —————————————————————————

    /// <summary>中文没有大小写,但必须有:用户 190 个生词里 178 个含中文。</summary>
    [Fact]
    public void Split_MatchesChineseWord() {
        var text = "他去过高要,高要是个区。";
        var segments = WordEmphasis.Split(text, "高要");

        Assert.Equal(2, segments.Count(s => s.IsMatch));
        AssertLossless(text, segments);
    }

    // ————————————————————————— 不该高亮的情形 —————————————————————————

    /// <summary>单字词不参与 —— 口径同列表那一侧:一个字几乎命中每一行,满屏加粗等于没加粗。</summary>
    [Fact]
    public void Split_SkipsSingleCharacterWord() {
        var segments = WordEmphasis.Split("说的就是你", "说");

        Assert.Equal(new[] { new EmphasisSegment("说的就是你", false) }, segments);
    }

    /// <summary>词条两端带空白(自建词表里常见)要先修掉,否则空格会跟着被加粗。</summary>
    [Fact]
    public void Split_TrimsWhitespaceAroundTheWord() {
        var segments = WordEmphasis.Split("a cat here", "  cat  ");

        Assert.Equal(new[] {
            new EmphasisSegment("a ", false),
            new EmphasisSegment("cat", true),
            new EmphasisSegment(" here", false)
        }, segments);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Split_WithNoUsableWord_ReturnsOnePlainSegment(string? word) {
        var segments = WordEmphasis.Split("原样正文", word);

        Assert.Equal(new[] { new EmphasisSegment("原样正文", false) }, segments);
    }

    [Fact]
    public void Split_WithEmptyText_ReturnsNothing() {
        Assert.Empty(WordEmphasis.Split(string.Empty, "cat"));
        Assert.Empty(WordEmphasis.Split(null, "cat"));
    }

    [Fact]
    public void Split_WhenNothingMatches_ReturnsTheWholeTextUntouched() {
        var segments = WordEmphasis.Split("完全无关的一句话", "cat");

        Assert.Equal(new[] { new EmphasisSegment("完全无关的一句话", false) }, segments);
        AssertLossless("完全无关的一句话", segments);
    }

    /// <summary>词比正文还长 —— 不能越界,也不能把正文吃掉。</summary>
    [Fact]
    public void Split_WhenWordIsLongerThanText_ReturnsTheWholeTextUntouched() {
        const string text = "short";
        var segments = WordEmphasis.Split(text, "a very long word");

        Assert.Equal(new[] { new EmphasisSegment(text, false) }, segments);
        AssertLossless(text, segments);
    }

    /// <summary>正文恰好等于生词:整段命中,没有空的前后段。</summary>
    [Fact]
    public void Split_WhenTextEqualsWord_ProducesASingleMatchedSegment() {
        var segments = WordEmphasis.Split("beautiful", "beautiful");

        Assert.Equal(new[] { new EmphasisSegment("beautiful", true) }, segments);
    }

    // ————————————————————————— 特殊字符 —————————————————————————

    /// <summary>按**字面**匹配,不是正则 —— 含 <c>+</c>/<c>(</c> 的词条不能被当成模式解释掉。</summary>
    [Theory]
    [InlineData("C++ is fine", "C++")]
    [InlineData("a (b) c", "(b)")]
    [InlineData("价格 3.5 元", "3.5")]
    [InlineData("结尾 dollar$ 符号", "dollar$")]
    [InlineData("a**b", "**")]
    public void Split_TreatsTheWordAsLiteralText(string text, string word) {
        var segments = WordEmphasis.Split(text, word);
        Assert.Contains(segments, s => s.IsMatch);
        AssertLossless(text, segments);
    }

    /// <summary>换行/制表符要原样保留在段里(界面按包不换行的前提切好了,但这里不该自作主张)。</summary>
    [Fact]
    public void Split_KeepsWhitespaceInsideTheText() {
        const string text = "第一行\n第二行 cat 结尾\t";
        var segments = WordEmphasis.Split(text, "cat");

        Assert.Contains(segments, s => s.IsMatch);
        AssertLossless(text, segments);
    }

    // ————————————————————————— 代理对安全 —————————————————————————

    /// <summary>
    /// 正文里夹着代理对(emoji / 扩展区汉字)时,切点不能落在一个码元对中间 ——
    /// 那会把一个字符劈成两个"豆腐块"。这里用一个 emoji 直接贴着命中词的场景:
    /// 命中词前面的段必须以**完整的** emoji 收尾。
    ///
    /// (构造方式:先按"字符"取原文的前缀,再断言段里没有落单的代理码元。)
    /// </summary>
    [Fact]
    public void Split_NeverSplitsASurrogatePair() {
        const string emoji = "\U0001F600";              // 2 个 UTF-16 码元
        var text = string.Concat("开头", emoji, "cat", emoji, "结尾");

        var segments = WordEmphasis.Split(text, "cat");

        Assert.Equal(text, string.Concat(segments.Select(s => s.Text)));
        foreach (var segment in segments) {
            Assert.False(char.IsHighSurrogate(segment.Text[^1]),
                $"段尾不能是落单的高位代理:{Describe(segment.Text)}");
            Assert.False(char.IsLowSurrogate(segment.Text[0]),
                $"段首不能是落单的低位代理:{Describe(segment.Text)}");
        }
    }

    /// <summary>
    /// 畸形词条(从代理对**中间**截断,只剩低位那一半)不能把正文切坏。
    ///
    /// 构造要点:低位半截必须**带够两个字**才进得了切分(单字词在上游就被挡掉),
    /// 所以拼成 "低位代理 + y",正文里正好有这一串。
    /// 命中会落在低位代理上 —— 要么劈坏字符,要么整段原样返回;正确做法是后者。
    /// </summary>
    [Fact]
    public void Split_WithMalformedWord_LeavesTheTextIntact() {
        const string emoji = "\U0001F600";
        var text = "x" + emoji + "y";
        var lowHalf = emoji.Substring(1, 1);            // 落单的低位代理

        var segments = WordEmphasis.Split(text, lowHalf + "y");

        Assert.DoesNotContain(segments, s => s.IsMatch);
        Assert.Equal(text, string.Concat(segments.Select(s => s.Text)));
    }

    private static string Describe(string value) {
        var builder = new StringBuilder();
        foreach (var ch in value) builder.Append("U+").Append(((int)ch).ToString("X4")).Append(' ');
        return builder.ToString().TrimEnd();
    }
}
