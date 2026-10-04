using System.Text;
using Xunit;
using KindleMate2.Infrastructure.Helpers;

namespace KindleMate2.Tests;

/// <summary>
/// 分享图文件名的字节预算截断(<see cref="StringHelper.ComposeTruncatedFileName"/>)。
/// </summary>
/// <remarks>
/// 文件名结构是 <c>&lt;书名&gt;_&lt;位置&gt;_&lt;时间&gt;.png</c>,位置与时间是区分
/// 「同一本书同一天不同标注」的唯一来源。旧实现 <c>joined[..80]</c> 有三处缺陷:
/// <list type="number">
/// <item>按 UTF-16 <c>char</c> 截断,而全 emoji 时 80 个码点 = 320 字节,早超 255;</item>
/// <item>会劈开代理对,产出孤立代理 / 非法 UTF-8;</item>
/// <item>从右往左砍 —— 书名一长就把位置与时间整段砍掉,同书同日两条标注退回同一个名字,
/// 用户连存两张就互相覆盖(与文件名带这两段的理由直接冲突)。</item>
/// </list>
/// <para>
/// 纯逻辑原本在 Avalonia 工程里,而测试工程**不引用** Avalonia(见 KindleMate2.Tests.csproj 注释)
/// ⇒ 已把它提到 <see cref="StringHelper.ComposeTruncatedFileName"/> 共用,本文件即测它。
/// ShareCardModel.SuggestedFileName 的接线由 <c>--smoke</c> 的 "share card:" 探针端到端覆盖
/// (断言文件名须含位置、不含冒号、同书同日另一条不同)。
/// </para>
/// </remarks>
public sealed class ShareCardFileNameTests {
    /// <summary>与 Program.cs 自检里那条一致的位置样例。</summary>
    private const string Location = "113-113";

    /// <summary>标注时间已按 <c>yyyyMMdd-HHmmss</c> 归一。</summary>
    private const string Time = "20170611-065728";

    /// <summary>上限口径与 ShareCardModel.SuggestedFileName 一致(255 字节)。</summary>
    private const int MaxTotalUtf8Bytes = 255;

    /// <summary>
    /// 复刻 ShareCardModel.SuggestedFileName 的入参口径:书名可空 → 用兜底前缀;
    /// 位置 + 时间是必须保留的尾段。测试固定传入时间,故不受 <c>DateTime.Now</c> 影响。
    /// </summary>
    private static string Suggest(string book, string location, string time, string fallbackPrefix = "分享图") {
        var head = book.Length > 0 ? book : fallbackPrefix;
        return StringHelper.ComposeTruncatedFileName(
            head, new[] { location, time }, ".png", MaxTotalUtf8Bytes);
    }

