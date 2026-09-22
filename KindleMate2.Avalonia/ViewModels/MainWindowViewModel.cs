using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using KindleMate2.Avalonia.Models;
using KindleMate2.Avalonia.Services;
using KindleMate2.Application.Models;
using KindleMate2.Application.Services;
using KindleMate2.Avalonia.Collections;
using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Infrastructure.Helpers;
using KindleMate2.Infrastructure.Repositories.KM2DB;
using KindleMate2.Shared;
using KindleMate2.Shared.Books;
using KindleMate2.Shared.Constants;
using KindleMate2.Shared.Diagnostics;

namespace KindleMate2.Avalonia.ViewModels;

/// <summary>
/// 主界面 VM(内容优先布局):
/// 顶部命令栏(全局搜索 + 视图/主题/语言) · 左栏导航(标注/生词 + 列表) · 中列表 · 右预览面板 · 底状态栏。
/// 数据全部来自现有分层(仓储 / 实体),UI 零业务逻辑。
/// </summary>
public sealed class MainWindowViewModel : INotifyPropertyChanged {
    private string _searchText = string.Empty;
    private string _searchType = TypeTextMap.SearchTypes[0];
    private string _statusText = Strings.Ui_Status_Initial;
    private string _deviceStatus = Strings.Ui_Status_DeviceOffline;
    private int _domainIndex;
    private bool _isBusy;
    private bool _isDarkTheme;
    private bool _isListMode = true;
    private bool _sortDescending = true;

    private NavItem? _selectedNav;
    private ListItem? _selectedItem;
    private Clipping? _selectedClipTable;
    private Lookup? _selectedLookupTable;
    private DetailModel _detail = DetailModel.Empty;

    // 全量数据(打开库时一次性装载)
    private List<Clipping> _allClippings = new();
    private List<Lookup> _allLookups = new();
    private List<Vocab> _allVocabs = new();
    private int _originLineCount;
    private int _distinctWordCount;

    /// <summary>当前库的服务会话(仓储 + 导入/导出/维护/设备管理器)。未打开库时为 null。</summary>
    private DatabaseSession? _session;

    /// <summary>当前会话,供视图层轮询设备状态。</summary>
    public DatabaseSession? Session => _session;

    public bool HasSession => _session != null;

    /// <summary>应用级设置(由 App 注入);为 null 时不做持久化,便于无头自检。</summary>
    public AppSettings? Settings { get; set; }

    public MainWindowViewModel() {
        // 必须在 UI 线程构造:Progress<T> 在此捕获同步上下文,
        // 之后后台线程调用 Report 时会自动回到 UI 线程更新属性。
        _progressReporter = new Progress<OperationProgress>(p => Progress = p);

        // 表格里"整列同值"的列是否该收起,由**表内容**决定 ⇒ 表一变就重算(见 RefreshTableColumnRedundancy)。
        // 挂在集合通知上而不是逐个 Rebuild 方法里去调:换表内容的路径不止一条
        // (筛选 / 回收站 / 删除后刷新 / 恢复…),漏掉一处就会出现"换了视图列没跟着换"。
        ClipTable.CollectionChanged += (_, _) => RefreshTableColumnRedundancy();
        LookupTable.CollectionChanged += (_, _) => RefreshTableColumnRedundancy();
    }

    private readonly Dictionary<string, string> _noteHighlightMap = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Vocab> _vocabByWordKey = new(StringComparer.Ordinal);

    /// <summary>左栏导航(书籍 / 生词)。</summary>
    public ObservableCollection<NavItem> NavItems { get; } = new();

    /// <summary>主列表(内容优先)。数千条量级,整体替换用 <c>ReplaceAll</c> 避免逐条通知淹没 UI 线程。</summary>
    public BulkObservableCollection<ListItem> Items { get; } = new();

    /// <summary>表格视图 · 标注。</summary>
    public BulkObservableCollection<Clipping> ClipTable { get; } = new();

    /// <summary>表格视图 · 生词。</summary>
    public BulkObservableCollection<Lookup> LookupTable { get; } = new();

    public IReadOnlyList<string> SearchTypes => TypeTextMap.SearchTypes;

    public string SearchText {
        get => _searchText;
        set { if (_searchText == value) return; _searchText = value; OnPropertyChanged(); ApplyFilter(); }
    }

    public string SearchType {
        get => _searchType;
        set { if (_searchType == value) return; _searchType = value; OnPropertyChanged(); ApplyFilter(); }
    }

    public string StatusText {
        get => _statusText;
        set { if (_statusText == value) return; _statusText = value; OnPropertyChanged(); }
    }

    public string DeviceStatus {
        get => _deviceStatus;
        set {
            if (_deviceStatus == value) return;
            _deviceStatus = value;
            OnPropertyChanged();
            // 设备已连接时提示里带盘符,所以文案变化也要带上提示一起刷新
            OnPropertyChanged(nameof(SyncToDeviceHint));
        }
    }

