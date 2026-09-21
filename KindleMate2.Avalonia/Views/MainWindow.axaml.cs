using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using KindleMate2.Application.Models;
using KindleMate2.Avalonia.ViewModels;
using KindleMate2.Infrastructure.Helpers;
using KindleMate2.Shared;
using KindleMate2.Shared.Books;
using KindleMate2.Shared.Constants;

namespace KindleMate2.Avalonia.Views;

public partial class MainWindow : Window {
    private DispatcherTimer? _deviceTimer;

    public MainWindow() {
        InitializeComponent();
        // 尽早夹取,避免窗口先按 XAML 的 1200x780 显示再跳变;Opened 里再兜底一次(幂等)。
        ClampToWorkingArea();
    }

    private MainWindowViewModel? Vm => DataContext as MainWindowViewModel;

    /// <summary>
    /// 把窗口尺寸夹进当前屏幕的可用区域内。
    /// </summary>
    /// <remarks>
    /// XAML 里写死了 <c>Width="1200" Height="780"</c>,而 <c>WindowStartupLocation=CenterScreen</c>
    /// 在窗口超出屏幕时会把余量**均分到上下两端** —— 于是在可用高度不足 780 的机器上
    /// (1366x768 去掉任务栏后约 728;1080p 开 150% 缩放时逻辑可用高度只有约 720),
    /// 标题栏与底部状态栏会**同时**溢出屏幕。宽度同理。
    /// 这里按 <c>WorkingArea</c>(已扣除任务栏)夹取;MinWidth/MinHeight 也必须一起夹,
    /// 否则可用区域比最小值还小时,最小值自身就会顶穿屏幕。
    /// 可用区域足够大时本方法不改变任何取值 —— 大屏行为与之前完全一致。
    /// </remarks>
    private void ClampToWorkingArea() {
        if (Screens is not { } screens) {
            return;
        }

        var screen = screens.ScreenFromWindow(this) ?? screens.Primary;
        if (screen is not { } current) {
            return;
        }

        // WorkingArea 是物理像素,而窗口的 Width/Height 是逻辑单位,必须按缩放换算。
        var scaling = current.Scaling > 0 ? current.Scaling : 1d;
        var maxWidth = current.WorkingArea.Width / scaling;
        var maxHeight = current.WorkingArea.Height / scaling;

        MinWidth = Math.Min(MinWidth, maxWidth);
        MinHeight = Math.Min(MinHeight, maxHeight);
        Width = Math.Min(Width, maxWidth);
        Height = Math.Min(Height, maxHeight);
    }

    private async void OnOpened(object? sender, EventArgs e) {
        // 构造期若还拿不到屏幕信息,这里兜底;夹取是取小值,重复调用无副作用。
        ClampToWorkingArea();
        SyncThemeFromApplication();
        if (Vm is { } vm) {
            // 对齐原版 FrmMain 构造函数:固定 KM2.dat → 不存在则建库 → 一次性 lookups 迁移
            var (fatal, ok, error) = await vm.PrepareDatabaseAsync();
            if (fatal) {
                await AppDialog.AlertAsync(this, Strings.Error,
                    MessageHelper.BuildMessage(Strings.Create_Database_Failed, new Exception(error)));
                Close();   // 原版此处 Environment.Exit(0)
                return;
            }
            if (vm.MigrationWarning.Length > 0) {
                // 原版:迁移失败只警告,不阻断启动
                await AppDialog.AlertAsync(this, Strings.Error, vm.MigrationWarning);
            }
            if (!ok) {
                // 原版 RefreshData 的 catch:正文 = BuildMessage(Failed, ex),标题 = Error
                await AppDialog.AlertAsync(this, Strings.Error,
                    MessageHelper.BuildMessage(Strings.Failed, new Exception(error)));
            }
        }
        // 对齐原版 FrmMain_Load:备份恢复 / 删除备份的两个连续确认。
        // 注意原版的既定行为:第二个确认与第一个的回答**无关** —— 即使用户拒绝恢复,
        // 仍会追问是否删除备份。不要"顺手修正"成互斥。
        if (Vm is { } mainVm) {
            var (askBackup, backupFile) = mainVm.CheckStartupBackup();
            if (askBackup) {
                var restore = await AppDialog.ConfirmAsync(this, Strings.Confirm, Strings.Confirm_Restore_Database, Strings.Ui_Action_Ok);
                if (restore) {
                    try { mainVm.RestoreFromBackup(backupFile); }
                    catch (Exception ex) { await AppDialog.AlertAsync(this, Strings.Error, ex.Message); }
                }
                var deleteBackup = await AppDialog.ConfirmAsync(this, Strings.Confirm, Strings.Confirm_Delete_Backup, Strings.Ui_Action_Ok);
                if (deleteBackup) {
                    try { mainVm.DeleteBackup(backupFile); }
                    catch (Exception ex) { await AppDialog.AlertAsync(this, Strings.Error, ex.Message); }
                }
            }
        }

        StartDevicePolling();
        _ = RefreshDeviceStatusAsync();
    }

