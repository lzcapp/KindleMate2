using Xunit;
using KindleMate2.Shared.Clippings;

namespace KindleMate2.Tests;

/// <summary>
/// 首尾标点清洗规则的用例(纯逻辑,不碰库)。
///
/// 这组用例的重点不是"能把标点清掉",而是**规则刻意克制的地方不许被清掉** ——
/// 句末的「。！？」、半句上的逗号、破折号与省略号、以及**左**引号。
/// 清过头会改写用户的正文,而这批数据是要导出/同步出去的,
/// 所以每一条"克制"都得有一条用例钉住(否则日后有人顺手把两个集合一合并就静默改了语义)。
/// </summary>
public sealed class ClippingCleanRulesTests {

    // ————————————————————————— 首部:清「上一句的收尾」 —————————————————————————

    /// <summary>用户报的那个场景:划线的段落边界落在标点上,开头多出一个句号。</summary>
    [Fact]
    public void Clean_RemovesLeadingSentenceEndPunctuation() {
        var result = Clean("。重点是理解这一点。");

        Assert.Equal(ClippingCleanOutcome.Cleaned, result.Outcome);
        Assert.Equal("重点是理解这一点。", result.Text);
    }

    /// <summary>剥掉首部标点后可能又露出空白,而且「。 」这种"标点+空格"要连剥两轮才收敛。</summary>
    [Fact]
    public void Clean_PeelsLeadingNoiseAcrossMultipleRounds() {
        var result = Clean("。 ，；重点是");

        Assert.Equal(ClippingCleanOutcome.Cleaned, result.Outcome);
        Assert.Equal("重点是", result.Text);
    }

    /// <summary>右引号与右括号在首部 —— 结构上只可能是上一句的收尾。</summary>
    [Fact]
    public void Clean_RemovesLeadingRightQuotesAndClosingBrackets() {
        var result = Clean("\u201D\u2019）】》」』｝他说");

        Assert.Equal(ClippingCleanOutcome.Cleaned, result.Outcome);
        Assert.Equal("他说", result.Text);
    }

    /// <summary>半角标点同样处理,否则中英混排的标注会漏清。</summary>
    [Fact]
    public void Clean_RemovesLeadingAsciiPunctuation() {
        var result = Clean(",.;:!?)]}abc");

        Assert.Equal(ClippingCleanOutcome.Cleaned, result.Outcome);
        Assert.Equal("abc", result.Text);
    }

    /// <summary>全角句点(FF0E)与间隔号(B7)在首部也是噪音。</summary>
    [Fact]
    public void Clean_RemovesLeadingFullWidthPeriodAndMiddleDot() {
        var result = Clean("\uFF0E\u00B7正文");

        Assert.Equal(ClippingCleanOutcome.Cleaned, result.Outcome);
        Assert.Equal("正文", result.Text);
    }

    // ————————————————————————— 尾部:清「下一句的开头」 —————————————————————————

    [Fact]
    public void Clean_RemovesTrailingLeftQuotesAndOpeningBrackets() {
        var result = Clean("正文（【《「『｛");

        Assert.Equal(ClippingCleanOutcome.Cleaned, result.Outcome);
        Assert.Equal("正文", result.Text);
    }

    [Fact]
    public void Clean_RemovesTrailingAsciiOpeners() {
        var result = Clean("正文([{");

        Assert.Equal(ClippingCleanOutcome.Cleaned, result.Outcome);
        Assert.Equal("正文", result.Text);
    }

    // ————————————————————————— 刻意不清:句末标点 —————————————————————————

    /// <summary>
    /// **最关键的一条克制**:句末的「。！？」与 ASCII <c>. ! ?</c> 是句子真正结束的地方,
    /// 清掉就等于把用户划的这句话截断了。
    /// </summary>
    [Theory]
    [InlineData("这是一句话。")]
    [InlineData("真没想到！")]
    [InlineData("是真的吗？")]
    [InlineData("Hello!")]
    [InlineData("Is it?")]
    [InlineData("End.")]
    public void Clean_KeepsSentenceEndingPunctuation(string input) {
        var result = Clean(input);

        Assert.Equal(ClippingCleanOutcome.Unchanged, result.Outcome);
        Assert.Equal(input, result.Text);
    }

    /// <summary>
    /// 标注**本来就可能停在半句上**,那个逗号是用户自己选中的,不是噪音。
    /// (首部的逗号要清、尾部的逗号要留 —— 这条不对称是规则的核心。)
    /// </summary>
    [Theory]
    [InlineData("标注停在半句，")]
    [InlineData("clipping stops mid sentence,")]
    [InlineData("标注停在分号；")]
    public void Clean_KeepsTrailingCommaOfHalfSentence(string input) {
        var result = Clean(input);

        Assert.Equal(ClippingCleanOutcome.Unchanged, result.Outcome);
        Assert.Equal(input, result.Text);
    }

    /// <summary>破折号与省略号两端都歧义大于噪声(「——他走过来」也可以是正文起头),一律不碰。</summary>
    [Theory]
    [InlineData("——他走过来")]
    [InlineData("他走过去——")]
    [InlineData("……然后呢")]
    [InlineData("然后呢……")]
    public void Clean_LeavesDashesAndEllipsisAlone(string input) {
        var result = Clean(input);

        Assert.Equal(ClippingCleanOutcome.Unchanged, result.Outcome);
        Assert.Equal(input, result.Text);
    }

    // ————————————————————————— 刻意不清:左引号 —————————————————————————

