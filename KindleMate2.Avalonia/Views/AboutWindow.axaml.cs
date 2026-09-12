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

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