    /// <summary>
    /// 编辑选中标注的正文 —— 对齐原版 <c>ShowContentEditDialog</c>(双击内容列触发):
    /// 「取消 / 内容为空 / 与原文相同」一律**静默返回**(原版如此,不弹任何提示);
    /// 成功弹 Successful + Clippings_Revised,失败弹 Clippings_Revised_Failed。
    /// </summary>
    private async void OnEditClipping(object? sender, TappedEventArgs e) {
        if (Vm is not { } vm) return;
        if (!vm.HasSelectedItem) return;
        var key = vm.SelectedClippingKey;
        if (key.Length == 0) return;

        var current = vm.SelectedClippingContent;
        var edited = await AppDialog.PromptMultilineAsync(this, Strings.Edit_Clippings, Strings.Content, current);
        if (edited == null) return;

        var trimmed = edited.Trim();
        if (trimmed.Length == 0) return;
        if (string.Equals(trimmed, current, StringComparison.Ordinal)) return;

        await ShowResultAsync(await vm.SaveClippingContentAsync(key, trimmed));
    }

    private void SyncThemeFromApplication() {
        if (Vm is not { } vm) return;
        var variant = global::Avalonia.Application.Current?.ActualThemeVariant;
        vm.IsDarkTheme = variant != ThemeVariant.Light;
    }

    // —— 设备状态轮询(计时器由视图层持有,VM 不依赖 UI 调度器) ——

    private void StartDevicePolling() {
        _deviceTimer ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
        _deviceTimer.Tick -= OnDeviceTimerTick;
        _deviceTimer.Tick += OnDeviceTimerTick;
        _deviceTimer.Start();
    }

    private void OnDeviceTimerTick(object? sender, EventArgs e) => _ = RefreshDeviceStatusAsync();

    private async Task RefreshDeviceStatusAsync() {
        if (Vm is not { } vm || !vm.HasSession) return;
        (string Text, bool Connected) probe;
        try {
            probe = await Task.Run(vm.ProbeDevice);
        } catch {
            // 走与正常路径同一个本地化键 —— 此前硬编码中文,英文界面下会漏出中文
            probe = (Strings.Ui_Status_DeviceOffline, false);
        }
        vm.DeviceStatus = probe.Text;
        vm.IsDeviceConnected = probe.Connected;
    }

    /// <summary>
    /// 关窗时停掉设备轮询。
    /// **必须停**:DispatcherTimer 通过 Tick 委托**强引用**窗口,不停表则窗口与 VM 永不被回收,
    /// 且旧表会继续每 8 秒轮询一次。切换语言会重建主窗口(TryRebuildWindow → previous.Close()),
    /// 因此这段是"每次切语言都会泄漏一份轮询"的正解。
    /// </summary>
    protected override void OnClosed(EventArgs e) {
        _deviceTimer?.Stop();
        _deviceTimer = null;
        base.OnClosed(e);
    }

    // —— 域 / 视图 / 排序 / 主题 ——

    private void OnSelectClipDomain(object? sender, RoutedEventArgs e) {
        if (Vm is { } vm) vm.DomainIndex = 0;
    }

    private void OnSelectWordDomain(object? sender, RoutedEventArgs e) {
        if (Vm is { } vm) vm.DomainIndex = 1;
    }

    private void OnShowList(object? sender, RoutedEventArgs e) {
        if (Vm is { } vm) vm.IsListMode = true;
    }

    private void OnShowTable(object? sender, RoutedEventArgs e) {
        if (Vm is { } vm) vm.IsListMode = false;
    }

