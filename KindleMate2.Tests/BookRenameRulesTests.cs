using System.Collections.Generic;
using Xunit;
using KindleMate2.Shared.Books;

namespace KindleMate2.Tests;

/// <summary>
/// 「重命名书籍」判定的回归用例。
///
/// 需求背景:用户报告「明明没有同名的书,却弹『同名书籍已存在,确认要合并吗?』」。
/// 排查结论是撞名检查把**当前这本书自己**也算成了占用者:
/// 只要在重命名对话框里只改作者、书名不动,新书名必然等于旧书名,
/// 而书单里必然有这本书自己的行 —— 于是拿自己撞自己。
/// 更糟的是真点了「确定」之后,作者会被覆写成旧值(合并分支用旧书作者),白改一场。
///
/// 真实数据也印证了这条路径:用户库里 285 本书**没有任何重名**
/// (序数、大小写、首尾空白、NFKC 归一后都不重),却有 5 本书内部存在两种作者写法
/// —— 正是「想改作者」的动机。
///
/// 判定已抽到 <see cref="BookRenameRules"/>(Avalonia 视图层包不进测试工程,故不再放在那里)。
/// 这里的用例钉两件事:
///   1. <see cref="BookRenameRules.IsNameTakenByOther"/> 必须排除「自己」;
///   2. <see cref="BookRenameRules.Decide"/> 的**判定顺序** —— 「什么都没改」优先于「撞名」,
///      且「只改作者」必须落到 Rename。
/// </summary>
public sealed class BookRenameRulesTests {
    private static readonly string?[] Books = { "零伯爵", "乙一作品集", "他改变了中国" };

    // —— IsNameTakenByOther ——

    /// <summary>
    /// 本次 bug 的核心:书名不动时,即便"这个名字在书单里"(就是它自己),也不算被别人占用。
    /// </summary>
    [Fact]
    public void OnlyAuthorChanged_NameIsNotTaken_IfTheOnlyHolderIsItself() {
        Assert.False(BookRenameRules.IsNameTakenByOther(Books, "零伯爵", exceptName: "零伯爵"));
    }

    /// <summary>
    /// 不排除自己时的对照 —— 证明上一条不是"恒为 false"的假绿:同一个输入、只去掉排除项就会变 true。
    /// </summary>
    [Fact]
    public void WithoutExceptTheBookMatchesItself() {
        Assert.True(BookRenameRules.IsNameTakenByOther(Books, "零伯爵"));
    }

    /// <summary>真的撞上别的书 → 占用(这条路径本来就该弹确认)。</summary>
    [Fact]
    public void TakenByAnotherBook_IsReported() {
        Assert.True(BookRenameRules.IsNameTakenByOther(Books, "他改变了中国", exceptName: "零伯爵"));
    }

    /// <summary>书单里没有这个名字 → 不占用。</summary>
    [Fact]
    public void NameAbsentFromList_IsNotTaken() {
        Assert.False(BookRenameRules.IsNameTakenByOther(Books, "不存在的书", exceptName: "零伯爵"));
    }

    /// <summary>
    /// 序数比较,不做大小写归一 —— 与书籍列表的 GroupBy(..., StringComparer.Ordinal) 同口径。
    /// 若这里改成忽略大小写,就会出现「列表里没有这本书,判定却说被占用」的矛盾。
    /// </summary>
    [Fact]
    public void ComparisonIsOrdinalAndCaseSensitive() {
        Assert.False(BookRenameRules.IsNameTakenByOther(new[] { "A Room of One's Own" }, "a room of one's own"));
    }

    /// <summary>
    /// 排除是按**名字**排除,不是"排除其中一条" —— 书单里出现两个同名时也会一起排除。
    /// 这不是缺陷:书籍列表本就按 BookName 分组(<c>GroupBy(..., StringComparer.Ordinal)</c>),
    /// 同名书在界面上**本来就是一项**,不存在"能被区分出来的另一本同名书"。
    /// 保持同一个口径,才不会出现"列表里只有一项,判定却说被别的书占用"。
    /// </summary>
    [Fact]
    public void ExcludingIsByNameSoEveryOccurrenceIsExcluded() {
        Assert.False(BookRenameRules.IsNameTakenByOther(new[] { "零伯爵", "零伯爵" }, "零伯爵", exceptName: "零伯爵"));
    }

