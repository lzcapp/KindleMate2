using System;
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
using KindleMate2.Avalonia.ViewModels;
using KindleMate2.Infrastructure.Helpers;
using KindleMate2.Shared;
using KindleMate2.Shared.Constants;

namespace KindleMate2.Avalonia.Views;

public partial class MainWindow : Window {
    private DispatcherTimer? _deviceTimer;

    public MainWindow() {
        InitializeComponent();
    }

    private MainWindowViewModel? Vm => DataContext as MainWindowViewModel;

    private async void OnOpened(object? sender, EventArgs e) {
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
        string status;
        try {
            status = await Task.Run(vm.ProbeDeviceStatus);
        } catch {
            // 走与正常路径同一个本地化键 —— 此前硬编码中文,英文界面下会漏出中文
            status = Strings.Ui_Status_DeviceOffline;
        }
        vm.DeviceStatus = status;
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

    /// <summary>打开指定目录(对应原版 Process.Start(AppConstants.ExplorerFileName, path))。</summary>
    private void OpenInExplorer(string path) {
        try {
            if (string.IsNullOrWhiteSpace(path)) return;
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        } catch (Exception ex) {
            KindleMate2.Avalonia.Services.AppLog.Write(ex);
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

    private void OnMenuRestart(object? sender, RoutedEventArgs e) {
        if (Vm is { } vm) vm.StatusText = Strings.Restart;
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

    private async void OnDeleteSelected(object? sender, RoutedEventArgs e) {
        if (Vm is not { } vm) return;
        if (!vm.HasSelectedItem) {
            vm.StatusText = Strings.Ui_Status_NoSelection;
            return;
        }
        var ok = await AppDialog.ConfirmAsync(this, Strings.Confirm,
            Strings.Confirm_Delete_Selected_Clippings, Strings.Ui_Action_Ok, danger: true);
        if (!ok) return;
        await ShowResultAsync(await vm.DeleteSelectedAsync());
    }

    /// <summary>
    /// 重命名书籍 —— 严格对齐原版 <c>ShowBookRenameDialog</c>(FrmMain.cs:1160-1206):
    /// ① 双字段「书名 + 作者」,两者都必填(由对话框禁用「确定」实现原版的 e.Cancel 校验);
    /// ② 两个值都未变 → 提示 Books_Title_Not_Changed;
    /// ③ 目标书名已存在 → 确认「同名合并」,确认后改用**旧书的作者**(原版行为,保证合并后作者一致);
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

        if (string.Equals(newName, oldName, StringComparison.Ordinal) &&
            string.Equals(newAuthor, oldAuthor, StringComparison.Ordinal)) {
            await AppDialog.AlertAsync(this, Strings.Prompt, Strings.Books_Title_Not_Changed);
            return;
        }

        if (vm.IsBookNameTaken(newName)) {
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
            KindleMate2.Avalonia.Services.AppLog.Write(ex);
            return false;
        }
    }

    private async void OnMenuAbout(object? sender, RoutedEventArgs e) {
        if (Vm is not { } vm) return;
        var about = await Task.Run(() => AboutViewModel.Load(vm.Session));
        await new AboutWindow(about).ShowDialog(this);
    }

    private void OnMenuImportFromDevice(object? sender, RoutedEventArgs e) {
        if (Vm is { } vm) vm.StatusText = Strings.Ui_Menu_ImportFromDevice;
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
