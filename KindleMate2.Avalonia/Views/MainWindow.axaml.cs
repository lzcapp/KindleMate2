using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using KindleMate2.Avalonia.ViewModels;

namespace KindleMate2.Avalonia.Views;

public partial class MainWindow : Window {
    public MainWindow() {
        InitializeComponent();
    }

    private MainWindowViewModel? Vm => DataContext as MainWindowViewModel;

    private async void OnOpened(object? sender, EventArgs e) {
        SyncThemeFromApplication();
        if (Vm is { } vm) {
            await vm.TryAutoOpenAsync();
        }
    }

    private void SyncThemeFromApplication() {
        if (Vm is not { } vm) return;
        var variant = Application.Current?.ActualThemeVariant;
        vm.IsDarkTheme = variant != ThemeVariant.Light;
    }

    // —— 域 / 视图 / 排序 ——

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
        if (Application.Current is { } app) {
            app.RequestedThemeVariant = vm.IsDarkTheme ? ThemeVariant.Dark : ThemeVariant.Light;
        }
    }

    // —— 数据库 ——

    private async void OnMenuOpenDatabase(object? sender, RoutedEventArgs e) {
        if (Vm is not { } vm) return;
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions {
            Title = "选择 KindleMate2 数据库",
            AllowMultiple = false,
            FileTypeFilter = new[] {
                new FilePickerFileType("KindleMate2 数据库") { Patterns = new[] { "*.db" } },
                FilePickerFileTypes.All
            }
        });
        var file = files.FirstOrDefault();
        if (file is null) return;
        var path = file.TryGetLocalPath();
        if (string.IsNullOrEmpty(path)) {
            vm.StatusText = $"无法解析本地路径: {file.Path}";
            return;
        }
        await vm.OpenDatabaseAsync(path);
    }

    private async void OnMenuRefresh(object? sender, RoutedEventArgs e) {
        if (Vm is not { } vm) return;
        if (string.IsNullOrEmpty(vm.DbPath)) {
            vm.StatusText = "尚未打开数据库";
            return;
        }
        await vm.OpenDatabaseAsync(vm.DbPath);
    }

    private void OnMenuStatistics(object? sender, RoutedEventArgs e) => Stub("统计");

    private void OnMenuRestart(object? sender, RoutedEventArgs e) => Stub("重启");

    private void OnMenuExit(object? sender, RoutedEventArgs e) => Close();

    // —— 剪贴板 ——

    private async void OnCopyDetail(object? sender, RoutedEventArgs e) => await CopyDetailAsync();

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

    // —— 右键菜单动作 ——

    private async void OnRefreshCurrent(object? sender, RoutedEventArgs e) {
        if (Vm is not { } vm) return;
        if (string.IsNullOrEmpty(vm.DbPath)) {
            vm.StatusText = "尚未打开数据库";
            return;
        }
        await vm.OpenDatabaseAsync(vm.DbPath);
    }

    private async void OnContextCopy(object? sender, RoutedEventArgs e) => await CopyDetailAsync();

    private void OnDeleteSelected(object? sender, RoutedEventArgs e) => Stub("删除选中记录");
    private void OnExportCurrent(object? sender, RoutedEventArgs e) => Stub("导出当前范围");
    private void OnRenameCurrent(object? sender, RoutedEventArgs e) => Stub("重命名");

    // —— 占位动作(阶段 3 接入真实导入 / 导出 / 维护) ——

    private void Stub(string name) {
        if (Vm is { } vm) {
            vm.StatusText = $"【待接入】{name}(界面已就绪,数据操作在后续阶段接入)";
        }
    }

    private void OnLanguageStub(object? sender, RoutedEventArgs e) => Stub("切换界面语言");

    private void OnMenuImportClippings(object? sender, RoutedEventArgs e) => Stub("导入 Kindle 标注");
    private void OnMenuImportVocab(object? sender, RoutedEventArgs e) => Stub("导入 Kindle 生词本");
    private void OnMenuImportKmDatabase(object? sender, RoutedEventArgs e) => Stub("导入 Kindle Mate 数据库");
    private void OnMenuImportKmateDatabase(object? sender, RoutedEventArgs e) => Stub("导入 KMate 数据库");
    private void OnMenuImportFromDevice(object? sender, RoutedEventArgs e) => Stub("从 Kindle 设备导入");
    private void OnMenuSyncToDevice(object? sender, RoutedEventArgs e) => Stub("同步到 Kindle 设备");
    private void OnMenuExportMarkdown(object? sender, RoutedEventArgs e) => Stub("导出为 Markdown");
    private void OnMenuCleanDb(object? sender, RoutedEventArgs e) => Stub("清理数据库");
    private void OnMenuRebuildDb(object? sender, RoutedEventArgs e) => Stub("重建数据库");
    private void OnMenuBackup(object? sender, RoutedEventArgs e) => Stub("备份");
    private void OnMenuDeleteAll(object? sender, RoutedEventArgs e) => Stub("清空数据");
    private void OnMenuAbout(object? sender, RoutedEventArgs e) => Stub("关于");
    private void OnMenuGithub(object? sender, RoutedEventArgs e) => Stub("GitHub 仓库");
}
