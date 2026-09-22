using System;

namespace KindleMate2.Shared.Clippings;

/// <summary>
/// 一条改动的**可视分解**:被掐掉的首部噪音 + 保留下来的正文 + 被掐掉的尾部噪音。
///
/// 为什么要专门做这个分解,而不是把「改前 / 改后」两整条并排丢给用户看:
/// 清洗只动**首尾各一两个标点**,两条长文本并排时那点差异在折行之后就找不着了。
/// 把被删掉的字符单独摘出来,预览才能一眼看出"到底改了什么"。
///
/// 抽到 Shared 这一层**只为了可测**(与 <see cref="ClippingCleanRules"/> 同样的理由:
/// 测试工程不引用 Avalonia,逻辑留在视图层就钉不住)。
/// </summary>
/// <param name="LeadingNoise">从开头掐掉的噪音(含其间的空白)。</param>
/// <param name="Core">保留下来的正文 —— 恒等于清洗结果。</param>
/// <param name="TrailingNoise">从结尾掐掉的噪音(含其间的空白)。</param>
public readonly record struct ClippingCleanDiff(string LeadingNoise, string Core, string TrailingNoise) {

    /// <summary>这条改动确实掐掉了东西。理论上恒为 true —— 没改过的条目不会进预览。</summary>
    public bool HasNoise => LeadingNoise.Length > 0 || TrailingNoise.Length > 0;

    /// <summary>
    /// 按「改前 / 改后」算出分解结果。
    /// 不变量:<c>LeadingNoise + Core + TrailingNoise == 改前</c> 且 <c>Core == 改后</c>。
    /// </summary>
    public static ClippingCleanDiff Compute(string? before, string? after) {
        var original = before ?? string.Empty;
        var cleaned = after ?? string.Empty;

        // 规则只会**掐头去尾**,所以清洗结果必然是原文的一个连续子串。
        // 取**最早**出现的那个位置:能算在头上的一律算头上,与规则"先剥头再剥尾"的顺序一致。
        // (万一算在尾上,显示的字符也仍然是被删掉的那些,视觉上不会说错话。)
        var start = cleaned.Length == 0
            ? original.Length
            : original.IndexOf(cleaned, StringComparison.Ordinal);

        if (start < 0) {
            // 不该发生(规则只删不改)。真到了这里就退化成"整条都换了":
            // 预览难看,但至少不会把话说错。
            return new ClippingCleanDiff(original, cleaned, string.Empty);
        }

        return new ClippingCleanDiff(
            original[..start],
            cleaned,
            original[(start + cleaned.Length)..]);
    }
}

/// <summary>
/// 一段文本的**中间省略**结果:超出上限时保留首尾、中间省略。
///
/// 为什么不能像常见做法那样只留前缀:清洗动的就是**首尾两端**,
/// 只留前缀会把"尾部被掐掉的标点"正好截掉 —— 于是「改前」与「改后」在预览里长得
/// 一模一样(本功能上一版就是这么翻车的:89 条改动,预览里一条差异都看不出来)。
/// </summary>
/// <param name="Head">显示用前缀;被省略时**已含省略号**(拼接时不必再补)。</param>
/// <param name="Tail">显示用后缀;未被省略时为空串。</param>
/// <param name="Elided">是否真的省略了中间。</param>
public readonly record struct ElidedText(string Head, string Tail, bool Elided) {

    /// <summary>拼接好的显示文本。</summary>
    public string Text => Head + Tail;

    /// <summary>中间省略号 —— 视图层单独渲染时用它。</summary>
    public const string Ellipsis = "…";

    /// <summary>
    /// 保留首尾的中间省略。<paramref name="maxChars"/> 非正数表示不省略。
    /// </summary>
    public static ElidedText From(string? text, int maxChars) {
        var value = text ?? string.Empty;
        if (maxChars <= 0 || value.Length <= maxChars) {
            return new ElidedText(value, string.Empty, false);
        }

        // 前 2/3、后 1/3:噪音多数落在尾部(句末标点),但也可能落在首部,两边都得给够。
        // 两个长度都至少留 1,免得 maxChars 很小时出现空片段。
        var headLength = Math.Max(1, maxChars * 2 / 3);
        var tailLength = Math.Max(1, maxChars - headLength);
        // 拼接后总长会多出一个省略号,这里让它顶着 maxChars —— 差一个字符无关紧要,
        // 但别让 head+tail 自己就超过 maxChars。
        return new ElidedText(value[..headLength] + Ellipsis, value[^tailLength..], true);
    }
}
