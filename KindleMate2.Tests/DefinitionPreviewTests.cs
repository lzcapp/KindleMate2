using System;
using System.Text;
using KindleMate2.Application.Services;
using Xunit;

namespace KindleMate2.Tests;

/// <summary>
/// 释义显示截断的用例。全部是纯函数,样本按**实测长度**构造
/// (「高要」百科 414 字 / 首句 28 字;「买办」现汉 39 字;「针指」百科 28 字)。
/// </summary>
public sealed class DefinitionPreviewTests {

    /// <summary>实测的「高要」百科首句:28 字(含句号)。</summary>
    private const string GaoYaoFirstSentence = "高要市，建县于汉元鼎六年（前111年），县东北有高峡山。";

    [Fact]
    public void Shorten_LeavesTextWithinTheLimitAlone() {
        // 词典释义实测都 ≤40 字 —— 这条路径必须**完全不碰**它们
        const string dictionaryEntry = "殖民地、半殖民地国家中，替外国资本家在本国市场上经营企业、开设银行等的代理人。";

        var (text, shortened) = DefinitionPreview.Shorten(dictionaryEntry);

        Assert.Equal(dictionaryEntry, text);
        Assert.False(shortened);
    }

    [Fact]
    public void Shorten_ReturnsUnchangedAtExactlyTheLimit() {
        var exact = new string('字', DefinitionPreview.MaxCharacters);

        var (text, shortened) = DefinitionPreview.Shorten(exact);

        Assert.Equal(exact, text);
        Assert.False(shortened);
    }

    [Theory]
    [InlineData("")]
    public void Shorten_ReturnsEmptyUnchanged(string input) {
        var (text, shortened) = DefinitionPreview.Shorten(input);

        Assert.Equal(input, text);
        Assert.False(shortened);
    }

    [Fact]
    public void Shorten_CutsAtSentenceEndAndKeepsTheLeadingSentences() {
        // 首句 28 字 + 第二句 70 字 = 98 ≤ 100 ⇒ 两句都留下;第三句起丢掉
        var second = new string('甲', 69) + "。";                 // 70 字
        var third = new string('乙', 200) + "。";
        var full = GaoYaoFirstSentence + second + third;

        var (text, shortened) = DefinitionPreview.Shorten(full);

        Assert.True(shortened);
        Assert.Equal(GaoYaoFirstSentence + second + "…", text);
        Assert.True(text.Length <= DefinitionPreview.MaxCharacters + 1);
    }

    [Fact]
    public void Shorten_DropsTheWholeSentenceWhenItDoesNotFit() {
        // 首句 28 字,第二句 85 字(28+85=113 > 100)⇒ 只留首句,不切半句
        var second = new string('丙', 84) + "。";

        var (text, shortened) = DefinitionPreview.Shorten(GaoYaoFirstSentence + second);

        Assert.True(shortened);
        Assert.Equal(GaoYaoFirstSentence + "…", text);
        Assert.DoesNotContain("丙", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Shorten_HardCutsWhenASingleSentenceExceedsTheLimit() {
        // 百科常见形态:一整段没有句号的长文 ⇒ 只能硬截,不假装切在句末
        var runOn = new string('丁', 200);

        var (text, shortened) = DefinitionPreview.Shorten(runOn);

        Assert.True(shortened);
        Assert.Equal(new string('丁', DefinitionPreview.MaxCharacters) + "…", text);
    }

    [Fact]
    public void Shorten_KeepsWholeLinesAndCutsInsideTheOffendingLine() {
        // 多行释义(音标行 + 若干条):整行放得下就整行留,放不下的那行整条丢弃 —— 宁可少显示,也不留半句
        var builder = new StringBuilder("mǎi bàn");
        for (var i = 0; i < 4; i++) builder.Append('\n').Append(new string((char)('a' + i), 30));
        var full = builder.ToString();   // 7 + 4×(1+30) = 131 字 > 100

        var (text, shortened) = DefinitionPreview.Shorten(full);

        Assert.True(shortened);
        Assert.StartsWith("mǎi bàn\naaaaa", text, StringComparison.Ordinal);
        Assert.Contains("cccccccc", text, StringComparison.Ordinal);    // 第 4 行(98 字内)留下
        Assert.DoesNotContain("dddd", text, StringComparison.Ordinal);  // 第 5 行放不下 ⇒ 整行不要
        Assert.EndsWith("…", text, StringComparison.Ordinal);
    }

    /// <summary>实测的「高要」百科摘要全文(**414 字 / 9 句**,取自 2026-09-23 的真实响应、原样嵌入)。</summary>
    private const string GaoYaoBaikeFull =
        "高要市，建县于汉元鼎六年（前111年），县东北有高峡山。位于广东中部偏西、西江中下游，东经112°11′～112°50′、北纬22°47′～23°26′，属珠江三角洲经济区和肇庆市经济发展中心区。东邻佛山市三水区，南与佛山市高明区、新兴县接壤，西连云浮、德庆，北接广宁、四会县。东与珠江三角洲腹地接壤，西与粤西相联，处于经济发达的珠江三角洲和资源丰富的西江经济走廊结合部。市城区与国家优秀旅游城市肇庆市隔江相望，距广州90公里，香港138海里，西江黄金水道、三茂铁路、国道324线以及广肇高速公路穿境而过。全市总面积2196平方公里，常住人口75万多人。东西最宽67公里，南北最长74公里。耕地面积65万亩，约占30%，山地面积157.5万亩，约占60%，水系面积占10%。 全市每年为国家提供粮食商品量27.7万吨，蔬菜商品量约90万吨，是一个以粮食和蔬菜生产为主的农业大市，是广东省粮、菜、鱼、果、林生产的重点市（县）之一。";

    [Fact]
    public void Shorten_TruncatesTheRealEncyclopediaSummaryToItsLeadingSentences() {
        // **真实数据回归**:这条摘要就是用户截图里那段把标注挤出屏幕的正文。
        // 414 字 → 前两句 98 字 + 省略号 = 99 字。
        var (text, shortened) = DefinitionPreview.Shorten(GaoYaoBaikeFull);

        Assert.True(shortened);
        Assert.Equal(
            "高要市，建县于汉元鼎六年（前111年），县东北有高峡山。位于广东中部偏西、西江中下游，"
            + "东经112°11′～112°50′、北纬22°47′～23°26′，属珠江三角洲经济区和肇庆市经济发展中心区。…",
            text);
    }

    [Fact]
    public void Shorten_DoesNotSplitSurrogatePairs() {
        // 上限正好落在高代理位之前时要让开一位,否则末尾会出现半个 emoji(乱码方块)
        var withEmoji = "a" + string.Concat(System.Linq.Enumerable.Repeat("😀", 60));

        var (text, shortened) = DefinitionPreview.Shorten(withEmoji);

        Assert.True(shortened);
        Assert.EndsWith("…", text, StringComparison.Ordinal);
        var body = text[..^1];
        Assert.False(char.IsHighSurrogate(body[^1]));
        Assert.DoesNotContain('\uFFFD', body);
    }
}
