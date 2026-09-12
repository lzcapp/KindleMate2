using System;
using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using KindleMate2.Avalonia.Services;
using KindleMate2.Avalonia.ViewModels;
using KindleMate2.Avalonia.Views;
using KindleMate2.Infrastructure.Helpers;
using KindleMate2.Shared.Constants;

namespace KindleMate2.Avalonia;

public partial class App : global::Avalonia.Application {
    /// <summary>应用级设置(主题 / 语言 / 上次打开的库),在 Initialize 时装载。</summary>
    public AppSettings Settings { get; private set; } = new();

    public override void Initialize() {
        Settings = AppSettings.Load();
        Settings.ApplyCulture();
        AvaloniaXamlLoader.Load(this);
        ApplySavedTheme();
    }

    private void ApplySavedTheme() {
        RequestedThemeVariant = Settings.Theme switch {
            "dark" => ThemeVariant.Dark,
            "light" => ThemeVariant.Light,
            _ => ThemeVariant.Default
        };
    }

    public override void OnFrameworkInitializationCompleted() {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
            desktop.MainWindow = new MainWindow {
                DataContext = new MainWindowViewModel { Settings = Settings }
            };
        }

        // 对齐原版 FrmMain:进程退出时自动备份数据库。
        // 只在 App 层注册一次(主窗口在切换语言时会被重建,放在窗口里会重复注册导致多次备份)。
        AppDomain.CurrentDomain.ProcessExit += (_, _) => {
            try {
                var programPath = Environment.CurrentDirectory;
                DatabaseHelper.BackupDatabase(
                    programPath,
                    Path.Combine(programPath, AppConstants.BackupsPathName),
                    AppConstants.DatabaseFileName);
            } catch (Exception ex) {
                Console.WriteLine($"[ProcessExit backup] {ex.Message}");
            }
        };

        base.OnFrameworkInitializationCompleted();
    }
}
