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

        // 只有**窗口路径**才把库层日志落到程序目录 error.log。
        // 自检是控制台程序:它们的日志(含故意打开坏库的堆栈)应当打在 stderr 上,
        // 而不是灌进 error.log —— 否则这份"查线上问题用"的日志会被自检噪音淹没。
        FileLogSink.Initialize();

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
            // 从本地化键取搜索类型,而不是硬编码中文 —— 否则自检只在中文 locale 下有效
            vm.SearchType = KindleMate2.Shared.Strings.Ui_Search_Type_Content;
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

            // 应用级设置往返(主题 / 语言)
            var settingsDir = Path.Combine(Path.GetTempPath(), "km2settings");
            var settings = AppSettings.Load(settingsDir);
            settings.Theme = "light";
            settings.Language = "zh-hant";
            settings.Save();
            var reloaded = AppSettings.Load(settingsDir);
            report.AppendLine($"settings: theme={reloaded.Theme} lang={reloaded.Language} path={reloaded.FilePath}");

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
                var (startupFatal, startupOk, startupError) = startupVm.PrepareDatabaseAsync().GetAwaiter().GetResult();
                var newDbPath = Path.Combine(freshDir, AppConstants.DatabaseFileName);
                report.AppendLine($"startup: fatal={startupFatal} ok={startupOk} err='{startupError}' created={File.Exists(newDbPath)} hasSession={startupVm.HasSession}");
                report.AppendLine($"  newDbSize={new FileInfo(newDbPath).Length}B  migrationWarning='{startupVm.MigrationWarning}'");
            } finally {
                Environment.CurrentDirectory = originalCwd;
                try { Directory.Delete(freshDir, true); } catch { /* 清理失败不影响结论 */ }
            }

            // —— 回归:坏库不得抛异常,且不得留下坏会话 ——
            // 此前的缺陷链:OpenDatabaseAsync 在装载成功前就赋值 _session(于是 HasSession 说谎)
            // + ReloadAsync 只有 try/finally 没有 catch + 调用方是 async void
            // → 打开一个 schema 不匹配的库后点「刷新」会直接崩进程。
            // 注意:原版对库损坏没有预校验,就是把异常交给 RefreshData 的 catch 弹错误框 ——
            // 因此这里断言的是"不抛异常 + 不留坏会话",而不是某个友好文案。
            var badPath = Path.Combine(Path.GetTempPath(), "km2-bad-" + Guid.NewGuid().ToString("N") + ".dat");
            File.WriteAllBytes(badPath, new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07 });
            try {
                // 场景 A:已打开有效库时再打开坏库 —— 坏库必须清干净,旧会话不得残留
                vm.OpenDatabaseAsync(badPath).GetAwaiter().GetResult();
                report.AppendLine($"bad db (with valid open): hasSession={vm.HasSession} (expect False)");
                vm.ReloadAsync().GetAwaiter().GetResult();
                report.AppendLine("  reload -> no throw (OK)");

                // 场景 B:全新实例直接打开坏库
                var fresh = new MainWindowViewModel();
                fresh.OpenDatabaseAsync(badPath).GetAwaiter().GetResult();
                report.AppendLine($"bad db (fresh): hasSession={fresh.HasSession} status={fresh.StatusText}");
                fresh.ReloadAsync().GetAwaiter().GetResult();
                report.AppendLine("  reload -> no throw (OK)");

                // 场景 C:空文件(合法 SQLite 但缺 schema,等价于旧版 KM2.db 那类文件)
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
            // 在原当前目录下先把源库解析成绝对路径 —— 下面会把 cwd 切到临时目录,
            // 末尾的「导入 Kindle Mate 2 数据库」回归还要用它当源。
            var sourceDbFull = Path.GetFullPath(dbPath);
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
            // 5. 重命名第一本书(契约:成功标题 Successful、正文 Books_Renamed)
            // 5. 重命名书籍(契约:成功标题 Successful、正文 Books_Renamed)
            //    刻意选一本**在生词本里有记录**的书,好验证改名是否同步到生词本 ——
            //    原版同时调 LookupService 与 ClippingService;此前只调了后者,是个真 bug。
            var lookupRepo = vm.Session!.LookupRepository;
            var booksWithLookups = lookupRepo.GetAll()
                .Where(l => !string.IsNullOrWhiteSpace(l.Title))
                .Select(l => l.Title!)
                .ToHashSet(StringComparer.Ordinal);
            var navIndex = 1;
            for (var i = 1; i < vm.NavItems.Count; i++) {
                if (booksWithLookups.Contains(vm.NavItems[i].Name)) {
                    navIndex = i;
                    break;
                }
            }
            vm.SelectedNav = vm.NavItems[navIndex];
            var oldName = vm.CurrentBookName;
            var oldAuthor = vm.CurrentBookAuthor;
            var lookupsOldBefore = lookupRepo.GetAll()
                .Count(l => string.Equals(l.Title, oldName, StringComparison.Ordinal));

            var renameResult = vm.RenameCurrentBookAsync(oldName + "_renamed", oldAuthor).GetAwaiter().GetResult();
            report.AppendLine($"rename: ok={renameResult.Ok} title={renameResult.Title} msg={renameResult.Message}");
            report.AppendLine($"  -> nav 含新名={vm.NavItems.Any(n => n.Name == oldName + "_renamed")}");

            var allLookups = lookupRepo.GetAll();
            var lookupsNewAfter = allLookups.Count(l => string.Equals(l.Title, oldName + "_renamed", StringComparison.Ordinal));
            var lookupsStillOld = allLookups.Count(l => string.Equals(l.Title, oldName, StringComparison.Ordinal));
            report.AppendLine($"  -> 生词本同步改名: 改名前该书 {lookupsOldBefore} 条 -> 改名后 新名 {lookupsNewAfter} 条 / 仍为旧名 {lookupsStillOld} 条");

            // 5b. 编辑标注正文(对齐原版 ShowContentEditDialog:同时写 clippings 与 original_clipping_lines.line4)
            vm.SelectedNav = vm.NavItems[0];
            if (vm.Items.Count > 0) {
                vm.SelectedItem = vm.Items[0];
                var editKey = vm.SelectedClippingKey;
                const string edited = "【编辑自检】改写后的正文";
                var editResult = vm.SaveClippingContentAsync(editKey, edited).GetAwaiter().GetResult();
                report.AppendLine($"edit clipping: ok={editResult.Ok} title={editResult.Title} msg={editResult.Message}");
                var reread = vm.ClipTable.FirstOrDefault(c => c.Key == editKey);
                var origLine = vm.Session?.OriginalClippingLineService.GetOriginalClippingLineByKey(editKey);
                report.AppendLine($"  -> clippings.content 已更新={reread?.Content == edited} / original.line4 已更新={origLine?.Line4 == edited}");
            }

            // 6. 删除一条标注(契约:原版成功不弹窗 → 成功时 Silent)
            var beforeDelete = vm.ClipTable.Count;
            var deleteResult = vm.DeleteSelectedAsync().GetAwaiter().GetResult();
            report.AppendLine($"delete: ok={deleteResult.Ok} kind={deleteResult.Kind} (clips {beforeDelete} -> {vm.ClipTable.Count})");

            // 7. 清理 / 重建(契约:成功标题分别为 Clean_Database / Rebuild_Database)
            var cleanResult = vm.CleanDatabaseAsync().GetAwaiter().GetResult();
            report.AppendLine($"clean: ok={cleanResult.Ok} title={cleanResult.Title} msg={cleanResult.Message}");

            // 从设备导入:本机无 Kindle,应走「设备未连接」前置守卫 —— 返回失败而不是崩溃/静默。
            // (真实取文件与导入需要接上设备,自检覆盖不到。)
            var deviceImport = vm.ImportFromDeviceAsync().GetAwaiter().GetResult();
            report.AppendLine($"import from device: ok={deviceImport.Ok} title={deviceImport.Title} msg={deviceImport.Message}");

            // 设备状态:无设备时文案与「菜单栏按钮显隐」标志应同时为未连接(二者出自同一次探测)
            vm.RefreshDeviceStatus();
            report.AppendLine($"device status: text='{vm.DeviceStatus}' connected={vm.IsDeviceConnected}");

            // 清理的进度上报核对(同步 sink 捕获阶段序列)
            var cleanStages = new List<KindleMate2.Application.Models.OperationProgress>();
            vm.Session!.Km2DatabaseService.CleanDatabase(vm.Session.DatabasePath, out _, new SyncProgress(cleanStages.Add));
            report.AppendLine($"  clean progress stages: {string.Join(" > ", cleanStages.Select(p => p.Stage).Distinct())}");
            var cleanMax = cleanStages.LastOrDefault(p => p.Total > 0);
            report.AppendLine($"  清理末次带数量的上报: {cleanMax.Stage} {cleanMax.Current}/{cleanMax.Total}");

            // 回归:CleanDatabase 传入空路径不得抛异常。
            // 导入 Kindle Mate / KMate 数据库的收尾清理正是传 string.Empty,
            // 此前 new FileInfo("") 抛 "The path is empty",导致整条导入在最后一步失败。
            vm.Session!.Km2DatabaseService.CleanDatabase(string.Empty, out var emptyPathResult);
            report.AppendLine($"clean(empty path): 未抛异常 OK, empty={emptyPathResult.GetValueOrDefault(AppConstants.EmptyCount)} dup={emptyPathResult.GetValueOrDefault(AppConstants.DuplicatedCount)}");
            var rebuildResult = vm.RebuildDatabaseAsync().GetAwaiter().GetResult();
            report.AppendLine($"rebuild: ok={rebuildResult.Ok} title={rebuildResult.Title} msg={rebuildResult.Message}");

            // 8. 清空(契约:成功正文 Data_Cleared;含自动备份)
            var clearResult = vm.ClearAllDataAsync().GetAwaiter().GetResult();
            report.AppendLine($"clear: ok={clearResult.Ok} title={clearResult.Title} msg={clearResult.Message} -> clips={vm.ClipTable.Count}");

            // 不变量:整批替换(只发一次 Reset)必须能被 ItemsControl 感知。
            // 这是"大列表整批替换"方案的前提 —— 一旦此断言失败,说明该优化不可用,
            // 界面会出现"集合有数据但列表空白"。
            try {
                var probeBag = new KindleMate2.Avalonia.Collections.BulkObservableCollection<int>();
                var probeList = new global::Avalonia.Controls.ListBox { ItemsSource = probeBag };
                probeBag.ReplaceAll(Enumerable.Range(1, 5));
                var afterFirst = probeList.ItemCount;
                probeBag.ReplaceAll(Enumerable.Range(1, 3));
                var afterSecond = probeList.ItemCount;
                var ok = afterFirst == 5 && afterSecond == 3;
                report.AppendLine($"bulk-collection probe: ItemCount {afterFirst}/{afterSecond}(期望 5/3) -> {(ok ? "OK" : "失败!Reset 未被控件感知")}");
            } catch (Exception ex) {
                report.AppendLine($"bulk-collection probe: 抛异常 {ex.GetType().Name}: {ex.Message}");
            }

            // 回归:批量插入的"逐条降级"兜底 —— 批次里故意放两条同 key 的行,整批必然失败;
            // 应降级为逐条插入:成功 1 条、跳过 1 条(而不是整批回滚、一条都没有)。
            try {
                const string stamp = "2099-01-01 00:00:00";
                var dupKey = stamp + "|位置 #9999-9999";
                var repo = vm.Session!.ClippingRepository;
                var batch = new List<KindleMate2.Domain.Entities.KM2DB.Clipping> {
                    new() { Key = dupKey, Content = "自检-A", BookName = "自检书", AuthorName = "自检", ClippingDate = stamp, PageNumber = 9999 },
                    new() { Key = dupKey, Content = "自检-B", BookName = "自检书", AuthorName = "自检", ClippingDate = stamp, PageNumber = 9999 },
                };
                var inserted = repo.Add(batch);
                var inDb = repo.GetAll().Count(c => c.Key == dupKey);
                report.AppendLine($"batch fallback: Add 返回={inserted}(期望 1),库内该 key={inDb} 条(期望 1)");
            } catch (Exception ex) {
                report.AppendLine($"batch fallback: 抛异常 {ex.GetType().Name}: {ex.Message}");
            }

            // 日志出口自检:Shared 的 AppLog 写出的内容应能落到(当时的)程序目录下的 error.log。
            // 库层那 18 处 AppLog.Write 走的是同一个出口,故这里验证通过即代表库层日志可被收走。
            // 注意:自检路径**故意不自动**初始化文件 sink(否则自检噪音会灌进应用的 error.log),
            // 所以这里显式初始化一次再验证。
            FileLogSink.Initialize();
            var logPath = Path.Combine(work, FileLogSink.FileName);
            if (File.Exists(logPath)) File.Delete(logPath);
            KindleMate2.Shared.Diagnostics.AppLog.Write("自检:日志出口探针");
            var logOk = File.Exists(logPath) && File.ReadAllText(logPath).Contains("日志出口探针", StringComparison.Ordinal);
            report.AppendLine($"log sink: {FileLogSink.FileName} 已写入={logOk}");

            report.AppendLine($"backups={Directory.GetFiles(Path.Combine(work, "Backups")).Length}");

            // 回归:完整走一遍「导入旧版 Kindle Mate 数据库」。
            // 该路径收尾会调 CleanDatabase(string.Empty),此前必然抛 "The path is empty"
            // 导致数据虽已导入、整个操作却报失败。
            // 源库由可选的第 6 个命令行参数指定(仓库已不再自带旧格式样本);
            // 未提供则回退到 <原当前目录>/KM2.db,再没有就跳过。
            var cmdArgs = Environment.GetCommandLineArgs();
            var legacyKmDb = cmdArgs.Length > 6 ? cmdArgs[6] : Path.Combine(originalCwd, "KM2.db");
            if (File.Exists(legacyKmDb)) {
                var legacyResult = vm.ImportKmDatabaseAsync(legacyKmDb).GetAwaiter().GetResult();
                report.AppendLine($"import legacy KM db: ok={legacyResult.Ok} title={legacyResult.Title} msg={legacyResult.Message}");
                report.AppendLine("  注:此步只为证明收尾清理不再抛 'The path is empty'。传入的旧库属另一套" +
                                  "关系型 schema(books + clippings.book_id),任何现有导入器都不认,故 ok 本就为 False。");
            } else {
                report.AppendLine($"import legacy KM db: 跳过(未提供旧格式样本;可选第 6 参数)");
            }

            // 回归:同一份 My Clippings 里出现**重复 key** 时,整批导入不得失败。
            // key = 「日期|位置」,两条内容不同但日期与位置相同的条目会算出同一个 key。
            // 判重集合若不在批内同步更新,两条都会进批次,插入时撞 UNIQUE(clippings.key)。
            // 放在最后并先清空,避免影响前面各步的统计。
            vm.ClearAllDataAsync().GetAwaiter().GetResult();
            if (File.Exists(clippingsPath)) {
                var raw = File.ReadAllLines(clippingsPath);
                var firstSep = Array.IndexOf(raw, "==========");
                if (firstSep > 0) {
                    var oneEntry = raw.Take(firstSep + 1).ToArray();
                    var dupSample = Path.Combine(work, "dup_key_clippings.txt");
                    File.WriteAllLines(dupSample, oneEntry.Concat(oneEntry).ToArray());

                    var dupVm = new MainWindowViewModel();
                    dupVm.OpenDatabaseAsync(Path.Combine(work, AppConstants.DatabaseFileName)).GetAwaiter().GetResult();
                    var dupResult = dupVm.ImportKindleClippingsAsync(dupSample).GetAwaiter().GetResult();
                    report.AppendLine($"duplicate-key import: ok={dupResult.Ok} title={dupResult.Title} msg={dupResult.Message}");
                    report.AppendLine($"  -> clips={dupVm.ClipTable.Count}(期望 1:重复 key 应被跳过而不是让整批失败)");
                }
            }

            // 9. 空库重新导入 —— 验证导入确实写入(前面因判重导入 0 条),同时测量真实批量导入耗时
            if (File.Exists(clippingsPath)) {
                // 进度上报核对:用同步 sink 捕获阶段序列,确认应用层确实在按阶段上报(含条数)
                var stages = new List<KindleMate2.Application.Models.OperationProgress>();
                var swImport = System.Diagnostics.Stopwatch.StartNew();
                vm.ImportKindleClippingsAsync(clippingsPath).GetAwaiter().GetResult();
                swImport.Stop();
                vm.Session!.ImportManager.ImportKindleClippings(clippingsPath, new SyncProgress(stages.Add));
                report.AppendLine($"reimport into empty: {swImport.ElapsedMilliseconds} ms -> clips={vm.ClipTable.Count}");
                report.AppendLine($"  progress stages: {string.Join(" > ", stages.Select(p => p.Stage).Distinct())}");
                var withCounts = stages.LastOrDefault(p => p.Total > 0);
                report.AppendLine($"  末次带数量的上报: {withCounts.Stage} {withCounts.Current}/{withCounts.Total}");
            }
            if (File.Exists(vocabDbPath)) {
                var swVocab = System.Diagnostics.Stopwatch.StartNew();
                vm.ImportKindleWordsAsync(vocabDbPath).GetAwaiter().GetResult();
                swVocab.Stop();
                vm.DomainIndex = 1;
                report.AppendLine($"reimport vocab: {swVocab.ElapsedMilliseconds} ms -> lookups={vm.LookupTable.Count}");
            }

            // 回归:删除左栏节点 —— 对齐原版 DeleteBookNodes() / DeleteWordNodes()
            // (整本删除 / 整词删除;成功静默,只有失败才弹 Delete_Failed)
            vm.DomainIndex = 0;
            if (vm.NavItems.Count > 1) {
                vm.SelectedNav = vm.NavItems[1];
                var bookToDrop = vm.SelectedNav.Key;
                var bookDropResult = vm.DeleteCurrentNavNodeAsync().GetAwaiter().GetResult();
                var remain = vm.Session!.ClippingService.GetClippingsByBookName(bookToDrop).Count;
                report.AppendLine($"delete book node: ok={bookDropResult.Ok} kind={bookDropResult.Kind} '{bookToDrop}' 剩余={remain} 条(期望 0)");
            }
            vm.DomainIndex = 1;
            if (vm.NavItems.Count > 1) {
                vm.SelectedNav = vm.NavItems[1];
                var wordToDrop = vm.SelectedNav.Key;
                var wordKeyToDrop = vm.Session!.VocabService.GetAllVocabs()
                    .FirstOrDefault(v => string.Equals(v.Word, wordToDrop, StringComparison.Ordinal))?.WordKey;
                var lookupsBefore = wordKeyToDrop == null ? 0
                    : vm.Session.LookupService.GetAllLookups().Count(l => l.WordKey == wordKeyToDrop);
                var wordDropResult = vm.DeleteCurrentNavNodeAsync().GetAwaiter().GetResult();
                var lookupsAfter = wordKeyToDrop == null ? 0
                    : vm.Session.LookupService.GetAllLookups().Count(l => l.WordKey == wordKeyToDrop);
                report.AppendLine($"delete word node: ok={wordDropResult.Ok} kind={wordDropResult.Kind} '{wordToDrop}' 查询 {lookupsBefore} -> {lookupsAfter} 条(期望 0)");
            // 回收站回归(2026-09-13 新功能):删除一条 → 应进入回收站 → 恢复后回到主表。
            vm.DomainIndex = 0;
            if (vm.Items.Count > 0) {
                vm.SelectedItem = vm.Items.FirstOrDefault();
                var binKey = vm.SelectedClippingKey;
                vm.DeleteSelectedAsync().GetAwaiter().GetResult();
                var inBin = vm.Session!.Km2DatabaseService.GetDeletedOriginalLines().Any(l => l.Key == binKey);
                vm.LoadRecycleBinAsync().GetAwaiter().GetResult();
                var binShows = vm.ClipTable.Any(c => c.Key == binKey);
                vm.SelectedItem = vm.Items.FirstOrDefault(i => i.Clipping?.Key == binKey);
                var restored = vm.RestoreSelectedFromRecycleBinAsync().GetAwaiter().GetResult();
                var backInList = vm.Session.ClippingService.GetClippingByKey(binKey) != null;
                report.AppendLine($"recycle bin: 删除后进回收站={inBin} / 回收站可见={binShows} / 恢复 ok={restored.Ok} / 回主表={backInList}");
            }

            }
            vm.DomainIndex = 0;

            // 回归:新增入口「导入 Kindle Mate 2 数据库」(本程序自己的库格式)。
            // 用命令行传入的**原始库**当 KM2 源(它与工作副本同格式、且全程未被改动),
            // 先清空工作副本再整批导回来 —— 断言条数回到 baseline,证明合并分支真的走通,
            // 而不是"不抛异常就算过"。放在最后:它会把工作副本填满,不影响前面各步的统计。
            vm.ClearAllDataAsync().GetAwaiter().GetResult();
            var km2AfterClear = vm.Session!.ClippingService.GetCount();
            var km2ImportResult = vm.ImportKm2DatabaseAsync(sourceDbFull).GetAwaiter().GetResult();
            var km2After = vm.Session.ClippingService.GetCount();
            report.AppendLine($"import KM2 db: ok={km2ImportResult.Ok} title={km2ImportResult.Title} msg={km2ImportResult.Message}");
            report.AppendLine($"  -> clips 清空后 {km2AfterClear} / 导入后 {km2After}(期望 0 -> baseline {clips0})");

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

    /// <summary>同步的进度接收器:自检里用它捕获阶段序列(Progress&lt;T&gt; 是异步投递的,不适合断言)。</summary>
    private sealed class SyncProgress(Action<KindleMate2.Application.Models.OperationProgress> handler)
        : IProgress<KindleMate2.Application.Models.OperationProgress> {
        public void Report(KindleMate2.Application.Models.OperationProgress value) => handler(value);
    }
}
