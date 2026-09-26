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
        // 在线释义是联网功能:自检一律关掉。CI 不该依赖第三方词典(慢、易 flaky),
        // 也不该把词条发出去;关掉之后"选中生词"这条路径在 CI 里变成纯本地行为。
        // 与 DeviceManager 的 detectMtpDevices 同类 —— 测试确定性接缝。
        MainWindowViewModel.OnlineDefinitionAllowed = false;
        try {
            var vm = new MainWindowViewModel();
            vm.OpenDatabaseAsync(dbPath).GetAwaiter().GetResult();
            report.AppendLine($"open: hasSession={vm.HasSession} status={vm.StatusText}");
            report.AppendLine($"online definition: enabled={MainWindowViewModel.OnlineDefinitionAllowed}");

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
            // 「运行环境」那一行必须带上 **RID**:这一行的用途就是排查"装的是哪个平台的包"
            // (例如"macOS 包里有没有 Devices.MacOS.dll"),而旧写法用 Environment.OSVersion.Platform,
            // 它在 macOS 与 Linux 上**都**返回 "Unix" ⇒ 根本分不出平台。
            // 断言与**运行时的 RID** 比,而不是写死 "osx-arm64" —— 这样三个平台都成立。
            var runtimeRid = System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier;
            var runtimeOs = System.Runtime.InteropServices.RuntimeInformation.OSDescription;
            // 两半都要在:OSDescription 负责"人读得出是哪个系统",RID 负责"对应哪个发布包"。
            // 只查 RID 的话,把平台那半退回 Environment.OSVersion.Platform(显示 "Unix")照样能过 —— 那就白测了。
            var runtimeShowsOs = about.Runtime.Contains(runtimeOs, StringComparison.Ordinal);
            var runtimeShowsRid = about.Runtime.Contains(runtimeRid, StringComparison.Ordinal);
            var runtimeOk = runtimeShowsOs && runtimeShowsRid;
            // 行首用 **ASCII 探针名**、行尾用 `result=OK` —— 与其它探针同一形状。
            // CI 的 grep 模式必须能匹配**真实输出**(踩过一次:模式里写了个实际不存在的单空格,
            // 于是 CI 三个作业全红)。ASCII 前缀 + `.*result=OK` 是最不容易写错的形状。
            report.AppendLine($"about runtime: os='{runtimeOs}' rid='{runtimeRid}'" +
                              $" showsOs={runtimeShowsOs} showsRid={runtimeShowsRid}" +
                              $" -> result={(runtimeOk ? "OK" : "失败!运行环境分不出平台")}");

            // 清洗预览的**视图层数据**。VM 在 Avalonia 工程里,单测项目不引用 Avalonia ⇒ 只能在这里钉。
            // 钉的是上一版真正翻车的那一点:被清洗掉的标点必须**单独**暴露给视图(视图靠它加删除线),
            // 而且不能被正文的中间省略吃掉 —— 否则「改前 / 改后」在预览里会一模一样,预览等于白做。
            ProbeCleanPreview(report);

            // 表格里"整列同值"的列是否收起(同样够不着单测)
            ProbeTableColumns(report);

            // 列表项元信息行:书名不在 MetaTail 里(它单独占一列、负责省略)
            ProbeListItemMeta(report);

            // 生词域中栏的两段结构(查询 / 标注)
            ProbeWordSections(report);
            // 生词域右栏详情里的生词高亮(与中栏那行同源)
            ProbeDetailEmphasis(report);

            // 左栏右键菜单的域感知
            ProbeNavMenu(report);
            // 「重命名生词」两处菜单的可用性(与上一条同属"右键菜单",放在一起)
            ProbeWordRename(report);
            // 分享卡片的内容组装
            ProbeShareCard(report);
            // 列表行 / 详情面板右键菜单的可用性判定
            ProbeContextMenuAvailability(report);
            // 删除一行之后,列表不该跳回顶部(挑行规则)
            ProbeRowRestore(report);

            // 「库内有任意数据」门禁:纯生词库不应被当成空库
            ProbeDataGate(report);

            // 平台实现核对:Windows / macOS / Linux 各应为自己的 Devices.<平台>.DeviceManager,
            // 只有这三者之外的平台才落到 NullDeviceManager。
            // 走静态工厂断言,因此不依赖"库能打开"(CI 用空文件即可验证)。
            var platformDevice = DatabaseSession.CreateDeviceManager(Path.GetDirectoryName(dbPath) ?? ".").GetType().FullName;
            report.AppendLine($"device(platform): {platformDevice}");
            report.AppendLine($"device(session): {vm.Session?.DeviceManager.GetType().FullName ?? "<无会话>"} status={vm.ProbeDeviceStatus()}");
            // 更新功能:自检**不联网**(CI 不该依赖外网),只报初始状态与当前平台的安装包形态。
            // 真正的联网探测在 KindleMate2.Tests 的 UpdateProbeTests 里手动触发(它验证过线上响应与解析器契约一致)。
            report.AppendLine($"update: version={MainWindowViewModel.CurrentVersion}" +
                              $" available={vm.IsUpdateAvailable}" +
                              $" target={KindleMate2.Application.Services.UpdateInstaller.TargetForCurrentPlatform()}");
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
                report.AppendLine($"i18n[{code}]: menu={Strings.Ui_Menu_Statistics} | type={Strings.Ui_Type_Highlight} | summary={Strings.Ui_Status_SummaryClippings} | syncHint={Strings.Ui_Menu_SyncToDevice_NeedsDevice}");
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

                // —— 「同步到 Kindle 设备」菜单项:设备未连接时置灰,并说清是"没连设备"还是"没开库" ——
                // 借这一节的理由:这里能凑齐三种**确定性**状态,且 CI 只跑 --smoke(见 build.yml 的
                // Boot self-check)。绑定本身由编译期校验(x:DataType 编译期绑定),自检钉的是
                // **绑定源**的取值(菜单项 IsEnabled 绑 IsDeviceConnected,悬停提示绑 SyncToDeviceHint)。
                // 关键:设备是否已连接这一项**不能照真实探测结果断言** —— 开发机上可能真插着 Kindle
                // (实测本机就插着一台 PW6 → 探测为 True),CI runner 上必然为 False。照真实探测写,
                // 这条自检就随现场硬件变色,红了绿了都说明不了代码对不对。
                // 真实探测结果只作诊断信息,单独一行,不参与断言。
                // 状态① 库还没开:探测必然「未连接」,提示得先说「打开数据库」,不能先喊「连接设备」
                startupVm.RefreshDeviceStatus();
                report.AppendLine($"  sync menu(no session): enabled={startupVm.IsDeviceConnected}" +
                                  $" hintOpensDb={startupVm.SyncToDeviceHint == Strings.Ui_Status_OpenDatabaseFirst}");

                var (startupFatal, startupOk, startupError) = startupVm.PrepareDatabaseAsync().GetAwaiter().GetResult();
                var newDbPath = Path.Combine(freshDir, AppConstants.DatabaseFileName);
                report.AppendLine($"startup: fatal={startupFatal} ok={startupOk} err='{startupError}' created={File.Exists(newDbPath)} hasSession={startupVm.HasSession}");
                report.AppendLine($"  newDbSize={new FileInfo(newDbPath).Length}B  migrationWarning='{startupVm.MigrationWarning}'");

                // 本机真实探测一次,仅作诊断(插着设备时 enabled=True 是正确行为,不是缺陷)
                startupVm.RefreshDeviceStatus();
                report.AppendLine($"  sync menu(real probe): enabled={startupVm.IsDeviceConnected} status='{startupVm.DeviceStatus}'");
                // 状态② 有库 + 设备未连接 → 置灰,提示「先连设备」
                startupVm.IsDeviceConnected = false;
                report.AppendLine($"  sync menu(offline): enabled={startupVm.IsDeviceConnected}" +
                                  $" hintNeedsDevice={startupVm.SyncToDeviceHint == Strings.Ui_Menu_SyncToDevice_NeedsDevice}");
                // 状态③ 有库 + 设备已连接 → 可点,提示不再停在「先连设备」
                startupVm.IsDeviceConnected = true;
                report.AppendLine($"  sync menu(online): enabled={startupVm.IsDeviceConnected}" +
                                  $" hintNeedsDevice={startupVm.SyncToDeviceHint == Strings.Ui_Menu_SyncToDevice_NeedsDevice}");

                // 回收站视图必须"进得去也出得来"。IsRecycleBinView 此前只置 true、从无置 false,
                // 于是看过一次回收站之后:列表右键菜单永久停在「恢复」(「删除」再也不出现),
                // 「管理 → 清空回收站」也永久可见。
                // 复位点选在 ApplyFilter —— "主列表要被重建成常规内容"的唯一漏斗
                // (筛选 / 排序 / 换节点 / 换域 / 重载都从它走),漏掉任何一个调用方都会留下变体。
                //
                // 同时钉住另外两条:
                //   · 进入回收站要**清掉左栏选中** —— 否则"再点一次原来那本书"是无变化事件
                //     (SelectedNav 的 setter 对同一实例直接 return),用户出不来;
                //   · 标题在回收站视图下要显示「回收站」,而不是左栏那个节点名(内容跨书,那是撒谎)。
                // 断言一律与 Strings.* 比较(两侧同源),不写死中文 —— CI 是 en 环境。
                if (startupVm.HasSession) {
                    // HeaderTitle 是**计算属性** —— 直接读它永远是"对"的,漏发通知也看不出来。
                    // 所以这里数通知次数:视图只认通知,不发就等于没更新。
                    var titleNotified = 0;
                    // DeletedCount 也要数:它是**另一个独立的绑定源**(左栏「回收站 N」绑的就是它),
                    // 只通知 StatusLeft 的话状态栏会更新、左栏却永远停在初始值 0 —— 实测过的真症状。
                    var deletedNotified = 0;
                    // 进/出回收站时,右键菜单那两条可用性也必须收到通知 —— 否则菜单项会停在初始状态。
                    var menuNotified = 0;
                    startupVm.PropertyChanged += (_, e) => {
                        if (e.PropertyName == nameof(MainWindowViewModel.HeaderTitle)) titleNotified++;
                        if (e.PropertyName == nameof(MainWindowViewModel.DeletedCount)) deletedNotified++;
                        if (e.PropertyName is nameof(MainWindowViewModel.CanRenameSelectedItemBook)
                            or nameof(MainWindowViewModel.CanEditSelectedClipping)) menuNotified++;
                    };

                    var navBefore = startupVm.SelectedNav != null;
                    startupVm.LoadRecycleBinAsync().GetAwaiter().GetResult();
                    var titleOnEnter = titleNotified;
                    var enteredBin = startupVm.IsRecycleBinView;
                    var navCleared = startupVm.SelectedNav == null;
                    var binTitle = startupVm.HeaderTitle == Strings.Ui_Nav_RecycleBin;
                    startupVm.ApplyFilter();
                    var titleOnLeave = titleNotified;
                    var leftBin = startupVm.IsRecycleBinView;
                    var normalTitle = startupVm.HeaderTitle == Strings.Ui_Header_AllClippings;
                    // navBefore 是**前提**而不是结论:左栏本来就空的话,"被清成 null"证明不了任何事。
                    var binOk = navBefore && enteredBin && navCleared && binTitle
                                && titleOnEnter >= 1 && titleOnLeave > titleOnEnter
                                && !leftBin && normalTitle
                                && deletedNotified >= 1
                                && menuNotified >= 2;   // 进回收站时「重命名书籍」「编辑标注」各通知一次
                    report.AppendLine($"recycle bin view: 进入前有选中={navBefore}(期望 True,前提)" +
                                      $" 进入={enteredBin}(期望 True) 左栏已清空={navCleared}(期望 True)" +
                                      $" 标题为回收站={binTitle}(期望 True) 回常规列表后={leftBin}(期望 False)" +
                                      $" 标题为全部标注={normalTitle}(期望 True)" +
                                      $" 标题通知=[进入后:{titleOnEnter} 离开后:{titleOnLeave}](期望 ≥1 且递增)" +
                                      $" DeletedCount 通知={deletedNotified}(期望 ≥1,左栏「回收站 N」靠它)" +
                                      $" 菜单可用性通知={menuNotified}(期望 ≥2)" +
                                      $" -> result={(binOk ? "OK" : "失败!回收站视图状态不正确")}");

                    // —— 表格模式:选中哪一行,动作就必须作用在那一行 ——
                    //
                    // 两个 DataGrid 的选中项此前**从不回写** _selectedItem,而删除 / 重命名书籍 /
                    // 重命名生词 / 编辑标注 / 分享图**一律只读 _selectedItem**(列表的选中行)。
                    // 表格模式下列表是隐藏的,用户根本改不了它 ⇒ 在表格里点第 4 行、右键「删除」,
                    // 删掉的是列表里选中的那一行(重建后通常是第一行),而详情面板显示的是表格里那一行
                    // —— 看到 A、操作作用在 B。2026-09-23 无头复现后修(见 SyncSelectionFromTable)。
                    //
                    // 这里做**端到端**核对:建临时库 → 切表格模式 → 选中某一行 → **真删** → 回查库里少了谁。
                    // 只比 SelectedItem 是不够的:"同步写对了、但删除仍读旧源"照样能绿。
                    // 全程不碰 GUI —— --smoke 本来就新建了一个库(见上面的 freshDir)。
                    {
                        var t = startupVm.Session!;
                        for (var i = 0; i < 4; i++) {
                            t.ClippingRepository.Add(new KindleMate2.Domain.Entities.KM2DB.Clipping {
                                Key = $"tblk{i}",
                                Content = $"内容{i}",
                                BookName = $"书{i}",
                                AuthorName = "作者",
                                ClippingTypeLocation = "标注 位置 #1-1"
                            });
                        }
                        startupVm.DomainIndex = 0;
                        startupVm.ReloadAsync().GetAwaiter().GetResult();
                        startupVm.IsListMode = false;                             // 切到表格模式
                        var clipRow = startupVm.ClipTable[3];                     // 在表格里点第 4 行
                        var clipTarget = clipRow.Key;                             // 期望:动作作用在**这一行**
                        // 一切取值都取自"选中行自己",**不写死键名** —— 列表默认是倒序,
                        // 行号对应的键随排序变(第一版写死了 w2,当场红)。
                        startupVm.SelectedClipTable = clipRow;
                        var clipTargetActual = startupVm.SelectedItem?.Clipping?.Key ?? "<无>";
                        var clipBookActual = startupVm.SelectedItemBookName;      // 详情面板那几条动作的目标
                        startupVm.DeleteSelectedAsync().GetAwaiter().GetResult();
                        var left = t.ClippingRepository.GetAll()
                            .Select(c => c.Key)
                            .Where(k => k.StartsWith("tblk", StringComparison.Ordinal))
                            .OrderBy(k => k)
                            .ToList();

                        // 生词表格走另一条支路(SelectedLookupTable 的 setter 与标注那边不是同一个)
                        for (var i = 0; i < 4; i++) {
                            t.VocabService.AddVocab(new KindleMate2.Domain.Entities.KM2DB.Vocab {
                                Id = $"en:w{i}", WordKey = $"en:w{i}", Word = $"w{i}"
                            });
                            t.LookupService.AddLookup(new KindleMate2.Domain.Entities.KM2DB.Lookup {
                                // timestamp 必须有值:删除走的是 (word_key, timestamp) 定位,空值会被拒
                                WordKey = $"en:w{i}", Usage = $"用法{i}", Title = $"编号{i}书",
                                Timestamp = $"2020-01-0{i + 1}"
                            });
                        }
                        startupVm.DomainIndex = 1;
                        startupVm.ReloadAsync().GetAwaiter().GetResult();
                        startupVm.IsListMode = false;
                        var wordRow = startupVm.LookupTable[2];                   // 在生词表里点第 3 行
                        var wordTarget = wordRow.WordKey ?? string.Empty;
                        startupVm.SelectedLookupTable = wordRow;
                        startupVm.DeleteSelectedAsync().GetAwaiter().GetResult();
                        var wordsLeft = t.LookupService.GetAllLookups()
                            .Select(l => l.WordKey ?? string.Empty)
                            .Where(k => k.StartsWith("en:w", StringComparison.Ordinal))
                            .OrderBy(k => k)
                            .ToList();

                        // 四件事一起断言:两条支路的删除目标、详情面板的目标书名、**真的删掉了哪一行**。
                        var tableOk = clipTargetActual == clipTarget
                                      && clipBookActual == clipRow.BookName
                                      && left.Count == 3 && !left.Contains(clipTarget)
                                      && wordsLeft.Count == 3 && !wordsLeft.Contains(wordTarget);
                        report.AppendLine($"table selection: 标注表 表格选中={clipTarget}" +
                                          $" 删除实际作用在={clipTargetActual}/书名={clipBookActual}(期望 tblk3/书3)" +
                                          $" 删完剩=[{string.Join(",", left)}](期望 3 条且不含 {clipTarget})" +
                                          $" 生词表 表格选中={wordTarget} 删完剩=[{string.Join(",", wordsLeft)}](期望 3 条且不含它)" +
                                          $" -> result={(tableOk ? "OK" : "失败!表格里选中哪一行,动作就该作用在那一行")}");
                    }

                    // —— 重命名生词撞名:**静默并入**,而且数据要真改对 ——
                    //
                    // 2026-09-23 用户实测:撞名时被弹框「已存在同名生词,请先处理那一个再改名」拦下,
                    // 他要求静默解决(可以合并或删除)。改成并入后,这里做**端到端**核对:
                    // 真库、真改名、真回查 —— 只比 VM 属性证明不了"lookups 真被搬走、重号行真被丢掉"。
                    // (顺带把"MergeWordKey 那条 SQL 没有覆盖"的缺口补上。)
                    {
                        var t = startupVm.Session!;
                        // A 的键下 t1/t2,B 的键下 t2/t3 —— t2 重号(同一个词、同一时间、同一本书),
                        // 并入时该被丢掉;t1 该搬到 B 下;B 自己的 t2/t3 不动。
                        t.VocabService.AddVocab(new KindleMate2.Domain.Entities.KM2DB.Vocab {
                            Id = "en:wmA", WordKey = "en:wmA", Word = "wmA"
                        });
                        t.VocabService.AddVocab(new KindleMate2.Domain.Entities.KM2DB.Vocab {
                            Id = "en:wmB", WordKey = "en:wmB", Word = "wmB"
                        });
                        // 再放一个**没有 word_key** 的词条:它只有显示名可改,键应保持为空
                        t.VocabService.AddVocab(new KindleMate2.Domain.Entities.KM2DB.Vocab {
                            Id = "nokey", WordKey = null, Word = "wmC"
                        });
                        foreach (var (key, ts) in new[] {
                                     ("en:wmA", "2020-01-01"), ("en:wmA", "2020-01-02"),
                                     ("en:wmB", "2020-01-02"), ("en:wmB", "2020-01-03")
                                 }) {
                            t.LookupService.AddLookup(new KindleMate2.Domain.Entities.KM2DB.Lookup {
                                WordKey = key, Usage = ts, Title = "书", Timestamp = ts
                            });
                        }

                        startupVm.DomainIndex = 1;
                        startupVm.ReloadAsync().GetAwaiter().GetResult();
                        // 左栏节点数用**差值**断言,不写死 —— 前面几个探针也往库里塞过词
                        var wordNodesBefore = startupVm.NavItems.Count(n => !n.IsAll);

                        // ① wmA → wmB(撞名)
                        var merged = startupVm.RenameWordAsync("wmA", "wmB").GetAwaiter().GetResult();
                        var keysAfter = t.LookupService.GetAllLookups()
                            .Where(l => (l.WordKey ?? string.Empty).StartsWith("en:wm", StringComparison.Ordinal))
                            .Select(l => l.Timestamp ?? string.Empty)
                            .OrderBy(s => s)
                            .ToList();
                        var staleRows = t.LookupService.GetAllLookups().Count(l => l.WordKey == "en:wmA");
                        var aVocab = t.VocabService.GetAllVocabs().FirstOrDefault(v => v.Id == "en:wmA");
                        var bVocab = t.VocabService.GetAllVocabs().FirstOrDefault(v => v.Id == "en:wmB");
                        var wordNodesAfter = startupVm.NavItems.Count(n => !n.IsAll);

                        // ② wmC → wmD(空位,不该并入;无键词条的键保持为空)
                        var plain = startupVm.RenameWordAsync("wmC", "wmD").GetAwaiter().GetResult();
                        var cVocab = t.VocabService.GetAllVocabs().FirstOrDefault(v => v.Id == "nokey");

                        var mergeOk = merged.Ok && merged.Message == Strings.Word_Merged
                                      && keysAfter.SequenceEqual(new[] { "2020-01-01", "2020-01-02", "2020-01-03" })
                                      && staleRows == 0
                                      && aVocab is { Word: "wmB", WordKey: "en:wmB" }
                                      && bVocab is not null
                                      && aVocab.Frequency == 3 && bVocab.Frequency == 3   // 并入后词频要跟着重算
                                      && wordNodesAfter == wordNodesBefore - 1        // wmA 那个节点消失了
                                      && plain.Ok && plain.Message == Strings.Word_Renamed
                                      && cVocab is { Word: "wmD", WordKey: null };
                        report.AppendLine($"word merge: 并入后 lookups=[{string.Join("/", keysAfter)}](期望 01-01/02/03)" +
                                          $" 旧键残留={staleRows}(期望 0)" +
                                          $" A 词条=[{aVocab?.Word}/{aVocab?.WordKey}](期望 wmB/en:wmB)" +
                                          $" 词频=[{aVocab?.Frequency}/{bVocab?.Frequency}](期望 3/3)" +
                                          $" 左栏节点 {wordNodesBefore}→{wordNodesAfter}(期望少 1)" +
                                          $" 文案并入={merged.Message == Strings.Word_Merged}(期望 True)" +
                                          $" 改空位={plain.Message == Strings.Word_Renamed}/无键词条=[{cVocab?.Word}/{cVocab?.WordKey ?? "<null>"}](期望 wmD/null)" +
                                          $" -> result={(mergeOk ? "OK" : "失败!撞名该静默并入,且数据要真改对")}");
                    }
                } else {
                    report.AppendLine("recycle bin view: 跳过(启动自检没拿到会话)");
                }
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
    /// 「清洗标注文本」预览窗口的数据探针。
    ///
    /// 为什么必须放在这里:预览的 VM 在 Avalonia 工程里,而测试工程**不引用 Avalonia**
    /// (那是刻意的分层),所以这段拼装逻辑在单测里够不着 —— 只能靠这个无头自检钉住。
    ///
    /// 断言的三件事,正是上一版预览真正翻车的地方:
    /// <list type="number">
    /// <item>被清洗掉的标点要**单独**给出(视图靠它加删除线),不能混在正文里;</item>
    /// <item>**尾部**噪音不能被正文的中间省略吃掉(上一版只留前缀,尾部改动永远看不见);</item>
    /// <item>拼出来的「改前」与「改后」必须不同 —— 一样就等于预览没起作用。</item>
    /// </list>
    ///
    /// 断言里**不得出现本地化文案**:自检在 CI 上跑的是 en 环境,拿中文串去比必然假红。
    /// </summary>
    private static void ProbeCleanPreview(System.Text.StringBuilder report) {
        const string tailNoise = "\u300C";     // 「
        // 必须长过 ClippingCleanPreviewRow.DefaultCoreChars(80),否则测不到"中间省略"这一环。
        var longCore = string.Concat(Enumerable.Repeat(
            "留學派和家人們都相當恐懼不安,因為自己可能會在不知不覺間,被誣陷為間諜團或是體制反對勢力。", 3));

        var cleanReport = new KindleMate2.Application.Models.ClippingCleanReport {
            Scanned = 9,
            ChangedCount = 2,
            AllPunctuationCount = 1,
            Changes = new[] {
                // 只在首部有噪音
                new KindleMate2.Application.Models.ClippingCleanChange(
                    "2099-01-01 00:00:00|100-120", "自检书甲", "。他走过去。", "他走过去。"),
                // 只在尾部有噪音,且正文长到会被中间省略 —— 上一版正是被这里截掉
                new KindleMate2.Application.Models.ClippingCleanChange(
                    "2099-01-02 00:00:00|130-150", "自检书乙", longCore + tailNoise, longCore)
            }
        };

        // 概要里必须同时出现"改多少"与"删多少" —— 删行是这一步唯一不可逆的部分,
        // 而它可能因为清洗把内容归一化而凭空多出来(见 ScanDatabaseMaintenance)。
        var plan = new KindleMate2.Application.Models.DatabaseMaintenancePlan {
            Cleaning = cleanReport,
            Cleanup = new KindleMate2.Application.Models.DatabaseCleanPlan {
                Removals = new[] {
                    new KindleMate2.Application.Models.DatabaseCleanRemoval(
                        "k9", "自检书", "", KindleMate2.Application.Models.DatabaseCleanRemovalReason.Empty),
                    new KindleMate2.Application.Models.DatabaseCleanRemoval(
                        "k7", "自检书", "", KindleMate2.Application.Models.DatabaseCleanRemovalReason.Empty),
                    new KindleMate2.Application.Models.DatabaseCleanRemoval(
                        "k6", "自检书", "", KindleMate2.Application.Models.DatabaseCleanRemovalReason.Empty),
                    new KindleMate2.Application.Models.DatabaseCleanRemoval(
                        "k8", "自检书", "重复的内容", KindleMate2.Application.Models.DatabaseCleanRemovalReason.Duplicated),
                    new KindleMate2.Application.Models.DatabaseCleanRemoval(
                        "k5", "自检书", "重复的内容", KindleMate2.Application.Models.DatabaseCleanRemovalReason.Duplicated),
                    new KindleMate2.Application.Models.DatabaseCleanRemoval(
                        "k4", "自检书", "重复的内容", KindleMate2.Application.Models.DatabaseCleanRemovalReason.Duplicated),
                    new KindleMate2.Application.Models.DatabaseCleanRemoval(
                        "k3", "自检书", "重复的内容", KindleMate2.Application.Models.DatabaseCleanRemovalReason.Duplicated),
                    new KindleMate2.Application.Models.DatabaseCleanRemoval(
                        "k2", "自检书", "重复的内容", KindleMate2.Application.Models.DatabaseCleanRemovalReason.Duplicated)
                }
            }
        };
        var preview = new ClippingCleanPreviewViewModel(plan);
        var head = preview.Rows[0];
        var tail = preview.Rows[1];

        var beforeLine = tail.LeadingNoise + tail.Head + tail.Tail + tail.TrailingNoise;
        var afterLine = tail.Head + tail.Tail;

        // 断言**只比数字与顺序**,不比文案:CI 是 en 环境,拿中文串比必然假红。
        // 四个数字刻意取得互不相同(改动 2 / 扫描 9 / 空条目 3 / 重复项 5),
        // 这样"出现顺序递增"就能证明四个参数没串位 —— 串位是这类拼接最常见的错法。
        var digits = new[] { "2", "9", "3", "5" };
        var summaryMentionsCleanup = true;
        var cursor = 0;
        foreach (var digit in digits) {
            var at = preview.Summary.IndexOf(digit, cursor, StringComparison.Ordinal);
            if (at < 0) { summaryMentionsCleanup = false; break; }
            cursor = at + 1;
        }
        var ok = preview.Rows.Count == 2 && summaryMentionsCleanup
                 && head.LeadingNoise == "\u3002" && head.TrailingNoise.Length == 0
                 && tail.LeadingNoise.Length == 0 && tail.TrailingNoise == tailNoise
                 && tail.Tail.Length > 0                       // 正文被中间省略 ⇒ 尾部仍在
                 && beforeLine.EndsWith(tailNoise, StringComparison.Ordinal)
                 && !string.Equals(beforeLine, afterLine, StringComparison.Ordinal);

        report.AppendLine($"clean preview: rows={preview.Rows.Count}" +
                          $" headNoise='{head.LeadingNoise}' tailNoise='{tail.TrailingNoise}'" +
                          $" coreElided={tail.Tail.Length > 0}" +
                          $" changed={(ok ? "yes" : "NO")}" +
                          $" 概要含四项计数(按序)={summaryMentionsCleanup}" +
                          $" -> result={(ok ? "OK" : "失败!预览拼装不符合预期")}");
    }

    /// <summary>
    /// 表格列冗余判定的探针:整列同值时收起「书籍 / 作者」/「生词」/「词干」列。
    ///
    /// 判据是**表里的数据**(而不是"左栏选了什么"),所以这里直接换表内容来验:
    /// 跨书 → 显示;同一本书 → 收起;再换回跨书 → 重新显示。
    /// 最后一跳最要紧:只在加载时算一次、之后不跟着表走的话,列会永远停在收起状态。
    /// 同时记录**通知序列** —— 判定对了但没发通知,视图照样不会更新(这类"接线"缺陷单测抓不到)。
    ///
    /// 「词干」与「生词」分开判:样本里特意放一组"同一个词干、不同词形"
    /// (beautiful / beautifully),那时只该收起词干列 —— 硬绑在一起就会藏错。
    /// </summary>
    private static void ProbeTableColumns(System.Text.StringBuilder report) {
        var vm = new MainWindowViewModel();
        var bookNotified = new List<bool>();
        var wordNotified = new List<bool>();
        var stemNotified = new List<bool>();
        vm.PropertyChanged += (_, e) => {
            if (e.PropertyName == nameof(MainWindowViewModel.ShowClipBookColumns)) bookNotified.Add(vm.ShowClipBookColumns);
            if (e.PropertyName == nameof(MainWindowViewModel.ShowWordColumn)) wordNotified.Add(vm.ShowWordColumn);
            if (e.PropertyName == nameof(MainWindowViewModel.ShowStemColumn)) stemNotified.Add(vm.ShowStemColumn);
        };

        vm.ClipTable.ReplaceAll(new[] { MakeClipping("甲书", "a"), MakeClipping("乙书", "b") });
        var mixedBooks = vm.ShowClipBookColumns;
        vm.ClipTable.ReplaceAll(new[] { MakeClipping("甲书", "a"), MakeClipping("甲书", "b") });
        var sameBook = vm.ShowClipBookColumns;
        vm.ClipTable.ReplaceAll(new[] { MakeClipping("甲书", "a"), MakeClipping("乙书", "b") });
        var mixedBooksAgain = vm.ShowClipBookColumns;

        // ① 选中某个生词:词与词干都整列同值 ⇒ 两列都收起
        vm.LookupTable.ReplaceAll(new[] { MakeLookup("en:beautiful", "beautiful"), MakeLookup("en:beautiful", "beautiful") });
        var sameWord = vm.ShowWordColumn;
        var sameWordStem = vm.ShowStemColumn;

        // ② 同一词干的不同词形:词干列是废话,生词列不是 ⇒ 只收起词干
        vm.LookupTable.ReplaceAll(new[] { MakeLookup("en:beautiful", "beautiful"), MakeLookup("en:beautifully", "beautiful") });
        var stemOnly = vm.ShowStemColumn;
        var wordKept = vm.ShowWordColumn;

        // ③ 不同词干:两列都回来
        vm.LookupTable.ReplaceAll(new[] { MakeLookup("en:beautiful", "beautiful"), MakeLookup("en:careful", "careful") });
        var mixedWords = vm.ShowWordColumn;
        var mixedStems = vm.ShowStemColumn;

        var notified = bookNotified.Count == 2 && !bookNotified[0] && bookNotified[1]
                       && wordNotified.Count == 2 && !wordNotified[0] && wordNotified[1]
                       && stemNotified.Count == 2 && !stemNotified[0] && stemNotified[1];
        var ok = mixedBooks && !sameBook && mixedBooksAgain
                 && !sameWord && !sameWordStem
                 && !stemOnly && wordKept
                 && mixedWords && mixedStems
                 && notified;

        report.AppendLine($"table columns: 跨书={mixedBooks}(期望 True) 同书={sameBook}(期望 False)" +
                          $" 再跨书={mixedBooksAgain}(期望 True)" +
                          $" 同词={sameWord}/同词干={sameWordStem}(期望 False/False)" +
                          $" 同词干异词形:词={wordKept}(期望 True) 词干={stemOnly}(期望 False)" +
                          $" 异词异干={mixedWords}/{mixedStems}(期望 True/True)" +
                          $" 通知=[book:{bookNotified.Count} word:{wordNotified.Count} stem:{stemNotified.Count}](各期望 2,且 False→True)" +
                          $" -> result={(ok ? "OK" : "失败!列显隐判定不符合预期")}");
    }

    private static KindleMate2.Domain.Entities.KM2DB.Clipping MakeClipping(string book, string content) =>
        new() { Key = book + "|" + content, Content = content, BookName = book };

    /// <summary><c>Lookup.Word</c> 是从 <c>WordKey</c>("语言:词")里切出来的,所以只能设 WordKey。</summary>
    private static KindleMate2.Domain.Entities.KM2DB.Lookup MakeLookup(string wordKey, string? stem = null) =>
        new() { WordKey = wordKey, Stem = stem, Usage = "u" };

    /// <summary>
    /// 列表项元信息行的构成(模型层,同样够不着单测)。
    ///
    /// 钉住一条不变量:**书名不在 <c>MetaTail</c> 里**。
    /// 它单独占一列、由那一列负责省略;若有人把书名重新并回 <c>MetaTail</c>,
    /// 那一行会把书名显示两遍(一列一截),而且长书名又会把「第 N 页」挤没 ——
    /// 正是这次修掉的毛病(横向 StackPanel 让 TextTrimming 永不触发、整行溢出)。
    /// </summary>
    private static void ProbeListItemMeta(System.Text.StringBuilder report) {
        const string longBook = "第一本复杂性创伤后压力症候群自我疗愈圣经:在童年创伤中求生到茁壮的恢复指南";

        var clipItem = new KindleMate2.Avalonia.Models.ListItem { Key = "k1", Primary = "内容", Book = longBook, Place = "第 798 页" };
        var wordItem = new KindleMate2.Avalonia.Models.ListItem { Key = "k2", Primary = "用法", Book = "某本书", Extra = "词干 beautiful · 词频 3" };
        var bareItem = new KindleMate2.Avalonia.Models.ListItem { Key = "k3", Primary = "内容" };

        var ok = clipItem.MetaTail == "第 798 页" && clipItem.HasMetaTail
                 && !clipItem.MetaTail.Contains(longBook, StringComparison.Ordinal)
                 && wordItem.MetaTail == "词干 beautiful · 词频 3"
                 && !wordItem.MetaTail.Contains("某本书", StringComparison.Ordinal)
                 && !bareItem.HasMetaTail && bareItem.MetaTail.Length == 0;

        report.AppendLine($"list meta row: 标注项 MetaTail='{clipItem.MetaTail}'(期望只有页码)" +
                          $" 生词项 MetaTail='{wordItem.MetaTail}'(期望只有词干/词频)" +
                          $" 无元信息项 HasMetaTail={bareItem.HasMetaTail}(期望 False)" +
                          $" -> result={(ok ? "OK" : "失败!书名不该出现在 MetaTail 里")}");
    }

    /// <summary>
    /// 生词域中栏的两段结构(查询 / 标注)。
    ///
    /// 2026-09-22:右栏那段「这个词出现过的标注」挪到中栏,与查询分成两段。
    /// 这里钉三件事:
    /// <list type="number">
    /// <item>切段后行的**顺序与归属**:标题 → 查询行 → 标题 → 标注行,不多不少;</item>
    /// <item>标题行**不是记录**(不挂 Clipping/Lookup)⇒ 选中它右栏必须是空面板 ——
    ///   否则会留着上一条的内容,看起来像面板坏了;</item>
    /// <item><c>HeaderSubtitle</c> **只数查询行**:它要是把标题行/标注行也数进去,
    ///   抬头就会把「1 条查询」说成「3 条查询」。</item>
    /// </list>
    /// 不切段(全部生词视图 / 有搜索词)时必须是一条**平表** —— 一个标题行都不许有;
    /// 空段也不许留标题行(「0 条标注」只是噪音)。
    ///
    /// 断言里**不得出现本地化文案**(CI 跑的是 en 环境):所以只比结构、数量,
    /// 以及"副标题加不加标注段都该是同一句",不比标题文字。
    /// </summary>
    private static void ProbeWordSections(System.Text.StringBuilder report) {
        var vm = new MainWindowViewModel { DomainIndex = 1 };   // 1 = 生词域(会重建左栏,必须在塞行之前)
        var lookups = new List<KindleMate2.Domain.Entities.KM2DB.Lookup> { MakeLookup("en:beautiful", "beautiful") };
        var clippings = new List<KindleMate2.Domain.Entities.KM2DB.Clipping> {
            MakeClipping("甲书", "一句含 beautiful 的标注"),
            MakeClipping("乙书", "另一句含 beautiful 的标注")
        };
        var noLookups = new List<KindleMate2.Domain.Entities.KM2DB.Lookup>();
        var noClippings = new List<KindleMate2.Domain.Entities.KM2DB.Clipping>();

        // 判据的三种输入都要走一遍:选中生词 → 切;全部生词 / 有搜索词 → 不切
        var sectioned = vm.BuildWordDomainItems(lookups, clippings, "beautiful", hasSearch: false);
        var allWords = vm.BuildWordDomainItems(lookups, clippings, selectedWord: null, hasSearch: false);
        var searching = vm.BuildWordDomainItems(lookups, clippings, "beautiful", hasSearch: true);
        var onlyClippings = vm.BuildWordDomainItems(noLookups, clippings, "beautiful", hasSearch: false);
        var nothing = vm.BuildWordDomainItems(noLookups, noClippings, "beautiful", hasSearch: false);

        var h0 = sectioned.ElementAtOrDefault(0);
        var r1 = sectioned.ElementAtOrDefault(1);
        var h2 = sectioned.ElementAtOrDefault(2);
        var r3 = sectioned.ElementAtOrDefault(3);

        // 选中标题行 ⇒ 右栏空面板;选中查询行 ⇒ 有详情(两者必须不同,否则"空面板"根本没生效)
        vm.Items.ReplaceAll(sectioned);
        if (r1 != null) vm.SelectedItem = r1;
        var lookupDetail = vm.Detail.HasSelection;
        var rowHasSelection = vm.HasSelectedItem;
        if (h0 != null) vm.SelectedItem = h0;
        var headerDetail = vm.Detail.HasSelection;
        // 标题行也不算"有选中项":否则「删除」会先弹确认框、确认后才报「未选择」
        var headerHasSelection = vm.HasSelectedItem;

        // 副标题只报查询条数 ⇒ 加不加标注段都该是同一句话(否则会变成「3 条查询」)
        vm.Items.ReplaceAll(vm.BuildWordDomainItems(lookups, noClippings, "beautiful", hasSearch: false));
        var subtitleAlone = vm.HeaderSubtitle;
        vm.Items.ReplaceAll(sectioned);
        var subtitleWithClippings = vm.HeaderSubtitle;
        var subtitleStable = string.Equals(subtitleAlone, subtitleWithClippings, StringComparison.Ordinal);

        var headerDistinct = h0 is not null && h2 is not null
                             && h0.SectionTitle.Length > 0 && h2.SectionTitle.Length > 0
                             && !string.Equals(h0.SectionTitle, h2.SectionTitle, StringComparison.Ordinal);
        var structureOk = sectioned.Count == 5
                          && h0 is { IsSectionHeader: true, Clipping: null, Lookup: null }
                          && r1 is { IsSectionHeader: false, Lookup: not null }
                          && h2 is { IsSectionHeader: true, Clipping: null, Lookup: null }
                          && r3 is { IsSectionHeader: false, Clipping: not null }
                          && sectioned.Count(i => i.Clipping != null) == 2
                          && headerDistinct;
        // 两条"不切段"的判据都要钉:全部生词视图、以及有搜索词时
        var flatOk = allWords.Count == 3 && allWords.All(i => !i.IsSectionHeader)
                     && searching.Count == 3 && searching.All(i => !i.IsSectionHeader);
        var emptyOk = onlyClippings.Count == 3 && onlyClippings[0].IsSectionHeader
                      && onlyClippings.Count(i => i.Clipping != null) == 2
                      && nothing.Count == 0;

        var ok = structureOk && flatOk && emptyOk && lookupDetail && !headerDetail && subtitleStable
                 && !headerHasSelection && rowHasSelection;

        report.AppendLine($"word sections: 切段行数={sectioned.Count}(期望 5)" +
                          $" 标题0={h0?.IsSectionHeader}(期望 True) 查询行={r1?.Lookup != null}(期望 True)" +
                          $" 标题2={h2?.IsSectionHeader}(期望 True)" +
                          $" 标注行={sectioned.Count(i => i.Clipping != null)}(期望 2)" +
                          $" 两个标题不同={headerDistinct}(期望 True)" +
                          $" 全部生词平表={allWords.Count}(期望 3)/标题行 {allWords.Count(i => i.IsSectionHeader)}(期望 0)" +
                          $" 有搜索词平表={searching.Count}(期望 3)/标题行 {searching.Count(i => i.IsSectionHeader)}(期望 0)" +
                          $" 只有标注={onlyClippings.Count}(期望 3) 全空={nothing.Count}(期望 0)" +
                          $" 查询行详情={lookupDetail}(期望 True) 标题行详情={headerDetail}(期望 False)" +
                          $" 可操作={rowHasSelection}/{headerHasSelection}(期望 True/False)" +
                          $" 副标题不随标注段变={subtitleStable}(期望 True)" +
                          $" -> result={(ok ? "OK" : "失败!两段结构不符合预期")}");
    }

    /// <summary>
    /// 生词域里**右栏详情**的正文也要把生词加粗 —— 与中栏那一行同源。
    ///
    /// 起因(2026-09-24 用户报):生词域选中「朋友」,中栏「标注」段那行里「朋友」是粗的,
    /// 点开右栏(同一句话的详情)却是素的 —— 高亮当时只在**生词详情**与**列表行**两处做了,
    /// 剪藏详情那条路漏了,看上去像"高亮时灵时不灵"。
    ///
    /// 正负例都要:
    ///   · 正例 —— 生词域 + 选中生词 ⇒ 命中的那一段**真的加粗**(读 Run.FontWeight,
    ///     不是只读分段数据 —— 分段对了但没画成粗体,用户看到的还是没高亮);
    ///   · 负例 —— 标注域(nav.Key 是**书名**)与「全部生词」⇒ 一段都不许命中。
    ///     负例的正文里**必须含那个书名**,否则把域判据删掉探针照样绿(空断言)——
    ///     而真实后果是:在标注域点开一条标注,书名只要出现在正文里就会被高亮。
    ///
    /// 顺带把 <c>EmphasisInlines</c> 那层钉上:它此前记的是"没有自动化,加粗没生效只能人工看",
    /// 而在自检里读一遍 Run 序列就能验 —— 那句话可以撤掉了。
    /// </summary>
    private static void ProbeDetailEmphasis(System.Text.StringBuilder report) {
        const string word = "朋友";
        const string body = "原来明朝士大夫称儒学生员叫做朋友。";

        // —— 正例:生词域 + 选中生词「朋友」——
        var vm = new MainWindowViewModel { DomainIndex = 1 };
        vm.SelectedNav = new KindleMate2.Avalonia.Models.NavItem { Key = word, Name = word };
        var clip = MakeClipping("儒林外史", body);
        // 中栏那一段**走真实构建路径**(ToListItem 里就用 selectedWord 切分段)。
        // 手搓一个 ListItem 会让"中栏同源"变成空断言:PrimarySegments 默认就是空表。
        var section = vm.BuildWordDomainItems(
            Array.Empty<KindleMate2.Domain.Entities.KM2DB.Lookup>(), new[] { clip }, word, hasSearch: false);
        var row = section.FirstOrDefault(i => i.Clipping is not null);
        vm.Items.ReplaceAll(section);
        if (row is not null) vm.SelectedItem = row;
        var detail = vm.Detail;

        var bodyHit = MatchedEmphasis(detail.BodySegments);
        var bodyBold = BoldText(detail.BodyInlines);
        var bodyLossless = EmphasisPlainText(detail.BodyInlines) == body;
        // 中栏那一行:同一个词、同一串字,加粗也必须一致(否则又是"同一句话两种样子")
        var rowHit = row is null ? string.Empty : MatchedEmphasis(row.PrimarySegments);

        // 笔记条目走另一个分支(引文 + 笔记正文)。这一条没有配对划线 ⇒ 引文为空,
        // 钉的是**笔记正文**那半 —— 它与中栏那行是同一串字(ToListItem(Clipping))。
        var noteClip = new KindleMate2.Domain.Entities.KM2DB.Clipping {
            Key = "n1",
            Content = "笔记里也提到朋友",
            BookName = "儒林外史",
            PageNumber = 253,
            BriefType = (long)KindleMate2.Domain.Entities.KM2DB.BriefType.Note
        };
        var noteRow = new KindleMate2.Avalonia.Models.ListItem { Key = noteClip.Key, Primary = noteClip.Content, Clipping = noteClip };
        vm.Items.ReplaceAll(new[] { noteRow });
        vm.SelectedItem = noteRow;
        var noteDetail = vm.Detail;
        var noteHit = MatchedEmphasis(noteDetail.NoteSegments);
        var noteBold = BoldText(noteDetail.NoteInlines);
        var noteLossless = EmphasisPlainText(noteDetail.NoteInlines) == noteClip.Content;

        // —— 负例①:标注域。nav.Key 是**书名**,而正文里正好有书名 ——
        const string book = "儒林外史";
        var vmBook = new MainWindowViewModel();
        vmBook.DomainIndex = 0;
        vmBook.SelectedNav = new KindleMate2.Avalonia.Models.NavItem { Key = book, Name = book };
        var bookClip = MakeClipping(book, "儒林外史里,朋友二字反复出现。");
        var bookRow = new KindleMate2.Avalonia.Models.ListItem { Key = bookClip.Key, Primary = bookClip.Content, Clipping = bookClip };
        vmBook.Items.ReplaceAll(new[] { bookRow });
        vmBook.SelectedItem = bookRow;
        var bookMatched = MatchedEmphasis(vmBook.Detail.BodySegments);
        // 没高亮也不能把正文渲染丢字(分段为空时要退回整段)
        var bookIntact = EmphasisPlainText(vmBook.Detail.BodyInlines) == bookClip.Content;

        // —— 负例②:「全部生词」不是某一个词 ——
        var vmAll = new MainWindowViewModel { DomainIndex = 1 };
        vmAll.SelectedNav = new KindleMate2.Avalonia.Models.NavItem { Key = string.Empty, Name = "全部生词", IsAll = true };
        var allClip = MakeClipping("儒林外史", body);
        var allRow = new KindleMate2.Avalonia.Models.ListItem { Key = allClip.Key, Primary = body, Clipping = allClip };
        vmAll.Items.ReplaceAll(new[] { allRow });
        vmAll.SelectedItem = allRow;
        var allMatched = MatchedEmphasis(vmAll.Detail.BodySegments);

        var ok = bodyHit == word && bodyBold == word && bodyLossless && rowHit == word
                 && noteHit == word && noteBold == word && noteLossless
                 && bookMatched.Length == 0 && bookIntact && allMatched.Length == 0;

        report.AppendLine($"detail emphasis: 正文命中='{bodyHit}'/加粗='{bodyBold}'(期望 {word})" +
                          $" 正文还原={bodyLossless}(期望 True)" +
                          $" 中栏同源='{rowHit}'(期望 {word})" +
                          $" 笔记 命中='{noteHit}'/加粗='{noteBold}'/还原={noteLossless}" +
                          $" 标注域(书名当词)命中长度={bookMatched.Length}(期望 0)" +
                          $" 全部生词命中长度={allMatched.Length}(期望 0)" +
                          $" 未高亮时正文不丢字={bookIntact}" +
                          $" -> result={(ok ? "OK" : "失败!右栏详情没把生词加粗,或域判据漏了")}");
    }

    /// <summary>命中的那几段接起来(一段都没命中 ⇒ 空串)。</summary>
    private static string MatchedEmphasis(IReadOnlyList<KindleMate2.Application.Services.EmphasisSegment> segments) =>
        string.Concat(segments.Where(s => s.IsMatch).Select(s => s.Text));

    /// <summary>渲染形态里**全部**文字 —— 必须与原文逐字相等,否则"没做高亮"反而把正文改没了。</summary>
    private static string EmphasisPlainText(global::Avalonia.Controls.Documents.InlineCollection inlines) =>
        string.Concat(inlines.OfType<global::Avalonia.Controls.Documents.Run>().Select(r => r.Text ?? string.Empty));

    /// <summary>渲染形态里**加粗**的那几段 —— 用户看到的那一层,此前只靠肉眼。</summary>
    private static string BoldText(global::Avalonia.Controls.Documents.InlineCollection inlines) =>
        string.Concat(inlines.OfType<global::Avalonia.Controls.Documents.Run>()
                             .Where(r => r.FontWeight == global::Avalonia.Media.FontWeight.Bold)
                             .Select(r => r.Text ?? string.Empty));

    /// <summary>
    /// 左栏右键菜单的**域感知**:生词本里不该出现「重命名书籍」「导出」。
    ///
    /// 那两个动作都是对**某本书**做的:生词域里点「重命名书籍」会拿"那个词"当书名去找同名书
    /// 改名 —— 不只是菜单难看,是**能改到数据**;「导出」则按"书名 == 这个词"导出一本同名书
    /// (或什么都没有)。2026-09-22 用户发现后修。
    ///
    /// 同时钉**通知**:两条可用性都依赖「哪个域 + 选中哪个节点」,换域时不发通知,
    /// 菜单项就会停在初始状态 —— 这个坑本仓已踩过两次。其中"只切域、不动节点"最要紧:
    /// 空库时 SelectedNav 不产生变化,压根走不到 ApplyFilter,只能靠 DomainIndex 那边补。
    /// </summary>
    private static void ProbeNavMenu(System.Text.StringBuilder report) {
        var vm = new MainWindowViewModel();
        var notified = 0;
        var wordNotified = 0;
        vm.PropertyChanged += (_, e) => {
            if (e.PropertyName == nameof(MainWindowViewModel.CanRenameCurrentBook)) notified++;
            if (e.PropertyName == nameof(MainWindowViewModel.CanRenameCurrentWord)) wordNotified++;
        };

        // ① 生词域 + 具体生词 ⇒「重命名书籍」「导出」收起;
        //    「重命名生词」正好相反 —— 它是生词域里唯一该出现的那条改名入口。
        vm.DomainIndex = 1;
        vm.SelectedNav = new KindleMate2.Avalonia.Models.NavItem { Key = "小心", Name = "小心" };
        var wordRename = vm.CanRenameCurrentBook;
        var wordExport = vm.CanExportCurrent;
        var wordRenameWord = vm.CanRenameCurrentWord;

        // ② 切回标注域 + 具体书 ⇒ 前两条出现,「重命名生词」收起(互斥)
        vm.DomainIndex = 0;
        vm.SelectedNav = new KindleMate2.Avalonia.Models.NavItem { Key = "某本书", Name = "某本书" };
        var bookRename = vm.CanRenameCurrentBook;
        var bookExport = vm.CanExportCurrent;
        var bookRenameWord = vm.CanRenameCurrentWord;

        // ③ 「全部标注」:标注域、但不是具体书 ⇒ 导出可用、两条改名都不可用
        vm.SelectedNav = new KindleMate2.Avalonia.Models.NavItem { Key = string.Empty, Name = "全部", IsAll = true };
        var allRename = vm.CanRenameCurrentBook;
        var allExport = vm.CanExportCurrent;
        var allRenameWord = vm.CanRenameCurrentWord;
        var allCurrentWord = vm.CurrentWord;

        // ④ 只切域、不动节点 —— 最容易漏通知的一条
        var before = notified;
        var wordBefore = wordNotified;
        vm.DomainIndex = 1;
        var switchNotified = notified > before;
        var switchNotifiedWord = wordNotified > wordBefore;

        var ok = !wordRename && !wordExport && wordRenameWord && bookRename && bookExport
                 && !bookRenameWord && !allRename && allExport && !allRenameWord
                 && allCurrentWord.Length == 0
                 && switchNotified && switchNotifiedWord && notified >= 4 && wordNotified >= 4;

        report.AppendLine($"nav menu: 生词域 重命名书籍={wordRename}/导出={wordExport}/重命名生词={wordRenameWord}(期望 False/False/True)" +
                          $" 标注域具体书={bookRename}/{bookExport}/生词={bookRenameWord}(期望 True/True/False)" +
                          $" 全部标注={allRename}/{allExport}/生词={allRenameWord}(期望 False/True/False)" +
                          $" 全部标注无当前词={allCurrentWord.Length == 0}(期望 True)" +
                          $" 只切域有通知={switchNotified}/{switchNotifiedWord}(期望 True/True)" +
                          $" 通知共={notified}/{wordNotified}(各期望 ≥4)" +
                          $" -> result={(ok ? "OK" : "失败!左栏菜单没有跟着域走")}");
    }

    /// <summary>
    /// 「重命名生词」在**视图层**能钉的部分:两处菜单的可用性。
    ///
    /// 拼新键、撞名判定那两条纯规则在 <c>WordRenameRulesTests</c> 里(Shared 层,单测够得着);
    /// 这里只钉 VM 这层单测够不到的:可用性跟着**选中行 / 域与节点**走,
    /// 而且换行必须发通知 —— 绑定的计算属性不发通知,菜单项就会永远停在初始状态
    /// (这个坑本仓已踩过两次)。
    ///
    /// 可用性必须**正负例都验**:只验"选中查询行时为 true"的话,把它改成恒真探针照样绿。
    /// </summary>
    private static void ProbeWordRename(System.Text.StringBuilder report) {
        var vm = new MainWindowViewModel();
        var notified = 0;
        vm.PropertyChanged += (_, e) => {
            if (e.PropertyName is nameof(MainWindowViewModel.CanRenameSelectedWord)
                or nameof(MainWindowViewModel.SelectedItemWord)) {
                notified++;
            }
        };

        // —— 详情面板那条:按**选中行自己**判(不按域) ——
        vm.SelectedItem = new KindleMate2.Avalonia.Models.ListItem {
            Key = "k1", Primary = "用法",
            Lookup = new KindleMate2.Domain.Entities.KM2DB.Lookup { WordKey = "en:beautiful" }
        };
        var lookupWord = vm.SelectedItemWord;
        var lookupCan = vm.CanRenameSelectedWord;

        vm.SelectedItem = new KindleMate2.Avalonia.Models.ListItem {
            Key = "k2", Primary = "内容", Book = "某本书",
            Clipping = new KindleMate2.Domain.Entities.KM2DB.Clipping { Key = "c1", Content = "内容", BookName = "某本书" }
        };
        var clipWord = vm.SelectedItemWord;
        var clipCan = vm.CanRenameSelectedWord;

        // 分组标题行不是记录、也没有词 ⇒ 必须不可用(否则点下去只会弹一个改不动的框)
        vm.SelectedItem = new KindleMate2.Avalonia.Models.ListItem { Key = "h", SectionTitle = "1 条查询" };
        var headerCan = vm.CanRenameSelectedWord;

        vm.SelectedItem = null;
        var noneCan = vm.CanRenameSelectedWord;

        // —— 左栏那条:按**域 + 节点**判 ——
        vm.DomainIndex = 1;
        vm.SelectedNav = new KindleMate2.Avalonia.Models.NavItem { Key = "小心", Name = "小心" };
        var wordNodeCan = vm.CanRenameCurrentWord;
        var wordNodeCurrent = vm.CurrentWord;

        vm.DomainIndex = 0;
        vm.SelectedNav = new KindleMate2.Avalonia.Models.NavItem { Key = "某本书", Name = "某本书" };
        var bookNodeCan = vm.CanRenameCurrentWord;

        vm.DomainIndex = 1;
        vm.SelectedNav = new KindleMate2.Avalonia.Models.NavItem { Key = string.Empty, Name = "全部", IsAll = true };
        var allNodeCan = vm.CanRenameCurrentWord;
        var allNodeCurrent = vm.CurrentWord;

        // 4 次换选中行 × 2 个属性 = 8;换节点还会顺带清掉选中项,故只多不少。
        var ok = lookupWord == "beautiful" && lookupCan
                 && clipWord.Length == 0 && !clipCan
                 && !headerCan && !noneCan
                 && wordNodeCan && wordNodeCurrent == "小心"
                 && !bookNodeCan
                 && !allNodeCan && allNodeCurrent.Length == 0
                 && notified >= 8;

        report.AppendLine($"word rename: 查询行 词='{lookupWord}'/可用={lookupCan}(期望 beautiful/True)" +
                          $" 标注行 词='{clipWord}'/可用={clipCan}(期望 空/False)" +
                          $" 标题行={headerCan}(期望 False) 无选中={noneCan}(期望 False)" +
                          $" 生词节点 可用={wordNodeCan}/当前词='{wordNodeCurrent}'(期望 True/小心)" +
                          $" 标注域节点={bookNodeCan}(期望 False)" +
                          $" 全部生词节点={allNodeCan}/当前词长度={allNodeCurrent.Length}(期望 False/0)" +
                          $" 通知={notified}(期望 ≥8)" +
                          $" -> result={(ok ? "OK" : "失败!重命名生词的菜单可用性不符合预期")}");
    }

    /// 分享卡片的内容组装。
    ///
    /// 内容口径是**用户定死的**:只留「标注内容 + 书名 + 作者」,页数/日期一律舍去 ——
    /// 这条断言就钉这个口径(多塞进页数/日期会立刻红)。
    /// 顺带数 <c>CanShareSelectedClipping</c> 的通知次数:它绑在菜单项/按钮的 IsVisible 上,
    /// 不发通知就会停在初始状态(同一个坑本仓已踩过两次)。
    /// </summary>
    private static void ProbeShareCard(System.Text.StringBuilder report) {
        var vm = new MainWindowViewModel();
        var notified = 0;
        vm.PropertyChanged += (_, e) => {
            if (e.PropertyName == nameof(MainWindowViewModel.CanShareSelectedClipping)) notified++;
        };

        var clip = new KindleMate2.Domain.Entities.KM2DB.Clipping {
            Key = "2017-06-11 06:57:28|113-113",
            Content = "  一段标注正文  ",
            BookName = "某本书",
            AuthorName = "某作者",
            BriefType = (long)KindleMate2.Domain.Entities.KM2DB.BriefType.Highlight
        };
        vm.SelectedItem = new KindleMate2.Avalonia.Models.ListItem {
            Key = "k1", Primary = "内容", Book = "某本书", Clipping = clip
        };
        var card = vm.BuildShareCardModel();
        var canShare = vm.CanShareSelectedClipping;

        // 生词项不是标注 ⇒ 做不出分享图。
        // **可用性本身也要断言负例**:只验"选中标注时为 true"的话,把可用性判定改成恒真
        // 探针照样绿 —— 2026-09-22 用变异实测到过这一点(同一次变异里 context menu 探针红了、
        // 这个探针没红,就是因为那边验了负例、这边没验)。
        vm.SelectedItem = new KindleMate2.Avalonia.Models.ListItem {
            Key = "k2", Primary = "用法",
            Lookup = new KindleMate2.Domain.Entities.KM2DB.Lookup { WordKey = "en:beautiful", Title = "某本书" }
        };
        var lookupCard = vm.BuildShareCardModel();
        var lookupCanShare = vm.CanShareSelectedClipping;

        vm.SelectedItem = null;
        var noneCard = vm.BuildShareCardModel();
        var noneCanShare = vm.CanShareSelectedClipping;

        // 文件名防重复:同一本书、**同一天**、不同位置的两条标注必须给出**不同**的建议名 ——
        // 旧口径(书名 + 当天日期)在这两例上会撞成同一个名字,用户连存两张就误覆盖。
        var fileName = card?.SuggestedFileName("分享图") ?? string.Empty;
        var sameDayOtherClipping = new KindleMate2.Avalonia.ViewModels.ShareCardModel {
            BookName = "某本书", Location = "200-200", ClippingTime = "2017-06-11-06:57:28"
        }.SuggestedFileName("分享图");
        var fileNameOk = fileName.EndsWith(".png", StringComparison.Ordinal)
                         && fileName.Contains("113-113", StringComparison.Ordinal)
                         && !fileName.Contains(':', StringComparison.Ordinal)
                         && !string.Equals(fileName, sameDayOtherClipping, StringComparison.Ordinal);

        var ok = canShare && card is { } c && fileNameOk
                 && c.Quote == "一段标注正文"        // Flatten 会把首尾空白压掉
                 && c.BookName == "某本书"
                 && c.AuthorName == "某作者"
                 && c.HasType && c.IsHighlight
                 && lookupCard is null && noneCard is null
                 && !lookupCanShare && !noneCanShare
                 && notified >= 3;
        report.AppendLine($"share card: 可分享={canShare}(期望 True)" +
                          $" 正文='{card?.Quote}' 书名='{card?.BookName}' 作者='{card?.AuthorName}'" +
                          $" 类型={card?.TypeText}(期望 划线)" +
                          $" 生词项={lookupCard is null}/可分享={lookupCanShare} 无选中={noneCard is null}/可分享={noneCanShare}(期望 True/False/True/False)" +
                          $" 文件名='{fileName}'(须含位置、不含冒号、与同书同日的另一条不同={!string.Equals(fileName, sameDayOtherClipping, StringComparison.Ordinal)})" +
                          $" 通知={notified}(期望 ≥3)" +
                          $" -> result={(ok ? "OK" : "失败!分享卡片内容不符合口径")}");
    }

    /// 列表行 / 详情面板右键菜单的可用性判定(<c>CanRenameSelectedItemBook</c> /
    /// <c>CanEditSelectedClipping</c> / <c>SelectedItemBookName</c>)。
    ///
    /// 两条判定都取自**选中行自己**而不是左栏节点 —— 左栏停在「全部标注」或搜索结果里时,
    /// 行所属的书与节点根本不是一回事(沿用节点名会改错书)。
    ///
    /// 同时**数通知次数**:这三个都是绑定到菜单项 <c>IsVisible</c> 的计算属性,
    /// 不发通知就会停在初始状态 —— 同一个坑刚在左栏「回收站 N」上踩过。
    /// </summary>
    private static void ProbeContextMenuAvailability(System.Text.StringBuilder report) {
        var vm = new MainWindowViewModel();
        var notified = new List<string>();
        vm.PropertyChanged += (_, e) => {
            if (e.PropertyName is nameof(MainWindowViewModel.CanRenameSelectedItemBook)
                or nameof(MainWindowViewModel.CanEditSelectedClipping)
                or nameof(MainWindowViewModel.SelectedItemBookName)) {
                notified.Add(e.PropertyName);
            }
        };

        var clipping = new KindleMate2.Domain.Entities.KM2DB.Clipping {
            Key = "k1", Content = "内容", BookName = "某本书"
        };
        vm.SelectedItem = new KindleMate2.Avalonia.Models.ListItem {
            Key = "k1", Primary = "内容", Book = "某本书", Clipping = clipping
        };
        var clipBook = vm.SelectedItemBookName;
        var clipRename = vm.CanRenameSelectedItemBook;
        var clipEdit = vm.CanEditSelectedClipping;

        var lookup = new KindleMate2.Domain.Entities.KM2DB.Lookup { WordKey = "en:beautiful", Title = "某本书" };
        vm.SelectedItem = new KindleMate2.Avalonia.Models.ListItem {
            Key = "k2", Primary = "用法", Book = "某本书", Lookup = lookup
        };
        var lookupRename = vm.CanRenameSelectedItemBook;
        var lookupEdit = vm.CanEditSelectedClipping;

        vm.SelectedItem = null;
        var noneRename = vm.CanRenameSelectedItemBook;
        var noneEdit = vm.CanEditSelectedClipping;

        // 三次换行 × 三个属性 = 9 次通知。断言与**运行时值**比,不比文案。
        var ok = clipBook == "某本书" && clipRename && clipEdit
                 && lookupRename && !lookupEdit
                 && !noneRename && !noneEdit
                 && notified.Count >= 9;
        report.AppendLine($"context menu: 标注行 改名={clipRename}/编辑={clipEdit}(期望 True/True) 书名='{clipBook}'" +
                          $" 生词行 改名={lookupRename}/编辑={lookupEdit}(期望 True/False)" +
                          $" 无选中 改名={noneRename}/编辑={noneEdit}(期望 False/False)" +
                          $" 通知={notified.Count}(期望 ≥9)" +
                          $" -> result={(ok ? "OK" : "失败!右键菜单可用性判定不符合预期")}");
    }

    /// <summary>
    /// 删除**一行**之后,列表不该跳回顶部 —— 钉住"重建后落回原处"的挑行规则。
    ///
    /// 从前删除走统一写操作壳:重载 → RebuildNav → ApplyFilter → 重建列表,
    /// 而重建的最后一步是"选中第一条"。于是删掉第 200 条会被扔回第 1 条
    /// (Avalonia 的 <c>ListBox.AutoScrollToSelectedItem</c> 默认打开,选中项一变视口就跟着走),
    /// 右栏详情也一并跳回第一条;连续清理时每删一条都要重新滚下去找位置。
    ///
    /// 这里只钉**纯判据**(<see cref="MainWindowViewModel.PickRowByIndex"/>);
    /// 接线 —— 删除时把索引传下去、重建时读回来 —— 由 build.yml 的源码断言兜住。
    /// 理由同别的视图层探针:单测工程不引 Avalonia,够不着 VM。
    /// </summary>
    private static void ProbeRowRestore(System.Text.StringBuilder report) {
        var rows = new List<KindleMate2.Avalonia.Models.ListItem> {
            ProbeRow("r0"), ProbeRow("r1"), ProbeRow("r2"), ProbeRow("r3")
        };
        // 生词域的形状:标题行夹在记录之间(标题行不是记录,挑中它右栏只会空着)
        var sectioned = new List<KindleMate2.Avalonia.Models.ListItem> {
            ProbeHeader("h0"), ProbeRow("w1"), ProbeHeader("h2"), ProbeRow("w3")
        };
        var empty = new List<KindleMate2.Avalonia.Models.ListItem>();

        var inPlace = MainWindowViewModel.PickRowByIndex(rows, 2)?.Key;      // 原位:同一格里现在坐着谁
        var lastGone = MainWindowViewModel.PickRowByIndex(rows, 4)?.Key;     // 删的是最后一条 ⇒ 退回上一条
        var farBeyond = MainWindowViewModel.PickRowByIndex(rows, 99)?.Key;
        var noRequest = MainWindowViewModel.PickRowByIndex(rows, -1);        // 没带请求 ⇒ 不还原
        var emptyRows = MainWindowViewModel.PickRowByIndex(empty, 0);
        var landsOnHeader = MainWindowViewModel.PickRowByIndex(sectioned, 2)?.Key;
        var onlyHeaders = MainWindowViewModel.PickRowByIndex(
            new List<KindleMate2.Avalonia.Models.ListItem> { ProbeHeader("h0") }, 0);

        var ok = inPlace == "r2" && lastGone == "r3" && farBeyond == "r3"
                 && noRequest is null && emptyRows is null
                 && landsOnHeader == "w3" && onlyHeaders is null;

        report.AppendLine($"row restore: 原位={inPlace}(期望 r2)" +
                          $" 删末条={lastGone}(期望 r3) 远超界={farBeyond}(期望 r3)" +
                          $" 无请求={noRequest is null}(期望 True) 空表={emptyRows is null}(期望 True)" +
                          $" 落点是标题行={landsOnHeader}(期望 w3)" +
                          $" 全是标题行={onlyHeaders is null}(期望 True)" +
                          $" -> result={(ok ? "OK" : "失败!挑行规则不符合预期")}");
    }

    /// <summary>
    /// 「库内有任意数据」门禁探针:只导入了生词库(没有任何标注)时,备份/统计/清空不应被当成空库挡下;
    /// 真空中(标注与生词都没有)才算空。VM 在 Avalonia 工程里、单测够不着,故在此钉。
    /// </summary>
    private static void ProbeDataGate(System.Text.StringBuilder report) {
        var work = Path.Combine(Path.GetTempPath(), "km2-gate-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(work);
        try {
            // 纯生词库:只有 vocab + lookups,没有 clippings
            var vocabOnlyDb = Path.Combine(work, "vocab-only.dat");
            KindleMate2.Infrastructure.Helpers.DatabaseHelper.CreateDatabase(vocabOnlyDb, out _);
            using (var conn = new Microsoft.Data.Sqlite.SqliteConnection(
                       KindleMate2.Infrastructure.Helpers.DatabaseHelper.GetConnectionString(vocabOnlyDb))) {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText =
                    "INSERT INTO vocab (id, word_key, word, timestamp) VALUES ('w1', 'en:apple', 'apple', '2026-01-01 00:00:00');" +
                    "INSERT INTO lookups (word_key, usage, title, authors, timestamp) VALUES ('en:apple', 'u', 'Book', 'A', '2026-01-01 00:00:00');";
                cmd.ExecuteNonQuery();
            }
            var vocabVm = new MainWindowViewModel();
            vocabVm.OpenDatabaseAsync(vocabOnlyDb).GetAwaiter().GetResult();

            // 真空库:两者都没有
            var emptyDb = Path.Combine(work, "empty.dat");
            KindleMate2.Infrastructure.Helpers.DatabaseHelper.CreateDatabase(emptyDb, out _);
            var emptyVm = new MainWindowViewModel();
            emptyVm.OpenDatabaseAsync(emptyDb).GetAwaiter().GetResult();

            var ok = !vocabVm.HasClippingData && vocabVm.HasVocabularyData && vocabVm.HasAnyData
                     && !emptyVm.HasClippingData && !emptyVm.HasVocabularyData && !emptyVm.HasAnyData;

            report.AppendLine($"data gate: vocab-only(clip={vocabVm.HasClippingData}" +
                              $" word={vocabVm.HasVocabularyData} any={vocabVm.HasAnyData})" +
                              $" empty-any={emptyVm.HasAnyData}" +
                              $" -> result={(ok ? "OK" : "失败!纯生词库未被算作有数据")}");
        } finally {
            try { Directory.Delete(work, true); } catch { /* best effort */ }
        }
    }

    /// <summary>普通记录行(SectionTitle 为空即非标题行 —— IsSectionHeader 是它派生出来的)。</summary>
    private static KindleMate2.Avalonia.Models.ListItem ProbeRow(string key) =>
        new() { Key = key };
    /// <summary>分组标题行:只设 SectionTitle,不挂 Clipping / Lookup。</summary>
    private static KindleMate2.Avalonia.Models.ListItem ProbeHeader(string title) =>
        new() { Key = title, SectionTitle = title };

    /// <summary>
    /// 写操作端到端自检:把真实库复制到临时目录后,在该副本上依次执行
    /// 导入 / 导出 / 备份 / 重命名 / 删除 / 清理 / 重建 / 清空,逐步核对结果。
    /// </summary>
    private static int RunOperations(string dbPath, string clippingsPath, string vocabDbPath, string outFile) {
        var report = new System.Text.StringBuilder();
        // 同 --smoke:写操作自检也会选中生词,联网查释义在这里必须关掉(见 RunSmoke 的说明)。
        MainWindowViewModel.OnlineDefinitionAllowed = false;
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

            // 7. 维护数据库(清洗 + 清理)/ 重建
            //
            // 这一段同时补上了此前的一个**覆盖缺口**:`CleanClippingTextsAsync` 的编排
            // (无条件备份 → 落库 → 写清单)过去在仓库里没有任何一处会验到 —— 单测只覆盖服务层,
            // 而本自检完全没走清洗路径。现在走的就是合并后的那一个入口,顺带把
            // 「先备份再改」这条安全底线也纳入了自检。
            var maintainResult = vm.MaintainDatabaseAsync().GetAwaiter().GetResult();
            report.AppendLine($"maintain: ok={maintainResult.Ok} title={maintainResult.Title} msg={maintainResult.Message}");
            // 落盘清单:文件名形如 Maintenance_<stamp>.txt(此前是 ClippingClean_*)
            var manifests = Directory.Exists(work)
                ? Directory.GetFiles(work, "Maintenance_*.txt", SearchOption.AllDirectories).Length
                : 0;
            report.AppendLine($"  -> 维护清单 Maintenance_*.txt = {manifests} 份(期望 ≥1)");

            // 从设备导入:本机无 Kindle,应走「设备未连接」前置守卫 —— 返回失败而不是崩溃/静默。
            // (真实取文件与导入需要接上设备,自检覆盖不到。)
            var deviceImport = vm.ImportFromDeviceAsync().GetAwaiter().GetResult();
            report.AppendLine($"import from device: ok={deviceImport.Ok} title={deviceImport.Title} msg={deviceImport.Message}");

            // 设备状态:无设备时文案与「菜单栏按钮显隐」标志应同时为未连接(二者出自同一次探测)
            vm.RefreshDeviceStatus();
            report.AppendLine($"device status: text='{vm.DeviceStatus}' connected={vm.IsDeviceConnected}");
            // 「同步到 Kindle 设备」菜单项的可用性:视图层把 IsEnabled 绑在 IsDeviceConnected 上
            // (绑定本身由编译期校验),这里报的是那个绑定源的取值 + 悬停提示。
            // 本机/CI 通常都没接 Kindle → 期望 enabled=False、提示为「先连设备」。
            report.AppendLine($"sync menu: enabled={vm.IsDeviceConnected} hint='{vm.SyncToDeviceHint}'");

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

            // 退出备份的落点:必须落在 Backups/OnExit 子目录,而不是与手动备份混在 Backups 根下
            // (它每次关闭都产生一份,混放会让根目录迅速被时间戳文件淹没)。
            // 「只留最新 3 份」的选删规则由单测 BackupRetentionTests 钉住,这里只验接线是否还在。
            // 注:根目录那批 *_backup_*.dat 是上面手动备份留下的 —— 本行刻意不递归,
            // 就是为了让"退出备份没混进根目录"这件事在报告里直接可读。
            App.BackupOnExit(vm);
            var backupRoot = Path.Combine(work, AppConstants.BackupsPathName);
            var exitBackupDir = Path.Combine(backupRoot, AppConstants.ExitBackupsPathName);
            var exitBackupCount = Directory.Exists(exitBackupDir)
                ? Directory.GetFiles(exitBackupDir, "*.dat").Length
                : -1;
            report.AppendLine($"backups={Directory.GetFiles(backupRoot).Length}");
            report.AppendLine($"exit backup: {AppConstants.BackupsPathName}/{AppConstants.ExitBackupsPathName} " +
                              $"份数={exitBackupCount}(期望 1) " +
                              $"根目录的备份文件={Directory.GetFiles(backupRoot, "*_backup_*.dat").Length}(只应是手动备份)");

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
            // 显式挑一条真正是标注的项:此前直接取 FirstOrDefault() 的 key,而 SelectedClippingKey
            // 在选不到标注时返回空串 —— binKey 一空,下面四项断言就全 False,
            // 看着像回收站坏了,其实只是选样失败(产品链路本身另有单测覆盖)。
            // ⚠️ 必须排除本探针自己造的合成条目(batch fallback 插的「自检书」):它走的是
            // repository.Add,没有对应的 original_clipping_lines 行 —— 而「已删除」的判据恰恰是
            // 「原始行还在、clippings 已删」,拿它测必然四项全 False(此前那四个 False 就是这么来的;
            // 产品链路本身由 DataLayerFixTests.DeleteClipping_EntersRecycleBin_AndCanBeRestored 覆盖)。
            vm.SelectedItem = vm.Items.FirstOrDefault(i => i.Clipping != null
                && !string.Equals(i.Clipping.BookName, "自检书", StringComparison.Ordinal));
            var binKey = vm.SelectedClippingKey;
            if (string.IsNullOrEmpty(binKey)) {
                report.AppendLine("recycle bin: 跳过(当前没有可选的标注,取不到 key)");
            } else {
                var beforeCount = vm.Session!.Km2DatabaseService.GetDeletedOriginalLines().Count;
                vm.DeleteSelectedAsync().GetAwaiter().GetResult();
                var afterList = vm.Session.Km2DatabaseService.GetDeletedOriginalLines();
                var inBin = afterList.Any(l => l.Key == binKey);
                report.AppendLine($"  -> binKey={binKey} / 删除前回收站={beforeCount} 条 / 删除后={afterList.Count} 条");
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
