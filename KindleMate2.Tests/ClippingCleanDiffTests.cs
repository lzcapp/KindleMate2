using System;
using System.Collections.Generic;
using Xunit;
using KindleMate2.Shared.Clippings;

namespace KindleMate2.Tests;

/// <summary>
/// 「改动预览」的两块纯逻辑:差异分解(<see cref="ClippingCleanDiff"/>)与保留首尾的中间省略
/// (<see cref="ElidedText"/>)。
///
/// 为什么要为"显示"写用例:上一版预览把「改前 / 改后」都截成**前 60 字**,
/// 而清洗恰恰只动首尾 —— 尾部那个被清掉的标点永远落在截断线之外,
/// 于是 89 条改动在预览里**一条差异都看不出来**,整个预览等于白做。
/// 所以这里钉住的不是"能截断",而是「**截断之后首尾的改动仍然可见**」。
/// </summary>
public sealed class ClippingCleanDiffTests {

    // ————————————————————————— 差异分解 —————————————————————————

    /// <summary>首部噪音:上一句的收尾标点被划进来,正是本功能要解决的问题。</summary>
    [Fact]
    public void Compute_AttributesLeadingPunctuationToLeadingNoise() {
        var diff = ClippingCleanDiff.Compute("。重点是理解这一点。", "重点是理解这一点。");

        Assert.Equal("。", diff.LeadingNoise);
        Assert.Equal("重点是理解这一点。", diff.Core);
        Assert.Equal(string.Empty, diff.TrailingNoise);
        Assert.True(diff.HasNoise);
    }

    /// <summary>尾部噪音:下一句的开头(左引号 / 左括号)。</summary>
    [Fact]
    public void Compute_AttributesTrailingPunctuationToTrailingNoise() {
        var diff = ClippingCleanDiff.Compute("他走了。「", "他走了。");

        Assert.Equal(string.Empty, diff.LeadingNoise);
        Assert.Equal("他走了。", diff.Core);
        Assert.Equal("「", diff.TrailingNoise);
    }

    /// <summary>两端都有噪音 —— 这是"取最早子串位置"最容易被算错的一档。</summary>
    [Fact]
    public void Compute_SplitsBothEndsWhenNoiseIsOnEachSide() {
        var diff = ClippingCleanDiff.Compute("。他走了。「", "他走了。");

        Assert.Equal("。", diff.LeadingNoise);
        Assert.Equal("他走了。", diff.Core);
        Assert.Equal("「", diff.TrailingNoise);
    }

    /// <summary>
    /// 首尾噪音字符相同时不许张冠李戴:必须**各归各端**。
    /// 「。」只能算首部(尾部只清左引号/左括号),所以这里期望首部拿「。」、尾部拿「(」。
    /// </summary>
    [Fact]
    public void Compute_DoesNotSwapEndsWhenBothSidesLookSimilar() {
        var diff = ClippingCleanDiff.Compute("。a(", "a");

        Assert.Equal("。", diff.LeadingNoise);
        Assert.Equal("a", diff.Core);
        Assert.Equal("(", diff.TrailingNoise);
    }

    /// <summary>没改过的条目:三段拼回去等于原文,且噪音为空。</summary>
    [Fact]
    public void Compute_ReportsNoNoiseForUnchangedText() {
        var diff = ClippingCleanDiff.Compute("重点是理解这一点。", "重点是理解这一点。");

        Assert.False(diff.HasNoise);
        Assert.Equal("重点是理解这一点。", diff.Core);
    }

