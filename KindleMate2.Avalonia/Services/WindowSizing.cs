using System;
using Avalonia.Controls;

namespace KindleMate2.Avalonia.Services;

/// <summary>
/// 把窗口尺寸夹进当前屏幕**可用区域**(已扣除任务栏)的公共逻辑。
///
/// 原先是 <c>MainWindow</c> 里的私有方法,这次抽出来给「清洗预览」窗口复用 ——
/// 那个窗口的内容是**不定长**的(改动条数由库里的数据决定),更容易顶穿屏幕。
/// 抽成一处而不是各写一遍:夹取口径(物理像素 → 逻辑单位、MinWidth/MinHeight 也要一起夹)
/// 写错一次就够难受了。
/// </summary>
internal static class WindowSizing {

    /// <summary>
    /// 按当前屏幕的可用区域夹取窗口的 <c>Width/Height</c> 与 <c>MinWidth/MinHeight</c>。
    ///
    /// 拿不到屏幕信息时**什么都不改**(宁可超出也不要把尺寸改成 0);
    /// 可用区域足够大时同样不改变任何取值 —— 大屏行为与之前完全一致。
    /// 取小值,重复调用无副作用。
    /// </summary>
    public static void ClampToWorkingArea(Window window) {
        if (window.Screens is not { } screens) {
            return;
        }

        var screen = screens.ScreenFromWindow(window) ?? screens.Primary;
        if (screen is not { } current) {
            return;
        }

        // WorkingArea 是物理像素,而窗口的 Width/Height 是逻辑单位,必须按缩放换算。
        var scaling = current.Scaling > 0 ? current.Scaling : 1d;
        var maxWidth = current.WorkingArea.Width / scaling;
        var maxHeight = current.WorkingArea.Height / scaling;

        window.MinWidth = Math.Min(window.MinWidth, maxWidth);
        window.MinHeight = Math.Min(window.MinHeight, maxHeight);
        window.Width = Math.Min(window.Width, maxWidth);
        window.Height = Math.Min(window.Height, maxHeight);
    }
}
