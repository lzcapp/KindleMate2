using System;
using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using KindleMate2.Avalonia.ViewModels;

namespace KindleMate2.Avalonia;

internal static class Program {
    [STAThread]
    public static int Main(string[] args) {
        // 无头自检:--smoke <db> [out.txt] —— 不开窗口,验证数据库读取链路后退出。
        // 供开发/CI 验证"壳能复用现有 Infrastructure 打开 KM2.db"。
        if (args.Length >= 2 && args[0] == "--smoke") {
            return RunSmoke(args[1], args.Length > 2 ? args[2] : null);
        }

        return BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    private static int RunSmoke(string dbPath, string? outFile) {
        var report = new System.Text.StringBuilder();
        try {
            var vm = new MainWindowViewModel();
            vm.OpenDatabaseAsync(dbPath).GetAwaiter().GetResult();

            // 域 0:标注 → 左列表书籍,右表全量标注
            vm.DomainIndex = 0;
            var bookCount = vm.LeftItems.Count;
            var clipTotal = vm.Clippings.Count;
            report.AppendLine($"books={bookCount} clips={clipTotal}");
            report.AppendLine($"status: {vm.StatusText}");

            if (bookCount > 0) {
                vm.SelectedItem = vm.LeftItems[0];
                var firstBook = ((Models.BookItem)vm.LeftItems[0]).Name;
                report.AppendLine($"select book '{firstBook}' -> clips={vm.Clippings.Count}");
            }

            // 域 1:生词 → 左列表词,右表查询记录
            vm.DomainIndex = 1;
            var wordCount = vm.LeftItems.Count;
            var lookupTotal = vm.Lookups.Count;
            report.AppendLine($"words={wordCount} lookups={lookupTotal}");

            if (outFile != null) {
                File.WriteAllText(outFile, report.ToString());
            }
            return 0;
        } catch (Exception ex) {
            var msg = $"FAIL: {ex}";
            if (outFile != null) {
                File.WriteAllText(outFile, msg);
            }
            return 1;
        }
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
