using System;
using System.Collections.Generic;

namespace KindleMate2.Application.Services;

/// <summary>
/// 一段正文,以及它是不是"命中的生词"。
/// <c>IsMatch</c> 为 true 的那几段在界面上要加粗 —— 但**怎么画由视图层决定**,
/// 这里只切分、不带任何字体/颜色信息(所以本类不引 Avalonia,可无头单测)。
/// </summary>
public readonly record struct EmphasisSegment(string Text, bool IsMatch);

/// <summary>
/// 把一段正文按生词切成若干段,供界面把命中的那几段**加粗**。
///
/// 需求原话(2026-09-23):「生词的标注中,能否把生词加粗或者什么方式高亮显示」——
/// 中栏「N 条标注」那一段列的是"正文里出现过这个词的剪藏",一条几十个字,
/// 生词藏在中间读起来得自己找;右栏的用法句同理。
///
/// 匹配口径**刻意与 <c>MainWindowViewModel.FindClippingsContainingWord</c> 完全一致**
/// (大小写不敏感 + 子串命中 + 单字词不参与)。不为高亮另立一套口径是有原因的:
/// 那一句正是"这一行为什么会出现在列表里"的判据 —— 两边口径一旦分叉,
/// 就会出现"列出来了却一个字也没加粗"的行,看上去像高亮坏了。
///
/// 具体规则与理由:
/// <list type="bullet">
///   <item><b>大小写不敏感</b>(<see cref="StringComparison.OrdinalIgnoreCase"/>),但输出**保留原文大小写** ——
///         生词条里存的是 "Beautiful",句子里可能是 "beautiful",改写会让正文失真;</item>
///   <item><b>子串命中,不要求整词</b>:单词在句子里几乎总是变形出现("beautiful" 在句子里是
///         "beautifully"),而中文没有词边界,整词匹配根本无从下手。代价是 "run" 会命中 "running"
///         的前三个字母 —— 但"看见了才认得出"比"漏掉"好,这与列表本身的召回口径一致;</item>
///   <item><b>单字词不给高亮</b>(长度 ≤ 1):口径同列表 —— 一个字几乎命中每一行,满屏加粗等于没加粗;</item>
///   <item><b>从左往右、不重叠</b>:重叠匹配会切出互相嵌套的段,渲染层没法画。</item>
/// </list>
///
/// 不变量:各段 <c>Text</c> 依次相接**必须逐字等于**入参 <paramref name="text"/>
/// (用例里钉了这条,含代理对与"一个字都没命中"的情形)。
/// </summary>
public static class WordEmphasis {

    /// <summary>
    /// 切分。<paramref name="text"/> 为空 ⇒ 返回空表;<paramref name="word"/> 为空/纯空白/单字
    /// ⇒ 返回**整段未命中**一条(调用方不必再判一次,渲染出来与不加粗完全一致)。
    /// </summary>
    public static IReadOnlyList<EmphasisSegment> Split(string? text, string? word) {
        if (string.IsNullOrEmpty(text)) return Array.Empty<EmphasisSegment>();

        // 词条两端可能有空白(自建词表里常见);带进去会让空格一起被加粗
        var needle = word?.Trim();
        if (string.IsNullOrEmpty(needle) || needle.Length <= 1) {
            return new[] { new EmphasisSegment(text, false) };
        }

        var segments = new List<EmphasisSegment>();
        // ⚠️ 两个游标**必须分开**,否则"跳过畸形匹配"会吃掉正文:
        //   · searchFrom = 下一次从哪儿开始找;
        //   · emitted    = 已经输出到哪儿了(收尾时从这里补齐)。
        // 早先只用一个游标,跳过时把它一起推走,那段文本就再也不会被输出 ——
        // 拼接还原不出原文,而且是**静默**的(用例里那条"逐字还原"就是这么抓到的)。
        var searchFrom = 0;
        var emitted = 0;
        while (searchFrom < text.Length) {
            var hit = text.IndexOf(needle, searchFrom, StringComparison.OrdinalIgnoreCase);
            if (hit < 0) break;

            var end = hit + needle.Length;
            // 代理对安全:切点落在**低位代理**上说明它前半截(高位代理)落在切点左边,
            // 照切会把一个 emoji / 罕见汉字劈成两半,渲染出来是两个豆腐块。
            // 命中段的**末尾**同理:end 处是低位代理,说明命中段以落单的高位代理收尾。
            // 两种都只可能出自畸形数据(词条本身就从代理对中间截断),放过这一位继续找。
            if (char.IsLowSurrogate(text[hit]) ||
                (end < text.Length && char.IsLowSurrogate(text[end]))) {
                searchFrom = hit + 1;
                continue;
            }

            if (hit > emitted) segments.Add(new EmphasisSegment(text[emitted..hit], false));
            segments.Add(new EmphasisSegment(text[hit..end], true));
            emitted = end;
            searchFrom = end;
        }

        // 一次性收尾:没命中过时(segments 为空)这里就是整段原文
        if (emitted < text.Length) segments.Add(new EmphasisSegment(text[emitted..], false));
        return segments;
    }
}