    /// <summary>
    /// 删除左栏当前节点 —— 对齐原版 <c>DeleteNodes()</c> + <c>MenuBooksDelete_Click</c>。
    /// 按节点类型用**不同的确认文案**(原版如此,标题统一 Strings.Confirm):
    /// 全部标注 / 某本书的全部标注 / 全部生词 / 某个词的全部查询。
    /// 删除本身成功**不弹提示**(与原版一致),失败才弹 Delete_Failed。
    /// </summary>
    private async void OnDeleteNavNode(object? sender, RoutedEventArgs e) {
        if (Vm is not { } vm) return;
        if (vm.SelectedNav is not { } nav) return;

        var isClip = vm.IsClipDomain;
        var message = nav.IsAll
            ? (isClip ? Strings.Confirm_Clear_Clippings : Strings.Confirm_Clear_Vocabulary)
            : (isClip ? Strings.Confirm_Delete_Clippings_Book : Strings.Confirm_Delete_Lookups_Vocabs);

        var ok = await AppDialog.ConfirmAsync(this, Strings.Confirm, message, Strings.Ui_Action_Ok);
        if (!ok) return;   // 原版选「否」直接返回

        await ShowResultAsync(await vm.DeleteCurrentNavNodeAsync());
        // 原版删除后把选中切回「全部」节点(此时原节点已不存在)
        vm.SelectedNav = vm.NavItems.FirstOrDefault();
    }

    /// <summary>左栏键盘操作 —— 对齐原版两棵树的 KeyDown:Delete 删节点,Enter 重命名。</summary>
    private void OnNavKeyDown(object? sender, KeyEventArgs e) {
        switch (e.Key) {
            case Key.Delete:
                OnDeleteNavNode(sender, e);
                e.Handled = true;
                break;
            case Key.Enter:
                OnRenameCurrent(sender, e);
                e.Handled = true;
                break;
        }
    }

    private void OnToggleSort(object? sender, RoutedEventArgs e) {
        if (Vm is { } vm) vm.SortDescending = !vm.SortDescending;
    }

    private void OnToggleTheme(object? sender, RoutedEventArgs e) {
        if (Vm is not { } vm) return;
        vm.IsDarkTheme = !vm.IsDarkTheme;
        if (global::Avalonia.Application.Current is { } app) {
            app.RequestedThemeVariant = vm.IsDarkTheme ? ThemeVariant.Dark : ThemeVariant.Light;
        }
        vm.PersistTheme(vm.IsDarkTheme);
    }

    // —— 数据库 ——
    // 原版没有「打开数据库」入口:库固定为 <当前目录>/KM2.dat,不存在则自动建库。
    // 此前的库选择器/多库记忆/Probe 拒绝均为超出范围的发明,已移除(F 项)。

    private async void OnMenuRefresh(object? sender, RoutedEventArgs e) {
        if (Vm is not { } vm) return;
        if (!vm.HasSession) {
            vm.StatusText = Strings.Ui_Status_NoDatabase;
            return;
        }
        await vm.ReloadAsync();
    }

