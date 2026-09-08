using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
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
}
