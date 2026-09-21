using System;
using System.Collections.Generic;
using System.Linq;

namespace KindleMate2.Shared.Books;

/// <summary>点了「确定」之后,「重命名书籍」该走哪条路。</summary>
public enum BookRenameAction {
    /// <summary>书名与作者都没改 —— 提示「书名未修改」,不落库。</summary>
    Unchanged,

    /// <summary>直接改名(含只改作者这种「书名不动」的情况)。</summary>
    Rename,

    /// <summary>新书名已被**别的**书占用 —— 先弹「同名合并」确认。</summary>
    ConfirmCombine
}

/// <summary>
/// 「重命名书籍」的判定规则 —— 抽到这一层只是为了**可测**:
/// 三条分支(没变 / 直接改 / 撞名要确认合并)原本全在 Avalonia 的视图层,
/// 而测试工程不引用 Avalonia,包不进来也就钉不住。
///
/// 这里出过真 bug:撞名检查原先只问「这个名字在不在书单里」,
/// 而书单里必然有**当前这本书自己**。于是「只改作者、书名不动」时拿自己撞自己,
/// 凭空弹出「同名书籍已存在,确认要合并吗?」;
/// 用户点「确定」后作者还会被覆写成旧值(<see cref="BookRenameAction.ConfirmCombine"/>
/// 分支会用旧书的作者),等于白改一场却提示成功。
/// </summary>
public static class BookRenameRules {
    /// <summary>
    /// 该书名是否被**别的**书占用。
    /// <paramref name="exceptName"/> 用来排除「当前正操作的那本书自己」,传 null 表示不排除任何书。
    ///
    /// 比较用**序数**比较,不做大小写/空白归一 —— 必须与书籍列表的分组口径
    /// (<c>GroupBy(c =&gt; c.BookName, StringComparer.Ordinal)</c>)一致,
    /// 否则会出现「列表里明明没有这个名字」与「判定说已被占用」互相矛盾的观感。
    /// </summary>
    public static bool IsNameTakenByOther(IEnumerable<string?> names, string? candidate, string? exceptName = null) =>
        names.Any(name => string.Equals(name, candidate, StringComparison.Ordinal)
                       && !string.Equals(name, exceptName, StringComparison.Ordinal));

    /// <summary>
    /// 决定重命名的走向。<paramref name="nameTakenByOtherBook"/> 由调用方先用
    /// <see cref="IsNameTakenByOther"/> 算好 —— 它要读数据,不属于本层职责。
    ///
    /// 判定**顺序**即语义:先判「什么都没改」,再判「撞了别的书」。
    /// 只有这样,「只改作者」才会落到 <see cref="BookRenameAction.Rename"/> 而不是被当成撞名。
    /// </summary>
    public static BookRenameAction Decide(
        string? oldName, string? oldAuthor, string? newName, string? newAuthor, bool nameTakenByOtherBook) {
        if (string.Equals(newName, oldName, StringComparison.Ordinal) &&
            string.Equals(newAuthor, oldAuthor, StringComparison.Ordinal)) {
            return BookRenameAction.Unchanged;
        }

        return nameTakenByOtherBook ? BookRenameAction.ConfirmCombine : BookRenameAction.Rename;
    }
}