    /// <summary>
    /// 不变量(对规则真实产出的**每一条**样本都成立):
    /// <c>首部噪音 + 正文 + 尾部噪音 == 改前</c>,且 <c>正文 == 改后</c>。
    ///
    /// 这条断言把分解与规则绑在一起 —— 日后谁改了规则的剥法(比如改成"中间也挖"),
    /// 这里会立刻红,而不是等到界面上少显示一个字符才发现。
    /// </summary>
    [Theory]
    [InlineData("。重点是理解这一点。")]
    [InlineData("。 ，；重点是")]
    [InlineData("\u201D\u2019）】》」』｝他说")]
    [InlineData(",.;:!?)]}abc")]
    [InlineData("他走了。「")]
    [InlineData("。他走了。「")]
    // 下面几条是报告里的真实样例(2026-09-21 实测库),噪音位置各不相同:
    // 只在首部 / 只在尾部 / 首尾都有 —— 用同一个不变量一起钉住。
    [InlineData("。 资本主义旧社会留给我们的最大祸害之一,就是书本与生活实践完全脱节。")]
    [InlineData(":是為了打造「兩個朝鮮」的帝國主義國家計謀,終其一生堅決反對。")]
    [InlineData("。 暗地里盘算著,要是拿着槍上戰場的中國開發核武,北韓也要追隨。")]
    [InlineData("留學派和家人們都相當恐懼不安,因為自己可能會在不知不覺間,被誣陷為間諜團或是體制反對勢力。「")]
    [InlineData("， 經歷了這樣的過程。")]
    public void Compute_ReassemblesIntoTheOriginal(string original) {
        var cleaned = ClippingCleanRules.Clean(original);
        // 只对真正改过的样本立论(整条皆标点的那种会被跳过,不进预览)。
        Assert.Equal(ClippingCleanOutcome.Cleaned, cleaned.Outcome);

        var diff = ClippingCleanDiff.Compute(original, cleaned.Text);

        Assert.Equal(cleaned.Text, diff.Core);
        Assert.Equal(original, diff.LeadingNoise + diff.Core + diff.TrailingNoise);
        Assert.True(diff.HasNoise);
    }

    /// <summary>清洗是幂等的 ⇒ 对已清洗过的文本再算一次,必须一点噪音都没有(否则预览会一直显示"还能再清")。</summary>
    [Fact]
    public void Compute_IsIdempotentOnAlreadyCleanedText() {
        var once = ClippingCleanRules.Clean("。他走了。「");
        var twice = ClippingCleanRules.Clean(once.Text);
        var diff = ClippingCleanDiff.Compute(once.Text, twice.Text);

        Assert.False(diff.HasNoise);
    }

    // ————————————————————————— 中间省略 —————————————————————————

    /// <summary>没超上限就原样返回,不补省略号 —— 短标注(预览里占多数)不该被加上「…」。</summary>
    [Fact]
    public void Elide_KeepsShortTextUntouched() {
        var elided = ElidedText.From("重点是", 10);

        Assert.False(elided.Elided);
        Assert.Equal("重点是", elided.Text);
        Assert.Equal(string.Empty, elided.Tail);
    }

    /// <summary>
    /// **本组最要紧的一条**:省略之后尾部必须还在。
    /// 尾部正是"句末标点被清掉"的位置,截掉它预览就失去意义(上一版就是这么错的)。
    /// </summary>
    [Fact]
    public void Elide_KeepsTheTailVisibleSoTrailingNoiseStaysObservable() {
        const string longText = "资本主义旧社会留给我们的最大祸害之一,就是书本与生活实践完全脱节,因为那些书本把什么都描写得好得了不得,其实大半都是最。";
        var elided = ElidedText.From(longText, 20);

        Assert.True(elided.Elided);
        Assert.EndsWith("最。", elided.Tail, StringComparison.Ordinal);
        Assert.EndsWith("最。", elided.Text, StringComparison.Ordinal);
        Assert.Contains(ElidedText.Ellipsis, elided.Text, StringComparison.Ordinal);
        // 首尾都留了东西 —— 只剩一头说明"保留首尾"没做到。
        Assert.NotEmpty(elided.Head.TrimEnd(ElidedText.Ellipsis.ToCharArray()));
        Assert.NotEmpty(elided.Tail);
    }

    /// <summary>省略只掐中间:首尾的字符必须原样保留,不许在字符边界上切坏。</summary>
    [Fact]
    public void Elide_CutsOnlyTheMiddle() {
        const string text = "0123456789ABCDEFGHIJ";
        var elided = ElidedText.From(text, 10);

        Assert.StartsWith("012345", elided.Head, StringComparison.Ordinal);
        Assert.EndsWith("HIJ", elided.Tail, StringComparison.Ordinal);
        Assert.DoesNotContain("789", elided.Text, StringComparison.Ordinal);
    }

