using System;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using KindleMate2.Avalonia.ViewModels;
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

    private void OnOpenRepo(object? sender, RoutedEventArgs e) {
        try {
            Process.Start(new ProcessStartInfo { FileName = AppConstants.RepoUrl, UseShellExecute = true });
        } catch {
            // 打不开浏览器时静默忽略,不影响对话框可用性
        }
    }

    /// <summary>
    /// 点击「程序路径」→ 用资源管理器打开该目录,对应原版 FrmAboutBox 的 lblPath 链接
    /// (原版即 <c>Process.Start("explorer.exe", lblPath.Text)</c>,无确认、无提示)。
    /// </summary>
    private void OnOpenProgramPath(object? sender, RoutedEventArgs e) {
        if (DataContext is not AboutViewModel vm || vm.ProgramPath.Length == 0) return;
        try {
            Process.Start(new ProcessStartInfo { FileName = vm.ProgramPath, UseShellExecute = true });
        } catch {
            // 目录不存在或无权限时静默忽略
        }
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