    private async Task<string?> PickFileAsync(string title, string typeName, string[] patterns) {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = new[] {
                new FilePickerFileType(typeName) { Patterns = patterns },
                FilePickerFileTypes.All
            }
        });
        var file = files.FirstOrDefault();
        if (file is null) return null;
        var path = file.TryGetLocalPath();
        if (string.IsNullOrEmpty(path)) {
            if (Vm is { } vm) vm.StatusText = string.Format(CultureInfo.CurrentCulture, Strings.Ui_Status_BadPath, file.Path);
            return null;
        }
        return path;
    }

    // —— 导入 ——

    private async void OnMenuImportClippings(object? sender, RoutedEventArgs e) {
        if (Vm is not { } vm) return;
        var path = await PickFileAsync(Strings.Ui_Pick_Clippings, Strings.Ui_FileType_Clippings, new[] { "*.txt" });
        if (path == null) return;   // 原版:取消选择即静默返回
        await ShowResultAsync(await vm.ImportKindleClippingsAsync(path));
    }

    private async void OnMenuImportVocab(object? sender, RoutedEventArgs e) {
        if (Vm is not { } vm) return;
        var path = await PickFileAsync(Strings.Ui_Pick_Words, Strings.Ui_FileType_Words, new[] { "*.db" });
        if (path == null) return;
        await ShowResultAsync(await vm.ImportKindleWordsAsync(path));
    }

    private async void OnMenuImportKmDatabase(object? sender, RoutedEventArgs e) {
        if (Vm is not { } vm) return;
        var path = await PickFileAsync(Strings.Ui_Pick_KmDatabase, Strings.Ui_FileType_KmDatabase, new[] { "*.dat", "*.db" });
        if (path == null) return;
        await ShowResultAsync(await vm.ImportKmDatabaseAsync(path));
    }

    /// <summary>
    /// 导入另一个 Kindle Mate 2 数据库(本程序自己的库格式,通常来自另一台机器或旧备份)。
    /// 与原版 Kindle Mate 的库同 schema,故合并逻辑复用;入口单列只为让来源一目了然。
    /// </summary>
    private async void OnMenuImportKm2Database(object? sender, RoutedEventArgs e) {
        if (Vm is not { } vm) return;
        var path = await PickFileAsync(Strings.Ui_Pick_Km2Database, Strings.Ui_FileType_Km2Database, new[] { "*.dat", "*.db" });
        if (path == null) return;
        await ShowResultAsync(await vm.ImportKm2DatabaseAsync(path));
    }

    private async void OnMenuImportKmateDatabase(object? sender, RoutedEventArgs e) {
        if (Vm is not { } vm) return;
        var path = await PickFileAsync(Strings.Ui_Pick_KmateDatabase, Strings.Ui_FileType_KmateDatabase, new[] { "*.dat", "*.db" });
        if (path == null) return;
        await ShowResultAsync(await vm.ImportKmateDatabaseAsync(path));
    }

    // —— 通用操作反馈(严格按原版语义弹框) ——

    /// <summary>
    /// 把 VM 返回的 <see cref="MainWindowViewModel.OperationResult"/> 呈现为对话框。
    /// 静默分支不弹任何窗 —— 原版有大量此类分支(导出失败、用户取消),这是既定行为,不要"补全"。
    /// </summary>
    private async Task ShowResultAsync(MainWindowViewModel.OperationResult result) {
        if (result.Kind == MainWindowViewModel.FeedbackKind.Silent) return;
        if (result.Title.Length == 0 && result.Message.Length == 0) return;

        if (result.Kind == MainWindowViewModel.FeedbackKind.OpenFolderPrompt) {
            // 原版:YesNo 追问「需要打开文件夹吗?」,选「是」后打开资源管理器
            var open = await AppDialog.ConfirmAsync(this, result.Title, result.Message, Strings.Ui_Action_Ok);
            if (open) OpenInExplorer(result.FolderToOpen);
            return;
        }

        await AppDialog.AlertAsync(this, result.Title, result.Message);
    }

    /// <summary>
    /// 打开指定目录(对应原版 <c>Process.Start("explorer.exe", path)</c>)。
    /// 目录由本方法负责创建(导出 / 备份产物可能尚不存在);平台命令交由 <see cref="ShellHelper"/>,
    /// 原先直接 <c>Process.Start(路径)</c> 的写法只对目录成立,不能用来「选中文件」。
    /// </summary>
    private void OpenInExplorer(string path) {
        try {
            if (string.IsNullOrWhiteSpace(path)) return;
            Directory.CreateDirectory(path);
            ShellHelper.OpenDirectory(path);
        } catch (Exception ex) {
            KindleMate2.Shared.Diagnostics.AppLog.Write(ex);
        }
    }

    // —— 导出 ——

    private async void OnMenuExportMarkdown(object? sender, RoutedEventArgs e) {
        if (Vm is not { } vm) return;
        // 原版 MenuExportMd_Click:一次导出「标注 + 生词本」两份,任一失败即静默无提示
        await ShowResultAsync(await vm.ExportAllMarkdownAsync());
    }

    // —— 维护 ——

    private async void OnMenuBackup(object? sender, RoutedEventArgs e) {
        if (Vm is { } vm) await ShowResultAsync(await vm.BackupDatabaseAsync());
    }

    /// <summary>
    /// 清理数据库。确认框为**用户指定**(2026-09-13;原版 MenuClean_Click 无确认框);
    /// 执行前的「无数据 → 数据库无需清理」提示由 VM 按原版逻辑返回。
    /// </summary>
    private async void OnMenuCleanDb(object? sender, RoutedEventArgs e) {
        if (Vm is not { } vm) return;
        if (vm.HasClippingData) {
            var ok = await AppDialog.ConfirmAsync(this, Strings.Ui_Menu_CleanDatabase,
                Strings.Ui_Dlg_CleanMessage, Strings.Ui_Dlg_CleanOk);
            if (!ok) return;
        }
        await ShowResultAsync(await vm.CleanDatabaseAsync());
    }

    /// <summary>
    /// 清洗标注文本(2026-09-21 新增;原版无此功能)。
    /// 与「清理数据库」是两件事:那个判重、删空条目、VACUUM,动的是**行数**;
    /// 这个只改每条的首尾标点,**一条都不删**。
    /// 流程刻意做成"先看后做":只读预扫 → 确认框给出条数与若干条「改前 → 改后」样例 → 确认后才落库。
    /// 清洗在应用内没有撤销路径,所以落库前 VM 会无条件先备份数据库、并留一份改动清单。
    /// </summary>
    private async void OnMenuCleanClipping(object? sender, RoutedEventArgs e) {
        if (Vm is not { } vm) return;

        var preview = await vm.PreviewClippingCleanAsync();
        if (preview is null) {
            // 无会话 / 正忙 / 读库失败对用户是同一件事:这次洗不了,不必细分。
            await ShowResultAsync(new MainWindowViewModel.OperationResult(false,
                Strings.Ui_ClippingClean_Failed, Strings.Ui_Status_OpenDatabaseFirst));
            return;
        }
        if (preview.ChangedCount == 0) {
            // 无需清洗不是错误,但也不该静默 —— 否则用户会以为点了没反应。
            await ShowResultAsync(new MainWindowViewModel.OperationResult(false,
                Strings.Ui_Menu_CleanClippingText, Strings.Ui_ClippingClean_None));
            return;
        }

        var ok = await AppDialog.ConfirmAsync(this, Strings.Ui_Menu_CleanClippingText,
            BuildCleanPreview(preview), Strings.Ui_Dlg_ClippingClean_Ok);
        if (!ok) return;
        await ShowResultAsync(await vm.CleanClippingTextsAsync());
    }

    /// <summary>确认框里最多列几条样例 —— 再多用户也不会读,框还会长到看不完。</summary>
    private const int CleanSampleRows = 5;

    /// <summary>样例里单侧文本的显示上限。确认框只为让人核对"清洗口径对不对",不必看全文。</summary>
    private const int CleanSampleChars = 60;

    private static string BuildCleanPreview(ClippingCleanReport report) {
        var lines = new List<string> {
            string.Format(CultureInfo.CurrentCulture, Strings.Ui_Dlg_ClippingClean_Message_Format,
                report.ChangedCount, report.Scanned),
            string.Empty,
            Strings.Ui_Dlg_ClippingClean_Samples + ":"
        };
        lines.AddRange(report.Changes.Take(CleanSampleRows).Select(change =>
            string.Format(CultureInfo.CurrentCulture, Strings.Ui_Dlg_ClippingClean_Sample_Format,
                FlattenForDialog(change.Before), FlattenForDialog(change.After))));
        if (report.ChangedCount > CleanSampleRows) {
            lines.Add("…");
        }
        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>把标注正文压成一行并截断 —— 换行会把确认框撑散,长文也读不过来。</summary>
    private static string FlattenForDialog(string? text) {
        var flat = (text ?? string.Empty).Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ');
        return flat.Length <= CleanSampleChars ? flat : flat[..CleanSampleChars] + "…";
    }

    private async void OnMenuRebuildDb(object? sender, RoutedEventArgs e) {
        if (Vm is not { } vm) return;
        var ok = await AppDialog.ConfirmAsync(this, Strings.Confirm, Strings.Confirm_Rebuild_Database, Strings.Ui_Action_Ok);
        if (!ok) return;   // 原版:确认框选 No → 静默 return
        await ShowResultAsync(await vm.RebuildDatabaseAsync());
    }

    private async void OnMenuDeleteAll(object? sender, RoutedEventArgs e) {
        if (Vm is not { } vm) return;
        var ok = await AppDialog.ConfirmAsync(this, Strings.Confirm, Strings.Confirm_Clear_All_Data,
            Strings.Ui_Action_Ok, danger: true);
        if (!ok) return;
        await ShowResultAsync(await vm.ClearAllDataAsync());
    }

    private async void OnMenuSyncToDevice(object? sender, RoutedEventArgs e) {
        if (Vm is not { } vm) return;
        if (vm.DeviceStatus.Contains(Strings.Ui_Status_DeviceOffline, StringComparison.Ordinal) ||
            vm.DeviceStatus.Length == 0) {
            vm.StatusText = Strings.Ui_Status_NoDevice;
            return;
        }
        var ok = await AppDialog.ConfirmAsync(this, Strings.Confirm, Strings.Confirm_Sync_To_Kindle, Strings.Ui_Action_Ok);
        if (!ok) return;
        await ShowResultAsync(await vm.SyncToDeviceAsync());
    }

    private async void OnMenuStatistics(object? sender, RoutedEventArgs e) {
        if (Vm is not { } vm) return;
        // 原版 MenuStatistic_Click:库为空时提示 Database_Empty 并拒绝打开统计窗
        if (!vm.HasClippingData) {
            await AppDialog.AlertAsync(this, Strings.Prompt, Strings.Database_Empty);
            return;
        }
        var stats = await Task.Run(() => StatisticsViewModel.Load(vm.Session));
        await new StatisticsWindow(stats).ShowDialog(this);
    }

    /// <summary>
    /// 重启程序 —— 对齐原版 <c>Restart()</c>(FrmMain.cs:1573):
    /// 启动同路径的新进程后立即退出当前进程。
    /// (原版为 <c>Process.Start(ExecutablePath)</c> + <c>Environment.Exit(0)</c>;
    /// 退出会触发 App 层的 ProcessExit 备份,与原版一致。)
    /// 此前这里只是把「重启」写进状态栏的占位。
    /// </summary>
    private void OnMenuRestart(object? sender, RoutedEventArgs e) {
        try {
            // ProcessPath 比 Assembly.Location 更可靠(单文件发布/裁剪场景同样有效)
            var executable = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(executable)) {
                Process.Start(new ProcessStartInfo { FileName = executable, UseShellExecute = true });
            }
            Environment.Exit(0);
        } catch (Exception ex) {
            KindleMate2.Shared.Diagnostics.AppLog.Write(ex);
        }
    }

    private void OnMenuExit(object? sender, RoutedEventArgs e) => Close();

    // —— 剪贴板 ——

    private async void OnCopyDetail(object? sender, RoutedEventArgs e) => await CopyDetailAsync();

    private async void OnContextCopy(object? sender, RoutedEventArgs e) => await CopyDetailAsync();

    private async Task CopyDetailAsync() {
        if (Vm is not { } vm) return;
        var text = vm.BuildCopyText();
        if (string.IsNullOrWhiteSpace(text)) {
            vm.StatusText = Strings.Ui_Status_NothingToCopy;
            return;
        }
        IClipboard? clipboard = GetTopLevel(this)?.Clipboard;
        if (clipboard is null) {
            vm.StatusText = Strings.Ui_Status_ClipboardUnavailable;
            return;
        }
        await clipboard.SetTextAsync(text);
        vm.StatusText = Strings.Ui_Status_Copied;
    }

    // —— 列表 / 详情动作 ——

    /// <summary>点击左栏「回收站」→ 把已删除(可恢复)的条目载入主列表(仅显示,不改数据)。</summary>
    private async void OnOpenRecycleBin(object? sender, TappedEventArgs e) {
        if (Vm is not { } vm) return;
        await vm.LoadRecycleBinAsync();
    }

    /// <summary>恢复回收站中选中的那一条(原版无此功能)。</summary>
    private async void OnRestoreFromRecycleBin(object? sender, RoutedEventArgs e) {
        if (Vm is not { } vm) return;
        await ShowResultAsync(await vm.RestoreSelectedFromRecycleBinAsync());
        // 恢复后刷新回收站视图,让该条从列表消失
        await vm.LoadRecycleBinAsync();
    }

    /// <summary>清空回收站 —— 彻底删除其中全部条目。确认框与"清空数据"同理,属不可逆操作。</summary>
    private async void OnPurgeRecycleBin(object? sender, RoutedEventArgs e) {
        if (Vm is not { } vm) return;
        var ok = await AppDialog.ConfirmAsync(this, Strings.Confirm,
            Strings.Ui_Menu_PurgeRecycleBin, Strings.Ui_Action_Ok, danger: true);
        if (!ok) return;
        await ShowResultAsync(await vm.PurgeRecycleBinAsync());
        await vm.LoadRecycleBinAsync();
    }

    private async void OnDeleteSelected(object? sender, RoutedEventArgs e) {
        if (Vm is not { } vm) return;
        if (!vm.HasSelectedItem) {
            vm.StatusText = Strings.Ui_Status_NoSelection;
            return;
        }
        // 原版对「删除选中的查询」另有一条文案(DeleteLookupRows → Confirm_Delete_Lookups),
        // 与删除标注区分开;这里按选中项类型选择,保持与原版文案一致。
        var confirmText = vm.IsLookupSelected
            ? Strings.Confirm_Delete_Lookups
            : Strings.Confirm_Delete_Selected_Clippings;
        var ok = await AppDialog.ConfirmAsync(this, Strings.Confirm,
            confirmText, Strings.Ui_Action_Ok, danger: true);
        if (!ok) return;
        await ShowResultAsync(await vm.DeleteSelectedAsync());
    }

    /// <summary>
    /// 重命名书籍 —— 严格对齐原版 <c>ShowBookRenameDialog</c>(FrmMain.cs:1160-1206):
    /// ① 双字段「书名 + 作者」,两者都必填(由对话框禁用「确定」实现原版的 e.Cancel 校验);
    /// ② 两个值都未变 → 提示 Books_Title_Not_Changed;
    /// ③ 目标书名已被**别的**书占用 → 确认「同名合并」,确认后改用**旧书的作者**(原版行为,保证合并后作者一致);
    ///    「只改作者、书名不动」不算撞名 —— 那还是同一本书,谈不上与谁合并(见 <see cref="BookRenameRules"/>);
    /// ④ 改名同时落到生词本与标注两处(在 VM 内完成)。
    /// </summary>
    private async void OnRenameCurrent(object? sender, RoutedEventArgs e) {
        if (Vm is not { } vm) return;
        if (!vm.CanRenameCurrentBook) {
            vm.StatusText = Strings.Ui_Status_PickBookFirst;
            return;
        }

        var oldName = vm.CurrentBookName;
        var oldAuthor = vm.CurrentBookAuthor;

        var input = await AppDialog.PromptTwoFieldsAsync(this, Strings.Rename,
            Strings.Book_Title, oldName, Strings.Author, oldAuthor);
        if (input is not { } fields) return;

        var newName = fields.First.Trim();
        var newAuthor = fields.Second.Trim();

        if (newName.Length == 0) return;
        if (oldAuthor.Length > 0 && newAuthor.Length == 0) newAuthor = oldAuthor;

        // 判定走 BookRenameRules(可测);撞名检查必须用 exceptBookName 排除**当前这本书自己**,
        // 否则「只改作者、书名不动」会拿自己撞自己,凭空弹「同名书籍已存在」。
        var action = BookRenameRules.Decide(oldName, oldAuthor, newName, newAuthor,
            vm.IsBookNameTaken(newName, exceptBookName: oldName));

        if (action == BookRenameAction.Unchanged) {
            await AppDialog.AlertAsync(this, Strings.Prompt, Strings.Books_Title_Not_Changed);
            return;
        }

        if (action == BookRenameAction.ConfirmCombine) {
            var combine = await AppDialog.ConfirmAsync(this, Strings.Confirm,
                Strings.Confirm_Same_Title_Combine, Strings.Ui_Action_Ok);
            if (!combine) return;
            newAuthor = vm.GetBookAuthor(oldName);
        }

        await ShowResultAsync(await vm.RenameCurrentBookAsync(newName, newAuthor));
    }

    private async void OnExportCurrent(object? sender, RoutedEventArgs e) {
        if (Vm is { } vm) await ShowResultAsync(await vm.ExportCurrentBookMarkdownAsync());
    }

    private async void OnRefreshCurrent(object? sender, RoutedEventArgs e) {
        if (Vm is { } vm && vm.HasSession) await vm.ReloadAsync();
    }

    private void OnLanguageHans(object? sender, RoutedEventArgs e) => SetLanguage("zh-hans", Strings.Ui_Lang_Hans);
    private void OnLanguageHant(object? sender, RoutedEventArgs e) => SetLanguage("zh-hant", Strings.Ui_Lang_Hant);
    private void OnLanguageEn(object? sender, RoutedEventArgs e) => SetLanguage("en", Strings.Ui_Lang_En);
    private void OnLanguageAuto(object? sender, RoutedEventArgs e) => SetLanguage("auto", Strings.Ui_Lang_Auto);

    /// <summary>
    /// 语言切换:写入设置并应用文化,然后重建主窗口。
    /// 界面文案通过 <c>{x:Static}</c> 在 XAML 加载时求值,重建窗口是让其重新求值的最直接路径。
    /// </summary>
    private void SetLanguage(string language, string display) {
        if (Vm is not { } vm) return;
        vm.PersistLanguage(language);
        var message = string.Format(CultureInfo.CurrentCulture, Strings.Ui_Result_LanguageSet, display);
        if (!TryRebuildWindow()) {
            vm.StatusText = message;
            return;
        }
        if (Vm is { } fresh) fresh.StatusText = message;
    }

    /// <summary>重建主窗口,使 XAML 中的静态文案按新文化重新求值。</summary>
    private bool TryRebuildWindow() {
        try {
            if (global::Avalonia.Application.Current is not { } app) return false;
            if (app.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop) return false;
            var settings = app is App current ? current.Settings : null;
            var replacement = new MainWindow {
                DataContext = new MainWindowViewModel { Settings = settings }
            };
            var previous = desktop.MainWindow;
            desktop.MainWindow = replacement;
            replacement.Show();
            previous?.Close();
            // 旧窗口只 Close 不会释放会话:DatabaseSession 持有 DeviceManager 等资源,
            // 不显式释放则每切一次语言都泄漏一份。Stop 由 OnClosed 负责。
            (previous?.DataContext as MainWindowViewModel)?.ReleaseSession();
            return true;
        } catch (Exception ex) {
            KindleMate2.Shared.Diagnostics.AppLog.Write(ex);
            return false;
        }
    }

    /// <summary>
    /// 「帮助 → 检查更新」。有更新时同时点亮主界面状态栏的「更新」按钮;
    /// 无更新或检查失败都只弹一句"已是最新" —— 检查更新的失败不该打断使用。
    /// </summary>
    private async void OnMenuCheckUpdates(object? sender, RoutedEventArgs e) {
        if (Vm is not { } vm) return;
        var message = await vm.CheckForUpdatesAsync();
        await AppDialog.AlertAsync(this, Strings.Successful, message);
    }

    /// <summary>
    /// 状态栏「更新」按钮:下载 → 交给替换脚本 → **退出本进程**。
    /// 退出是必须的:脚本正 `kill -0` 等我们死掉,之后它才会替换文件并重新启动应用。
    /// </summary>
    private async void OnUpdateClick(object? sender, RoutedEventArgs e) {
        if (Vm is not { } vm) return;

        var (restart, message) = await vm.DownloadAndApplyUpdateAsync();
        if (restart) {
            Environment.Exit(0);
        }

        await AppDialog.AlertAsync(this, Strings.Failed, message);
    }

    private async void OnMenuAbout(object? sender, RoutedEventArgs e) {
        if (Vm is not { } vm) return;
        var about = await Task.Run(() => AboutViewModel.Load(vm.Session));
        await new AboutWindow(about).ShowDialog(this);
    }

    /// <summary>
    /// 从设备导入(设备 → 库)。原版此操作**没有确认框** —— 点了就直接取文件并导入,
    /// 故这里也不加确认;设备未连接时由 VM 返回「设备未连接」而不是静默失败。
    /// </summary>
    private async void OnMenuImportFromDevice(object? sender, RoutedEventArgs e) {
        if (Vm is not { } vm) return;
        await ShowResultAsync(await vm.ImportFromDeviceAsync());
    }

    /// <summary>
    /// 菜单栏「Kindle设备已连接」按钮 —— 对齐原版 <c>menuKindle</c>:
    /// 设备连接时才出现,点击即执行「从设备导入」(与原版同样**不弹确认框**)。
    /// 这也是原版对该功能**唯一对用户可见**的入口。
    /// </summary>
    private async void OnMenuKindleConnected(object? sender, RoutedEventArgs e) {
        if (Vm is not { } vm) return;
        await ShowResultAsync(await vm.ImportFromDeviceAsync());
    }

    private void OnMenuGithub(object? sender, RoutedEventArgs e) {
        try {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo {
                FileName = AppConstants.RepoUrl,
                UseShellExecute = true
            });
        } catch (Exception ex) {
            if (Vm is { } vm) vm.StatusText = ex.Message;
        }
    }
}
