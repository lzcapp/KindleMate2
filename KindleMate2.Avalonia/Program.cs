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

            var firstBook = vm.Books.Count > 0 ? vm.Books[0].Name : "(空)";
            report.AppendLine($"OK books={vm.Books.Count} first={firstBook}");
            report.AppendLine($"status: {vm.StatusText}");

            // 选中第一本书,验证 Clippings 加载
            if (vm.Books.Count > 0) {
                vm.SelectedBook = vm.Books[0];
                System.Threading.Thread.Sleep(500); // 等 fire-and-forget 的异步加载完成
                report.AppendLine($"clippings(firstBook)={vm.Clippings.Count} status: {vm.StatusText}");
            }

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
