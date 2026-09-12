using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace KindleMate2.Avalonia.Views;

/// <summary>
/// 通用模态对话框(确认 / 文本输入)。
/// 桌面版用 MessageBox + 自制重命名弹窗;Avalonia 侧统一成一个可复用的窗口,
/// 保证深色/浅色主题与设计令牌一致。
/// </summary>
public partial class AppDialog : Window {
    private bool _isPrompt;

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

    /// <summary>文本输入对话框。返回 null 表示取消。</summary>
    public static async Task<string?> PromptAsync(Window owner, string title, string message,
        string initial = "", string okText = "确定") {
        var dialog = new AppDialog();
        dialog.Configure(title, message, okText, false, initial);
        var ok = await dialog.ShowDialog<bool>(owner);
        return ok ? dialog.InputBox.Text ?? string.Empty : null;
    }

    private void Configure(string title, string message, string okText, bool danger, string? initial) {
        Title = title;
        TitleText.Text = title;
        MessageText.Text = message;
        MessageText.IsVisible = message.Length > 0;
        OkButton.Content = okText;
        OkButton.Classes.Clear();
        OkButton.Classes.Add(danger ? "danger" : "primary");

        if (initial != null) {
            _isPrompt = true;
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
        if (e.Key == Key.Enter && !_isPrompt) {
            Close(true);
            return;
        }
        base.OnKeyDown(e);
    }
}
