using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using KindleMate2.Avalonia.ViewModels;
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
            await vm.TryAutoOpenAsync();
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
        var path = await PickFileAsync("选择 KindleMate2 数据库", "KindleMate2 数据库", new[] { "*.dat", "*.db" });
        if (path == null) return;
        await vm.OpenDatabaseAsync(path);
        _ = RefreshDeviceStatusAsync();
    }

    private async void OnMenuRefresh(object? sender, RoutedEventArgs e) {
        if (Vm is not { } vm) return;
        if (!vm.HasSession) {
            vm.StatusText = "尚未打开数据库";
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
            if (Vm is { } vm) vm.StatusText = $"无法解析本地路径: {file.Path}";
            return null;
        }
        return path;
    }

    // —— 导入 ——

    private async void OnMenuImportClippings(object? sender, RoutedEventArgs e) {
        if (Vm is not { } vm) return;
        var path = await PickFileAsync("选择 Kindle 标注文件(My Clippings.txt)", "Kindle 标注", new[] { "*.txt" });
        if (path != null) await vm.ImportKindleClippingsAsync(path);
    }

    private async void OnMenuImportVocab(object? sender, RoutedEventArgs e) {
        if (Vm is not { } vm) return;
        var path = await PickFileAsync("选择 Kindle 生词本(vocab.db)", "Kindle 生词本", new[] { "*.db" });
        if (path != null) await vm.ImportKindleWordsAsync(path);
    }

    private async void OnMenuImportKmDatabase(object? sender, RoutedEventArgs e) {
        if (Vm is not { } vm) return;
        var path = await PickFileAsync("选择 Kindle Mate 数据库", "Kindle Mate 数据库", new[] { "*.dat", "*.db" });
        if (path != null) await vm.ImportKmDatabaseAsync(path);
    }

    private async void OnMenuImportKmateDatabase(object? sender, RoutedEventArgs e) {
        if (Vm is not { } vm) return;
        var path = await PickFileAsync("选择 KMate 数据库", "KMate 数据库", new[] { "*.dat", "*.db" });
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
        var ok = await AppDialog.ConfirmAsync(this, "清理数据库",
            "将删除内容为空的标注记录。建议先备份,是否继续?", "清理");
        if (ok) await vm.CleanDatabaseAsync();
    }

    private async void OnMenuRebuildDb(object? sender, RoutedEventArgs e) {
        if (Vm is not { } vm) return;
        var ok = await AppDialog.ConfirmAsync(this, "重建数据库",
            "将按书籍与页码重新整理标注顺序。建议先备份,是否继续?", "重建");
        if (ok) await vm.RebuildDatabaseAsync();
    }

    private async void OnMenuDeleteAll(object? sender, RoutedEventArgs e) {
        if (Vm is not { } vm) return;
        var ok = await AppDialog.ConfirmAsync(this, "清空数据",
            "将删除库内全部标注、生词与查询记录(会自动先备份一份数据库)。此操作不可撤销,是否继续?", "清空", danger: true);
        if (ok) await vm.ClearAllDataAsync();
    }

    private async void OnMenuSyncToDevice(object? sender, RoutedEventArgs e) {
        if (Vm is not { } vm) return;
        if (vm.DeviceStatus.Contains("未连接", StringComparison.Ordinal)) {
            vm.StatusText = "未检测到 Kindle 设备";
            return;
        }
        var ok = await AppDialog.ConfirmAsync(this, "同步到 Kindle 设备",
            "将先从设备读取现有文件做备份,再把当前标注写回设备。是否继续?", "同步");
        if (ok) await vm.SyncToDeviceAsync();
    }

    private async void OnMenuStatistics(object? sender, RoutedEventArgs e) {
        if (Vm is not { } vm) return;
        var stats = await Task.Run(() => StatisticsViewModel.Load(vm.Session));
        await new StatisticsWindow(stats).ShowDialog(this);
    }

    private void OnMenuRestart(object? sender, RoutedEventArgs e) => Stub("重启");

    private void OnMenuExit(object? sender, RoutedEventArgs e) => Close();

    // —— 剪贴板 ——

    private async void OnCopyDetail(object? sender, RoutedEventArgs e) => await CopyDetailAsync();

    private async void OnContextCopy(object? sender, RoutedEventArgs e) => await CopyDetailAsync();

    private async Task CopyDetailAsync() {
        if (Vm is not { } vm) return;
        var text = vm.BuildCopyText();
        if (string.IsNullOrWhiteSpace(text)) {
            vm.StatusText = "没有可复制的内容";
            return;
        }
        IClipboard? clipboard = GetTopLevel(this)?.Clipboard;
        if (clipboard is null) {
            vm.StatusText = "剪贴板不可用";
            return;
        }
        await clipboard.SetTextAsync(text);
        vm.StatusText = "已复制到剪贴板";
    }

    // —— 列表 / 详情动作 ——

    private async void OnDeleteSelected(object? sender, RoutedEventArgs e) {
        if (Vm is not { } vm) return;
        if (!vm.HasSelectedItem) {
            vm.StatusText = "未选中可删除的记录";
            return;
        }
        var ok = await AppDialog.ConfirmAsync(this, "删除记录",
            "将永久删除当前选中的记录。此操作不可撤销,是否继续?", "删除", danger: true);
        if (ok) await vm.DeleteSelectedAsync();
    }

    private async void OnRenameCurrent(object? sender, RoutedEventArgs e) {
        if (Vm is not { } vm) return;
        if (!vm.CanRenameCurrentBook) {
            vm.StatusText = "请先在左侧选择一本书";
            return;
        }
        var name = await AppDialog.PromptAsync(this, "重命名书籍", "输入新的书名:", vm.CurrentBookName, "重命名");
        if (name == null) return;
        if (string.IsNullOrWhiteSpace(name)) {
            vm.StatusText = "书名不能为空";
            return;
        }
        if (string.Equals(name, vm.CurrentBookName, StringComparison.Ordinal)) {
            vm.StatusText = "书名未变更";
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

    // —— 占位动作(待迁移的页面) ——

    private void Stub(string name) {
        if (Vm is { } vm) {
            vm.StatusText = $"【待接入】{name}(界面已就绪,功能在后续阶段接入)";
        }
    }

    private void OnLanguageHans(object? sender, RoutedEventArgs e) => SetLanguage("zh-hans", "简体中文");
    private void OnLanguageHant(object? sender, RoutedEventArgs e) => SetLanguage("zh-hant", "繁体中文");
    private void OnLanguageEn(object? sender, RoutedEventArgs e) => SetLanguage("en", "English");
    private void OnLanguageAuto(object? sender, RoutedEventArgs e) => SetLanguage("auto", "跟随系统");

    /// <summary>
    /// 语言切换:写入设置并立即应用文化(影响日期 / 数字格式与资源查找)。
    /// 界面控件文案的抽取(改为读取 Shared.Strings 资源)仍在进行中,
    /// 因此当前切换后需重启才完全生效 —— 这里如实提示。
    /// </summary>
    private void SetLanguage(string language, string display) {
        if (Vm is not { } vm) return;
        vm.PersistLanguage(language);
        vm.StatusText = $"界面语言已设为「{display}」;文案资源抽取完成后重启即可完整生效";
    }

    private async void OnMenuAbout(object? sender, RoutedEventArgs e) {
        if (Vm is not { } vm) return;
        var about = await Task.Run(() => AboutViewModel.Load(vm.Session));
        await new AboutWindow(about).ShowDialog(this);
    }

    private void OnMenuImportFromDevice(object? sender, RoutedEventArgs e) => Stub("从 Kindle 设备导入");

    private void OnMenuGithub(object? sender, RoutedEventArgs e) {
        try {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo {
                FileName = AppConstants.RepoUrl,
                UseShellExecute = true
            });
        } catch (Exception ex) {
            Stub($"打开仓库失败:{ex.Message}");
        }
    }
}
