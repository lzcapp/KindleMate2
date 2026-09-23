using System.Collections.Generic;
using Xunit;
using KindleMate2.Shared.Vocabs;

namespace KindleMate2.Tests;

/// <summary>
/// 「重命名生词」判定的回归用例。
///
/// 需求背景:用户要求左栏生词节点与生词详情面板的右键都能「重命名生词」。
/// 难点不在对话框,而在**"一个生词"的身份是 <c>word_key</c> 而不是显示名**:
/// <list type="bullet">
/// <item><c>vocab.word</c> 是存储列,左栏按它分组;</item>
/// <item><c>lookups</c> 表**没有 word 列**,<c>Lookup.Word</c> 是从 <c>word_key</c>
///   现算的(取第一个 <c>:</c> 之后),中栏列表按它过滤;</item>
/// <item>两者不一致时,左栏能显示新名、中栏却一条查询都匹配不上 —— 像是"记录没了"。</item>
/// </list>
/// 所以改名必须**同时**改 <c>word_key</c> 与 <c>word</c>,且 <c>word_key</c> 的前缀原样保留。
/// 规则抽到 <see cref="WordRenameRules"/>(Avalonia 层包不进测试工程)。
/// </summary>
public sealed class WordRenameRulesTests {
    private static readonly string?[] Words = { "beautiful", "小心", "careful" };

    /// <summary>
    /// 复刻 <c>KM2DB.Lookup.Word</c> 的取法(第一个 <c>:</c> 之后;没有冒号就是整串)。
    /// 用它来钉"拼出来的键,读回来必须正好是新词"这条不变量 ——
    /// 不能只在测试里比字符串,那只能证明"我拼对了字面量"。
    /// </summary>
    private static string WordOf(string wordKey) {
        var index = wordKey.IndexOf(':');
        return index >= 0 ? wordKey[(index + 1)..] : wordKey;
    }

    // —— BuildWordKey ——

    /// <summary>英文词:保留 <c>en:</c> 前缀。</summary>
    [Fact]
    public void KeepsEnglishPrefix() {
        Assert.Equal("en:beautifully", WordRenameRules.BuildWordKey("en:beautiful", "beautifully"));
    }

    /// <summary>中文词:保留 <c>zh:</c> 前缀(中文词的键同样是"语言码 + 词")。</summary>
    [Fact]
    public void KeepsChinesePrefix() {
        Assert.Equal("zh:测验", WordRenameRules.BuildWordKey("zh:测试", "测验"));
    }

    /// <summary>没有前缀的旧键:新键就是新词本身,不要凭空造一个前缀出来。</summary>
    [Fact]
    public void WithoutPrefix_UsesTheNewWordAsIs() {
        Assert.Equal("beautifully", WordRenameRules.BuildWordKey("beautiful", "beautifully"));
    }

