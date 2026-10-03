using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using KindleMate2.Avalonia.ViewModels;
using KindleMate2.Infrastructure.Helpers;
using KindleMate2.Shared;
using KindleMate2.Shared.Constants;

namespace KindleMate2.Avalonia.Views;

/// <summary>关于页。信息取自程序集元数据 + 当前打开的库,不再硬编码 KM2.dat。</summary>
public partial class AboutWindow : Window {
    public AboutWindow() {
        InitializeComponent();
    }

    public AboutWindow(AboutViewModel vm) : this() {
        DataContext = vm;
        BuildDeviceRows(vm);
    }

    /// <summary>
    /// 「设备」段按数据**现场生成行**:VM 里为 null 的字段(拔线竞态、Windows MTP 拿不到固件/容量、
    /// 版本文件没读到…)对应行根本不生成 —— 这就是"不用 — 占位、拿不到整行隐藏"的落地方式。
    ///
    /// 为什么不用 Grid.RowSpacing + IsVisible:Avalonia 的 RowSpacing 按 RowDefinitions 数量
    /// **固定计入**(源码 <c>RowSpacing * (DefinitionsV.Count - 1)</c>),隐藏行高度归零也照扣间距,
    /// 中间藏一行就留个 18px 空洞。行容器 StackPanel 虽会跳过不可见子项,但每行各自一个 Grid 时
    /// Auto 标签列宽不一、值列对不齐。而"一个共享标签列的 Grid、只装真实存在的行"两者兼得:
    /// 行数=真实行数 → 间距无残留;共用列 → 标签天然对齐。
    /// </summary>
    private void BuildDeviceRows(AboutViewModel vm) {
        if (!vm.IsDevicePresent) {
            return;
        }

        var grid = new Grid {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            ColumnSpacing = 14,   // 与上面信息区一致
            RowSpacing = 9,
        };
        var row = 0;

        void AddRow(string? label, string? value) {
            if (string.IsNullOrEmpty(label) || string.IsNullOrEmpty(value)) {
                return;
            }
            // 每加一行补一个 Auto 行定义 —— 与 ColumnDefinitions 同构。
            // **漏了这一步所有行会叠在一起**:Grid 没有 RowDefinitions 时只存在 Row 0,
            // 而下面 Grid.SetRow 会设到 1/2/3 —— 真机实测「连接方式 / 容量」两行直接重叠(2026-10-03)。
            // 行定义数 = 真实行数,于是 RowSpacing 的 `RowSpacing * (Count - 1)` 正好是
            // (行数 - 1) 个间距,不会留空洞 —— 本方法要的就是这个。
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var labelBlock = new TextBlock {
                Classes = { "tt" },
                Text = label,
                VerticalAlignment = VerticalAlignment.Center,
            };
            var valueBlock = new TextBlock {
                FontSize = 12,
                Text = value,
                TextWrapping = TextWrapping.Wrap,   // 卷路径可能很长,窗口 SizeToContent 自己撑高
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetRow(labelBlock, row);
            Grid.SetColumn(labelBlock, 0);
            Grid.SetRow(valueBlock, row);
            Grid.SetColumn(valueBlock, 1);
            grid.Children.Add(labelBlock);
            grid.Children.Add(valueBlock);
            row++;
        }

        AddRow(Strings.Ui_About_DeviceConnection, vm.DeviceConnection);
        AddRow(Strings.Ui_About_DevicePath, vm.DevicePath);
        AddRow(Strings.Ui_About_DeviceFirmware, vm.DeviceFirmware);
        AddRow(Strings.Ui_About_DeviceStorage, vm.DeviceStorage);
        DeviceRows.Children.Add(grid);
    }

    /// <summary>
    /// 点击「GitHub 仓库」→ 打开浏览器。打不开时**复制地址到剪贴板并告知用户**
    /// —— 对齐原版 <c>OpenUrl()</c>(FrmMain.cs:1562)的兜底分支,而不是静默失败
    /// (至少让用户能手动粘贴打开)。
    /// </summary>
    private async void OnOpenRepo(object? sender, RoutedEventArgs e) {
        try {
            Process.Start(new ProcessStartInfo { FileName = AppConstants.RepoUrl, UseShellExecute = true });
        } catch {
            await CopyToClipboardAndTellAsync(AppConstants.RepoUrl);
        }
    }

    /// <summary>复制文本到剪贴板并提示 —— 对应原版 OpenUrl 的兜底分支。</summary>
    private async Task CopyToClipboardAndTellAsync(string text) {
        try {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard != null) await clipboard.SetTextAsync(text);
        } catch {
            // 剪贴板不可用也只能作罢,不能因此再抛
        }
        await AppDialog.AlertAsync(this, Strings.Prompt, Strings.Repo_URL_Copied);
    }

    /// <summary>
    /// 点击「程序路径」→ 用文件管理器打开程序所在目录。
    /// </summary>
    private void OnOpenProgramPath(object? sender, RoutedEventArgs e) =>
        TryOpenDirectory(DataContext is AboutViewModel vm ? vm.ProgramPath : null);

    /// <summary>
    /// 点击「数据路径」→ 用文件管理器打开数据目录(KM2.dat、备份、导入导出都落在这里)。
    /// 与「程序路径」共用同一套处理,样式与行为都对齐。
    /// </summary>
    private void OnOpenDataPath(object? sender, RoutedEventArgs e) =>
        TryOpenDirectory(DataContext is AboutViewModel vm ? vm.DataPath : null);

    /// <summary>
    /// 用文件管理器打开目录,对应原版 FrmAboutBox 的 lblPath 链接
    /// (原版即 <c>Process.Start("explorer.exe", lblPath.Text)</c>,无确认、无提示)。
    /// 这里刻意不创建目录:路径只是展示用的位置,不存在或无权限就静默忽略。
    /// </summary>
    private static void TryOpenDirectory(string? path) {
        if (string.IsNullOrEmpty(path)) return;
        try {
            ShellHelper.OpenDirectory(path);
        } catch {
            // 目录不存在或无权限时静默忽略
        }
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