    // —— Decide ——

    /// <summary>① 书名与作者都没改 → 提示「书名未修改」。</summary>
    [Fact]
    public void BothUnchanged_ReportsUnchanged() {
        Assert.Equal(BookRenameAction.Unchanged,
            BookRenameRules.Decide("零伯爵", "威廉·吉布森", "零伯爵", "威廉·吉布森", nameTakenByOtherBook: false));
    }

    /// <summary>
    /// ② **本次报告的 bug**:只改作者、书名不动 → 直接改,绝不弹合并确认。
    /// 这里刻意让 nameTakenByOtherBook 为 false(排除自己之后的正确取值)。
    /// </summary>
    [Fact]
    public void OnlyAuthorChanged_RenamesWithoutCombineConfirm() {
        Assert.Equal(BookRenameAction.Rename,
            BookRenameRules.Decide("零伯爵", "威廉·吉布森", "零伯爵", "[美] 威廉·吉布森;姚向辉", nameTakenByOtherBook: false));
    }

    /// <summary>
    /// ③ 判定顺序:即便调用方把「被别的书占用」误传成 true,「什么都没改」也要先胜出 ——
    /// 否则会出现「什么都没动却问你要不要合并」这种更离谱的提示。
    /// </summary>
    [Fact]
    public void UnchangedWinsOverCombineConfirm() {
        Assert.Equal(BookRenameAction.Unchanged,
            BookRenameRules.Decide("零伯爵", "威廉·吉布森", "零伯爵", "威廉·吉布森", nameTakenByOtherBook: true));
    }

    /// <summary>④ 改名到没被占用的名字 → 直接改。</summary>
    [Fact]
    public void RenamedToAFreeName_Renames() {
        Assert.Equal(BookRenameAction.Rename,
            BookRenameRules.Decide("零伯爵", "威廉·吉布森", "新名字", "威廉·吉布森", nameTakenByOtherBook: false));
    }

    /// <summary>⑤ 改名撞上别的书 → 先确认合并。</summary>
    [Fact]
    public void RenamedOntoAnotherBook_AsksToCombine() {
        Assert.Equal(BookRenameAction.ConfirmCombine,
            BookRenameRules.Decide("零伯爵", "威廉·吉布森", "他改变了中国", "威廉·吉布森", nameTakenByOtherBook: true));
    }

    /// <summary>
    /// ⑥ 端到端串起来(数据 → 判定):只改作者时,只要把书单和"自己"的名字一起传进来,
    /// 结果必须是 Rename —— 也就是用户在界面上不会再看到那个对话框。
    /// </summary>
    [Fact]
    public void EndToEnd_AuthorOnlyEdit_DoesNotAskToCombine() {
        const string book = "乙一作品集";
        var taken = BookRenameRules.IsNameTakenByOther(Books, book, exceptName: book);
        Assert.Equal(BookRenameAction.Rename,
            BookRenameRules.Decide(book, "乙一", book, "[日] 乙一", taken));
    }

    // —— 书单为空 / 值缺失等边界 ——

    [Fact]
    public void EmptyList_IsNeverTaken() {
        Assert.False(BookRenameRules.IsNameTakenByOther(new List<string?>(), "零伯爵"));
    }

    /// <summary>书单里的 null <c>BookName</c> 不应把"名字为空的新书名"判成占用(书籍列表本就过滤掉空白书名)。</summary>
    [Fact]
    public void NullNamesInListDoNotMatch() {
        Assert.False(BookRenameRules.IsNameTakenByOther(new string?[] { null, "零伯爵" }, null));
    }
}
