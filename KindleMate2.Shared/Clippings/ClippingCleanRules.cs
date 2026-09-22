using System;

namespace KindleMate2.Shared.Clippings;

/// <summary>一次清洗的三种结局。</summary>
public enum ClippingCleanOutcome {
    /// <summary>首尾没有可清的噪音 —— 原样保留,不落库。</summary>
    Unchanged,

    /// <summary>去掉了首尾噪音 —— 应当落库。</summary>
    Cleaned,

    /// <summary>
    /// 整条内容除标点与空白外别无他物(例如只划到一个「。」)。
    /// **不落库**:清下去会得到空串,把一条标注变成空条目,比留着噪音更糟。
    /// </summary>
    AllPunctuation
}

/// <summary>清洗结果。<see cref="Text"/> 永远是「应当落库的文本」—— 三态下都可用。</summary>
public readonly record struct ClippingCleanResult(string Text, ClippingCleanOutcome Outcome) {
    public bool Changed => Outcome == ClippingCleanOutcome.Cleaned;
}

/// <summary>
/// Kindle 划线的**首尾标点清洗**规则。
///
/// 要解决的问题:Kindle 的划线段落边界是落在标点上的,于是"上一句话的收尾标点"
/// 经常被一起划进来。实测样本里,一条标注的内容是「。重点是……」—— 开头那个句号
/// 属于前一句,不是这条标注的一部分。
///
/// 规则为什么是**两个不对称的集合**,而不是"首尾都去掉标点":
/// <list type="bullet">
/// <item>**首部**只清「结构上属于收尾」的字符(。，、；：！？ 与右引号、右括号)。
/// 一句话不可能以这些字符开头,所以清掉它们不会伤到正文。</item>
/// <item>**尾部**只清「结构上属于开头」的字符(左引号、左括号),外加**顿号**。
/// 这里**刻意不清**「。！？」与 ASCII 的 <c>. ! ?</c> —— 那是句子真正结束的地方;
/// 也**刻意不清**「，；：」—— 标注本来就可能停在半句上,那个逗号是用户自己选中的。
/// 而**顿号两端都清**:<c>、</c> 是并列项之间的分隔符,永远不可能作为一个片段的结尾
/// (带上它就等于"下一项不见了")⇒ 同一个字符在首尾的语义完全一样,不该有两种待遇。</item>
/// </list>
///
/// 三处刻意的克制:
/// <list type="number">
/// <item>**左右引号要分清**。左引号(201C/2018)是正文的一部分,右引号(201D/2019)才可能是噪音。
/// 全角字形下两者长得几乎一样,所以下面一律写 <c>\u</c> 转义 —— 直接粘字面量根本没法人眼复核。</item>
/// <item>**破折号与省略号不碰**。「——他走过来」可以是正文起头,「他走了——」也可以是正文收尾,
/// 两端都歧义大于噪声,留着比清错强。</item>
/// <item>**只清整条内容的最外侧**,不清每一行的行首行尾 —— 换行处大多是段落边界,
/// 一行的行首完全可能是正文。</item>
/// </list>
///
/// 抽到 Shared 这一层**只为了可测**:测试工程不引用 Avalonia,规则留在视图层就钉不住。
/// </summary>
public static class ClippingCleanRules {

    /// <summary>
    /// 首部可清:结构上只可能属于「上一句的收尾」。
    /// 。，、；：！？ / ）］】》」』｝ / 右双引号、右单引号 / 间隔号、全角句点 / , . ; : ! ? / ) ] }
    /// </summary>
    private const string LeadingNoise =
        "\u3002\uFF0C\u3001\uFF1B\uFF1A\uFF01\uFF1F" +
        "\uFF09\uFF3D\u3011\u300B\u300D\u300F\uFF5D" +
        "\u201D\u2019" +
        "\u00B7\uFF0E" +
        "\u002C\u002E\u003B\u003A\u0021\u003F" +
        "\u0029\u005D\u007D";

    /// <summary>
    /// 尾部可清:结构上只可能属于「下一句的开头」。
    /// （［【《「『｛ / 左双引号、左单引号 / ( [ { / **顿号**
    ///
    /// 顿号为什么两端都清,而逗号/分号/冒号只清首部:
    /// <c>、</c> 是**并列项之间的分隔符**,永远不可能作为一个片段的结尾 ——
    /// 带上它就等于"下一项不见了"。而 <c>，；：</c> 可以合理地停在一个从句/短语末尾
    /// (「他说，」是用户真可能选中的片段),所以那三个尾部保留。
    /// 首尾对同一个字符给出不同待遇本来就没有依据:它的两种位置语义完全一样。
    /// (2026-09-22 依据实测库定:全库 5783 条里以 <c>、</c> 结尾的只有 2 条,
    ///  两条都是列举被截断的残留,例如「…改革过程中采取的发展合作社、」。)
    /// </summary>
    private const string TrailingNoise =
        "\uFF08\uFF3B\u3010\u300A\u300C\u300E\uFF5B" +
        "\u201C\u2018" +
        "\u3001" +
        "\u0028\u005B\u007B";

    /// <summary>
    /// 清洗一条标注内容。可安全地反复调用(幂等):对已清洗过的文本再跑一次必然返回
    /// <see cref="ClippingCleanOutcome.Unchanged"/>。
    /// </summary>
    public static ClippingCleanResult Clean(string? content) {
        if (string.IsNullOrEmpty(content)) {
            return new ClippingCleanResult(content ?? string.Empty, ClippingCleanOutcome.Unchanged);
        }

        var work = content;
        // 逐轮剥:剥掉首部标点后前面可能又露出空白,剥掉尾部后同理,
        // 而且「。 」这种"标点+空格"要连剥两轮才收敛。只删不加 ⇒ 长度单调不增,
        // 长度不变即已收敛,不会死循环。
        while (true) {
            var next = work.Trim();
            if (next.Length > 0 && LeadingNoise.Contains(next[0], StringComparison.Ordinal)) {
                next = next[1..];
            }
            if (next.Length > 0 && TrailingNoise.Contains(next[^1], StringComparison.Ordinal)) {
                next = next[..^1];
            }
            if (next.Length == work.Length) {
                break;
            }
            work = next;
        }

        if (work.Length == 0) {
            // 整条都是标点/空白:保住原文,让调用方按"跳过"计数。
            return new ClippingCleanResult(content, ClippingCleanOutcome.AllPunctuation);
        }

        return string.Equals(work, content, StringComparison.Ordinal)
            ? new ClippingCleanResult(content, ClippingCleanOutcome.Unchanged)
            : new ClippingCleanResult(work, ClippingCleanOutcome.Cleaned);
    }
}
