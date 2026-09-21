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
using KindleMate2.Shared.Diagnostics;

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
        MainWindowViewModel? viewModel = null;
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
            viewModel = new MainWindowViewModel { Settings = Settings };
            desktop.MainWindow = new MainWindow { DataContext = viewModel };
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
        AppDomain.CurrentDomain.ProcessExit += (_, _) => BackupOnExit(viewModel);

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// 进程退出时自动备份**用户实际在编辑的那个库**。
    ///
    /// 原版只有一个固定的 <c>&lt;当前目录&gt;/KM2.dat</c>,而本壳允许从文件对话框打开别处的库,
    /// 此时那个库所在目录才是真正的"工作目录"。此前这里固定按当前目录构造路径(备份目标与
    /// 备份落点都取自 <c>Environment.CurrentDirectory</c>),一旦用户打开的是别处的库:
    /// 要么备份错对象(退出时"备份成功"了却与用户编辑的数据无关),要么因为别处没有 KM2.dat 而
    /// 抛 <see cref="FileNotFoundException"/> 被静默吞掉。现在与手动备份口径一致,都按会话走。
    ///
    /// 落点是 <c>&lt;Backups&gt;/OnExit/</c>(而非 Backups 根),且只保留最新
    /// <see cref="AppConstants.ExitBackupKeepCount"/> 份 —— 退出备份是**每次关闭都产生一份**的自动产物,
    /// 与用户手动触发的备份性质不同,不该混在同一层里互相淹没。详见 <see cref="AppConstants.ExitBackupsPathName"/>。
    /// </summary>
    /// <remarks>
    /// 访问级别是 <c>internal</c> 而非 <c>private</c>:<c>--ops</c> 写操作自检需要真实走一遍这条路径
    /// (否则"退出备份落在子目录且只留 3 份"在 CI 里没有任何一处会验到)。
    /// </remarks>
    internal static void BackupOnExit(MainWindowViewModel? viewModel) {
        try {
            var session = viewModel?.Session;
            var databasePath = session?.DatabasePath ?? AppPaths.DatabasePath;
            if (!File.Exists(databasePath)) {
                return;   // 还没建库就退出:不是异常,静默跳过(此前这里会抛 FileNotFoundException 进日志)
            }

            var workDirectory = session?.WorkDirectory ?? AppPaths.DataDirectory;
            var backupDirectory = session?.BackupDirectory
                                  ?? Path.Combine(workDirectory, AppConstants.BackupsPathName);

            // 退出备份单独放 Backups/OnExit 子目录,并只保留最新 N 份。
            // 理由:它每次关闭都产生一份,而手动备份 / 清洗前保护性备份都落在 Backups 根下 ——
            // 混在一起时根目录很快被一串时间戳文件淹没,用户真正主动要的那几份反而找不着。
            var exitBackupDirectory = Path.Combine(backupDirectory, AppConstants.ExitBackupsPathName);
            var databaseFileName = Path.GetFileName(databasePath);

            try {
                DatabaseHelper.BackupDatabase(workDirectory, exitBackupDirectory, databaseFileName);
            } finally {
                // 放在 finally 里:即便这一份没写成(例如同一秒内第二次退出导致文件名撞车),
                // 也该顺手把历史积压收敛掉 —— 清理本身不抛异常,不会盖住上面的失败。
                DatabaseHelper.PruneBackups(exitBackupDirectory, AppConstants.ExitBackupKeepCount, databaseFileName);
            }
        } catch (Exception ex) {
            // WinExe 没有控制台,Console.WriteLine 的消息无处可去 —— 写进文件日志。
            // 这里不弹窗:进程正在退出,弹窗没有意义。
            AppLog.Write(ex);
        }
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
