using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
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
            var (ok, error) = await vm.PrepareDatabaseAsync();
            if (!ok) {
                await AppDialog.AlertAsync(this, Strings.Error,
                    MessageHelper.BuildMessage(Strings.Create_Database_Failed, new Exception(error)));
                Close();   // 原版此处 Environment.Exit(0)
                return;
            }
            if (vm.MigrationWarning.Length > 0) {
                // 原版:迁移失败只警告,不阻断启动
                await AppDialog.AlertAsync(this, Strings.Error, vm.MigrationWarning);
            }
        }
        StartDevicePolling();
        _ = RefreshDeviceStatusAsync();
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
            status = "设备未连接";
        }
        vm.DeviceStatus = status;
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

    private async void OnMenuOpenDatabase(object? sender, RoutedEventArgs e) {
        if (Vm is not { } vm) return;
        var path = await PickFileAsync(Strings.Ui_Pick_Database, Strings.Ui_FileType_Database, new[] { "*.dat", "*.db" });
        if (path == null) return;
        await vm.OpenDatabaseAsync(path);
        _ = RefreshDeviceStatusAsync();
    }

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
        if (path != null) await vm.ImportKindleClippingsAsync(path);
    }

    private async void OnMenuImportVocab(object? sender, RoutedEventArgs e) {
        if (Vm is not { } vm) return;
        var path = await PickFileAsync(Strings.Ui_Pick_Words, Strings.Ui_FileType_Words, new[] { "*.db" });
        if (path != null) await vm.ImportKindleWordsAsync(path);
    }

    private async void OnMenuImportKmDatabase(object? sender, RoutedEventArgs e) {
        if (Vm is not { } vm) return;
        var path = await PickFileAsync(Strings.Ui_Pick_KmDatabase, Strings.Ui_FileType_KmDatabase, new[] { "*.dat", "*.db" });
        if (path != null) await vm.ImportKmDatabaseAsync(path);
    }

    private async void OnMenuImportKmateDatabase(object? sender, RoutedEventArgs e) {
        if (Vm is not { } vm) return;
        var path = await PickFileAsync(Strings.Ui_Pick_KmateDatabase, Strings.Ui_FileType_KmateDatabase, new[] { "*.dat", "*.db" });
        if (path != null) await vm.ImportKmateDatabaseAsync(path);
    }

    // —— 导出 ——

    private async void OnMenuExportMarkdown(object? sender, RoutedEventArgs e) {
        if (Vm is not { } vm) return;
        if (vm.IsWordDomain) await vm.ExportVocabsMarkdownAsync();
        else await vm.ExportClippingsMarkdownAsync();
    }

    // —— 维护 ——

    private async void OnMenuBackup(object? sender, RoutedEventArgs e) {
        if (Vm is { } vm) await vm.BackupDatabaseAsync();
    }

    private async void OnMenuCleanDb(object? sender, RoutedEventArgs e) {
        if (Vm is not { } vm) return;
        var ok = await AppDialog.ConfirmAsync(this, Strings.Ui_Menu_CleanDatabase,
            Strings.Ui_Dlg_CleanMessage, Strings.Ui_Dlg_CleanOk);
        if (ok) await vm.CleanDatabaseAsync();
    }

    private async void OnMenuRebuildDb(object? sender, RoutedEventArgs e) {
        if (Vm is not { } vm) return;
        var ok = await AppDialog.ConfirmAsync(this, Strings.Ui_Menu_RebuildDatabase,
            Strings.Ui_Dlg_RebuildMessage, Strings.Ui_Dlg_RebuildOk);
        if (ok) await vm.RebuildDatabaseAsync();
    }

    private async void OnMenuDeleteAll(object? sender, RoutedEventArgs e) {
        if (Vm is not { } vm) return;
        var ok = await AppDialog.ConfirmAsync(this, Strings.Ui_Menu_ClearData,
            Strings.Ui_Dlg_ClearMessage, Strings.Ui_Dlg_ClearOk, danger: true);
        if (ok) await vm.ClearAllDataAsync();
    }

    private async void OnMenuSyncToDevice(object? sender, RoutedEventArgs e) {
        if (Vm is not { } vm) return;
        if (vm.DeviceStatus.Contains(Strings.Ui_Status_DeviceOffline, StringComparison.Ordinal) ||
            vm.DeviceStatus.Length == 0) {
            vm.StatusText = Strings.Ui_Status_NoDevice;
            return;
        }
        var ok = await AppDialog.ConfirmAsync(this, Strings.Ui_Menu_SyncToDevice,
            Strings.Ui_Dlg_SyncMessage, Strings.Ui_Dlg_SyncOk);
        if (ok) await vm.SyncToDeviceAsync();
    }

    private async void OnMenuStatistics(object? sender, RoutedEventArgs e) {
        if (Vm is not { } vm) return;
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
        var ok = await AppDialog.ConfirmAsync(this, Strings.Ui_Op_DeleteClipping,
            Strings.Ui_Dlg_DeleteMessage, Strings.Ui_Dlg_DeleteOk, danger: true);
        if (ok) await vm.DeleteSelectedAsync();
    }

    private async void OnRenameCurrent(object? sender, RoutedEventArgs e) {
        if (Vm is not { } vm) return;
        if (!vm.CanRenameCurrentBook) {
            vm.StatusText = Strings.Ui_Status_PickBookFirst;
            return;
        }
        var name = await AppDialog.PromptAsync(this, Strings.Ui_Op_RenameBook,
            Strings.Ui_Dlg_RenameMessage, vm.CurrentBookName, Strings.Ui_Dlg_RenameOk);
        if (name == null) return;
        if (string.IsNullOrWhiteSpace(name)) {
            vm.StatusText = Strings.Ui_Status_BookNameEmpty;
            return;
        }
        if (string.Equals(name, vm.CurrentBookName, StringComparison.Ordinal)) {
            vm.StatusText = Strings.Ui_Status_BookNameUnchanged;
            return;
        }
        await vm.RenameCurrentBookAsync(name.Trim());
    }

    private async void OnExportCurrent(object? sender, RoutedEventArgs e) {
        if (Vm is { } vm) await vm.ExportCurrentBookMarkdownAsync();
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
            return true;
        } catch (Exception ex) {
            Console.WriteLine($"[TryRebuildWindow] {ex}");
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