    /// <summary>
    /// 全角字形下左右引号长得几乎一样,所以必须**按码位**区分:
    /// 201C/2018(左)是正文的一部分,**首部不清**;201D/2019(右)才是噪音。
    /// 这条一旦写反,「"他说」这类标注会被清成一个孤零零的右引号开头,或者把正文的对白引号吃掉。
    /// </summary>
    [Theory]
    [InlineData("\u201C他说")]   // 左双引号
    [InlineData("\u2018他说")]   // 左单引号
    public void Clean_KeepsLeadingLeftQuotes(string input) {
        var result = Clean(input);

        Assert.Equal(ClippingCleanOutcome.Unchanged, result.Outcome);
        Assert.Equal(input, result.Text);
    }

    [Theory]
    [InlineData("\u201D他说", "他说")]   // 右双引号
    [InlineData("\u2019他说", "他说")]   // 右单引号
    public void Clean_RemovesLeadingRightQuotes(string input, string expected) {
        var result = Clean(input);

        Assert.Equal(ClippingCleanOutcome.Cleaned, result.Outcome);
        Assert.Equal(expected, result.Text);
    }

    /// <summary>尾部同理,但方向相反:左引号在尾部要清,右引号在尾部是正文的收尾引号,要留。</summary>
    [Fact]
    public void Clean_RemovesTrailingLeftQuote_ButKeepsTrailingRightQuote() {
        Assert.Equal("他说", Clean("他说\u201C").Text);

        var kept = Clean("他说\u201D");
        Assert.Equal(ClippingCleanOutcome.Unchanged, kept.Outcome);
        Assert.Equal("他说\u201D", kept.Text);
    }

    // ————————————————————————— 全标点:不落库,免得造出空条目 —————————————————————————

    /// <summary>
    /// 整条只有标点(用户误划、或只划到一个句号)时**必须保住原文**:
    /// 清下去会得到空串,把一条标注变成空条目,比留着噪音更糟。
    /// 注意 <see cref="ClippingCleanResult.Text"/> 在这种情形下返回的是**原文**,
    /// 调用方据此计数并原样保留。
    /// </summary>
    [Theory]
    [InlineData("。")]
    [InlineData("。「")]
    [InlineData("。 ")]
    public void Clean_AllPunctuation_KeepsOriginalText(string input) {
        var result = Clean(input);

        Assert.Equal(ClippingCleanOutcome.AllPunctuation, result.Outcome);
        Assert.Equal(input, result.Text);
        Assert.False(result.Changed);
    }

    /// <summary>纯空白等同于「没有内容」,也算全标点(不落库)。</summary>
    [Fact]
    public void Clean_WhitespaceOnly_IsAllPunctuation() {
        var result = Clean("   ");

        Assert.Equal(ClippingCleanOutcome.AllPunctuation, result.Outcome);
        Assert.Equal("   ", result.Text);
    }

    // ————————————————————————— 收敛与边界 —————————————————————————

    /// <summary>
    /// 幂等是「重建数据库不会把清洗结果回退」的前提(重建会拿已清洗的文本再走一遍).
    /// 顺带覆盖"剥到收敛":标点与空白交错时必须连剥到不动点,而不是只剥一轮。
    /// </summary>
    [Theory]
    [InlineData("。重点是理解这一点。")]
    [InlineData("。 ，；重点是")]
    [InlineData("\u201D\u2019）】》」』｝他说")]
    [InlineData("正文（【《「『｛")]
    [InlineData("  。正文。  ")]
    public void Clean_IsIdempotent(string input) {
        var first = Clean(input);

        var second = Clean(first.Text);

        Assert.Equal(ClippingCleanOutcome.Unchanged, second.Outcome);
        Assert.Equal(first.Text, second.Text);
    }

    [Fact]
    public void Clean_NullOrEmpty_IsUnchanged() {
        var fromNull = ClippingCleanRules.Clean(null);
        Assert.Equal(ClippingCleanOutcome.Unchanged, fromNull.Outcome);
        Assert.Equal(string.Empty, fromNull.Text);

        var fromEmpty = Clean(string.Empty);
        Assert.Equal(ClippingCleanOutcome.Unchanged, fromEmpty.Outcome);
        Assert.Equal(string.Empty, fromEmpty.Text);
    }

    /// <summary>整条内容外层的空白顺手去掉(它同样是划线段落边界带出来的噪音)。</summary>
    [Fact]
    public void Clean_TrimsSurroundingWhitespace() {
        var result = Clean("  正文  ");

        Assert.Equal(ClippingCleanOutcome.Cleaned, result.Outcome);
        Assert.Equal("正文", result.Text);
    }

    [Fact]
    public void Clean_AlreadyCleanText_IsUnchanged() {
        var result = Clean("正文");

        Assert.Equal(ClippingCleanOutcome.Unchanged, result.Outcome);
        Assert.Equal("正文", result.Text);
    }

    /// <summary>
    /// **只清整条内容的最外侧**,不动中间:中间的标点是正文,中间行的行首更可能是正文。
    /// </summary>
    [Fact]
    public void Clean_TouchesOnlyTheOutermostCharacters() {
        var inner = Clean("。中间。还有。");
        Assert.Equal("中间。还有。", inner.Text);

        // 行首的标点属于正文,不能按"每一行的行首"去清
        var multiline = Clean("行一\n。行二");
        Assert.Equal(ClippingCleanOutcome.Unchanged, multiline.Outcome);
        Assert.Equal("行一\n。行二", multiline.Text);
    }

    private static ClippingCleanResult Clean(string input) => ClippingCleanRules.Clean(input);
}
