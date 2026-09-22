using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using KindleMate2.Application.Models;
using KindleMate2.Avalonia.Services;
using KindleMate2.Avalonia.ViewModels;

namespace KindleMate2.Avalonia.Views;

/// <summary>
/// 「维护数据库」的改动预览窗口 —— 只读,不写任何东西。
///
/// 流程仍是"先看后做":只读预扫 → 本窗口逐条列出会改什么 → 确认后才落库。
/// 清洗在应用内没有撤销路径,所以这一步是用户唯一的复核机会,值得给一个正经窗口:
/// 上一版把 5 条样例压成一段文字塞进通用确认框,结果是**尾部被截断** +
/// 无高亮 —— 89 条改动在预览里一条差异都看不出来,预览形同虚设。
/// </summary>
public partial class ClippingCleanPreviewWindow : Window {

    /// <summary>设计器 / XAML 载入用。</summary>
    public ClippingCleanPreviewWindow() {
        InitializeComponent();
    }

    public ClippingCleanPreviewWindow(DatabaseMaintenancePlan plan) : this() {
        DataContext = new ClippingCleanPreviewViewModel(plan);
        // 行数不定 ⇒ 高度更容易顶穿屏幕,与主窗口用同一套夹取口径。
        WindowSizing.ClampToWorkingArea(this);
    }

    /// <summary>打开预览并等待用户决定。返回 true 表示确认维护。</summary>
    public static async Task<bool> ConfirmAsync(Window owner, DatabaseMaintenancePlan plan) {
        var window = new ClippingCleanPreviewWindow(plan);
        return await window.ShowDialog<bool>(owner);
    }

    private void OnOk(object? sender, RoutedEventArgs e) => Close(true);

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);

    /// <summary>
    /// 与通用确认框保持一致:Esc 取消,回车确认(本窗口没有输入框,回车可以直接当确认)。
    /// 确认键落到「取消」上更危险 —— 这里是"要不要改库"的决定,默认必须是**不改**,
    /// 所以 Esc 与关窗都走取消。
    /// </summary>
    protected override void OnKeyDown(KeyEventArgs e) {
        if (e.Key == Key.Escape) {
            Close(false);
            return;
        }
        if (e.Key == Key.Enter) {
            Close(true);
            return;
        }
        base.OnKeyDown(e);
    }
}