    /// <summary>合法 UTF-8 的自检:编码再解码必须逐字还原(即没有半个字符 / 孤立 surrogate)。</summary>
    private static void AssertValidUtf8(string value) {
        Assert.Equal(value, Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(value)));
        Assert.DoesNotContain('\uFFFD', value);
        for (var i = 0; i < value.Length; i++) {
            if (char.IsHighSurrogate(value[i])) {
                Assert.True(i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]), "出现孤立的高代理");
                i++;
            } else {
                Assert.False(char.IsLowSurrogate(value[i]), "出现孤立的低代理");
            }
        }
    }

    // ————————————————————————— 超长书名:字节上限 + 尾段保留 —————————————————————————

    [Fact]
    public void LongChineseBook_WithinByteLimit_KeepsLocationAndTime_ValidUtf8() {
        // 200 个中文 = 600 字节,远超上限。位置与时间**必须**仍在(它们是区分度的来源)。
        var result = Suggest(new string('长', 200), Location, Time);

        Assert.True(Encoding.UTF8.GetByteCount(result) <= MaxTotalUtf8Bytes,
            $"结果 {Encoding.UTF8.GetByteCount(result)} 字节,超过 {MaxTotalUtf8Bytes}");
        AssertValidUtf8(result);
        Assert.Contains(Location, result, StringComparison.Ordinal);
        Assert.Contains(Time, result, StringComparison.Ordinal);
        Assert.EndsWith(".png", result, StringComparison.Ordinal);
    }

    [Fact]
    public void LongEmojiBook_WithinByteLimit_NoLoneSurrogate_KeepsLocationAndTime() {
        // 每个 📚 是 4 字节的代理对:按 char 截断既会低估长度,也会劈开代理对。
        var result = Suggest(string.Concat(Enumerable.Repeat("📚", 100)), Location, Time);

        Assert.True(Encoding.UTF8.GetByteCount(result) <= MaxTotalUtf8Bytes,
            $"结果 {Encoding.UTF8.GetByteCount(result)} 字节,超过 {MaxTotalUtf8Bytes}");
        AssertValidUtf8(result);
        Assert.Contains(Location, result, StringComparison.Ordinal);
        Assert.Contains(Time, result, StringComparison.Ordinal);
    }

    [Fact]
    public void LongBook_TruncationCutsBook_NotTail() {
        // 被砍的必须是书名:结果以「…时间.png」结尾,且不含完整书名。
        var book = new string('长', 200);

        var result = Suggest(book, Location, Time);

        Assert.EndsWith(Time + ".png", result, StringComparison.Ordinal);
        Assert.DoesNotContain(book, result, StringComparison.Ordinal);
    }

    // ————————————————————————— 核心回归:同书同日不同位置仍不同名 —————————————————————————

    [Fact]
    public void TruncatedLongBook_SameBookSameDay_DifferentLocation_StillDistinct() {
        // 这是本次的核心回归点:旧实现从右往左砍,书名一长就把位置(与时间)整段砍掉,
        // 于是同一本书、同一天、不同位置的两条标注会得到**同一个**建议名 —— 连存两张就互相覆盖。
        var book = new string('长', 200);

        var first = Suggest(book, "113-113", Time);
        var second = Suggest(book, "200-200", Time);

        Assert.NotEqual(first, second);
        Assert.Contains("113-113", first, StringComparison.Ordinal);
        Assert.Contains("200-200", second, StringComparison.Ordinal);
        Assert.Contains(Time, first, StringComparison.Ordinal);
        Assert.Contains(Time, second, StringComparison.Ordinal);
    }

    // ————————————————————————— 截断新产生的尾点 —————————————————————————

    [Fact]
    public void TruncationResultEndingWithDot_TrailingDotStripped() {
        // 250 个 'a' 之后正好是第 251 字节的 '.';主干预算 251(= 255 − ".png" 4)时结果以 '.' 结尾
        // → 必须被去掉(Windows 上文件名不能以点结尾)。
        var head = new string('a', 250) + "." + new string('b', 20);

        var result = StringHelper.ComposeTruncatedFileName(head, Array.Empty<string>(), ".png", MaxTotalUtf8Bytes);

        Assert.Equal(new string('a', 250) + ".png", result);
        Assert.False(result.EndsWith('.'));
    }

    [Fact]
    public void AllDotsStem_KeptAsIs_NotEmptiedByTrim() {
        // 整串都是点时保持原样:修成空名只会让调用方产出一个隐藏文件。
        var result = StringHelper.ComposeTruncatedFileName("...", Array.Empty<string>(), ".png", MaxTotalUtf8Bytes);

        Assert.Equal("....png", result);
    }

    // ————————————————————————— 回归保护:短名字输出逐字不变 —————————————————————————

    [Theory]
    [InlineData("某本书", "113-113", "2017-06-11-06:57:28", "某本书_113-113_2017-06-11-06_57_28.png")]
    [InlineData("Sapiens: A Brief History", "", "20200101-000000", "Sapiens_ A Brief History_20200101-000000.png")]
    [InlineData("深度工作", "200-200", "20200101-000000", "深度工作_200-200_20200101-000000.png")]
    public void ShortNames_OutputUnchanged(string book, string location, string time, string expected) {
        // 默认(不超预算)时输出必须与改动前逐字相同。
        Assert.Equal(expected, Suggest(book, location, time));
    }

    // ————————————————————————— 既有语义:缺段跳过 / 兜底前缀 —————————————————————————

    [Fact]
    public void EmptyBook_UsesFallbackPrefix() {
        Assert.Equal("分享图_20200101-000000.png", Suggest("", "", "20200101-000000"));
    }

    [Fact]
    public void EmptyBookAndEmptyPrefix_NoLeadingUnderscore() {
        // 全空时没有书名段,也不该留下前导下划线。
        Assert.Equal("20200101-000000.png", Suggest("", "", "20200101-000000", fallbackPrefix: ""));
    }

    [Fact]
    public void EmptyLocation_Skipped_NoDoubleUnderscore() {
        var result = Suggest("深度工作", "", "20200101-000000");

        Assert.Equal("深度工作_20200101-000000.png", result);
        Assert.DoesNotContain("__", result, StringComparison.Ordinal);
    }
}
