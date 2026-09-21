using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
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
