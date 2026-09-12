using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using KindleMate2.Avalonia.ViewModels;

namespace KindleMate2.Avalonia;

internal static class Program {
    [STAThread]
    public static int Main(string[] args) {
        // 无头自检:--smoke <db> [out.txt] —— 不开窗口,验证数据库读取链路 + 列表/详情成型后退出。
        if (args.Length >= 2 && args[0] == "--smoke") {
            return RunSmoke(args[1], args.Length > 2 ? args[2] : null);
        }

        // 写操作自检:--ops <db> <clippings.txt> <vocab.db> <out.txt>
        // 全程在临时副本上执行,不触碰真实库。
        if (args.Length >= 5 && args[0] == "--ops") {
            return RunOperations(args[1], args[2], args[3], args[4]);
        }

        return BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    private static int RunSmoke(string dbPath, string? outFile) {
        var report = new System.Text.StringBuilder();
        try {
            var vm = new MainWindowViewModel();
            vm.OpenDatabaseAsync(dbPath).GetAwaiter().GetResult();

            // 域 0:标注 → 左栏书籍,主列表标注,详情面板
            vm.DomainIndex = 0;
            report.AppendLine($"nav={vm.NavItems.Count} items={vm.Items.Count} table={vm.ClipTable.Count}");
            report.AppendLine($"status: {vm.StatusLeft}");
            report.AppendLine($"header: {vm.HeaderTitle} / {vm.HeaderSubtitle}");

            if (vm.NavItems.Count > 1) {
                var book = vm.NavItems[1];
                vm.SelectedNav = book;
                report.AppendLine($"select '{book.Name}' -> {vm.Items.Count} 条");
                report.AppendLine($"detail: [{vm.Detail.Kind}] {vm.Detail.Title} | {vm.Detail.Subtitle} | body={vm.Detail.Body.Length} quote={vm.Detail.HasQuote} note={vm.Detail.HasNote}");
            }

            // 笔记型标注:应同时带「划线」引用块与「笔记」正文
            vm.SelectedNav = vm.NavItems[0];
            var noteItem = vm.Items.FirstOrDefault(i => i.IsNote);
            if (noteItem != null) {
                vm.SelectedItem = noteItem;
                report.AppendLine($"note: [{vm.Detail.Kind}] title={vm.Detail.Title} quoteLabel={vm.Detail.QuoteLabel}/{vm.Detail.Quote.Length} noteLabel={vm.Detail.NoteLabel}/{vm.Detail.Note.Length} body={vm.Detail.Body.Length}");
                report.AppendLine($"copyText={vm.BuildCopyText().Length} chars");
            } else {
                report.AppendLine("note: <该库无笔记型标注>");
            }

            // 域 1:生词 → 左栏生词,主列表查询记录
            vm.DomainIndex = 1;
            report.AppendLine($"words={vm.NavItems.Count} lookups={vm.LookupTable.Count}");
            if (vm.NavItems.Count > 1) {
                vm.SelectedNav = vm.NavItems[1];
                report.AppendLine($"select word '{vm.NavItems[1].Name}' -> {vm.Items.Count} 条");
                report.AppendLine($"detail: {vm.Detail.Title} | {vm.Detail.Subtitle} | body={vm.Detail.Body.Length}");
            }
            report.AppendLine($"status(生词): {vm.StatusLeft}");

            // 搜索
            vm.DomainIndex = 0;
            vm.SearchType = "内容";
            vm.SearchText = "的";
            report.AppendLine($"search '的'(内容) -> {vm.Items.Count} 条");

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

    /// <summary>
    /// 写操作端到端自检:把真实库复制到临时目录后,在该副本上依次执行
    /// 导入 / 导出 / 备份 / 重命名 / 删除 / 清理 / 重建 / 清空,逐步核对结果。
    /// </summary>
    private static int RunOperations(string dbPath, string clippingsPath, string vocabDbPath, string outFile) {
        var report = new System.Text.StringBuilder();
        var work = Path.Combine(Path.GetTempPath(), "km2ops");
        try {
            if (Directory.Exists(work)) Directory.Delete(work, true);
            Directory.CreateDirectory(work);
            var dbCopy = Path.Combine(work, "KM2_ops.dat");
            File.Copy(dbPath, dbCopy, true);

            var vm = new MainWindowViewModel();
            vm.OpenDatabaseAsync(dbCopy).GetAwaiter().GetResult();
            vm.DomainIndex = 0;
            var clips0 = vm.ClipTable.Count;
            var books0 = vm.NavItems.Count;
            report.AppendLine($"baseline: clips={clips0} nav={books0}");

            // 1. 导入 Kindle 标注
            if (File.Exists(clippingsPath)) {
                vm.ImportKindleClippingsAsync(clippingsPath).GetAwaiter().GetResult();
                report.AppendLine($"import clippings: {vm.StatusText}");
                report.AppendLine($"  -> clips={vm.ClipTable.Count} (delta={vm.ClipTable.Count - clips0})");
            }

            // 2. 导入 Kindle 生词本
            if (File.Exists(vocabDbPath)) {
                vm.ImportKindleWordsAsync(vocabDbPath).GetAwaiter().GetResult();
                report.AppendLine($"import vocab: {vm.StatusText}");
                report.AppendLine($"  -> lookups={vm.LookupTable.Count}");
            }

            // 3. 导出 Markdown
            vm.ExportClippingsMarkdownAsync().GetAwaiter().GetResult();
            report.AppendLine($"export clippings: {vm.StatusText}");
            vm.ExportVocabsMarkdownAsync().GetAwaiter().GetResult();
            report.AppendLine($"export vocabs: {vm.StatusText}");
            var exportDir = Path.Combine(work, "Exports");
            report.AppendLine($"  -> exports={Directory.GetFiles(exportDir, "*.md").Length} 个 md 文件");

            // 4. 备份
            vm.BackupDatabaseAsync().GetAwaiter().GetResult();
            report.AppendLine($"backup: {vm.StatusText}");

            // 5. 重命名第一本书
            vm.SelectedNav = vm.NavItems[1];
            var oldName = vm.CurrentBookName;
            vm.RenameCurrentBookAsync(oldName + "_renamed").GetAwaiter().GetResult();
            report.AppendLine($"rename: {vm.StatusText}");
            report.AppendLine($"  -> nav 含新名={vm.NavItems.Any(n => n.Name == oldName + "_renamed")}");

            // 6. 删除一条标注
            var beforeDelete = vm.ClipTable.Count;
            vm.DeleteSelectedAsync().GetAwaiter().GetResult();
            report.AppendLine($"delete: {vm.StatusText} (clips {beforeDelete} -> {vm.ClipTable.Count})");

            // 7. 清理 / 重建
            vm.CleanDatabaseAsync().GetAwaiter().GetResult();
            report.AppendLine($"clean: {vm.StatusText}");
            vm.RebuildDatabaseAsync().GetAwaiter().GetResult();
            report.AppendLine($"rebuild: {vm.StatusText}");

            // 8. 清空(含自动备份)
            vm.ClearAllDataAsync().GetAwaiter().GetResult();
            report.AppendLine($"clear: {vm.StatusText} -> clips={vm.ClipTable.Count}");

            report.AppendLine($"backups={Directory.GetFiles(Path.Combine(work, "Backups")).Length}");

            // 9. 空库重新导入 —— 验证导入确实写入(前面因判重导入 0 条)
            if (File.Exists(clippingsPath)) {
                vm.ImportKindleClippingsAsync(clippingsPath).GetAwaiter().GetResult();
                report.AppendLine($"reimport into empty: {vm.StatusText} -> clips={vm.ClipTable.Count}");
            }
            if (File.Exists(vocabDbPath)) {
                vm.ImportKindleWordsAsync(vocabDbPath).GetAwaiter().GetResult();
                vm.DomainIndex = 1;
                report.AppendLine($"reimport vocab: {vm.StatusText} -> lookups={vm.LookupTable.Count}");
            }

            File.WriteAllText(outFile, report.ToString());
            return 0;
        } catch (Exception ex) {
            File.WriteAllText(outFile, report.AppendLine($"FAIL: {ex}").ToString());
            return 1;
        }
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
