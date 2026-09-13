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
using KindleMate2.Avalonia.Collections;
using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Infrastructure.Helpers;
using KindleMate2.Infrastructure.Repositories.KM2DB;
using KindleMate2.Shared;
using KindleMate2.Shared.Constants;

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
        set { if (_deviceStatus == value) return; _deviceStatus = value; OnPropertyChanged(); }
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
    public string HeaderTitle => _selectedNav is { IsAll: false } nav
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
        var databasePath = Path.Combine(Environment.CurrentDirectory, AppConstants.DatabaseFileName);

        if (!File.Exists(databasePath)) {
            if (!DatabaseHelper.CreateDatabase(databasePath, out var exception)) {
                return (true, false, exception.Message);
            }
        }

        try {
            DatabaseHelper.MigrateLookupsSchemaIfNeeded(databasePath);
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

    /// <summary>兼容旧调用点:进程级备份注册所需的库路径(与原版一致的固定路径)。</summary>
    public static string DefaultDatabasePath =>
        Path.Combine(Environment.CurrentDirectory, AppConstants.DatabaseFileName);

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
            OnPropertyChanged(nameof(HasSession));
            OnPropertyChanged(nameof(Session));

            var elapsedMs = await Task.Run(ReloadFromSession);
            RebuildNav();
            StatusText = $"{Path.GetFileName(path)} · {_allClippings.Count:N0} / {_allLookups.Count:N0} ({elapsedMs} ms)";
        } catch (Exception ex) {
            // 打开失败必须把会话清干净:否则 _session 非 null 会让 HasSession 说谎,
            // 后续任何刷新/导入都会在一个坏会话上重演同一个异常。
            _session?.Dispose();
            _session = null;
            ResetCollections();
            OnPropertyChanged(nameof(HasSession));
            OnPropertyChanged(nameof(Session));
            StatusText = string.Format(CultureInfo.CurrentCulture, Strings.Ui_Status_OpenFailed, ex.Message);
            Console.WriteLine(ex);
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

    public Task<OperationResult> ImportKindleWordsAsync(string path) =>
        RunOperationAsync(() => _session!.ImportManager.ImportKindleWords(path), true,
            Strings.Successful, Strings.Import_Failed);

    public Task<OperationResult> ImportKmDatabaseAsync(string path) =>
        RunOperationAsync(() => _session!.ImportManager.ImportKmDatabase(path), true,
            Strings.Successful, Strings.Import_Failed);

    public Task<OperationResult> ImportKmateDatabaseAsync(string path) =>
        RunOperationAsync(() => _session!.ImportManager.ImportKmateDatabase(path), true,
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
            var backupPath = Path.Combine(Environment.CurrentDirectory, AppConstants.BackupsPathName);
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

    /// <summary>
    /// 清理数据库 —— 对齐原版 <c>MenuClean_Click</c>:无标注数据时提示
    /// 「数据库无需清理」(标题 Prompt);否则走统一结果契约
    /// (成功标题 <c>Clean_Database</c>、失败标题 <c>Clear_Failed</c>)。
    /// 注:执行前的确认框由视图层弹出(用户 2026-09-13 指定;原版没有该确认)。
    /// </summary>
    public Task<OperationResult> CleanDatabaseAsync() {
        if (_session is not { } session) {
            return Task.FromResult(new OperationResult(false, Strings.Error, Strings.Ui_Status_OpenDatabaseFirst));
        }
        if (_allClippings.Count <= 0) {
            return Task.FromResult(new OperationResult(false, Strings.Prompt, Strings.Database_No_Need_Clean));
        }
        return RunOperationAsync(() => {
            // 清理最耗时的是判重扫描与 VACUUM,接上进度后不再"卡着不动"
            if (!session.Km2DatabaseService.CleanDatabase(session.DatabasePath, out var result, ProgressReporter)) {
                return string.Empty;
            }
            // 严格按原版 CleanDatabase() 的正文拼接格式与键名
            var countEmpty = result.TryGetValue(AppConstants.EmptyCount, out var e) ? e : "0";
            var countDuplicated = result.TryGetValue(AppConstants.DuplicatedCount, out var d) ? d : "0";
            var fileSizeDelta = result.TryGetValue(AppConstants.FileSizeDelta, out var f) ? f : "0";
            return Strings.Cleaned + Strings.Space + Strings.Empty_Content + Strings.Space + countEmpty +
                   Strings.Space + Strings.X_Rows + Strings.Symbol_Comma + Strings.Duplicate_Content + Strings.Space +
                   countDuplicated + Strings.Space + Strings.X_Rows + Strings.Symbol_Comma +
                   Strings.Database_Cleaned + Strings.Space + fileSizeDelta;
        }, true, Strings.Clean_Database, Strings.Clear_Failed);
    }

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
            var fileName = $"{Path.GetFileNameWithoutExtension(session.DatabasePath)}_{DateTime.Now:yyyyMMdd_HHmmss}{Path.GetExtension(session.DatabasePath)}";
            File.Copy(session.DatabasePath, Path.Combine(session.BackupDirectory, fileName), true);
            return session.Km2DatabaseService.DeleteAllData() ? Strings.Data_Cleared : string.Empty;
        }, true, Strings.Successful, Strings.Clear_Failed);
    }

    // —— 删除 / 重命名 ——

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

    public bool CanRenameCurrentBook => _selectedNav is { IsAll: false };

    public string CurrentBookName => _selectedNav is { IsAll: false } nav ? nav.Key : string.Empty;

    /// <summary>当前选中书籍的作者(重命名对话框的初值)。</summary>
    public string CurrentBookAuthor =>
        _selectedNav is { IsAll: false } nav ? GetBookAuthor(nav.Key) : string.Empty;

    /// <summary>取某本书的作者 —— 取第一行,与原版 <c>resultRows[0].AuthorName</c> 一致。</summary>
    public string GetBookAuthor(string bookName) =>
        _allClippings.FirstOrDefault(c => string.Equals(c.BookName, bookName, StringComparison.Ordinal))?.AuthorName
        ?? string.Empty;

    /// <summary>该书名是否已被其它书籍占用(原版据此弹「同名合并」确认)。</summary>
    public bool IsBookNameTaken(string bookName) =>
        _allClippings.Any(c => string.Equals(c.BookName, bookName, StringComparison.Ordinal));

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

    /// <summary>探测设备状态(可后台线程调用,不触碰绑定属性)。</summary>
    public string ProbeDeviceStatus() {
        if (_session == null) return Strings.Ui_Status_DeviceOffline;
        try {
            if (!_session.DeviceManager.IsKindleConnected()) return Strings.Ui_Status_DeviceOffline;
            var drive = _session.DeviceManager.DriveLetter;
            return string.IsNullOrWhiteSpace(drive)
                ? Strings.Ui_Status_DeviceOnline
                : string.Format(CultureInfo.CurrentCulture, Strings.Ui_Status_DeviceOnlineDrive, drive);
        } catch {
            return Strings.Ui_Status_DeviceOffline;
        }
    }

    /// <summary>在 UI 线程刷新设备状态。</summary>
    public void RefreshDeviceStatus() => DeviceStatus = ProbeDeviceStatus();

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
        var databasePath = Path.Combine(Environment.CurrentDirectory, AppConstants.DatabaseFileName);
        if (!File.Exists(databasePath)) return (false, string.Empty);
        if (_allClippings.Count > 0) return (false, string.Empty);

        var backupDir = Path.Combine(Environment.CurrentDirectory, AppConstants.BackupsPathName);
        if (!Directory.Exists(backupDir)) return (false, string.Empty);

        var backupFile = Path.Combine(backupDir, AppConstants.DatabaseFileName);
        if (!File.Exists(backupFile)) return (false, string.Empty);
        if (new FileInfo(backupFile).Length / 1024 < 20) return (false, string.Empty);

        return (true, backupFile);
    }

    /// <summary>从备份恢复库文件(原版为 <c>File.Copy(backup, db, true)</c>;实际生效在下次启动)。</summary>
    public void RestoreFromBackup(string backupFile) {
        var databasePath = Path.Combine(Environment.CurrentDirectory, AppConstants.DatabaseFileName);
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

    /// <summary>把 Vocab 的词干 / 词频回填到 Lookup(与 DataDisplayService 口径一致)。</summary>
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
