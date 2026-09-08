using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using KindleMate2.Avalonia.ViewModels;

namespace KindleMate2.Avalonia.Views;

public partial class MainWindow : Window {
    public MainWindow() {
        InitializeComponent();
    }

    private async void OnOpened(object? sender, EventArgs e) {
        if (DataContext is MainWindowViewModel vm) {
            await vm.TryAutoOpenAsync();
        }
    }

    private void OnDomainTabChanged(object? sender, SelectionChangedEventArgs e) {
        if (DataContext is MainWindowViewModel vm && sender is TabControl tabs) {
            vm.DomainIndex = tabs.SelectedIndex;
        }
    }

    private async void OnOpenDatabaseClick(object? sender, RoutedEventArgs e) {
        if (DataContext is not MainWindowViewModel vm) return;
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

    private void OnSearchClick(object? sender, RoutedEventArgs e) {
        if (DataContext is MainWindowViewModel vm) {
            // 搜索词 / 类型已通过 TwoWay 绑定触发 ApplyFilter,此 Click 仅作为回车/按钮的明确触发。
            vm.StatusText = string.IsNullOrWhiteSpace(vm.SearchText)
                ? "请输入搜索词"
                : $"搜索: {vm.SearchText} ({vm.SearchType})";
        }
    }

    private void OnToggleTheme(object? sender, RoutedEventArgs e) {
        if (DataContext is not MainWindowViewModel vm) return;
        vm.IsDarkTheme = !vm.IsDarkTheme;
        if (Application.Current is App app) {
            app.RequestedThemeVariant = vm.IsDarkTheme ? ThemeVariant.Dark : ThemeVariant.Light;
        }
    }

    // 菜单命令(阶段 1 占位;阶段 3 接真实导入/导出/维护)
    private void Stub(string name) {
        if (DataContext is MainWindowViewModel vm) {
            vm.StatusText = $"【占位】{name}(阶段 3 接入)";
        }
    }
    private void OnMenuRefresh(object? s, RoutedEventArgs e) {
        if (DataContext is MainWindowViewModel vm && !string.IsNullOrEmpty(vm.DbPath)) {
            _ = vm.OpenDatabaseAsync(vm.DbPath);
            return;
        }
        Stub("刷新");
    }
    private void OnMenuStatistics(object? s, RoutedEventArgs e) => Stub("统计");
    private void OnMenuRestart(object? s, RoutedEventArgs e) => Stub("重启");
    private void OnMenuExit(object? s, RoutedEventArgs e) => Close();
    private void OnMenuImportClippings(object? s, RoutedEventArgs e) => Stub("导入Kindle标注");
    private void OnMenuImportVocab(object? s, RoutedEventArgs e) => Stub("导入Kindle生词本");
    private void OnMenuImportKmDatabase(object? s, RoutedEventArgs e) => Stub("导入Kindle Mate数据库");
    private void OnMenuImportKmateDatabase(object? s, RoutedEventArgs e) => Stub("导入KMate数据库");
    private void OnMenuImportFromDevice(object? s, RoutedEventArgs e) => Stub("从Kindle设备导入");
    private void OnMenuSyncToDevice(object? s, RoutedEventArgs e) => Stub("同步到Kindle设备");
    private void OnMenuExportMarkdown(object? s, RoutedEventArgs e) => Stub("导出为Markdown");
    private void OnMenuCleanDb(object? s, RoutedEventArgs e) => Stub("清理数据库");
    private void OnMenuRebuildDb(object? s, RoutedEventArgs e) => Stub("重建数据库");
    private void OnMenuBackup(object? s, RoutedEventArgs e) => Stub("备份");
    private void OnMenuDeleteAll(object? s, RoutedEventArgs e) => Stub("清空数据");
    private void OnMenuAbout(object? s, RoutedEventArgs e) => Stub("关于");
    private void OnMenuGithub(object? s, RoutedEventArgs e) => Stub("GitHub仓库");
}
