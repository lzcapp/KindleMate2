using System;
using System.Collections.Generic;
using System.Linq;

namespace KindleMate2.Shared.Vocabs;

/// <summary>点了「确定」之后,「重命名生词」该走哪条路。</summary>
public enum WordRenameAction {
    /// <summary>新名字与旧名字**序数相同** —— 提示「生词名未修改」,不落库。</summary>
    Unchanged,

    /// <summary>可以改。</summary>
    Rename,

    /// <summary>
    /// 新名字已被**别的**生词占用 ⇒ **并入那一个**。
    ///
    /// 2026-09-23 改:原来这里弹「已存在同名生词,请先处理那一个再改名」把用户拦下,
    /// 用户明确要求**静默解决**(可以合并或删除)。改成合并:
    /// 把当前这个词的记录整批改挂到同名生词的键上,与目标行同 timestamp 或
    /// 同句同书的行丢掉(见 <c>ILookupRepository.MergeWordKey</c> 的说明 ——
    /// 丢掉的那些本来就是同一条记录)。
    /// </summary>
    MergeIntoExisting
}

/// <summary>
/// 「重命名生词」的判定规则 —— 抽到 Shared 这一层只为**可测**:
/// Avalonia 视图层/VM 包不进测试工程,放那里就钉不住。
///
/// 两条都极易写错,而且写错了**不会报错**,只会让数据前后不一致:
///
/// <list type="number">
/// <item>
/// **新名字必须落进 <c>word_key</c>,不能只改 <c>vocab.word</c>。**
/// <c>Lookup.Word</c> 不是存储列,而是**从 word_key 现算的**(取第一个 <c>:</c> 之后的内容,
/// 见 <c>KM2DB.Lookup.Word</c>),而中栏列表正是按 <c>Lookup.Word</c> 过滤的。
/// 只改显示列的话,左栏(按 <c>vocab.word</c> 分组)显示新名、中栏那批查询行却一条都匹配不上 ——
/// 看上去就像"这个词的查询记录凭空没了"。
/// </item>
/// <item>
/// **word_key 的前缀要原样保留**(<c>en:beautiful</c> → <c>en:beautifully</c>)。
/// 前缀是那个词所属的语言域,整串换掉等于把词搬到了另一个域。
/// </item>
/// </list>
/// </summary>
public static class WordRenameRules {
    /// <summary>
    /// 按旧键的形状拼出新键:保留第一个 <c>:</c> **之前**的整段(含冒号),后面接新词。
    ///
    /// 切分口径必须与 <c>KM2DB.Lookup.Word</c> 完全一致(那里也是 <c>IndexOf(':')</c>),
    /// 否则拼出来的键经 <c>Word</c> 现算之后,得到的不是用户刚输入的那个词。
    /// 旧键为空(没有 word_key 的词条)或压根没有冒号时,新键就是新词本身。
    /// </summary>
    public static string BuildWordKey(string? oldWordKey, string newWord) {
        if (string.IsNullOrEmpty(oldWordKey)) {
            return newWord;
        }
        var index = oldWordKey.IndexOf(':');
        return index >= 0 ? string.Concat(oldWordKey.AsSpan(0, index + 1), newWord) : newWord;
    }

    /// <summary>
    /// 新名字是否被**别的**生词占用。<paramref name="exceptWord"/> 传「正在改的那个词」,
    /// 用来把这一组自己排除掉(左栏一个词就是一项,不存在"能被区分出来的另一个同名词")。
    ///
    /// ⚠️ 比较用**忽略大小写** —— 必须与左栏生词的分组口径
    /// (<c>GroupBy(v =&gt; v.Word, StringComparer.OrdinalIgnoreCase)</c>)一致。
    /// 这一条与「重命名书籍」**刻意不同**:书籍是按 <c>Ordinal</c> 分组的,所以那边用序数比较。
    /// 口径弄反的后果是"列表里看着只有一个词,判定却说被占用"(或反之)。
    /// </summary>
    public static bool IsNameTakenByOther(IEnumerable<string?> words, string? candidate, string? exceptWord = null) =>
        words.Any(w => string.Equals(w, candidate, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(w, exceptWord, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// 决定重命名的走向。<paramref name="nameTakenByOtherWord"/> 由调用方先用
    /// <see cref="IsNameTakenByOther"/> 算好 —— 它要读数据,不属本层职责。
    ///
    /// 判定**顺序**即语义:「什么都没改」优先于「撞名」。反过来会出现
    /// "名字一个字都没动,却告诉你已被别的生词占用"。
    /// 注意"只改大小写"(beautiful → Beautiful)算**改了**(序数比较),会走到改名 ——
    /// 左栏是按忽略大小写分组的,改完整组一起变,不会分裂成两项。
    /// 撞名不再拒绝,而是并入(<see cref="WordRenameAction.MergeIntoExisting"/>)。
    /// </summary>
    public static WordRenameAction Decide(string? oldWord, string? newWord, bool nameTakenByOtherWord) {
        if (string.Equals(newWord, oldWord, StringComparison.Ordinal)) {
            return WordRenameAction.Unchanged;
        }

        return nameTakenByOtherWord ? WordRenameAction.MergeIntoExisting : WordRenameAction.Rename;
    }
}