    /// <summary>没有 word_key 的词条(导入时 word_key 为空,id 退化成 word)—— 同样只能给新词本身。</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void MissingOldKey_UsesTheNewWordAsIs(string? oldKey) {
        Assert.Equal("新词", WordRenameRules.BuildWordKey(oldKey, "新词"));
    }

    /// <summary>
    /// **本次最要紧的不变量**:拼出来的键,按 <c>Lookup.Word</c> 的方式读回来,
    /// 必须**逐字符**等于用户输入的新词。只要前缀切分口径与那边不一致(比如少切/多切一位),
    /// 中栏过滤就会全军覆没,而这条断言会立刻红。
    /// </summary>
    [Theory]
    [InlineData("en:beautiful", "beautifully", "beautifully")]
    [InlineData("zh:测试", "测验", "测验")]
    [InlineData("beautiful", "beautifully", "beautifully")]
    [InlineData(null, "新词", "新词")]
    [InlineData(":beautiful", "careful", "careful")]
    public void BuiltKeyReadsBackAsTheNewWord(string? oldKey, string newWord, string expectedWord) {
        var built = WordRenameRules.BuildWordKey(oldKey, newWord);
        Assert.Equal(expectedWord, WordOf(built));
    }

    /// <summary>
    /// 新词自己带冒号、而**旧键有前缀**时:仍然完整 ——
    /// 因为前缀边界取的是**旧键**的第一个冒号,新词是整段接在前缀之后的,
    /// 里面再有多少冒号都与切分无关。(这一条我第一版推错了,是这条用例把它逮出来的。)
    /// </summary>
    [Fact]
    public void NewWordContainingColon_WithPrefix_ReadsBackIntact() {
        var built = WordRenameRules.BuildWordKey("en:a:b", "x:y");
        Assert.Equal("en:x:y", built);
        Assert.Equal("x:y", WordOf(built));
    }

    /// <summary>
    /// ⚠️ **真正的已知边界(窄)**,有意记录:旧键**没有前缀**(导入时 <c>word_key</c> 为空、
    /// <c>id</c> 退化成 <c>word</c> 的那种词条)时,新键就是新词本身,
    /// 此时新词若自带 ASCII 冒号,会被 <c>Lookup.Word</c> 的"取第一个冒号之后"口径截断。
    /// 不为此加校验:生词本里的词是单词/短语,不带冒号;而改 <c>Lookup.Word</c> 的切分口径
    /// 会影响全部既有数据。这条断言的作用是:哪天有人动了切分口径,这里会先红。
    /// </summary>
    [Fact]
    public void NewWordContainingColon_WithoutPrefix_IsTruncated() {
        var built = WordRenameRules.BuildWordKey("beautiful", "x:y");
        Assert.Equal("x:y", built);
        Assert.Equal("y", WordOf(built));   // 只到第一个冒号为止
    }

    // —— IsNameTakenByOther ——

    /// <summary>改名到别的生词上 → 占用。</summary>
    [Fact]
    public void TakenByAnotherWord_IsReported() {
        Assert.True(WordRenameRules.IsNameTakenByOther(Words, "careful", exceptWord: "beautiful"));
    }

    /// <summary>没变名字时,即便"这个名字在生词表里"(就是它自己),也不算被别人占用。</summary>
    [Fact]
    public void TheWordItselfIsNotCountedAsAnotherHolder() {
        Assert.False(WordRenameRules.IsNameTakenByOther(Words, "beautiful", exceptWord: "beautiful"));
    }

    /// <summary>不排除自己时的对照 —— 证明上一条不是"恒为 false"的假绿。</summary>
    [Fact]
    public void WithoutExceptTheWordMatchesItself() {
        Assert.True(WordRenameRules.IsNameTakenByOther(Words, "beautiful"));
    }

    /// <summary>生词表里没有这个名字 → 不占用。</summary>
    [Fact]
    public void AbsentName_IsNotTaken() {
        Assert.False(WordRenameRules.IsNameTakenByOther(Words, "不存在的词", exceptWord: "beautiful"));
    }

    /// <summary>
    /// **与「重命名书籍」刻意不同的一条**:比较**忽略大小写** ——
    /// 左栏生词是按 <c>StringComparer.OrdinalIgnoreCase</c> 分组的,判定必须同口径,
    /// 否则会出现"列表里只有一个词,判定却说被占用"。
    /// </summary>
    [Fact]
    public void ComparisonIgnoresCase_ToMatchTheWordListGrouping() {
        Assert.True(WordRenameRules.IsNameTakenByOther(new[] { "Beautiful" }, "beautiful", exceptWord: "careful"));
    }

    /// <summary>反过来:正被改的那个词与自己大小写不同时,同样算"自己",不占用。</summary>
    [Fact]
    public void ExceptAlsoIgnoresCase() {
        Assert.False(WordRenameRules.IsNameTakenByOther(new[] { "Beautiful" }, "Beautiful", exceptWord: "beautiful"));
    }

    [Fact]
    public void EmptyList_IsNeverTaken() {
        Assert.False(WordRenameRules.IsNameTakenByOther(new List<string?>(), "beautiful"));
    }

    /// <summary>表里的 null <c>Word</c> 不该把"新名字为空"判成占用(左栏本就过滤掉空白词)。</summary>
    [Fact]
    public void NullNamesInListDoNotMatch() {
        Assert.False(WordRenameRules.IsNameTakenByOther(new string?[] { null, "beautiful" }, null));
    }

    // —— Decide ——

    /// <summary>① 一个字都没改 → 提示「生词名未修改」。</summary>
    [Fact]
    public void UnchangedWord_ReportsUnchanged() {
        Assert.Equal(WordRenameAction.Unchanged,
            WordRenameRules.Decide("beautiful", "beautiful", nameTakenByOtherWord: false));
    }

    /// <summary>
    /// ② **只改大小写算改了**(序数比较)—— 左栏忽略大小写分组,改完整组一起变,不会裂成两项。
    /// </summary>
    [Fact]
    public void CaseOnlyEdit_IsARename() {
        Assert.Equal(WordRenameAction.Rename,
            WordRenameRules.Decide("beautiful", "Beautiful", nameTakenByOtherWord: false));
    }

    /// <summary>③ 判定顺序:「什么都没改」优先于「撞名」——否则会提示"名字没动却已被占用"。</summary>
    [Fact]
    public void UnchangedWinsOverNameTaken() {
        Assert.Equal(WordRenameAction.Unchanged,
            WordRenameRules.Decide("beautiful", "beautiful", nameTakenByOtherWord: true));
    }

    /// <summary>④ 改名到没被占用的名字 → 改。</summary>
    [Fact]
    public void RenamedToAFreeName_Renames() {
        Assert.Equal(WordRenameAction.Rename,
            WordRenameRules.Decide("beautiful", "beautifully", nameTakenByOtherWord: false));
    }

    /// <summary>
    /// ⑤ 撞上别的生词 → **并入那一个**(不拦、不弹框)。
    /// 2026-09-23 改:原来这里返回"拒绝",用户实际遇到后要求静默解决(可合并或删除)。
    /// </summary>
    [Fact]
    public void RenamedOntoAnotherWord_MergesIntoIt() {
        Assert.Equal(WordRenameAction.MergeIntoExisting,
            WordRenameRules.Decide("beautiful", "careful", nameTakenByOtherWord: true));
    }

    /// <summary>⑥ 端到端串起来(数据 → 判定):改成已有生词落 MergeIntoExisting,改到空位落 Rename。</summary>
    [Fact]
    public void EndToEnd_DataPlusDecide() {
        const string word = "beautiful";
        var ontoExisting = WordRenameRules.IsNameTakenByOther(Words, "careful", exceptWord: word);
        var ontoFree = WordRenameRules.IsNameTakenByOther(Words, "beautifully", exceptWord: word);

        Assert.Equal(WordRenameAction.MergeIntoExisting, WordRenameRules.Decide(word, "careful", ontoExisting));
        Assert.Equal(WordRenameAction.Rename, WordRenameRules.Decide(word, "beautifully", ontoFree));
    }
}
