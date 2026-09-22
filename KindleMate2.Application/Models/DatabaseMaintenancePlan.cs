using System.Collections.Generic;

namespace KindleMate2.Application.Models;

/// <summary>一条会被「清理」删掉的条目,为什么删。</summary>
public enum DatabaseCleanRemovalReason {
    /// <summary>内容或书名为空 —— 留着没有任何信息。</summary>
    Empty,

    /// <summary>重复项:内容与其它条目相同、或被别的条目包含。</summary>
    Duplicated
}

/// <summary>
/// 一条将被「清理」删除的条目(只读预演用)。
///
/// 与 <see cref="ClippingCleanChange"/> 并列:那个记录"改成了什么",这个记录"要删什么"。
/// 两者都会被写进改动清单 —— 清洗没有撤销路径,清单是用户事后核对/回滚的凭据,
/// 而**删行比改字更需要留痕**。
/// </summary>
/// <param name="Key">标注主键(标注日期|位置)。</param>
/// <param name="BookName">书名。</param>
/// <param name="Content">删除时的内容(空条目就是空串)。</param>
/// <param name="Reason">删除原因。</param>
public sealed record DatabaseCleanRemoval(string Key, string BookName, string Content,
    DatabaseCleanRemovalReason Reason);

/// <summary>
/// 一次「清理」的只读预演:要删哪些行、各是什么原因。
/// 计数由 <see cref="Removals"/> 现算,不另存一份 —— 两份数字必然漂移。
/// </summary>
public sealed class DatabaseCleanPlan {
    /// <summary>逐条待删记录。</summary>
    public IReadOnlyList<DatabaseCleanRemoval> Removals { get; init; } = [];

    /// <summary>内容或书名为空的条数。</summary>
    public int EmptyCount {
        get {
            var count = 0;
            foreach (var removal in Removals) {
                if (removal.Reason == DatabaseCleanRemovalReason.Empty) count++;
            }
            return count;
        }
    }

    /// <summary>判重删掉的条数。</summary>
    public int DuplicatedCount {
        get {
            var count = 0;
            foreach (var removal in Removals) {
                if (removal.Reason == DatabaseCleanRemovalReason.Duplicated) count++;
            }
            return count;
        }
    }

    /// <summary>确实有东西要删。全为 0 时清理是**无事可做**,不是失败。</summary>
    public bool HasWork => Removals.Count > 0;
}

/// <summary>
/// 一次「维护数据库」的只读预演 = 清洗会改哪些 + 清理会删哪些。
///
/// 两件事**必须一起预演**:清洗会把内容归一化,于是两条原本只差一个首部标点的标注
/// 会变成**完全相同**,从而成为清理眼里的"重复项"。分开预演的话,用户看到的是
/// "改 N 条"、"删 0 条",而真正执行时却会删掉几条 —— 所见与所做对不上。
/// </summary>
public sealed class DatabaseMaintenancePlan {
    /// <summary>清洗部分(逐条差异)。</summary>
    public ClippingCleanReport Cleaning { get; init; } = new();

    /// <summary>清理部分(逐条待删)。</summary>
    public DatabaseCleanPlan Cleanup { get; init; } = new();

    /// <summary>整体无事可做 —— 用来区分"跑完了什么都没改"与"失败"。</summary>
    public bool HasWork => Cleaning.ChangedCount > 0 || Cleanup.HasWork;
}
