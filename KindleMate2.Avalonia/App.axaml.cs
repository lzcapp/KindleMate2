using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
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

#if MACOS
        // 开发/调试时跑的是**裸 dll**,没有 .app bundle ⇒ Dock 会显示 dotnet 的图标(像命令行程序)。
        // 显式设一次,让调试时的 Dock 图标也是应用自己的。发布版由 bundle 的 CFBundleIconFile 提供,
        // 这里再设一次是幂等的。放在此处是因为 NSApplication 到这一步才存在。
        ApplyDockIcon();
#endif

        base.OnFrameworkInitializationCompleted();
    }

#if MACOS
    /// <summary>
    /// 把 macOS 的 Dock 图标设成应用自己的。
    ///
    /// 资源是 <c>AvaloniaResource</c>(嵌在程序集里),而 NSImage 只能从**文件**加载 ——
    /// 所以先把它落到临时文件。失败只写日志:外观问题绝不该影响启动。
    /// </summary>
    private static void ApplyDockIcon() {
        try {
            // 已经在 .app bundle 里跑 ⇒ Dock 图标由 bundle 的 CFBundleIconFile 提供(发布流程里
            // 由 sips/iconutil 从**同一张** bookmark.png 生成),不必再覆盖 —— 让发布版的图标
            // 来源保持为系统的 bundle 机制这一条单一来源。
            // 反过来,开发/调试跑的是裸 dll(没有 bundle),macOS 只能拿可执行文件的图标(显示为
            // "exec"),这才是本方法要修的场景。
            if (AppContext.BaseDirectory.Contains(".app/Contents/", StringComparison.Ordinal)) {
                return;
            }

            // 用 **bookmark-dock.png**(内容占画布 80.5% 的 macOS 口径版本),不是 bookmark.png ——
            // 后者是 Windows 口径(内容 95%×100%),当 Dock 图标会明显比旁边的应用大一圈。
            var uri = new Uri("avares://KindleMate2/Assets/bookmark-dock.png");
            if (!AssetLoader.Exists(uri)) {
                // **不要静默返回** —— 这个分支若因 URI 写错而命中,表现就是"图标没变、
                // 日志里什么都没有",最难排查。写日志才有线索。
                AppLog.Write($"[App] Dock 图标资源不存在:{uri}(图标保持系统默认)");
                return;
            }

            using var stream = AssetLoader.Open(uri);
            var tempPath = Path.Combine(Path.GetTempPath(), "KindleMate2-dock-icon.png");
            using (var file = File.Create(tempPath)) {
                stream.CopyTo(file);
            }

            MacOsDockIcon.Apply(tempPath);
        } catch (Exception ex) {
            AppLog.Write($"[App] 设置 Dock 图标失败:{ex}");
        }
    }
#endif

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