    public bool IsBusy {
        get => _isBusy;
        set {
            if (_isBusy == value) return;
            _isBusy = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsProgressVisible));
        }
    }

    public bool IsProgressVisible => _isBusy;

    // —— 进度展示(取代此前的"跑马灯 + 读取中"占位) ——
    //
    // 设计要点:能算比例就给确定进度条(导入按「已处理 / 总条数」算),
    // 算不出才退化为不确定态;阶段文案由 VM 本地化(应用层只报结构化的 OperationStage)。

    private OperationProgress _progress = OperationProgress.At(OperationStage.None);
    private readonly IProgress<OperationProgress> _progressReporter;

    /// <summary>当前进度快照。</summary>
    public OperationProgress Progress {
        get => _progress;
        private set {
            _progress = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ProgressStageText));
            OnPropertyChanged(nameof(ProgressDetailText));
            OnPropertyChanged(nameof(ProgressValue));
            OnPropertyChanged(nameof(IsProgressIndeterminate));
        }
    }

    /// <summary>供后台操作上报进度;<see cref="Progress{T}"/> 构造于 UI 线程,回调会自动回到 UI 线程。</summary>
    public IProgress<OperationProgress> ProgressReporter => _progressReporter;

    /// <summary>进度条是否为不确定态(无法估计比例)。</summary>
    public bool IsProgressIndeterminate => _progress.Fraction is null;

    /// <summary>确定进度的百分比(0..100),供 ProgressBar.Value 使用。</summary>
    public double ProgressValue => (_progress.Fraction ?? 0d) * 100d;

    /// <summary>阶段文案,如「正在导入标注」。</summary>
    public string ProgressStageText => _progress.Stage switch {
        // 操作已开始但处理器还没报出第一个阶段:用中性文案,避免与「正在读取文件…」撞车
        OperationStage.None => Strings.Ui_Progress_Starting,
        OperationStage.ReadingFile => Strings.Ui_Progress_ReadingFile,
        OperationStage.Parsing => Strings.Ui_Progress_Parsing,
        OperationStage.Preparing => Strings.Ui_Progress_Preparing,
        OperationStage.Writing => Strings.Ui_Progress_Writing,
        OperationStage.Reloading => Strings.Ui_Progress_Reloading,
        _ => Strings.Ui_Status_Reading
    };

    /// <summary>数量明细,如「3,200 / 5,680 条」;无数量时为空。</summary>
    public string ProgressDetailText => _progress.Total > 0
        ? $"{_progress.Current:N0} / {_progress.Total:N0}"
        : string.Empty;

    public bool IsDarkTheme {
        get => _isDarkTheme;
        set { if (_isDarkTheme == value) return; _isDarkTheme = value; OnPropertyChanged(); }
    }

    public bool SortDescending {
        get => _sortDescending;
        set {
            if (_sortDescending == value) return;
            _sortDescending = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SortLabel));
            ApplyFilter();
        }
    }

    public string SortLabel => _sortDescending ? Strings.Ui_Sort_TimeDesc : Strings.Ui_Sort_TimeAsc;

    /// <summary>0 = 标注,1 = 生词本。</summary>
    public int DomainIndex {
        get => _domainIndex;
        set {
            var next = value < 0 ? 0 : value;
            if (_domainIndex == next) return;
            _domainIndex = next;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsClipDomain));
            OnPropertyChanged(nameof(IsWordDomain));
            OnPropertyChanged(nameof(DomainTabClippingClass));
            OnPropertyChanged(nameof(DomainTabWordClass));
            OnPropertyChanged(nameof(ShowClipTable));
            OnPropertyChanged(nameof(ShowWordTable));
            SelectedNav = null;
            SelectedItem = null;
            RebuildNav();
        }
    }

    public bool IsClipDomain => _domainIndex == 0;
    public bool IsWordDomain => _domainIndex == 1;
    public string DomainTabClippingClass => IsClipDomain ? "active" : string.Empty;
    public string DomainTabWordClass => IsWordDomain ? "active" : string.Empty;

    public bool IsListMode {
        get => _isListMode;
        set {
            if (_isListMode == value) return;
            _isListMode = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsTableMode));
            OnPropertyChanged(nameof(ShowList));
            OnPropertyChanged(nameof(ShowClipTable));
            OnPropertyChanged(nameof(ShowWordTable));
        }
    }

    public bool IsTableMode => !_isListMode;
    public bool ShowList => IsListMode;
    public bool ShowClipTable => IsTableMode && IsClipDomain;
    public bool ShowWordTable => IsTableMode && IsWordDomain;

    // —— 表格里"整列同值"的列 ——
    //
    // 左栏选中**某一本书**时,「书籍 / 作者」两列整列都是同一个值:白占 240px 宽度、
    // 把「内容」挤窄,每一行还在重复同一句话。生词表同理 —— 选中某个生词时
    // 「生词」与「词干」两列整列同值。
    //
    // 判据刻意取**表里的数据**而不是"左栏选了什么":
    //   · 回收站、以及"选中某本书后再看回收站"这类跨书视图同样会被正确处理,
    //     不必为每个视图补一条条件(左栏选中项与表内容本来就可能不同步);
    //   · 空表按"无冗余信息"处理 → 收起;
    //   · 表内容一变就重算,不会出现"换了视图列没跟着换"。
    //
    // 「生词」与「词干」**分开判**:两者通常一起同值,但并不必然 ——
    // 跨生词搜索时完全可能命中同一个词干的多个词形(beautiful / beautifully),
    // 那时「词干」列确实是废话,而「生词」列不是。硬绑在一起就会藏错。

    private bool _showClipBookColumns = true;
    private bool _showWordColumn = true;
    private bool _showStemColumn = true;

    /// <summary>标注表格是否显示「书籍 / 作者」两列(整列同值时不显示)。</summary>
    public bool ShowClipBookColumns => _showClipBookColumns;

    /// <summary>生词表格是否显示「生词」列(整列同值时不显示)。</summary>
    public bool ShowWordColumn => _showWordColumn;

    /// <summary>生词表格是否显示「词干」列(整列同值时不显示)。</summary>
    public bool ShowStemColumn => _showStemColumn;

    /// <summary>按当前表内容重算上面三个标志,变了才发通知。</summary>
    private void RefreshTableColumnRedundancy() {
        var showBookColumns = !AllSame(ClipTable, c => c.BookName);
        if (showBookColumns != _showClipBookColumns) {
            _showClipBookColumns = showBookColumns;
            OnPropertyChanged(nameof(ShowClipBookColumns));
        }

        var showWordColumn = !AllSame(LookupTable, l => l.Word);
        if (showWordColumn != _showWordColumn) {
            _showWordColumn = showWordColumn;
            OnPropertyChanged(nameof(ShowWordColumn));
        }

        var showStemColumn = !AllSame(LookupTable, l => l.Stem);
        if (showStemColumn != _showStemColumn) {
            _showStemColumn = showStemColumn;
            OnPropertyChanged(nameof(ShowStemColumn));
        }
    }

    /// <summary>
    /// 表内所有行的键是否**完全相同**(空表 / 单行视为相同)。
    /// 早退在第一个不同的值上 —— 数千行的表通常比两行就返回。
    /// </summary>
    private static bool AllSame<T>(IReadOnlyList<T> rows, Func<T, string?> key) {
        if (rows.Count <= 1) return true;
        var first = key(rows[0]) ?? string.Empty;
        for (var i = 1; i < rows.Count; i++) {
            if (!string.Equals(first, key(rows[i]) ?? string.Empty, StringComparison.Ordinal)) return false;
        }
        return true;
    }

    public NavItem? SelectedNav {
        get => _selectedNav;
        set {
            if (ReferenceEquals(_selectedNav, value)) return;
            _selectedNav = value;
            OnPropertyChanged();
            ApplyFilter();
        }
    }

    public ListItem? SelectedItem {
        get => _selectedItem;
        set {
            if (ReferenceEquals(_selectedItem, value)) return;
            _selectedItem = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelectedItem));
            RebuildDetailFromListItem();
        }
    }

    public bool HasSelectedItem => _selectedItem != null;

    /// <summary>表格模式 · 标注选中行。</summary>
    public Clipping? SelectedClipTable {
        get => _selectedClipTable;
        set {
            if (ReferenceEquals(_selectedClipTable, value)) return;
            _selectedClipTable = value;
            OnPropertyChanged();
            if (_selectedClipTable != null && IsClipDomain) Detail = BuildClippingDetail(_selectedClipTable);
        }
    }

    /// <summary>表格模式 · 生词选中行。</summary>
    public Lookup? SelectedLookupTable {
        get => _selectedLookupTable;
        set {
            if (ReferenceEquals(_selectedLookupTable, value)) return;
            _selectedLookupTable = value;
            OnPropertyChanged();
            if (_selectedLookupTable != null && IsWordDomain) Detail = BuildVocabDetail(_selectedLookupTable);
        }
    }

    public DetailModel Detail {
        get => _detail;
        private set { _detail = value; OnPropertyChanged(); }
    }

    // —— 左栏分区标题 / 计数 ——
    public string NavSectionTitle => IsClipDomain ? Strings.Books : Strings.Ui_Nav_Words;
    public int NavSectionCount => IsClipDomain ? BookCount : WordCount;

    // —— 主区标题 ——
    //
    // 回收站优先:它不是"某本书的列表"(内容跨书),左栏那个节点名放这里就是撒谎。
    // 进入/离开回收站时由 IsRecycleBinView 的 setter 一并通知本属性。
    public string HeaderTitle => IsRecycleBinView
        ? Strings.Ui_Nav_RecycleBin
        : _selectedNav is { IsAll: false } nav
            ? nav.Name
            : (IsClipDomain ? Strings.Ui_Header_AllClippings : Strings.Ui_Header_AllWords);

    public string HeaderSubtitle => IsClipDomain
        ? string.Format(CultureInfo.CurrentCulture, Strings.Ui_Text_ClippingCount, Items.Count)
        : string.Format(CultureInfo.CurrentCulture, Strings.Ui_Text_LookupCount, Items.Count);

    // —— 状态栏 ——
    public int BookCount => _allClippings.Select(c => c.BookName).Where(b => !string.IsNullOrWhiteSpace(b)).Distinct().Count();
    public int ClipCount => _allClippings.Count;
    public int WordCount => _distinctWordCount;
    public int LookupCount => _allLookups.Count;

    /// <summary>库内是否有标注数据 —— 清理/清空前是否需要提示或确认(对齐原版的 Count 判断)。</summary>
    public bool HasClippingData => _allClippings.Count > 0;

    /// <summary>已删除 = 原始标注行 − 当前标注(与原版 GetStatusText 口径一致)。</summary>
    public int DeletedCount => Math.Max(0, _originLineCount - _allClippings.Count);

    public string StatusLeft => IsClipDomain
        ? string.Format(CultureInfo.CurrentCulture, Strings.Ui_Status_SummaryClippings, BookCount, ClipCount, DeletedCount)
        : string.Format(CultureInfo.CurrentCulture, Strings.Ui_Status_SummaryVocab, WordCount, LookupCount);

    public string StatusRight => _deviceStatus;

    /// <summary>
    /// 启动流程 —— 严格对齐原 WinForms 版 <c>FrmMain</c> 构造函数里的数据库生命周期:
    /// <list type="number">
    ///   <item>库路径固定为 <c>&lt;当前目录&gt;/KM2.dat</c>(原版即如此,没有库选择器);</item>
    ///   <item>文件不存在则用 <c>DatabaseHelper.CreateDatabase</c> 自动建库,失败则报错并退出;</item>
    ///   <item>执行一次幂等的 <c>MigrateLookupsSchemaIfNeeded</c>(失败仅告警,不中断)。</item>
    /// </list>
    /// 建库失败由视图层弹错误框并退出(原版为 <c>Environment.Exit(0)</c>)——VM 不碰 UI。
    /// </summary>
    /// <returns>
    /// <c>Fatal</c> = 建库失败(原版会错误框 + 退出);<c>Ok</c> = 库已成功打开;
    /// <c>Error</c> 为消息(失败时)。
    /// </returns>
    public async Task<(bool Fatal, bool Ok, string Error)> PrepareDatabaseAsync() {
        var databasePath = AppPaths.DatabasePath;

        if (!File.Exists(databasePath)) {
            if (!DatabaseHelper.CreateDatabase(databasePath, out var exception)) {
                return (true, false, exception.Message);
            }
        }

        try {
            DatabaseHelper.MigrateLookupsSchemaIfNeeded(databasePath);
            // 老库补建 [clippings] 的查询索引(幂等;新建的库已由建库脚本带上)。
            DatabaseHelper.EnsureIndexesIfNeeded(databasePath);
        } catch (Exception ex) {
            // 原版此处只弹一个警告框,不阻断启动
            MigrationWarning = ex.Message;
            OnPropertyChanged(nameof(MigrationWarning));
        }

        await OpenDatabaseAsync(databasePath);
        // 原版对「库打不开」不退出:弹错误框,界面继续(数据为空)。
        return (false, HasSession, HasSession ? string.Empty : StatusText);
    }

    /// <summary>schema 迁移失败的告警文案(空表示无告警);由视图层弹一次提示。</summary>
    public string MigrationWarning { get; private set; } = string.Empty;

    /// <summary>持久化主题选择。</summary>
    public void PersistTheme(bool dark) {
        if (Settings is not { } settings) return;
        settings.Theme = dark ? "dark" : "light";
        settings.Save();
    }

    /// <summary>持久化语言选择并立即应用文化。</summary>
    public void PersistLanguage(string language) {
        if (Settings is not { } settings) return;
        settings.Language = language;
        settings.ApplyCulture();
        settings.Save();
    }

    /// <summary>
    /// 打开指定库。**注意:不再做 schema 预校验** —— 原版没有这层发明,
    /// 库损坏时就是让异常发生,由调用方(视图层)按原版方式弹错误框。
    /// 失败时必须把会话清干净,否则 <c>HasSession</c> 会说谎,后续刷新会在坏会话上重演异常。
    /// </summary>
    /// <summary>
    /// 释放当前数据库会话。主窗口被重建(切换语言)时,旧 VM 会连同窗口一起被丢弃 ——
    /// 但 <c>DatabaseSession</c> 持有 DeviceManager 等资源,**必须显式释放**,
    /// 否则每次切换语言都会泄漏一份会话(调用方见 MainWindow.TryRebuildWindow)。
    /// </summary>
    public void ReleaseSession() {
        _session?.Dispose();
        _session = null;
        NotifySessionStateChanged();
    }

    /// <summary>
    /// 会话建立 / 释放时统一发通知。
    /// <c>SyncToDeviceHint</c> 同样依赖"会话是否存在",合在一处发就**不会漏** ——
    /// 每个调用点各写两行 <c>OnPropertyChanged</c> 的写法,再加一个依赖项时必然漏掉某处。
    /// </summary>
    private void NotifySessionStateChanged() {
        OnPropertyChanged(nameof(HasSession));
        OnPropertyChanged(nameof(Session));
        OnPropertyChanged(nameof(SyncToDeviceHint));
    }

    public async Task OpenDatabaseAsync(string path) {
        if (IsBusy) return;
        if (!File.Exists(path)) {
            StatusText = string.Format(CultureInfo.CurrentCulture, Strings.Ui_Status_FileNotFound, path);
            return;
        }

        IsBusy = true;
        StatusText = Strings.Ui_Status_Loading;
        ResetCollections();

        try {
            _session?.Dispose();
            _session = new DatabaseSession(path);
            NotifySessionStateChanged();

            var elapsedMs = await Task.Run(ReloadFromSession);
            RebuildNav();
            StatusText = $"{Path.GetFileName(path)} · {_allClippings.Count:N0} / {_allLookups.Count:N0} ({elapsedMs} ms)";
        } catch (Exception ex) {
            // 打开失败必须把会话清干净:否则 _session 非 null 会让 HasSession 说谎,
            // 后续任何刷新/导入都会在一个坏会话上重演同一个异常。
            _session?.Dispose();
            _session = null;
            ResetCollections();
            NotifySessionStateChanged();
            StatusText = string.Format(CultureInfo.CurrentCulture, Strings.Ui_Status_OpenFailed, ex.Message);
            // 这里刻意吞掉异常(不向上抛),因此必须自己留痕:否则该错误只会短暂出现在状态栏,
            // 用户切走就再无从查起。WinExe 下 Console.WriteLine 无处可去,走文件日志。
            KindleMate2.Shared.Diagnostics.AppLog.Write(ex);
        } finally {
            IsBusy = false;
            OnPropertyChanged(nameof(StatusLeft));
            OnPropertyChanged(nameof(StatusRight));
        }
    }

    /// <summary>从当前会话重新装载全部数据(打开库 / 导入 / 维护 / 删除后)。须在后台线程调用。</summary>
    private long ReloadFromSession() {
        var session = _session ?? throw new InvalidOperationException("尚未打开数据库");
        var stopwatch = Stopwatch.StartNew();
        _allClippings = session.ClippingRepository.GetAll();
        _allLookups = session.LookupRepository.GetAll();
        _allVocabs = session.VocabRepository.GetAll();
        _originLineCount = session.OriginalClippingLineRepository.GetAll().Count;
        IndexVocabs();
        IndexNoteHighlights();
        EnrichLookups();
        stopwatch.Stop();
        return stopwatch.ElapsedMilliseconds;
    }

    /// <summary>重新装载数据并刷新界面(不改变当前库)。</summary>
    public async Task ReloadAsync() {
        if (!HasSession || IsBusy) return;
        var previous = _selectedNav is { IsAll: false } nav ? nav.Key : null;
        IsBusy = true;
        try {
            await Task.Run(ReloadFromSession);
            RebuildNav();
            RestoreSelection(previous);
        } catch (Exception ex) {
            // 这里必须捕获:ReloadAsync 由 async void 事件处理器调用,
            // 异常逃逸会直接击穿到 UI 层导致进程崩溃(库被删除/损坏时很容易触发)。
            StatusText = string.Format(CultureInfo.CurrentCulture, Strings.Ui_Status_OpenFailed, ex.Message);
        } finally {
            IsBusy = false;
            NotifyCounts();
        }
    }

    private void RestoreSelection(string? previousKey) {
        if (string.IsNullOrEmpty(previousKey)) return;
        var match = NavItems.FirstOrDefault(n => !n.IsAll && string.Equals(n.Key, previousKey, StringComparison.Ordinal));
        if (match != null) SelectedNav = match;
    }

    private void NotifyCounts() {
        OnPropertyChanged(nameof(StatusLeft));
        OnPropertyChanged(nameof(StatusRight));
        OnPropertyChanged(nameof(NavSectionCount));
    }

    /// <summary>操作反馈类型 —— 决定视图层弹哪种对话框(严格对齐原版)。</summary>
    public enum FeedbackKind {
        /// <summary>不弹窗。原版有大量静默分支(如导出失败、用户取消),不得擅自补提示。</summary>
        Silent,

        /// <summary>单按钮提示框,对应原版 <c>MessageBox(..., OK)</c>。</summary>
        Info,

        /// <summary>是/否追问,选「是」后打开资源管理器 —— 对应原版
        /// <c>Export_Successful / Backup_Successful</c> 后拼接 <c>Open_Folder</c> 的 YesNo 框。</summary>
        OpenFolderPrompt
    }

    /// <summary>
    /// 一次写操作的结果。语义对齐原版 <c>RunBackgroundTask</c>:
    /// 操作返回的字符串即成功提示正文;返回空串视为失败;抛异常视为失败(详情为异常消息)。
    /// </summary>
    public readonly record struct OperationResult(bool Ok, string Title, string Message,
        FeedbackKind Kind = FeedbackKind.Info, string FolderToOpen = "") {
        /// <summary>原版的静默分支:什么都不弹。</summary>
        public static OperationResult Silent => new(false, string.Empty, string.Empty, FeedbackKind.Silent);
    }

    /// <summary>
    /// 统一的写操作执行壳。**不弹窗** —— 按原版 <c>RunBackgroundTask</c> 的语义把结果交回视图层:
    /// <list type="bullet">
    ///   <item>返回非空串 → 成功,该串就是成功框正文(标题 = <paramref name="successTitle"/>);</item>
    ///   <item>返回空串 → 失败,弹一个「只有标题」的错误框(标题=正文=<paramref name="failureTitle"/>);</item>
    ///   <item>抛异常 → 失败,正文 = <c>failureTitle + 换行 + 异常消息</c>。</item>
    /// </list>
    /// 注意:成功与否**靠返回串是否为空判定**,不是靠 bool —— 这是原版的既定契约,不要"改进"。
    /// </summary>
    private async Task<OperationResult> RunOperationAsync(Func<string> operation, bool reload,
        string successTitle, string failureTitle, bool silentOnSuccess = false) {
        if (_session == null) {
            return new OperationResult(false, failureTitle, failureTitle);
        }
        if (IsBusy) return OperationResult.Silent;

        var previous = _selectedNav is { IsAll: false } nav ? nav.Key : null;
        IsBusy = true;
        Progress = OperationProgress.At(OperationStage.None);
        try {
            var result = await Task.Run(operation);
            if (reload) {
                Progress = OperationProgress.At(OperationStage.Reloading);
                await Task.Run(ReloadFromSession);
                RebuildNav();
                RestoreSelection(previous);
            }
            return string.IsNullOrWhiteSpace(result)
                ? new OperationResult(false, failureTitle, failureTitle)
                : silentOnSuccess
                    // 操作成功但原版不弹窗(如删除):仍如实标记 Ok=true,只是 Kind=Silent
                    ? new OperationResult(true, string.Empty, string.Empty, FeedbackKind.Silent)
                    : new OperationResult(true, successTitle, result);
        } catch (Exception ex) {
            return new OperationResult(false, failureTitle,
                $"{failureTitle}{Environment.NewLine}{ex.InnerException?.Message ?? ex.Message}");
        } finally {
            Progress = OperationProgress.At(OperationStage.None);
            IsBusy = false;
            NotifyCounts();
        }
    }

    // —— 导入 ——
    // 原版四路导入统一走 RunBackgroundTask(..., Strings.Successful, Strings.Import_Failed),
    // 成功正文即 ImportManager 的返回串。

    public Task<OperationResult> ImportKindleClippingsAsync(string path) =>
        RunOperationAsync(() => _session!.ImportManager.ImportKindleClippings(path, ProgressReporter), true,
            Strings.Successful, Strings.Import_Failed);

    /// <summary>
    /// 从设备导入(设备 → 库)—— 对齐原版 <c>ImportFromKindle</c>(FrmMain.cs:1272):
    /// ① 把设备上的 My Clippings.txt 与 vocab.db 取到 &lt;Backups&gt;/Imports/(文件名带时间戳,
    ///    既当备份也便于追溯);② 再走合并导入(标注 + 生词)。
    /// 成功标题 Successful、失败 Import_Failed;取文件失败时把异常抛出去,
    /// 由统一契约呈现「失败标题 + 异常详情」。
    /// </summary>
    public Task<OperationResult> ImportFromDeviceAsync() {
        if (_session is not { } session) {
            return Task.FromResult(new OperationResult(false, Strings.Error, Strings.Ui_Status_OpenDatabaseFirst));
        }
        if (!session.DeviceManager.IsKindleConnected()) {
            return Task.FromResult(new OperationResult(false, Strings.Error, Strings.Ui_Status_NoDevice));
        }

        var importDir = Path.Combine(session.BackupDirectory, AppConstants.ImportsPathName);
        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        var clippingsFile = Path.Combine(importDir, "MyClippings_" + stamp + FileExtension.TXT);
        var wordsFile = Path.Combine(importDir, "vocab_" + stamp + FileExtension.DB);

        return RunOperationAsync(() => {
            Directory.CreateDirectory(importDir);
            // 设备→本地这一步此前完全没有进度(整文件传输,大库上有可感知耗时),现在按「第几个文件」上报
            if (!session.DeviceManager.ImportFilesFromDevice(clippingsFile, wordsFile, out var failure, ProgressReporter)) {
                throw failure ?? new InvalidOperationException(Strings.Import_Failed);
            }
            return session.ImportManager.Import(clippingsFile, wordsFile, ProgressReporter);
        }, true, Strings.Successful, Strings.Import_Failed);
    }

    public Task<OperationResult> ImportKindleWordsAsync(string path) =>
        RunOperationAsync(() => _session!.ImportManager.ImportKindleWords(path, ProgressReporter), true,
            Strings.Successful, Strings.Import_Failed);

    public Task<OperationResult> ImportKmDatabaseAsync(string path) =>
        RunOperationAsync(() => _session!.ImportManager.ImportKmDatabase(path, ProgressReporter), true,
            Strings.Successful, Strings.Import_Failed);

    /// <summary>导入另一个 Kindle Mate 2 数据库(本程序自己的库格式)。</summary>
    public Task<OperationResult> ImportKm2DatabaseAsync(string path) =>
        RunOperationAsync(() => _session!.ImportManager.ImportKm2Database(path, ProgressReporter), true,
            Strings.Successful, Strings.Import_Failed);

    public Task<OperationResult> ImportKmateDatabaseAsync(string path) =>
        RunOperationAsync(() => _session!.ImportManager.ImportKmateDatabase(path, ProgressReporter), true,
            Strings.Successful, Strings.Import_Failed);

    // —— 导出 ——
    // 注:原版 MenuExportMd_Click 在导出失败时是 **完全静默**(直接 return,不弹任何窗);
    // 用户 2026-09-13 明确要求失败也要弹窗,故此处按用户要求返回失败结果 ——
    // 这是**有意偏离原版**的一处,已在提交说明中标注。

    public Task<OperationResult> ExportAllMarkdownAsync() {
        if (_session is not { } session) {
            return Task.FromResult(new OperationResult(false, Strings.Error, Strings.Ui_Status_OpenDatabaseFirst));
        }
        var exportDir = session.ExportDirectory;
        return Task.Run(() => {
            var clippingsOk = session.ExportManager.ExportClippingsToMarkdown();
            var vocabsOk = session.ExportManager.ExportVocabsToMarkdown();
            if (!clippingsOk || !vocabsOk) {
                return new OperationResult(false, Strings.Failed, Strings.Ui_Result_NothingToExport);
            }
            return new OperationResult(true, Strings.Successful,
                Strings.Export_Successful + Strings.Open_Folder,
                FeedbackKind.OpenFolderPrompt, exportDir);
        });
    }

    /// <summary>导出当前选中书籍的标注(原版 MenuBooksExport_Click 的书页分支)。</summary>
    public Task<OperationResult> ExportCurrentBookMarkdownAsync() {
        if (_session is not { } session) {
            return Task.FromResult(new OperationResult(false, Strings.Error, Strings.Ui_Status_OpenDatabaseFirst));
        }
        var book = _selectedNav is { IsAll: false } nav ? nav.Key : string.Empty;
        var exportDir = session.ExportDirectory;
        return Task.Run(() => {
            if (!session.ExportManager.ExportClippingsToMarkdown(book)) {
                return new OperationResult(false, Strings.Failed, Strings.Ui_Result_NothingToExport);
            }
            return new OperationResult(true, Strings.Successful,
                Strings.Export_Successful + Strings.Open_Folder,
                FeedbackKind.OpenFolderPrompt, exportDir);
        });
    }

    // —— 维护 ——

    /// <summary>
    /// 备份数据库。严格对齐原版 <c>MenuBackup_Click</c>:
    /// 先无条件执行一次库文件备份(无提示)→ 无标注数据则提示「没有数据可备份」→
    /// 否则 <c>BackupClippings</c>,成功弹「备份完成! 需要打开文件夹吗?」(选是打开 Backups 目录),
    /// 失败弹 <c>Backup_Clippings_Failed</c> 错误框。
    /// </summary>
    public Task<OperationResult> BackupDatabaseAsync() {
        if (_session is not { } session) {
            return Task.FromResult(new OperationResult(false, Strings.Error, Strings.Ui_Status_OpenDatabaseFirst));
        }
        return Task.Run(() => {
            // 提示"打开文件夹"要指向**备份真正落地的那个目录**(session 的),而不是当前目录下的
            // Backups —— 用户从文件对话框打开了别处的库时,两者不是同一个地方。
            var backupPath = session.BackupDirectory;
            session.ExportManager.BackupDatabase();

            if (_allClippings.Count == 0) {
                return new OperationResult(false, Strings.Prompt, Strings.No_Data_To_Backup);
            }

            if (session.ExportManager.BackupClippings(out var exception)) {
                return new OperationResult(true, Strings.Successful,
                    Strings.Backup_Successful + Strings.Open_Folder,
                    FeedbackKind.OpenFolderPrompt, backupPath);
            }

            return new OperationResult(false, Strings.Error,
                MessageHelper.BuildMessage(Strings.Backup_Clippings_Failed, exception!));
        });
    }

    // —— 维护数据库(2026-09-22:由「清理数据库」+「清洗标注文本」合并而来) ——
    //
    // 合并的理由:两件事本来就是**同一条流水线** —— 导入路径已经在跑
    // HandleClippings(内含清洗) → CleanDatabase(收尾清理),而导入结果文案也早就把
    // 「本次新增」与「收尾清理删除」分开展示。手动入口却拆成两个菜单项,名字只差一个字
    // (清理 / 清洗),用户既分不清该点哪个、也不知道先后。
    //
    // 顺序**固定为 清洗 → 清理**,不可颠倒:清洗把内容归一化(去掉首尾噪音标点)⇒
    // 两条原本"只差一个首部标点"的标注会变成**完全相同** ⇒ 成为清理眼里的重复项,
    // 而清理的规则是"内容相同则**两行都删**"。所以"改了什么"与"删了什么"必须一起预演 ——
    // ScanDatabaseMaintenance 就是先在内存里投影出清洗结果、再在投影上判重。
    //
    // 合并顺带修掉一个反差:此前「清理」(删空条目 + 判重删行 + VACUUM)只弹一句
    // "建议先备份",而「清洗」(只改标点、一条不删)反而**无条件**备份。现在统一为无条件备份。
    //
    // 清洗规则本体在 Shared/Clippings/ClippingCleanRules.cs(抽出去才有单测,
    // 测试工程不引用 Avalonia)。导入时也会自动走一遍 —— 挂在 HandleClippings 里。

    /// <summary>
    /// 只读预演:清洗会改哪些、清理会删哪些。不写任何东西。
    /// 返回 null 表示无会话 / 正忙 / 读库失败 —— 这几种对用户是同一件事:这次做不了。
    /// </summary>
    public Task<DatabaseMaintenancePlan?> PreviewMaintenanceAsync() {
        if (_session is not { } session || IsBusy) {
            return Task.FromResult<DatabaseMaintenancePlan?>(null);
        }
        return Task.Run(() => session.Km2DatabaseService.ScanDatabaseMaintenance(out var plan) ? plan : null);
    }

    /// <summary>
    /// 执行「维护数据库」:无条件备份 → 清洗 → 清理 → 落盘清单。
    ///
    /// **先备份再动**:清洗改字、清理删行,两者在应用内都没有撤销路径 ——
    /// 备份 + 落盘清单是唯一的回头路,所以备份不放进"可选"里。
    /// </summary>
    public Task<OperationResult> MaintainDatabaseAsync() {
        if (_session is not { } session) {
            return Task.FromResult(new OperationResult(false, Strings.Error, Strings.Ui_Status_OpenDatabaseFirst));
        }
        if (_allClippings.Count <= 0) {
            return Task.FromResult(new OperationResult(false, Strings.Prompt, Strings.Database_Empty));
        }
        return RunOperationAsync(() => {
            session.ExportManager.BackupDatabase();

            // 执行前**重新**预演一次,而不是复用确认框那一份:清单要记录的是"这一遍到底动了什么",
            // 重算一遍才与紧随其后的两步同源。判重是 O(n) 分组 + 少量包含检查,相比后面的 VACUUM 可忽略。
            session.Km2DatabaseService.ScanDatabaseMaintenance(out var plan);

            // ① 清洗:只改首尾标点,一条都不删
            if (!session.Km2DatabaseService.CleanClippingTexts(out var cleaning, ProgressReporter)) {
                return string.Empty;
            }

            // ② 清理:删空条目 + 判重 + VACUUM。
            //    无事可做时 CleanDatabase 以哨兵值收场并返回 false —— 那是**正常结果**,不是失败。
            //    合并成一个入口后这条尤其要紧:"清洗有改动、清理无事可做"是最常见的情形。
            var cleaned = session.Km2DatabaseService.CleanDatabase(session.DatabasePath, out var cleanResult, ProgressReporter);
            if (!cleaned && !IsNoNeedCleaning(cleanResult)) {
                return string.Empty;
            }
            var countEmpty = cleanResult.TryGetValue(AppConstants.EmptyCount, out var e) ? e : "0";
            var countDuplicated = cleanResult.TryGetValue(AppConstants.DuplicatedCount, out var d) ? d : "0";
            var fileSizeDelta = cleanResult.TryGetValue(AppConstants.FileSizeDelta, out var f) ? f : "0";

            var manifestPath = WriteMaintenanceManifest(session, cleaning, plan);
            var message = string.Format(CultureInfo.CurrentCulture, Strings.Ui_Maintenance_Result_Format,
                cleaning.ChangedCount, cleaning.Scanned, countEmpty, countDuplicated,
                fileSizeDelta, string.IsNullOrEmpty(manifestPath) ? "-" : manifestPath);
            if (cleaning.AllPunctuationCount > 0) {
                message += Environment.NewLine + string.Format(CultureInfo.CurrentCulture,
                    Strings.Ui_ClippingClean_Skipped_Format, cleaning.AllPunctuationCount);
            }
            return message;
        }, true, Strings.Ui_Menu_MaintainDatabase, Strings.Ui_Maintenance_Failed);
    }

    /// <summary>
    /// 「清理无事可做」的判据:<c>CleanDatabase</c> 在空条目与重复项都为 0 时会以
    /// <see cref="AppConstants.DatabaseNoNeedCleaning"/> 这个哨兵值收场并返回 false。
    /// 那是正常结果,不是失败 —— 别让它把整个维护报成失败。
    /// </summary>
    private static bool IsNoNeedCleaning(Dictionary<string, string> result) =>
        result.TryGetValue(AppConstants.Exception, out var message) &&
        string.Equals(message, AppConstants.DatabaseNoNeedCleaning, StringComparison.Ordinal);

    /// <summary>
    /// 把这一遍动过的东西落成清单文件,返回文件路径(写失败返回空串)。
    ///
    /// 两段的来源刻意不同:清洗段用**执行结果**(真的写进库的才算数,与
    /// <c>CleanClippingTexts</c> 内部同源),清理段用**执行前的预演** ——
    /// <c>CleanDatabase</c> 只回报条数、不回报删了哪几行,键只能从预演里拿。
    ///
    /// 清单写失败**不该**让整个维护算失败 —— 数据那时已经改完了,报"维护失败"反而是谎话;
    /// 吞掉异常、只留日志,正文里路径位置显示 "-"。
    /// </summary>
    private static string WriteMaintenanceManifest(DatabaseSession session, ClippingCleanReport cleaning,
        DatabaseMaintenancePlan? plan) {
        try {
            Directory.CreateDirectory(session.BackupDirectory);
            // 时间戳显式走 InvariantCulture:字符串插值里的格式说明符默认用 CurrentCulture,
            // 非公历日历下年份会变成 2569/1405 之类,清单文件名就读不出日期了
            // (与 ClearAllDataAsync 里那个备份文件名同一个坑)。
            var stamp = DateTime.Now.ToString(AppConstants.BackupDateFormat, CultureInfo.InvariantCulture);
            var path = Path.Combine(session.BackupDirectory, $"Maintenance_{stamp}.txt");

            var removals = plan?.Cleanup.Removals ?? [];
            var lines = new List<string> {
                $"# 维护数据库清单 {stamp}",
                $"# 清洗:扫描 {cleaning.Scanned} 条,改动 {cleaning.ChangedCount} 条,整条皆标点而跳过 {cleaning.AllPunctuationCount} 条",
                "#"
            };
            // 多行内容压成一行显示,否则一条标注就能把清单撑散;正文里的换行用 \n 记号代替。
            lines.AddRange(cleaning.Changes.Select(change =>
                $"--- 清洗 {change.BookName} | {change.Key}{Environment.NewLine}" +
                $"改前: {change.Before.Replace(Environment.NewLine, "\\n")}{Environment.NewLine}" +
                $"改后: {change.After.Replace(Environment.NewLine, "\\n")}"));

            // 删行比改字更需要留痕 —— 改错了还能看出改成了什么,删错了只剩一个键。
            lines.Add("#");
            lines.Add($"# 清理:删除 {removals.Count} 条(空条目 {plan?.Cleanup.EmptyCount ?? 0} / 重复项 {plan?.Cleanup.DuplicatedCount ?? 0})");
            lines.AddRange(removals.Select(removal =>
                $"--- 删除[{ReasonLabel(removal.Reason)}] {removal.BookName} | {removal.Key}{Environment.NewLine}" +
                $"内容: {removal.Content.Replace(Environment.NewLine, "\\n")}"));

            File.WriteAllLines(path, lines, new UTF8Encoding(false));
            return path;
        } catch (Exception e) {
            AppLog.Write(StringHelper.GetExceptionMessage(nameof(WriteMaintenanceManifest), e));
            return string.Empty;
        }
    }

    /// <summary>清单里的删除原因。清单正文本来就是中文(改前/改后),这里保持一致。</summary>
    private static string ReasonLabel(DatabaseCleanRemovalReason reason) =>
        reason == DatabaseCleanRemovalReason.Empty ? "空条目" : "重复项";

    /// <summary>
    /// 重建数据库 —— 原版成功正文为「解析 N 条标注,导入 M 条标注」,
    /// 成功标题 <c>Rebuild_Database</c>、失败标题 <c>Rebuild_Database + Failed</c>。
    /// </summary>
    public Task<OperationResult> RebuildDatabaseAsync() {
        if (_session is not { } session) {
            return Task.FromResult(new OperationResult(false, Strings.Error, Strings.Ui_Status_OpenDatabaseFirst));
        }
        return RunOperationAsync(() => {
            if (!session.Km2DatabaseService.RebuildDatabase(out var result)) {
                return string.Empty;
            }
            var parsedCount = result.TryGetValue(AppConstants.ParsedCount, out var p) ? p : "0";
            var insertedCount = result.TryGetValue(AppConstants.InsertedCount, out var i) ? i : "0";
            return Strings.Parsed_X + Strings.Space + parsedCount + Strings.Space + Strings.X_Clippings +
                   Strings.Symbol_Comma + Strings.Imported_X + Strings.Space + insertedCount +
                   Strings.Space + Strings.X_Clippings;
        }, true, Strings.Rebuild_Database, Strings.Rebuild_Database + Strings.Failed);
    }

    /// <summary>
    /// 清空全部数据 —— 原版 <c>MenuClear_Click</c>:库为空时提示「数据库为空」(标题 Prompt);
    /// 成功正文 <c>Data_Cleared</c>(标题 Successful)、失败 <c>Clear_Failed</c>。
    /// 确认框由视图层弹出。
    /// </summary>
    public Task<OperationResult> ClearAllDataAsync() {
        if (_session is not { } session) {
            return Task.FromResult(new OperationResult(false, Strings.Error, Strings.Ui_Status_OpenDatabaseFirst));
        }
        if (_allClippings.Count <= 0) {
            return Task.FromResult(new OperationResult(false, Strings.Prompt, Strings.Database_Empty));
        }
        return RunOperationAsync(() => {
            Directory.CreateDirectory(session.BackupDirectory);
            // 时间戳显式走 InvariantCulture:字符串插值里的格式说明符默认用 CurrentCulture,
            // 非公历日历下年份会变成 2569/1405/1448 之类,清空前的这份保底备份就读不出日期了。
            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            var fileName = $"{Path.GetFileNameWithoutExtension(session.DatabasePath)}_{stamp}{Path.GetExtension(session.DatabasePath)}";
            File.Copy(session.DatabasePath, Path.Combine(session.BackupDirectory, fileName), true);
            return session.Km2DatabaseService.DeleteAllData() ? Strings.Data_Cleared : string.Empty;
        }, true, Strings.Successful, Strings.Clear_Failed);
    }

    // —— 删除 / 重命名 ——

    /// <summary>
    /// 删除左栏当前节点 —— 对齐原版 <c>DeleteNodes()</c>(FrmMain.cs:1019):
    /// 按当前页分发(标注页 → 书籍节点;生词页 → 生词节点)。
    /// 原版由两棵树的 **Delete 键** 与右键菜单「删除」触发。
    /// </summary>
    public Task<OperationResult> DeleteCurrentNavNodeAsync() {
        if (_session is not { } session) {
            return Task.FromResult(new OperationResult(false, Strings.Error, Strings.Ui_Status_OpenDatabaseFirst));
        }
        if (_selectedNav is not { } nav) {
            return Task.FromResult(new OperationResult(false, Strings.Error, Strings.Ui_Status_NoSelection));
        }
        return IsClipDomain ? DeleteBookNodeAsync(session, nav) : DeleteWordNodeAsync(session, nav);
    }

    /// <summary>
    /// 删除「全部标注」或某一本书的全部标注 —— 对齐原版 <c>DeleteBookNodes()</c>(FrmMain.cs:1032)。
    /// **成功不弹窗**(原版只有 deletedCount == 0 时才报 Delete_Failed)。
    /// ⚠️ **有意偏离原版**:原版此处会连 `original_clipping_lines` 一起删,本版**保留原始行**,
    /// 使被删条目进入回收站、可恢复(回收站功能见 GetDeletedOriginalLines/RestoreFromOriginalLine)。
    /// </summary>
    private Task<OperationResult> DeleteBookNodeAsync(DatabaseSession session, NavItem nav) {
        var bookName = nav.Key;
        return RunOperationAsync(() => {
            if (nav.IsAll) {
                session.ClippingService.DeleteAllClippings();
                return "-";
            }

            var clippings = session.ClippingService.GetClippingsByBookName(bookName);
            var deleted = 0;
            foreach (var clipping in clippings) {
                if (session.ClippingService.DeleteClipping(clipping.Key)) deleted++;
            }
            return deleted == 0 ? string.Empty : "-";
        }, true, string.Empty, Strings.Delete_Failed, silentOnSuccess: true);
    }

    /// <summary>
    /// 删除「全部生词」或某个词的全部查询 —— 对齐原版 <c>DeleteWordNodes()</c>(FrmMain.cs:1062)。
    /// 原版判定:仅当「删 Vocab」与「删 Lookup」**都失败**时才报 Delete_Failed;
    /// 且按 <c>Word</c> 找**第一个**匹配的 WordKey(原版行为,保持一致)。
    /// </summary>
    private Task<OperationResult> DeleteWordNodeAsync(DatabaseSession session, NavItem nav) {
        var word = nav.Key;
        return RunOperationAsync(() => {
            if (nav.IsAll) {
                session.VocabService.DeleteAllVocabs();
                return "-";
            }

            var wordKey = session.VocabService.GetAllVocabs()
                .FirstOrDefault(v => string.Equals(v.Word, word, StringComparison.Ordinal))?.WordKey;
            if (wordKey == null) return string.Empty;

            var vocabDeleted = session.VocabService.DeleteVocabByWordKey(wordKey);
            var lookupDeleted = session.LookupService.DeleteLookup(wordKey);
            return vocabDeleted || lookupDeleted ? "-" : string.Empty;
        }, true, string.Empty, Strings.Delete_Failed, silentOnSuccess: true);
    }

    // —— 回收站(2026-09-13 新增;原版无此概念) ——
    //
    // 回收站 = 「原始行仍在、clippings 里已不存在」的条目,与原版"已删除 N 条"同一口径。
    // 显示到主列表(复用 Items/ClipTable),不改数据;恢复/彻底删除分别见下面两个方法。

    private bool _isRecycleBinView;

    /// <summary>当前是否正在查看回收站(而非正常的标注 / 生词列表)。</summary>
    public bool IsRecycleBinView {
        get => _isRecycleBinView;
        private set {
            if (_isRecycleBinView == value) return;
            _isRecycleBinView = value;
            OnPropertyChanged();
            // 标题依赖它(回收站视图下显示「回收站」而不是左栏那个节点名)。
            OnPropertyChanged(nameof(HeaderTitle));
        }
    }

    /// <summary>把回收站内容(仅显示)载入主列表。不触发 reload,避免被常规重建覆盖。</summary>
    public async Task<OperationResult> LoadRecycleBinAsync() {
        if (_session is not { } session) {
            return new OperationResult(false, Strings.Error, Strings.Ui_Status_OpenDatabaseFirst);
        }
        if (IsBusy) return OperationResult.Silent;

        IsBusy = true;
        Progress = OperationProgress.At(OperationStage.Preparing);
        try {
            // 后台构建,再回 UI 线程一次性替换集合(整批替换,避免逐条界面通知)
            var (items, table) = await Task.Run(() => {
                var deleted = session.Km2DatabaseService.GetDeletedOriginalLines();
                var list = new List<ListItem>(deleted.Count);
                var clips = new List<Clipping>(deleted.Count);
                foreach (var line in deleted) {
                    var header = MyClippingsHelper.ParseTitleAndAuthor(line.Line1 ?? string.Empty);
                    var clip = new Clipping {
                        Key = line.Key,
                        Content = line.Line4 ?? string.Empty,
                        BookName = header.Title,
                        AuthorName = header.Author
                    };
                    clips.Add(clip);
                    list.Add(ToListItem(clip));
                }
                return (list, clips);
            });

            Items.ReplaceAll(items);
            ClipTable.ReplaceAll(table);
            SelectedItem = Items.FirstOrDefault();
            SelectedClipTable = ClipTable.FirstOrDefault();

            // 回收站不是"某本书的列表":左栏那个高亮继续挂在书上会撒谎,
            // 更要紧的是 —— 留着高亮会让"再点一次这本书"变成**无变化**事件
            // (SelectedNav 的 setter 对同一实例直接 return,不会重建列表),
            // 用户就出不来了;清掉之后任何一次节点点击都是一次真正的切换。
            //
            // 这里直接改字段而**不走 setter**:setter 会顺带 ApplyFilter(),
            // 把常规列表整份重建一遍(实测库 5752 行)再被下面的回收站内容整个替换掉 —— 纯浪费。
            // 代价是得手动补上 setter 里还需要的那条通知(左栏列表靠它清掉选中)。
            _selectedNav = null;
            OnPropertyChanged(nameof(SelectedNav));

            IsRecycleBinView = true;
            return OperationResult.Silent;
        } finally {
            Progress = OperationProgress.At(OperationStage.None);
            IsBusy = false;
            NotifyCounts();
        }
    }

    /// <summary>恢复回收站中选中的那一条(重新插回 clippings;原始行原本就在,故可反复恢复)。</summary>
    public Task<OperationResult> RestoreSelectedFromRecycleBinAsync() {
        if (_session is not { } session) {
            return Task.FromResult(new OperationResult(false, Strings.Error, Strings.Ui_Status_OpenDatabaseFirst));
        }
        var key = SelectedClippingKey;
        if (key.Length == 0) {
            return Task.FromResult(new OperationResult(false, Strings.Error, Strings.Ui_Status_NoSelection));
        }
        return RunOperationAsync(() => {
            var line = session.OriginalClippingLineService.GetOriginalClippingLineByKey(key);
            if (line == null) return string.Empty;
            return session.Km2DatabaseService.RestoreFromOriginalLine(line) ? Strings.Restored : string.Empty;
        }, true, Strings.Successful, Strings.Restore_Failed);
    }

    /// <summary>清空回收站 —— 彻底删除其中全部条目(不影响仍在使用的数据)。</summary>
    public Task<OperationResult> PurgeRecycleBinAsync() {
        if (_session is not { } session) {
            return Task.FromResult(new OperationResult(false, Strings.Error, Strings.Ui_Status_OpenDatabaseFirst));
        }
        return RunOperationAsync(() => {
            var keys = session.Km2DatabaseService.GetDeletedOriginalLines().Select(l => l.Key).ToList();
            if (keys.Count == 0) return string.Empty;
            var removed = session.Km2DatabaseService.PurgeDeletedOriginalLines(keys);
            return removed > 0 ? "-" : string.Empty;
        }, true, string.Empty, Strings.Delete_Failed, silentOnSuccess: true);
    }

    /// <summary>
    /// 删除选中的标注 / 查询 —— 原版语义:确认框由视图层弹,**成功不弹窗**,
    /// 只有失败才弹 <c>Delete_Failed</c>(故成功时返回 Silent)。
    /// </summary>
    public Task<OperationResult> DeleteSelectedAsync() {
        if (_session is not { } session) {
            return Task.FromResult(new OperationResult(false, Strings.Error, Strings.Ui_Status_OpenDatabaseFirst));
        }
        if (_selectedItem?.Clipping is { } clip) {
            var key = clip.Key;
            return RunOperationAsync(() => session.ClippingService.DeleteClipping(key) ? "-" : string.Empty,
                true, string.Empty, Strings.Delete_Failed, silentOnSuccess: true);
        }
        if (_selectedItem?.Lookup is { } lookup) {
            var wordKey = lookup.WordKey ?? string.Empty;
            var timestamp = lookup.Timestamp ?? string.Empty;
            return RunOperationAsync(() => session.LookupRepository.Delete(wordKey, timestamp) ? "-" : string.Empty,
                true, string.Empty, Strings.Delete_Failed, silentOnSuccess: true);
        }
        return Task.FromResult(new OperationResult(false, Strings.Error, Strings.Ui_Status_NoSelection));
    }

    /// <summary>当前选中的是「查询(生词)」而非「标注」—— 决定删除确认用哪条文案。</summary>
    public bool IsLookupSelected => _selectedItem?.Lookup != null;

    public bool CanRenameCurrentBook => _selectedNav is { IsAll: false };

    public string CurrentBookName => _selectedNav is { IsAll: false } nav ? nav.Key : string.Empty;

    /// <summary>当前选中书籍的作者(重命名对话框的初值)。</summary>
    public string CurrentBookAuthor =>
        _selectedNav is { IsAll: false } nav ? GetBookAuthor(nav.Key) : string.Empty;

    /// <summary>取某本书的作者 —— 取第一行,与原版 <c>resultRows[0].AuthorName</c> 一致。</summary>
    public string GetBookAuthor(string bookName) =>
        _allClippings.FirstOrDefault(c => string.Equals(c.BookName, bookName, StringComparison.Ordinal))?.AuthorName
        ?? string.Empty;

    /// <summary>
    /// 该书名是否已被**别的**书占用(原版据此弹「同名合并」确认)。
    ///
    /// <paramref name="exceptBookName"/> 必须传「当前正操作的那本书原来的名字」:
    /// 书单里必然含这本书自己的行,不排除时「只改作者、书名不动」会拿自己撞自己,
    /// 凭空弹「同名书籍已存在」。判定本身在 <see cref="BookRenameRules"/> 里(可测)。
    /// </summary>
    public bool IsBookNameTaken(string bookName, string? exceptBookName = null) =>
        BookRenameRules.IsNameTakenByOther(_allClippings.Select(c => c.BookName), bookName, exceptBookName);

    /// <summary>
    /// 重命名书籍 —— 成功标题 Successful、正文 <c>Books_Renamed</c>;失败 <c>Book_Renamed_Failed</c>。
    /// 对齐原版:书名与作者**都要**改,且**生词本与标注两处都要**改 ——
    /// 此前只调了 ClippingService,导致改完书名后生词本里仍显示旧书名(见原版 FrmMain.cs:1196-1197)。
    /// </summary>
    public Task<OperationResult> RenameCurrentBookAsync(string newName, string newAuthor) {
        if (_session is not { } session || _selectedNav is not { IsAll: false } nav) {
            return Task.FromResult(new OperationResult(false, Strings.Prompt, Strings.Ui_Status_PickBookFirst));
        }
        var oldName = nav.Key;
        return RunOperationAsync(() => {
            session.LookupService.RenameBook(oldName, newName, newAuthor);
            return session.ClippingService.RenameBook(oldName, newName, newAuthor)
                ? Strings.Books_Renamed
                : string.Empty;
        }, true, Strings.Successful, Strings.Book_Renamed_Failed);
    }

    // —— 设备 ——

    /// <summary>
    /// 探测设备状态(可后台线程调用,不触碰绑定属性)。
    /// 同时返回"是否已连接",供菜单栏「Kindle设备已连接」按钮的显隐使用 ——
    /// 文案与连接判定出自同一次探测,避免再探一次设备或靠文案字符串反推状态。
    /// </summary>
    public (string Text, bool Connected) ProbeDevice() {
        if (_session == null) return (Strings.Ui_Status_DeviceOffline, false);
        try {
            if (!_session.DeviceManager.IsKindleConnected()) return (Strings.Ui_Status_DeviceOffline, false);
            var drive = _session.DeviceManager.DriveLetter;
            var text = string.IsNullOrWhiteSpace(drive)
                ? Strings.Ui_Status_DeviceOnline
                : string.Format(CultureInfo.CurrentCulture, Strings.Ui_Status_DeviceOnlineDrive, drive);
            return (text, true);
        } catch {
            return (Strings.Ui_Status_DeviceOffline, false);
        }
    }

    /// <summary>仅取状态文案(自检用;内部复用 <see cref="ProbeDevice"/>)。</summary>
    public string ProbeDeviceStatus() => ProbeDevice().Text;

    // ————————————————————————— 检查更新 —————————————————————————

    /// <summary>最近一次检查发现的可用更新;null 表示没有更新(或尚未检查过)。</summary>
    private UpdateInfo? _availableUpdate;

    /// <summary>是否有可用更新 —— 主界面那个「更新」按钮的显隐依据。</summary>
    public bool IsUpdateAvailable => _availableUpdate is not null;

    /// <summary>更新按钮的文案,如「有新版本 2026.09.17」。</summary>
    public string UpdateButtonText => _availableUpdate is null
        ? string.Empty
        : string.Format(CultureInfo.CurrentCulture, Strings.Ui_Update_Available, _availableUpdate.Version);

    /// <summary>当前应用版本(取自程序集,形如 <c>2026.9.17.0</c>),用于与发布页的 tag 比较。</summary>
    public static string CurrentVersion =>
        typeof(MainWindowViewModel).Assembly.GetName().Version?.ToString() ?? "0.0.0";

    /// <summary>
    /// 检查更新。**本方法不弹窗** —— 只把结论文本返回给调用方(菜单与按钮各自决定怎么呈现),
    /// 有更新时顺手点亮主界面的「更新」按钮。检查失败与"已是最新"对用户是同一种结果,
    /// 细节只进日志(见 <see cref="UpdateChecker"/>):不该因为连不上 GitHub 就弹个错误框。
    /// </summary>
    public async Task<string> CheckForUpdatesAsync() {
        var info = await UpdateChecker.CheckAsync(CurrentVersion).ConfigureAwait(true);

        _availableUpdate = info;
        OnPropertyChanged(nameof(IsUpdateAvailable));
        OnPropertyChanged(nameof(UpdateButtonText));

        if (info is null) {
            return Strings.Ui_Update_UpToDate;
        }

        // 发布页没有对本平台资产时(例如某次只发了部分平台),仍要告知有新版本,只是没有一键下载的入口
        return info.Asset is null
            ? $"{UpdateButtonText} —— {info.ReleaseUrl}"
            : UpdateButtonText;
    }

    /// <summary>
    /// 下载并启动替换脚本。返回 <c>Restart = true</c> 表示**调用方应当立即退出进程**:
    /// 脚本正在等我们退出,退出之后它才会替换文件并重新启动。
    ///
    /// 之所以把"退出"留给调用方(视图)而不是在这里 <c>Environment.Exit</c>:
    /// 进程级动作放在壳里,VM 只管业务结论 —— 与既有的「重启」菜单项一致。
    /// </summary>
    public async Task<(bool Restart, string Message)> DownloadAndApplyUpdateAsync() {
        if (_availableUpdate is not { } update || update.Asset is not { } asset) {
            return (false, Strings.Ui_Update_UpToDate);
        }

        try {
            var progress = new Progress<double>(fraction =>
                StatusText = string.Format(CultureInfo.CurrentCulture, Strings.Ui_Update_Downloading,
                    (int)Math.Round(fraction * 100)));

            var target = UpdateInstaller.TargetForCurrentPlatform();
            var prepared = await UpdateInstaller.PrepareAsync(asset, target, progress).ConfigureAwait(true);

            // 以 .app 包启动时才可能自动替换(开发期直接跑 dll 不具备这个前提)
            var executable = Environment.ProcessPath ?? string.Empty;
            UpdateInstaller.ApplyAndRestart(prepared, executable, Environment.ProcessId);

            StatusText = Strings.Ui_Update_Restarting;
            return (true, Strings.Ui_Update_Restarting);
        } catch (Exception ex) {
            KindleMate2.Shared.Diagnostics.AppLog.Write(ex);   // 与本文件其它位置一致的全限定写法
            var message = string.Format(CultureInfo.CurrentCulture, Strings.Ui_Update_ApplyFailed, ex.Message);
            StatusText = message;
            return (false, message);
        }
    }

    private bool _isDeviceConnected;

    /// <summary>
    /// 设备是否已连接 —— 菜单栏「Kindle设备已连接」按钮的显隐依据(对齐原版 menuKindle),
    /// 同时也是「同步到 Kindle 设备」菜单项的**启用条件**(未连接时置灰)。
    /// </summary>
    public bool IsDeviceConnected {
        get => _isDeviceConnected;
        set {
            if (_isDeviceConnected == value) return;
            _isDeviceConnected = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SyncToDeviceHint));
        }
    }

    /// <summary>
    /// 「同步到 Kindle 设备」菜单项的悬停提示。菜单项不可用时它是**唯一的解释来源**
    /// —— 灰掉的项点不动,原先"点了才在状态栏说明原因"的路子就断了:
    /// 没有会话 → 先说开库;有会话但没连设备 → 说去连设备。
    /// 设备已连接时给的是设备状态(含盘符),顺带说明这次同步会落到哪个盘。
    /// </summary>
    public string SyncToDeviceHint => !HasSession
        ? Strings.Ui_Status_OpenDatabaseFirst
        : IsDeviceConnected
            ? DeviceStatus
            : Strings.Ui_Menu_SyncToDevice_NeedsDevice;

    /// <summary>在 UI 线程刷新设备状态(文案 + 按钮显隐)。</summary>
    public void RefreshDeviceStatus() {
        var probe = ProbeDevice();
        DeviceStatus = probe.Text;
        IsDeviceConnected = probe.Connected;
    }

    /// <summary>同步到设备 —— 原版:确认框(视图层)后,成功 <c>Sync_Successful</c>;失败弹 <c>Sync_Failed</c>。</summary>
    public Task<OperationResult> SyncToDeviceAsync() {
        if (_session is not { } session) {
            return Task.FromResult(new OperationResult(false, Strings.Error, Strings.Ui_Status_OpenDatabaseFirst));
        }
        return RunOperationAsync(() => {
            session.ExportManager.SyncToKindle();
            return Strings.Sync_Successful;
        }, false, Strings.Successful, Strings.Sync_Failed);
    }

    // —— 启动时的备份确认(对齐原版 FrmMain_Load 第 242-261 行的条件链) ——

    /// <summary>
    /// 启动时是否需要询问「从备份恢复 / 删除备份」。条件链严格照搬原版:
    /// 库文件存在 → 库为空 → Backups 目录存在 → Backups/KM2.dat 存在 → 该文件 ≥ 20KB。
    /// 返回 (是否需要询问, 备份文件路径)。
    /// </summary>
    public (bool Ask, string BackupFile) CheckStartupBackup() {
        var databasePath = AppPaths.DatabasePath;
        if (!File.Exists(databasePath)) return (false, string.Empty);
        if (_allClippings.Count > 0) return (false, string.Empty);

        var backupDir = AppPaths.BackupsDirectory;
        if (!Directory.Exists(backupDir)) return (false, string.Empty);

        var backupFile = Path.Combine(backupDir, AppConstants.DatabaseFileName);
        if (!File.Exists(backupFile)) return (false, string.Empty);
        if (new FileInfo(backupFile).Length / 1024 < 20) return (false, string.Empty);

        return (true, backupFile);
    }

    /// <summary>从备份恢复库文件(原版为 <c>File.Copy(backup, db, true)</c>;实际生效在下次启动)。</summary>
    public void RestoreFromBackup(string backupFile) {
        var databasePath = AppPaths.DatabasePath;
        File.Copy(backupFile, databasePath, true);
    }

    /// <summary>删除备份文件(原版为 <c>File.Delete</c>)。</summary>
    public void DeleteBackup(string backupFile) => File.Delete(backupFile);

    // —— 编辑标注(对齐原版 ShowContentEditDialog) ——

    /// <summary>当前选中的标注键(供编辑入口取用);无选中或非标注时为空。</summary>
    public string SelectedClippingKey => _selectedItem?.Clipping?.Key ?? string.Empty;

    /// <summary>当前选中标注的正文(输入框初值)。</summary>
    public string SelectedClippingContent => _selectedItem?.Clipping?.Content ?? string.Empty;

    /// <summary>
    /// 保存编辑后的标注正文。对齐原版:更新 <c>clippings.content</c>,
    /// 并同步更新 <c>original_clipping_lines.line4</c>(存在该行时)。
    /// 成功标题 <c>Successful</c>、正文 <c>Clippings_Revised</c>;失败弹 <c>Clippings_Revised_Failed</c>。
    /// </summary>
    public Task<OperationResult> SaveClippingContentAsync(string key, string newContent) {
        if (_session is not { } session) {
            return Task.FromResult(new OperationResult(false, Strings.Error, Strings.Ui_Status_OpenDatabaseFirst));
        }
        return RunOperationAsync(() => {
            var clipping = session.ClippingService.GetClippingByKey(key);
            if (clipping == null) return string.Empty;
            clipping.Content = newContent;
            if (!session.ClippingService.UpdateClipping(clipping)) return string.Empty;

            var originalLine = session.OriginalClippingLineService.GetOriginalClippingLineByKey(key);
            if (originalLine != null) {
                originalLine.Line4 = newContent;
                session.OriginalClippingLineService.UpdateOriginalClippingLine(originalLine);
            }
            return Strings.Clippings_Revised;
        }, true, Strings.Successful, Strings.Clippings_Revised_Failed);
    }

    private void ResetCollections() {
        NavItems.Clear();
        Items.Clear();
        ClipTable.Clear();
        LookupTable.Clear();
        _allClippings = new List<Clipping>();
        _allLookups = new List<Lookup>();
        _allVocabs = new List<Vocab>();
        _originLineCount = 0;
        _distinctWordCount = 0;
        _noteHighlightMap.Clear();
        _vocabByWordKey.Clear();
        _selectedNav = null;
        _selectedItem = null;
        _selectedClipTable = null;
        _selectedLookupTable = null;
        Detail = DetailModel.Empty;
        OnPropertyChanged(nameof(SelectedNav));
        OnPropertyChanged(nameof(SelectedItem));
        OnPropertyChanged(nameof(SelectedClipTable));
        OnPropertyChanged(nameof(SelectedLookupTable));
    }

    private void IndexVocabs() {
        foreach (var vocab in _allVocabs) {
            if (!string.IsNullOrWhiteSpace(vocab.WordKey)) {
                _vocabByWordKey[vocab.WordKey!] = vocab;
            }
        }
    }

    /// <summary>笔记型标注需要配对的划线内容(按 书名 + 页码 建索引,避免逐次查库)。</summary>
    private void IndexNoteHighlights() {
        foreach (var clip in _allClippings) {
            if (clip.BriefType != (long)BriefType.Highlight) continue;
            var key = NoteKey(clip.BookName ?? string.Empty, clip.PageNumber ?? 0);
            if (!_noteHighlightMap.ContainsKey(key)) {
                _noteHighlightMap[key] = clip.Content;
            }
        }
    }

    /// <summary>把 Vocab 的词干 / 词频回填到 Lookup。</summary>
    private void EnrichLookups() {
        foreach (var lookup in _allLookups) {
            if (lookup.WordKey == null) continue;
            if (_vocabByWordKey.TryGetValue(lookup.WordKey, out var vocab)) {
                lookup.Stem = vocab.Stem ?? string.Empty;
                lookup.Frequency = vocab.Frequency?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            }
        }
    }

    private static string NoteKey(string bookName, int pageNumber) =>
        string.Concat(bookName, "\u0001", pageNumber.ToString(CultureInfo.InvariantCulture));

    // —— 导航 ——

    private void RebuildNav() {
        NavItems.Clear();
        if (IsClipDomain) {
            NavItems.Add(new NavItem { Key = string.Empty, Name = Strings.Ui_Header_AllClippings, IsAll = true, Count = _allClippings.Count });
            var groups = _allClippings
                .Where(c => !string.IsNullOrWhiteSpace(c.BookName))
                .GroupBy(c => c.BookName!, StringComparer.Ordinal)
                .Select(g => new NavItem { Key = g.Key, Name = g.Key, Count = g.Count() })
                .OrderBy(x => x.Name, StringComparer.CurrentCulture);
            foreach (var item in groups) NavItems.Add(item);
        } else {
            NavItems.Add(new NavItem { Key = string.Empty, Name = Strings.Ui_Header_AllWords, IsAll = true, Count = _allLookups.Count });
            var groups = _allVocabs
                .Where(v => !string.IsNullOrWhiteSpace(v.Word))
                .GroupBy(v => v.Word, StringComparer.OrdinalIgnoreCase)
                .Select(g => new NavItem {
                    Key = g.Key,
                    Name = g.Key,
                    Count = _allLookups.Count(l => string.Equals(l.Word, g.Key, StringComparison.OrdinalIgnoreCase))
                })
                .OrderBy(x => x.Name, StringComparer.CurrentCulture)
                .ToList();
            _distinctWordCount = groups.Count;
            foreach (var item in groups) NavItems.Add(item);
        }

        SelectedNav = NavItems.FirstOrDefault();
        OnPropertyChanged(nameof(NavSectionTitle));
        OnPropertyChanged(nameof(NavSectionCount));
        ApplyFilter();
    }

    // —— 过滤 / 列表重建 ——

    public void ApplyFilter() {
        // 走到这里就意味着**主列表要被重建成常规内容**(筛选 / 排序 / 换节点 / 换域 / 重载)——
        // 回收站那批合成行马上就被换掉了,所以"正在看回收站"必须跟着复位。
        //
        // 此前它只置 true、从无置 false(全仓库仅 LoadRecycleBinAsync 一处赋值),
        // 于是看过一次回收站之后:「列表右键菜单永久变成『恢复』(『删除』再也不出现)」
        // 且「管理 → 清空回收站」永久可见。
        //
        // 放在这个**唯一漏斗**上,而不是逐个调用方去补:ApplyFilter 的调用方
        // (SearchText / SearchType / SortDescending / SelectedNav / RebuildNav)正是
        // "用户要求看常规列表"的全部入口,漏掉任何一个都会留下同一个 bug 的变体。
        IsRecycleBinView = false;

        if (IsClipDomain) RebuildClippings();
        else RebuildLookups();

        OnPropertyChanged(nameof(HeaderTitle));
        OnPropertyChanged(nameof(HeaderSubtitle));
        OnPropertyChanged(nameof(StatusLeft));
        OnPropertyChanged(nameof(StatusRight));
        OnPropertyChanged(nameof(NavSectionTitle));
        OnPropertyChanged(nameof(NavSectionCount));
    }

    private void RebuildClippings() {
        IEnumerable<Clipping> query = _allClippings;
        if (_selectedNav is { IsAll: false } nav) {
            query = query.Where(c => string.Equals(c.BookName, nav.Key, StringComparison.Ordinal));
        }
        var keyword = _searchText.Trim();
        if (keyword.Length > 0) {
            var k = keyword;
            var type = _searchType;
            query = query.Where(c => MatchClipping(c, k, type));
        }

        var ordered = (_sortDescending
            ? query.OrderByDescending(c => c.ClippingDate, StringComparer.Ordinal)
            : query.OrderBy(c => c.ClippingDate, StringComparer.Ordinal)).ToList();

        // 整批替换;逐条 Add 会产生与条目数同量级的界面通知,数千条时足以卡死 UI 线程
        var items = new List<ListItem>(ordered.Count);
        foreach (var clip in ordered) {
            items.Add(ToListItem(clip));
        }
        Items.ReplaceAll(items);
        ClipTable.ReplaceAll(ordered);
        SelectedItem = Items.FirstOrDefault();
        SelectedClipTable = ClipTable.FirstOrDefault();
        if (SelectedItem == null) Detail = DetailModel.Empty;
    }

    private void RebuildLookups() {
        IEnumerable<Lookup> query = _allLookups;
        if (_selectedNav is { IsAll: false } nav) {
            query = query.Where(l => string.Equals(l.Word, nav.Key, StringComparison.OrdinalIgnoreCase));
        }
        var keyword = _searchText.Trim();
        if (keyword.Length > 0) {
            var k = keyword;
            query = query.Where(l =>
                (l.Word?.Contains(k, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (l.Usage?.Contains(k, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (l.Title?.Contains(k, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        var ordered = (_sortDescending
            ? query.OrderByDescending(l => l.Timestamp, StringComparer.Ordinal)
            : query.OrderBy(l => l.Timestamp, StringComparer.Ordinal)).ToList();

        // 整批替换;逐条 Add 会产生与条目数同量级的界面通知
        var items = new List<ListItem>(ordered.Count);
        foreach (var lookup in ordered) {
            items.Add(ToListItem(lookup));
        }
        Items.ReplaceAll(items);
        LookupTable.ReplaceAll(ordered);
        SelectedItem = Items.FirstOrDefault();
        SelectedLookupTable = LookupTable.FirstOrDefault();
        if (SelectedItem == null) Detail = DetailModel.Empty;
    }

    private static bool MatchClipping(Clipping c, string k, string type) {
        bool Hit(string? s) => !string.IsNullOrEmpty(s) && s.Contains(k, StringComparison.OrdinalIgnoreCase);
        if (type == Strings.Ui_Search_Type_Books) return Hit(c.BookName);
        if (type == Strings.Ui_Search_Type_Author) return Hit(c.AuthorName);
        if (type == Strings.Ui_Search_Type_Content) return Hit(c.Content);
        if (type == Strings.Ui_Search_Type_Note) {
            return c.BriefType == (long)BriefType.Note && (Hit(c.Content) || Hit(c.BookName));
        }
        return Hit(c.Content) || Hit(c.BookName) || Hit(c.AuthorName);
    }

    private static ListItem ToListItem(Clipping clip) {
        var (typeText, kind) = TypeTextMap.Of(clip.BriefType);
        return new ListItem {
            Key = clip.Key,
            Primary = Flatten(clip.Content),
            Book = clip.BookName ?? string.Empty,
            Place = (clip.PageNumber ?? 0) > 0
                ? string.Format(CultureInfo.CurrentCulture, Strings.Ui_Text_Page, clip.PageNumber ?? 0)
                : string.Empty,
            Time = clip.ClippingDate ?? string.Empty,
            TypeText = typeText,
            Kind = kind,
            Clipping = clip
        };
    }

    private ListItem ToListItem(Lookup lookup) {
        var wordKey = lookup.WordKey ?? string.Empty;
        string stem = string.Empty, frequency = string.Empty;
        if (wordKey.Length > 0 && _vocabByWordKey.TryGetValue(wordKey, out var vocab)) {
            stem = vocab.Stem ?? string.Empty;
            frequency = vocab.Frequency is > 0 ? vocab.Frequency.Value.ToString(CultureInfo.InvariantCulture) : string.Empty;
        }
        var extra = new List<string>();
        if (stem.Length > 0 && !string.Equals(stem, lookup.Word, StringComparison.OrdinalIgnoreCase)) {
            extra.Add(string.Format(CultureInfo.CurrentCulture, Strings.Ui_Text_Stem, stem));
        }
        if (frequency.Length > 0) {
            extra.Add(string.Format(CultureInfo.CurrentCulture, Strings.Ui_Text_Frequency, frequency));
        }

        var usage = Flatten(lookup.Usage ?? string.Empty);
        return new ListItem {
            Key = string.Concat(wordKey, "\u0001", lookup.Timestamp ?? string.Empty),
            Primary = usage.Length > 0 ? usage : lookup.Word,
            Book = lookup.Title ?? string.Empty,
            Time = lookup.Timestamp ?? string.Empty,
            Extra = string.Join(" · ", extra),
            TypeText = string.Empty,
            Kind = TypeKind.None,
            Lookup = lookup,
            LookupWordKey = wordKey
        };    }

    private static string Flatten(string? text) =>
        string.IsNullOrEmpty(text)
            ? string.Empty
            : text.Replace(AppConstants.SpaceForNewLine, " ").Replace("\r", " ").Replace("\n", " ").Trim();

    // —— 详情面板 ——

    private void RebuildDetailFromListItem() {
        if (_selectedItem == null) {
            Detail = DetailModel.Empty;
            return;
        }
        Detail = _selectedItem.Clipping != null
            ? BuildClippingDetail(_selectedItem.Clipping)
            : _selectedItem.Lookup != null
                ? BuildVocabDetail(_selectedItem.Lookup)
                : DetailModel.Empty;
    }

    private DetailModel BuildClippingDetail(Clipping clip) {
        var (typeText, kind) = TypeTextMap.Of(clip.BriefType);
        var place = (clip.PageNumber ?? 0) > 0
            ? string.Format(CultureInfo.CurrentCulture, Strings.Ui_Text_Page, clip.PageNumber ?? 0)
            : string.Empty;
        var subtitle = string.Join(" · ", new[] { clip.AuthorName ?? string.Empty, place }
            .Where(s => s.Length > 0));

        var model = new DetailModel {
            HasSelection = true,
            Title = clip.BookName ?? string.Empty,
            Subtitle = subtitle,
            TypeText = typeText,
            Kind = kind,
            Time = clip.ClippingDate ?? string.Empty
        };

        if (kind == TypeKind.Note) {
            var key = NoteKey(clip.BookName ?? string.Empty, clip.PageNumber ?? 0);
            var quote = _noteHighlightMap.TryGetValue(key, out var value) ? value : string.Empty;
            return new DetailModel {
                HasSelection = true,
                Title = model.Title,
                Subtitle = model.Subtitle,
                TypeText = model.TypeText,
                Kind = kind,
                Time = model.Time,
                HasQuote = quote.Length > 0,
                QuoteLabel = Strings.Ui_Type_Highlight,
                Quote = quote,
                HasNote = true,
                NoteLabel = Strings.Ui_Type_Note,
                Note = clip.Content
            };
        }

        return new DetailModel {
            HasSelection = true,
            Title = model.Title,
            Subtitle = model.Subtitle,
            TypeText = model.TypeText,
            Kind = kind,
            Time = model.Time,
            HasBody = true,
            Body = clip.Content
        };
    }

    private DetailModel BuildVocabDetail(Lookup seed) {
        var word = seed.Word;
        var wordKey = seed.WordKey ?? string.Empty;
        string stem = string.Empty, frequency = string.Empty;
        if (wordKey.Length > 0 && _vocabByWordKey.TryGetValue(wordKey, out var vocab)) {
            stem = vocab.Stem ?? string.Empty;
            frequency = vocab.Frequency is > 0 ? vocab.Frequency.Value.ToString(CultureInfo.InvariantCulture) : string.Empty;
        }

        var lookupEntries = new List<string>();
        foreach (var lookup in _allLookups.Where(l => string.Equals(l.WordKey, wordKey, StringComparison.OrdinalIgnoreCase))
                                          .OrderBy(l => l.Timestamp, StringComparer.Ordinal)) {
            var usage = lookup.Usage?.Replace(AppConstants.SpaceForNewLine, Environment.NewLine) ?? string.Empty;
            if (usage.Trim().Length == 0) continue;
            lookupEntries.Add(lookup.Title is { Length: > 0 }
                ? string.Concat(usage, string.Format(CultureInfo.CurrentCulture, AppConstants.BookTitleFormat, lookup.Title))
                : usage);
        }

        var clippingEntries = new List<string>();
        if (word.Length > 1) {
            foreach (var clip in _allClippings.OrderBy(c => c.PageNumber)) {
                var content = clip.Content.Replace(AppConstants.SpaceForNewLine, Environment.NewLine);
                if (content.Trim().Length == 0) continue;
                if (!content.Contains(word, StringComparison.OrdinalIgnoreCase)) continue;
                clippingEntries.Add(clip.BookName is { Length: > 0 }
                    ? string.Concat(content, string.Format(CultureInfo.CurrentCulture, AppConstants.BookTitleFormat, clip.BookName))
                    : content);
            }
        }

        var builder = new StringBuilder();
        foreach (var entry in lookupEntries) builder.Append("• ").Append(entry.Trim()).Append('\n');
        if (lookupEntries.Count > 0 && clippingEntries.Count > 0) builder.Append('\n');
        foreach (var entry in clippingEntries) builder.Append("• ").Append(entry.Trim()).Append('\n');

        var stats = new List<string>();
        if (stem.Length > 0 && !string.Equals(stem, word, StringComparison.OrdinalIgnoreCase)) {
            stats.Add(string.Format(CultureInfo.CurrentCulture, Strings.Ui_Text_Stem, stem));
        }
        if (frequency.Length > 0) {
            stats.Add(string.Format(CultureInfo.CurrentCulture, Strings.Ui_Text_Frequency, frequency));
        }
        if (lookupEntries.Count > 0) {
            stats.Add(string.Format(CultureInfo.CurrentCulture, Strings.Ui_Text_LookupCount, lookupEntries.Count));
        }
        if (clippingEntries.Count > 0) {
            stats.Add(string.Format(CultureInfo.CurrentCulture, Strings.Ui_Text_ClippingCount, clippingEntries.Count));
        }

        return new DetailModel {
            HasSelection = true,
            Title = word,
            Subtitle = string.Join(" · ", stats),
            HasBody = true,
            Body = builder.ToString().TrimEnd()
        };
    }

    /// <summary>复制当前详情为纯文本(右键「复制」/ 动作按钮用)。</summary>
    public string BuildCopyText() {
        if (!_detail.HasSelection) return string.Empty;
        var builder = new StringBuilder();
        builder.AppendLine(_detail.Title);
        if (_detail.Subtitle.Length > 0) builder.AppendLine(_detail.Subtitle);
        if (_detail.HasQuote) builder.AppendLine().AppendLine(_detail.QuoteLabel).AppendLine(_detail.Quote);
        if (_detail.HasNote) builder.AppendLine().AppendLine(_detail.NoteLabel).AppendLine(_detail.Note);
        if (_detail.HasBody) builder.AppendLine().AppendLine(_detail.Body);
        return builder.ToString().TrimEnd();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
