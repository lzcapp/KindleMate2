using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using KindleMate2.Shared;

namespace KindleMate2.Avalonia.Views;

/// <summary>
/// 通用模态对话框(确认 / 文本输入)。
/// 桌面版用 MessageBox + 自制重命名弹窗;Avalonia 侧统一成一个可复用的窗口,
/// 保证深色/浅色主题与设计令牌一致。
/// </summary>
public partial class AppDialog : Window {
    /// <summary>多行输入(标注正文编辑)时回车应换行而不是提交,故单独标记。</summary>
    private bool _isMultiline;

    public AppDialog() {
        InitializeComponent();
    }

    /// <summary>确认对话框。返回 true 表示用户点了确定。</summary>
    public static async Task<bool> ConfirmAsync(Window owner, string title, string message,
        string okText = "确定", bool danger = false) {
        var dialog = new AppDialog();
        dialog.Configure(title, message, okText, danger, null);
        return await dialog.ShowDialog<bool>(owner);
    }

    /// <summary>
    /// 提示对话框(只有「确定」,没有取消)—— 对应原版 WinForms 的 MessageBox(..., OK)。
    /// 用于错误 / 成功 / 警告这类单按钮反馈,保持与原版一致的交互。
    /// </summary>
    public static async Task AlertAsync(Window owner, string title, string message, string okText = "") {
        var dialog = new AppDialog();
        dialog.Configure(title, message, okText.Length > 0 ? okText : Strings.Ui_Action_Ok, false, null);
        dialog.CancelButton.IsVisible = false;
        await dialog.ShowDialog<bool>(owner);
    }

    /// <summary>文本输入对话框。返回 null 表示取消。</summary>
    public static async Task<string?> PromptAsync(Window owner, string title, string message,
        string initial = "", string okText = "确定") {
        var dialog = new AppDialog();
        dialog.Configure(title, message, okText, false, initial);
        var ok = await dialog.ShowDialog<bool>(owner);
        return ok ? dialog.InputBox.Text ?? string.Empty : null;
    }

    /// <summary>
    /// 多行文本输入对话框 —— 对应原版 <c>KeyValue.ValueTypes.Multiline</c> 字段
    /// (标注编辑用正文是多行文本)。返回 null 表示取消。
    /// </summary>
    public static async Task<string?> PromptMultilineAsync(Window owner, string title, string message,
        string initial = "", string okText = "确定", int lines = 8) {
        var dialog = new AppDialog();
        dialog.Configure(title, message, okText, false, initial);
        dialog._isMultiline = true;
        dialog.InputBox.AcceptsReturn = true;
        // 注意:本文件位于 namespace KindleMate2.Avalonia 内,Avalonia.Media / Avalonia.Layout
        // 会被解析成 KindleMate2.Avalonia.Media 等,必须用 global:: 限定。
        dialog.InputBox.TextWrapping = global::Avalonia.Media.TextWrapping.Wrap;
        dialog.InputBox.Height = lines * 20;
        dialog.InputBox.VerticalContentAlignment = global::Avalonia.Layout.VerticalAlignment.Top;
        var ok = await dialog.ShowDialog<bool>(owner);
        return ok ? dialog.InputBox.Text ?? string.Empty : null;
    }

    /// <summary>
    /// 双字段输入对话框 —— 对应原版重命名书籍时的「书名 + 作者」InputBox。
    /// 原版两个字段都不允许为空(任一为空则不允许提交),这里以禁用「确定」等价实现。
    /// 返回 null 表示取消。
    /// </summary>
    public static async Task<(string First, string Second)?> PromptTwoFieldsAsync(Window owner,
        string title, string firstLabel, string firstValue,
        string secondLabel, string secondValue, string okText = "") {
        var dialog = new AppDialog();
        dialog.Configure(title, firstLabel, okText.Length > 0 ? okText : Strings.Ui_Action_Ok, false, firstValue);
        dialog.SecondField.IsVisible = true;
        dialog.SecondLabel.Text = secondLabel;
        dialog.SecondInputBox.Text = secondValue;

        void Sync() => dialog.OkButton.IsEnabled =
            !string.IsNullOrWhiteSpace(dialog.InputBox.Text) &&
            !string.IsNullOrWhiteSpace(dialog.SecondInputBox.Text);
        dialog.InputBox.TextChanged += (_, _) => Sync();
        dialog.SecondInputBox.TextChanged += (_, _) => Sync();
        Sync();

        var ok = await dialog.ShowDialog<bool>(owner);
        return ok ? (dialog.InputBox.Text ?? string.Empty, dialog.SecondInputBox.Text ?? string.Empty) : null;
    }

    private void Configure(string title, string message, string okText, bool danger, string? initial) {        Title = title;
        TitleText.Text = title;
        MessageText.Text = message;
        MessageText.IsVisible = message.Length > 0;
        OkButton.Content = okText;
        OkButton.Classes.Clear();
        OkButton.Classes.Add(danger ? "danger" : "primary");

        if (initial != null) {
            InputBox.IsVisible = true;
            InputBox.Text = initial;
            InputBox.SelectAll();
            InputBox.AttachedToVisualTree += (_, _) => InputBox.Focus();
        }
    }

    private void OnOk(object? sender, RoutedEventArgs e) => Close(true);

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);

    protected override void OnKeyDown(KeyEventArgs e) {
        if (e.Key == Key.Escape) {
            Close(false);
            return;
        }
        // 单行/双字段输入里回车=提交(与原版 InputBox 一致);多行输入里回车应换行。
        // 「确定」被禁用(如必填项为空)时不提交。
        if (e.Key == Key.Enter && !_isMultiline && OkButton.IsEnabled) {
            Close(true);
            return;
        }
        base.OnKeyDown(e);
    }
}
