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
using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Infrastructure.Helpers;
using KindleMate2.Infrastructure.Repositories.KM2DB;
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
    private string _searchType = "全部";
    private string _statusText = "请选择一个 KM2 数据库开始";
    private string _deviceStatus = "设备未连接";
    private string _connectionString = string.Empty;
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

    public string SortLabel => _sortDescending ? "按时间 ↓" : "按时间 ↑";

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
    public string NavSectionTitle => IsClipDomain ? "书籍" : "生词";
    public int NavSectionCount => IsClipDomain ? BookCount : WordCount;

    // —— 主区标题 ——
    public string HeaderTitle => _selectedNav is { IsAll: false } nav
        ? nav.Name
        : (IsClipDomain ? "全部标注" : "全部生词");

    public string HeaderSubtitle => IsClipDomain
        ? $"{Items.Count:N0} 条"
        : $"{Items.Count:N0} 条查询";

    // —— 状态栏 ——
    public int BookCount => _allClippings.Select(c => c.BookName).Where(b => !string.IsNullOrWhiteSpace(b)).Distinct().Count();
    public int ClipCount => _allClippings.Count;
    public int WordCount => _distinctWordCount;
    public int LookupCount => _allLookups.Count;

    /// <summary>已删除 = 原始标注行 − 当前标注(与原版 GetStatusText 口径一致)。</summary>
    public int DeletedCount => Math.Max(0, _originLineCount - _allClippings.Count);

    public string StatusLeft => IsClipDomain
        ? $"共 {BookCount:N0} 本书 · {ClipCount:N0} 条标注 · 已删除 {DeletedCount:N0} 条"
        : $"共 {WordCount:N0} 个生词 · {LookupCount:N0} 条查询";

    public string StatusRight => _deviceStatus;

    public async Task TryAutoOpenAsync() {
        var args = Environment.GetCommandLineArgs();
        if (args.Length > 1 && File.Exists(args[1])) {
            await OpenDatabaseAsync(args[1]);
            return;
        }
        var candidates = new[] {
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "KM2.db"),
            Path.Combine(AppContext.BaseDirectory, "KM2.db")
        };
        foreach (var candidate in candidates.Select(Path.GetFullPath)) {
            if (File.Exists(candidate)) {
                await OpenDatabaseAsync(candidate);
                return;
            }
        }
    }

    public async Task OpenDatabaseAsync(string path) {
        if (IsBusy) return;
        if (!File.Exists(path)) {
            StatusText = $"文件不存在: {path}";
            return;
        }
        IsBusy = true;
        DbPath = path;
        StatusText = "正在读取数据库…";
        ResetCollections();

        try {
            _connectionString = DatabaseHelper.GetConnectionString(path);
            var cs = _connectionString;
            var snapshot = await Task.Run(() => {
                var sw = Stopwatch.StartNew();
                var clips = new ClippingRepository(cs).GetAll();
                var lookups = new LookupRepository(cs).GetAll();
                var vocabs = new VocabRepository(cs).GetAll();
                var originCount = new OriginalClippingLineRepository(cs).GetAll().Count;
                return new { ElapsedMs = sw.ElapsedMilliseconds, Clips = clips, Lookups = lookups, Vocabs = vocabs, OriginCount = originCount };
            });

            _allClippings = snapshot.Clips;
            _allLookups = snapshot.Lookups;
            _allVocabs = snapshot.Vocabs;
            _originLineCount = snapshot.OriginCount;

            IndexVocabs();
            IndexNoteHighlights();
            EnrichLookups();

            RebuildNav();
            StatusText = $"{Path.GetFileName(path)} · {_allClippings.Count:N0} 条标注 / {_allLookups.Count:N0} 条查询(读取 {snapshot.ElapsedMs} ms)";
        } catch (Exception ex) {
            StatusText = $"打开失败: {ex.Message}";
            Console.WriteLine(ex);
        } finally {
            IsBusy = false;
            OnPropertyChanged(nameof(StatusLeft));
            OnPropertyChanged(nameof(StatusRight));
        }
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
            NavItems.Add(new NavItem { Key = string.Empty, Name = "全部标注", IsAll = true, Count = _allClippings.Count });
            var groups = _allClippings
                .Where(c => !string.IsNullOrWhiteSpace(c.BookName))
                .GroupBy(c => c.BookName!, StringComparer.Ordinal)
                .Select(g => new NavItem { Key = g.Key, Name = g.Key, Count = g.Count() })
                .OrderBy(x => x.Name, StringComparer.CurrentCulture);
            foreach (var item in groups) NavItems.Add(item);
        } else {
            NavItems.Add(new NavItem { Key = string.Empty, Name = "全部生词", IsAll = true, Count = _allLookups.Count });
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
        return type switch {
            "书籍" => Hit(c.BookName),
            "作者" => Hit(c.AuthorName),
            "内容" => Hit(c.Content),
            "笔记" => c.BriefType == (long)BriefType.Note && (Hit(c.Content) || Hit(c.BookName)),
            _ => Hit(c.Content) || Hit(c.BookName) || Hit(c.AuthorName)
        };
    }

    private static ListItem ToListItem(Clipping clip) {
        var (typeText, kind) = TypeTextMap.Of(clip.BriefType);
        return new ListItem {
            Key = clip.Key,
            Primary = Flatten(clip.Content),
            Book = clip.BookName ?? string.Empty,
            Place = (clip.PageNumber ?? 0) > 0
                ? string.Concat("第 ", (clip.PageNumber ?? 0).ToString(CultureInfo.InvariantCulture), " 页")
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
        if (stem.Length > 0 && !string.Equals(stem, lookup.Word, StringComparison.OrdinalIgnoreCase)) extra.Add($"词干 {stem}");
        if (frequency.Length > 0) extra.Add($"词频 {frequency}");

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
        };
    }

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
            ? string.Concat("第 ", (clip.PageNumber ?? 0).ToString(CultureInfo.InvariantCulture), " 页")
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
                QuoteLabel = "划线",
                Quote = quote,
                HasNote = true,
                NoteLabel = "笔记",
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
        if (stem.Length > 0 && !string.Equals(stem, word, StringComparison.OrdinalIgnoreCase)) stats.Add($"词干 {stem}");
        if (frequency.Length > 0) stats.Add($"词频 {frequency}");
        if (lookupEntries.Count > 0) stats.Add($"{lookupEntries.Count:N0} 条查询");
        if (clippingEntries.Count > 0) stats.Add($"{clippingEntries.Count:N0} 条标注");

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
