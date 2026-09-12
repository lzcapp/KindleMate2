using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using KindleMate2.Avalonia.Services;
using KindleMate2.Avalonia.ViewModels;
using KindleMate2.Shared;
using KindleMate2.Shared.Constants;

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
            report.AppendLine($"open: hasSession={vm.HasSession} status={vm.StatusText}");

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
            // 注意:空库 / 打开失败时 NavItems 为空,这里必须判空 ——
            // CI 的跨平台作业就是拿空文件跑的,越界会让自检自己崩掉而误判产品有问题。
            if (vm.NavItems.Count > 0) {
                vm.SelectedNav = vm.NavItems[0];
            }
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

            // 统计页数据
            var stats = StatisticsViewModel.Load(vm.Session);
            report.AppendLine($"stats: byDate={stats.ClippingsByDate.Count} byHour={stats.ClippingsByHour.Count} byWeekday={stats.ClippingsByWeekday.Count} hasVocabs={stats.HasVocabs} empty={stats.IsEmpty}");
            report.AppendLine($"  {stats.ClippingSummary}");
            report.AppendLine($"  {stats.VocabSummary}");

            // 关于页数据
            var about = AboutViewModel.Load(vm.Session);
            report.AppendLine($"about: {about.Product} | ver={about.Version} | db={about.DatabaseName} ({about.DatabaseSize}) | runtime={about.Runtime}");

            // 平台实现核对:Windows 应为 Devices.Windows.DeviceManager,其他平台为 NullDeviceManager。
            // 走静态工厂断言,因此不依赖"库能打开"(CI 用空文件即可验证)。
            var platformDevice = DatabaseSession.CreateDeviceManager(Path.GetDirectoryName(dbPath) ?? ".").GetType().FullName;
            report.AppendLine($"device(platform): {platformDevice}");
            report.AppendLine($"device(session): {vm.Session?.DeviceManager.GetType().FullName ?? "<无会话>"} status={vm.ProbeDeviceStatus()}");
            report.AppendLine($"framework: {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");

            // 应用级设置往返(主题 / 语言 / 上次打开的库)
            var settingsDir = Path.Combine(Path.GetTempPath(), "km2settings");
            var settings = AppSettings.Load(settingsDir);
            settings.Theme = "light";
            settings.Language = "zh-hant";
            settings.LastDatabase = @"C:\tmp\demo.dat";
            settings.Save();
            var reloaded = AppSettings.Load(settingsDir);
            report.AppendLine($"settings: theme={reloaded.Theme} lang={reloaded.Language} db={reloaded.LastDatabase} path={reloaded.FilePath}");

            // 多语言资源核对:切换 Culture 后取同一条文案,验证三套卫星资源均可用
            var original = Strings.Culture;
            foreach (var code in new[] { "zh-Hans", "zh-Hant", "en" }) {
                Strings.Culture = new System.Globalization.CultureInfo(code);
                report.AppendLine($"i18n[{code}]: menu={Strings.Ui_Menu_Statistics} | type={Strings.Ui_Type_Highlight} | summary={Strings.Ui_Status_SummaryClippings}");
            }
            Strings.Culture = original;

            // —— 回归:启动应自动建库(对齐原版 FrmMain 的数据库生命周期) ——
            // 原版:库固定为 <当前目录>/KM2.dat,不存在则 CreateDatabase 自动创建。
            // 此前 Avalonia 壳没有这一步,导致"没有现成库就完全无法开始"。
            var originalCwd = Environment.CurrentDirectory;
            var freshDir = Path.Combine(Path.GetTempPath(), "km2-startup-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(freshDir);
            try {
                Environment.CurrentDirectory = freshDir;
                var startupVm = new MainWindowViewModel();
                var (startupOk, startupError) = startupVm.PrepareDatabaseAsync().GetAwaiter().GetResult();
                var newDbPath = Path.Combine(freshDir, AppConstants.DatabaseFileName);
                report.AppendLine($"startup: ok={startupOk} err='{startupError}' created={File.Exists(newDbPath)} hasSession={startupVm.HasSession}");
                report.AppendLine($"  probe(auto-created)={DatabaseSession.Probe(newDbPath).Result}");
                report.AppendLine($"  migrationWarning='{startupVm.MigrationWarning}'");
            } finally {
                Environment.CurrentDirectory = originalCwd;
                try { Directory.Delete(freshDir, true); } catch { /* 清理失败不影响结论 */ }
            }

            // —— 回归:坏库不得抛异常 ——
            // 此前的缺陷链:OpenDatabaseAsync 在装载成功前就赋值 _session(于是 HasSession 说谎)
            // + ReloadAsync 只有 try/finally 没有 catch + 调用方是 async void
            // → 打开一个 schema 不匹配的库后点「刷新」会直接崩进程。
            var badPath = Path.Combine(Path.GetTempPath(), "km2-bad-" + Guid.NewGuid().ToString("N") + ".dat");
            File.WriteAllBytes(badPath, new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07 });
            try {
                // 场景 A:已打开有效库时选错文件 —— 应保留原库,不破坏当前状态
                var dbBefore = vm.DbPath;
                vm.OpenDatabaseAsync(badPath).GetAwaiter().GetResult();
                report.AppendLine($"bad db (with valid open): keptSession={vm.HasSession} dbUnchanged={vm.DbPath == dbBefore}");
                report.AppendLine($"  status={vm.StatusText}");
                vm.ReloadAsync().GetAwaiter().GetResult();
                report.AppendLine("  reload -> no throw (OK)");

                // 场景 B:全新实例直接打开坏库 —— 不应留下坏会话
                var fresh = new MainWindowViewModel();
                fresh.OpenDatabaseAsync(badPath).GetAwaiter().GetResult();
                report.AppendLine($"bad db (fresh): hasSession={fresh.HasSession} status={fresh.StatusText}");
                fresh.ReloadAsync().GetAwaiter().GetResult();
                report.AppendLine("  reload -> no throw (OK)");

                // 场景 C:空文件 —— 是合法 SQLite 但缺 schema(等价于旧版 KM2.db 那类文件),
                // 应走「这不像是 Kindle Mate 2 的数据库」而不是抛出 no such column: key
                var emptyPath = Path.Combine(Path.GetTempPath(), "km2-empty-" + Guid.NewGuid().ToString("N") + ".dat");
                File.WriteAllBytes(emptyPath, Array.Empty<byte>());
                try {
                    var blank = new MainWindowViewModel();
                    blank.OpenDatabaseAsync(emptyPath).GetAwaiter().GetResult();
                    report.AppendLine($"empty db (fresh): hasSession={blank.HasSession} status={blank.StatusText}");
                    blank.ReloadAsync().GetAwaiter().GetResult();
                    report.AppendLine("  reload -> no throw (OK)");
                } finally {
                    try { File.Delete(emptyPath); } catch { /* 清理失败不影响结论 */ }
                }
            } finally {
                try { File.Delete(badPath); } catch { /* 清理失败不影响结论 */ }
            }
            try {
                Directory.Delete(settingsDir, true);
            } catch {
                // 清理失败不影响自检结论
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

    /// <summary>
    /// 写操作端到端自检:把真实库复制到临时目录后,在该副本上依次执行
    /// 导入 / 导出 / 备份 / 重命名 / 删除 / 清理 / 重建 / 清空,逐步核对结果。
    /// </summary>
    private static int RunOperations(string dbPath, string clippingsPath, string vocabDbPath, string outFile) {
        var report = new System.Text.StringBuilder();
        var work = Path.Combine(Path.GetTempPath(), "km2ops");
        var originalCwd = Environment.CurrentDirectory;
        try {
            if (Directory.Exists(work)) Directory.Delete(work, true);
            Directory.CreateDirectory(work);
            // 与原版一致的固定路径模型:库文件名必须是 KM2.dat,且当前目录即"程序目录"。
            // 这样备份 / 导出 / 导入目录全部落在临时副本上 —— 既不碰真实数据,走的又是产品真实路径。
            var dbCopy = Path.Combine(work, AppConstants.DatabaseFileName);
            File.Copy(dbPath, dbCopy, true);
            Environment.CurrentDirectory = work;

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

            // 3. 导出 Markdown(原版语义:任一失败即静默无提示;成功时正文为「导出成功!需要打开文件夹吗?」)
            var exportResult = vm.ExportAllMarkdownAsync().GetAwaiter().GetResult();
            report.AppendLine($"export all: ok={exportResult.Ok} kind={exportResult.Kind} msg={exportResult.Message}");
            var exportDir = Path.Combine(work, "Exports");
            report.AppendLine($"  -> exports={Directory.GetFiles(exportDir, "*.md").Length} 个 md 文件");

            // 4. 备份(原版语义:成功弹「备份完成!需要打开文件夹吗?」;无数据弹「没有数据可备份」)
            var backupResult = vm.BackupDatabaseAsync().GetAwaiter().GetResult();
            report.AppendLine($"backup: ok={backupResult.Ok} kind={backupResult.Kind} msg={backupResult.Message}");

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

            Environment.CurrentDirectory = originalCwd;
            File.WriteAllText(outFile, report.ToString());
            return 0;
        } catch (Exception ex) {
            Environment.CurrentDirectory = originalCwd;
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
