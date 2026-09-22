using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using KindleMate2.Application.Models;
using KindleMate2.Shared;

namespace KindleMate2.Avalonia.ViewModels;

/// <summary>
/// 「清洗标注文本」预览窗口的数据。
///
/// 与上一版(把 5 条样例塞进通用确认框的正文)相比,这里要解决三件事:
/// <list type="number">
/// <item>**看得见差异** —— 被清洗掉的标点单独摘出来由视图加删除线,
/// 而不是让用户在两段几乎一样的长文本里逐字比对;</item>
/// <item>**看得见全部** —— 可滚动列出全部改动,不再只给 5 条;</item>
/// <item>**知道改的是哪条** —— 每行带书名与位置。</item>
/// </list>
/// </summary>
public sealed class ClippingCleanPreviewViewModel {

    /// <summary>
    /// 最多列出多少行。改动条数由库里的数据决定,理论上可以上万 ——
    /// 全部铺出来只会让窗口开得又慢又长;超出部分照样会写进改动清单。
    /// 上一版只给 5 条,这里放宽到 200:实测库(5752 条标注)需要清洗的也就 89 条。
    /// </summary>
    public const int MaxRows = 200;

    public ClippingCleanPreviewViewModel(DatabaseMaintenancePlan plan) {
        ArgumentNullException.ThrowIfNull(plan);
        var cleaning = plan.Cleaning;
        var cleanup = plan.Cleanup;

        // 概要必须**同时**给出"改多少"与"删多少":删行是这一步里唯一不可逆的部分,
        // 而它恰恰可能因为清洗把内容归一化而**凭空多出来**(两条只差一个首部标点的标注
        // 清洗后完全相同 ⇒ 都成了重复项)。见 ScanDatabaseMaintenance 的说明。
        Summary = string.Format(CultureInfo.CurrentCulture, Strings.Ui_Maintenance_Message_Format,
            cleaning.ChangedCount, cleaning.Scanned, cleanup.EmptyCount, cleanup.DuplicatedCount);

        OkText = Strings.Ui_Maintenance_Ok;

        Skipped = string.Format(CultureInfo.CurrentCulture,
            Strings.Ui_ClippingClean_Skipped_Format, cleaning.AllPunctuationCount);
        HasSkipped = cleaning.AllPunctuationCount > 0;

        Header = string.Format(CultureInfo.CurrentCulture, "{0} ({1})",
            Strings.Ui_Dlg_ClippingClean_Samples, cleaning.ChangedCount);

        var shown = cleaning.Changes.Take(MaxRows).ToList();
        Rows = shown.Select(change => new ClippingCleanPreviewRow(change)).ToList();

        var hidden = cleaning.Changes.Count - shown.Count;
        HasMore = hidden > 0;
        More = string.Format(CultureInfo.CurrentCulture, Strings.Ui_Dlg_ClippingClean_More_Format, hidden);
    }

    /// <summary>概要:清洗改 N 条、扫描 M 条、清理删空条目 X / 重复项 Y、会先备份。</summary>
    public string Summary { get; }

    /// <summary>「整条皆标点已跳过」的提示。</summary>
    public string Skipped { get; }

    /// <summary>没有跳过的条目时,那条提示整行隐藏(不留一个空行)。</summary>
    public bool HasSkipped { get; }

    /// <summary>「改动预览 (N)」。</summary>
    public string Header { get; }

    /// <summary>未列出的条数提示(条数超出 <see cref="MaxRows"/> 时才显示)。</summary>
    public string More { get; }

    /// <summary>是否真的有条目没列出来。</summary>
    public bool HasMore { get; }

    /// <summary>确认按钮文案:带上条数,免得"清洗"两个字让人猜到底要洗多少。</summary>
    public string OkText { get; }

    /// <summary>逐条改动(最多 <see cref="MaxRows"/> 条)。</summary>
    public IReadOnlyList<ClippingCleanPreviewRow> Rows { get; }
}
