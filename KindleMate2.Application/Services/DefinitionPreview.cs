using System;
using System.Collections.Generic;

namespace KindleMate2.Application.Services;

/// <summary>
/// 释义的**显示截断**。详情面板只有 330px 宽,而百科摘要可能有几百字
/// (实测「高要」**414 字 / 9 句**,而「针指」只有 28 字、「买办」39 字)——
/// 不截断的话它会把下面的标注挤到屏幕外,那才是这个面板的主要用途。
///
/// **刻意不做"智能取舍"**:实测同一条规则对「高要」正好留住最有用的首句
/// ("高要市,建县于汉元鼎六年(前111年),县东北有高峡山。"),
/// 对「没骨花卉」却会切掉"什么叫没骨"的第二句 ⇒ **自动规则不可靠**,
/// 所以这里只负责"截得整齐一点",把"要不要看全文"交给调用方给用户的展开入口。
/// </summary>
public static class DefinitionPreview {
    /// <summary>显示上限(字符)。词典释义实测都 ≤40 字,永远碰不到它;只有百科会。</summary>
    public const int MaxCharacters = 100;

    private const char Ellipsis = '…';

    /// <summary>
    /// 把释义裁到 <see cref="MaxCharacters"/> 以内。
    /// <list type="bullet">
    ///   <item>整行放得下就整行留(行是天然语义单位:音标行 / 每条释义各占一行);</item>
    ///   <item>放不下的那一行**尽量切在句末**(。！？.!?);一个句子都放不下时整行丢弃;</item>
    ///   <item>连第一行都放不下(例如百科的单句长文)⇒ 硬截到上限,不假装切在句末;</item>
    ///   <item>任何情况下**只要丢掉了内容**,末尾都会带一个省略号 —— 用户据此知道"这不是全部"。</item>
    /// </list>
    /// </summary>
    /// <returns><c>Shortened</c> 为 true 表示有内容被丢掉(调用方据此决定要不要给"展开全文"入口)。</returns>
    public static (string Text, bool Shortened) Shorten(string text) {
        if (string.IsNullOrEmpty(text) || text.Length <= MaxCharacters) return (text, false);

        var kept = new List<string>();
        var length = 0;

        foreach (var line in text.Split('\n')) {
            var separator = kept.Count == 0 ? 0 : 1;   // 换行本身也占一个字符
            if (length + separator + line.Length <= MaxCharacters) {
                kept.Add(line);
                length += separator + line.Length;
                continue;
            }

            // 这一行放不下:按句末切一段,切不出整句就不留它 —— 宁可少显示,也不留半句
            var cut = CutAtSentenceEnd(line, MaxCharacters - length - separator);
            if (cut.Length > 0) kept.Add(cut);
            break;
        }

        if (kept.Count == 0) return (HardCut(text), true);
        return (string.Join('\n', kept) + Ellipsis, true);
    }

    /// <summary>在 <paramref name="room"/> 个字符内尽量切到句末(含句末标点);切不出整句则返回空串。</summary>
    private static string CutAtSentenceEnd(string line, int room) {
        if (room <= 0) return string.Empty;
        var lastEnd = -1;
        var limit = Math.Min(room, line.Length);
        for (var i = 0; i < limit; i++) {
            if (IsSentenceEnd(line[i])) lastEnd = i;
        }
        return lastEnd >= 0 ? line[..(lastEnd + 1)] : string.Empty;
    }

    /// <summary>硬截到上限(供"一句都放不下"的情况)。**不切断代理对**,免得半个 emoji 变成乱码。</summary>
    private static string HardCut(string text) {
        var length = MaxCharacters;
        if (length > 0 && char.IsHighSurrogate(text[length - 1])) length--;
        return text[..length] + Ellipsis;
    }

    private static bool IsSentenceEnd(char ch) => ch is '。' or '！' or '？' or '.' or '!' or '?';
}
