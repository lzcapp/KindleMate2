using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Avalonia.Threading;
using KindleMate2.Avalonia.Services;
using KindleMate2.Avalonia.ViewModels;
using KindleMate2.Avalonia.Views;
using KindleMate2.Infrastructure.Helpers;
using KindleMate2.Shared;
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

        // 全局异常兜底。本壳有 20+ 个 async void 事件处理器 —— 其异常无法向外抛出,
        // 没有兜底时任何未捕获异常都会让进程**直接消失、用户毫无提示**,还可能丢掉正在编辑的内容。
        // 策略:写日志 + 提示一次;UI 线程异常标记为已处理,避免整个进程消失。
        Dispatcher.UIThread.UnhandledException += (_, e) => {
            ReportCrash(e.Exception);
            e.Handled = true;
        };
        TaskScheduler.UnobservedTaskException += (_, e) => {
            ReportCrash(e.Exception);
            e.SetObserved();
        };

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
                // WinExe 没有控制台,Console.WriteLine 的消息无处可去 —— 写进文件日志。
                // 这里不弹窗:进程正在退出,弹窗没有意义。
                AppLog.Write(ex);
            }
        };

        base.OnFrameworkInitializationCompleted();
    }

    private static bool _crashReported;

    /// <summary>
    /// 记录未捕获异常并提示一次。
    /// **自身绝不能抛异常** —— 兜底代码再抛会把"可提示的错误"变成"静默崩溃"。
    /// </summary>
    private void ReportCrash(Exception? ex) {
        if (ex == null) return;
        AppLog.Write(ex);

        // 异常风暴(如重绘循环里连续抛)只提示第一次,避免弹窗刷屏
        if (_crashReported) return;
        _crashReported = true;

        try {
            var owner = (ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            if (owner != null) {
                _ = AppDialog.AlertAsync(owner, Strings.Error, ex.Message);
            }
        } catch {
            // 弹窗失败同样不能再抛
        }
    }
}
