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
    private const string DomainClipping = "标注";
    private const string DomainWord = "生词本";

    private string _dbPath = string.Empty;
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

    /// <summary>是否已注入设置(视图层据此启用主题 / 语言持久化)。</summary>
    public bool HasSettings => Settings != null;
    private readonly Dictionary<string, string> _noteHighlightMap = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Vocab> _vocabByWordKey = new(StringComparer.Ordinal);

    /// <summary>左栏导航(书籍 / 生词)。</summary>
    public ObservableCollection<NavItem> NavItems { get; } = new();

    /// <summary>主列表(内容优先)。</summary>
    public ObservableCollection<ListItem> Items { get; } = new();

    /// <summary>表格视图 · 标注。</summary>
    public ObservableCollection<Clipping> ClipTable { get; } = new();

    /// <summary>表格视图 · 生词。</summary>
    public ObservableCollection<Lookup> LookupTable { get; } = new();

    public IReadOnlyList<string> SearchTypes => TypeTextMap.SearchTypes;

    public string DbPath {
        get => _dbPath;
        set { if (_dbPath == value) return; _dbPath = value; OnPropertyChanged(); }
    }

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
    public async Task<(bool Ok, string Error)> PrepareDatabaseAsync() {
        var databasePath = Path.Combine(Environment.CurrentDirectory, AppConstants.DatabaseFileName);

        if (!File.Exists(databasePath)) {
            if (!DatabaseHelper.CreateDatabase(databasePath, out var exception)) {
                return (false, exception.Message);
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
        return (true, string.Empty);
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

    public async Task OpenDatabaseAsync(string path) {
        if (IsBusy) return;
        if (!File.Exists(path)) {
            StatusText = string.Format(CultureInfo.CurrentCulture, Strings.Ui_Status_FileNotFound, path);
            return;
        }
        // 校验放在任何状态变更之前:选错文件时应当保留当前已打开的库,
        // 否则会出现「DbPath 指向新文件、HasSession 却仍指向旧会话」的不一致状态。
        var (probe, probeDetail) = DatabaseSession.Probe(path);
        if (probe != DatabaseSession.ProbeResult.Valid) {
            StatusText = probe == DatabaseSession.ProbeResult.MissingSchema
                ? string.Format(CultureInfo.CurrentCulture, Strings.Ui_Status_NotKm2Database, probeDetail)
                : string.Format(CultureInfo.CurrentCulture, Strings.Ui_Status_OpenFailed, probeDetail);
            return;
        }

        IsBusy = true;
        DbPath = path;
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
            if (Settings is { } settings) {
                settings.LastDatabase = path;
                settings.Save();
            }
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

    /// <summary>统一的写操作执行壳:繁忙标记 / 异常兜底 / 状态栏文案 / 可选重载。</summary>
    private async Task RunOperationAsync(string name, Func<string> operation, bool reload) {
        if (_session == null) {
            StatusText = Strings.Ui_Status_OpenDatabaseFirst;
            return;
        }
        if (IsBusy) return;
        var previous = _selectedNav is { IsAll: false } nav ? nav.Key : null;
        IsBusy = true;
        StatusText = $"{name}…";
        try {
            var result = await Task.Run(operation);
            if (reload) {
                await Task.Run(ReloadFromSession);
                RebuildNav();
                RestoreSelection(previous);
            }
            StatusText = string.IsNullOrWhiteSpace(result)
                ? $"{name}:{Strings.Ui_Result_NoChange}"
                : $"{name}:{result}";
        } catch (Exception ex) {
            StatusText = string.Format(CultureInfo.CurrentCulture, Strings.Ui_Result_Failed, name, ex.Message);
        } finally {
            IsBusy = false;
            NotifyCounts();
        }
    }

    // —— 导入 ——

    public Task ImportKindleClippingsAsync(string path) =>
        RunOperationAsync(Strings.Ui_Menu_ImportClippings, () => _session!.ImportManager.ImportKindleClippings(path), true);

    public Task ImportKindleWordsAsync(string path) =>
        RunOperationAsync(Strings.Ui_Menu_ImportWords, () => _session!.ImportManager.ImportKindleWords(path), true);

    public Task ImportKmDatabaseAsync(string path) =>
        RunOperationAsync(Strings.Ui_Menu_ImportKmDatabase, () => _session!.ImportManager.ImportKmDatabase(path), true);

    public Task ImportKmateDatabaseAsync(string path) =>
        RunOperationAsync(Strings.Ui_Menu_ImportKmateDatabase, () => _session!.ImportManager.ImportKmateDatabase(path), true);

    // —— 导出 ——

    public Task ExportClippingsMarkdownAsync() =>
        RunOperationAsync(Strings.Ui_Op_ExportClippings,
            () => _session!.ExportManager.ExportClippingsToMarkdown()
                ? string.Format(CultureInfo.CurrentCulture, Strings.Ui_Result_Exported, _session.ExportDirectory)
                : Strings.Ui_Result_NothingToExport, false);

    public Task ExportVocabsMarkdownAsync() =>
        RunOperationAsync(Strings.Ui_Op_ExportWords,
            () => _session!.ExportManager.ExportVocabsToMarkdown()
                ? string.Format(CultureInfo.CurrentCulture, Strings.Ui_Result_Exported, _session.ExportDirectory)
                : Strings.Ui_Result_NothingToExport, false);

    /// <summary>导出当前选中书籍(或全部)的标注。</summary>
    public Task ExportCurrentBookMarkdownAsync() {
        var book = _selectedNav is { IsAll: false } nav ? nav.Key : string.Empty;
        var label = book.Length > 0
            ? string.Format(CultureInfo.CurrentCulture, Strings.Ui_Op_ExportBook, book)
            : Strings.Ui_Op_ExportAllClippings;
        return RunOperationAsync(label,
            () => _session!.ExportManager.ExportClippingsToMarkdown(book)
                ? string.Format(CultureInfo.CurrentCulture, Strings.Ui_Result_Exported, _session.ExportDirectory)
                : Strings.Ui_Result_NothingToExport, false);
    }

    // —— 维护 ——

    public Task BackupDatabaseAsync() =>
        RunOperationAsync(Strings.Ui_Op_Backup, () => {
            var session = _session!;
            Directory.CreateDirectory(session.BackupDirectory);
            var fileName = $"{Path.GetFileNameWithoutExtension(session.DatabasePath)}_{DateTime.Now:yyyyMMdd_HHmmss}{Path.GetExtension(session.DatabasePath)}";
            File.Copy(session.DatabasePath, Path.Combine(session.BackupDirectory, fileName), true);
            return string.Format(CultureInfo.CurrentCulture, Strings.Ui_Result_BackedUp, fileName);
        }, false);

    public Task CleanDatabaseAsync() =>
        RunOperationAsync(Strings.Ui_Menu_CleanDatabase, () => {
            var session = _session!;
            if (!session.Km2DatabaseService.CleanDatabase(session.DatabasePath, out var result)) {
                return result.TryGetValue(AppConstants.Exception, out var error) ? error : Strings.Ui_Result_CleanFailed;
            }
            return result.TryGetValue(AppConstants.TrimmedCount, out var trimmed)
                ? string.Format(CultureInfo.CurrentCulture, Strings.Ui_Result_CleanedCount, trimmed)
                : Strings.Ui_Result_Cleaned;
        }, true);

    public Task RebuildDatabaseAsync() =>
        RunOperationAsync(Strings.Ui_Menu_RebuildDatabase, () => {
            if (!_session!.Km2DatabaseService.RebuildDatabase(out var result)) {
                return result.TryGetValue(AppConstants.Exception, out var error) ? error : Strings.Ui_Result_RebuildFailed;
            }
            return Strings.Ui_Result_Rebuilt;
        }, true);

    public Task ClearAllDataAsync() =>
        RunOperationAsync(Strings.Ui_Menu_ClearData, () => {
            var session = _session!;
            Directory.CreateDirectory(session.BackupDirectory);
            var fileName = $"{Path.GetFileNameWithoutExtension(session.DatabasePath)}_{DateTime.Now:yyyyMMdd_HHmmss}{Path.GetExtension(session.DatabasePath)}";
            File.Copy(session.DatabasePath, Path.Combine(session.BackupDirectory, fileName), true);
            return session.Km2DatabaseService.DeleteAllData()
                ? string.Format(CultureInfo.CurrentCulture, Strings.Ui_Result_Cleared, fileName)
                : Strings.Ui_Result_ClearFailed;
        }, true);

    // —— 删除 / 重命名 ——

    public Task DeleteSelectedAsync() {
        if (_session == null) {
            StatusText = Strings.Ui_Status_OpenDatabaseFirst;
            return Task.CompletedTask;
        }
        if (_selectedItem?.Clipping is { } clip) {
            var key = clip.Key;
            return RunOperationAsync(Strings.Ui_Op_DeleteClipping,
                () => _session.ClippingService.DeleteClipping(key) ? Strings.Ui_Result_DeletedClipping : Strings.Ui_Result_DeleteFailed, true);
        }
        if (_selectedItem?.Lookup is { } lookup) {
            var wordKey = lookup.WordKey ?? string.Empty;
            var timestamp = lookup.Timestamp ?? string.Empty;
            return RunOperationAsync(Strings.Ui_Op_DeleteLookup,
                () => _session.LookupRepository.Delete(wordKey, timestamp) ? Strings.Ui_Result_DeletedLookup : Strings.Ui_Result_DeleteFailed, true);
        }
        StatusText = Strings.Ui_Status_NoSelection;
        return Task.CompletedTask;
    }

    public bool CanRenameCurrentBook => _selectedNav is { IsAll: false };

    public string CurrentBookName => _selectedNav is { IsAll: false } nav ? nav.Key : string.Empty;

    public Task RenameCurrentBookAsync(string newName) {
        if (_session == null || _selectedNav is not { IsAll: false } nav) {
            StatusText = Strings.Ui_Status_PickBookFirst;
            return Task.CompletedTask;
        }
        var oldName = nav.Key;
        var author = _allClippings
            .FirstOrDefault(c => string.Equals(c.BookName, oldName, StringComparison.Ordinal))?.AuthorName ?? string.Empty;
        return RunOperationAsync(Strings.Ui_Op_RenameBook,
            () => _session.ClippingService.RenameBook(oldName, newName, author)
                ? string.Format(CultureInfo.CurrentCulture, Strings.Ui_Result_Renamed, newName)
                : Strings.Ui_Result_RenameFailed, true);
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

    public Task SyncToDeviceAsync() =>
        RunOperationAsync(Strings.Ui_Menu_SyncToDevice, () => {
            _session!.ExportManager.SyncToKindle();
            return Strings.Ui_Result_Synced;
        }, false);

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

        Items.Clear();
        ClipTable.Clear();
        foreach (var clip in ordered) {
            Items.Add(ToListItem(clip));
            ClipTable.Add(clip);
        }
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

        Items.Clear();
        LookupTable.Clear();
        foreach (var lookup in ordered) {
            Items.Add(ToListItem(lookup));
            LookupTable.Add(lookup);
        }
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