    /// <summary>上限非正数表示不省略;空文本不许炸。</summary>
    [Fact]
    public void Elide_TreatsNonPositiveLimitAsNoLimit() {
        Assert.False(ElidedText.From("abcdef", 0).Elided);
        Assert.False(ElidedText.From("abcdef", -1).Elided);
        Assert.Equal(string.Empty, ElidedText.From(null, 10).Text);
        Assert.Equal(string.Empty, ElidedText.From(string.Empty, 10).Text);
    }

    /// <summary>上限极小时(head/tail 各自至少 1 个字符)也必须收敛,不能抛异常。</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Elide_SurvivesTinyLimits(int limit) {
        var elided = ElidedText.From("abcdef", limit);

        Assert.True(elided.Elided);
        Assert.NotEmpty(elided.Tail);
        Assert.EndsWith("f", elided.Tail, StringComparison.Ordinal);
    }

    /// <summary>
    /// 端到端拼装:预览里「改前」那一行 = 首部噪音 + 正文 + 尾部噪音。
    /// 把视图层的拼装口径固化下来 —— 尾部噪音必须排在**最末**,否则一眼看上去
    /// 会以为被清掉的标点在中间。
    ///
    /// 注意这条**测不到**"截断吃掉尾部"那个旧 bug(噪音是拼装时补回去的,
    /// 截断吃的是正文的尾巴)—— 那一环由
    /// <see cref="Elide_KeepsTheTailVisibleSoTrailingNoiseStaysObservable"/> 钉住。
    /// 这里只管"顺序对不对"。
    /// </summary>
    [Fact]
    public void PreviewLines_KeepTrailingNoiseAtTheVeryEnd() {
        const string original = "留學派和家人們都相當恐懼不安,因為自己可能會在不知不覺間,被誣陷為間諜團或是體制反對勢力。「";
        var cleaned = ClippingCleanRules.Clean(original);
        var diff = ClippingCleanDiff.Compute(original, cleaned.Text);
        var core = ElidedText.From(diff.Core, 20);

        var beforeLine = diff.LeadingNoise + core.Text + diff.TrailingNoise;
        var afterLine = core.Text;

        Assert.Equal(string.Empty, diff.LeadingNoise);
        Assert.Equal("「", diff.TrailingNoise);
        Assert.True(core.Elided, "正文应当被中间省略,否则测不到「省略」与「尾部噪音」的先后关系");
        Assert.EndsWith(diff.TrailingNoise, beforeLine, StringComparison.Ordinal);
        // 两行必须不同 —— 否则预览里"改前/改后"就是两段一样的字。
        Assert.NotEqual(beforeLine, afterLine);
    }

    /// <summary>报告里的真实样例(2026-09-21 实测库)逐条走一遍,确保没有一条会算出空噪音。</summary>
    [Fact]
    public void Compute_HoldsForEveryRealWorldSample() {
        var samples = new List<string> {
            "。暗地里盘算著,要是拿着槍上戰場的中國開發核武,北韓也要追隨。",
            ":是為了打造「兩個朝鮮」的帝國主義國家計謀,終其一生堅決反對。",
            "。 經歷了這樣的過程。",
            "。留學派和家人們都相當恐懼不安,因為自己可能會在不知不覺間,被誣陷為間諜團或是體制反對勢力。「"
        };

        foreach (var sample in samples) {
            var cleaned = ClippingCleanRules.Clean(sample);
            Assert.Equal(ClippingCleanOutcome.Cleaned, cleaned.Outcome);

            var diff = ClippingCleanDiff.Compute(sample, cleaned.Text);
            Assert.Equal(sample, diff.LeadingNoise + diff.Core + diff.TrailingNoise);
            Assert.True(diff.HasNoise, $"样本没有分解出任何噪音:{sample}");
        }
    }
}
