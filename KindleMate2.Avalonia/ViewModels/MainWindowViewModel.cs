using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using KindleMate2.Avalonia.Models;
using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Infrastructure.Helpers;
using KindleMate2.Infrastructure.Repositories.KM2DB;

namespace KindleMate2.Avalonia.ViewModels;

/// <summary>
/// 主界面 VM——Avalonia 迁移「阶段 1:主界面布局复刻」按原 WinForms UI 等价迁移:
/// 菜单栏 / 工具栏(类型+搜索+设备+主题) / 书树+☑ / 表格 / 底部 tab / 状态栏。
/// 数据全部来自现有分层(仓储 / 实体),UI 零业务逻辑。
/// </summary>
public class MainWindowViewModel : INotifyPropertyChanged {
    private string _dbPath = string.Empty;
    private string _searchText = string.Empty;
    private string _searchType = "全部";
    private string _statusText = "请选择 KM2.db 数据库";
    private string _deviceStatus = "📴 未连接";
    private bool _isBusy;
    private int _domainIndex; // 0=标注,1=生词本
    private object? _selectedItem;
    private bool _isDarkTheme = true;

    // 全量数据(打开库时一次性装载)
    private List<Clipping> _allClippings = new();
    private List<Lookup> _allLookups = new();
    private List<BookItem> _allBooks = new();
    private List<WordItem> _allWords = new();
    private int _totalDeleted; // 已删除数(阶段 1 由仓储 GetAll 推算:表可能无,先置 0)

    // 左侧列表(随域切换;原 UI 是 TreeView,阶段 1 用多选 ListBox 含复选框)
    public ObservableCollection<object> LeftItems { get; } = new();
    public ObservableCollection<ClippingRow> Clippings { get; } = new();
    public ObservableCollection<Lookup> Lookups { get; } = new();

    /// <summary>搜索类型下拉项(原 UI「全部 ▼」)。</summary>
    public string[] SearchTypes { get; } = { "全部", "书籍", "作者", "内容", "笔记" };

    public string DbPath { get => _dbPath; set { if (_dbPath == value) return; _dbPath = value; OnPropertyChanged(); OnPropertyChanged(nameof(BookCount)); } }
    public string SearchText { get => _searchText; set { if (_searchText == value) return; _searchText = value; OnPropertyChanged(); ApplyFilter(); } }
    public string SearchType { get => _searchType; set { if (_searchType == value) return; _searchType = value; OnPropertyChanged(); ApplyFilter(); } }
    public string StatusText { get => _statusText; set { if (_statusText == value) return; _statusText = value; OnPropertyChanged(); } }
    public string DeviceStatus { get => _deviceStatus; set { if (_deviceStatus == value) return; _deviceStatus = value; OnPropertyChanged(); } }
    public bool IsBusy { get => _isBusy; set { if (_isBusy == value) return; _isBusy = value; OnPropertyChanged(); } }
    public bool IsDarkTheme { get => _isDarkTheme; set { if (_isDarkTheme == value) return; _isDarkTheme = value; OnPropertyChanged(); } }
    public int DomainIndex { get => _domainIndex; set { if (_domainIndex == value) return; _domainIndex = value; OnPropertyChanged(); OnPropertyChanged(nameof(LeftTitle)); SelectedItem = null; RefreshLeftItems(); ApplyFilter(); } }
    public object? SelectedItem { get => _selectedItem; set { if (ReferenceEquals(_selectedItem, value)) return; _selectedItem = value; OnPropertyChanged(); ApplyFilter(); } }

    public string LeftTitle => DomainIndex == 0 ? "书籍" : "生词";
    public string LeftCountInfo { get; private set; } = string.Empty;
    public string ScopeInfo { get; private set; } = string.Empty;

    // 状态栏(原 UI 底部)
    public int BookCount => _allBooks.Count;
    public int TotalClips => _allClippings.Count;
    public int DeletedClips { get; private set; }
    public string StatusBarText => $"📚 共 {BookCount} 本书,{TotalClips} 条标注,已删除 {DeletedClips} 条";

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
        Clippings.Clear();
        Lookups.Clear();
        LeftItems.Clear();
        _allClippings = new();
        _allLookups = new();
        _allBooks = new();
        _allWords = new();
        SelectedItem = null;

        try {
            var connectionString = DatabaseHelper.GetConnectionString(path);
            var snapshot = await Task.Run(() => {
                var sw = Stopwatch.StartNew();
                var clips = new ClippingRepository(connectionString).GetAll();
                var lookups = new LookupRepository(connectionString).GetAll();
                return new {
                    ElapsedMs = sw.ElapsedMilliseconds,
                    Clips = clips,
                    Lookups = lookups,
                    Books = clips.Where(c => !string.IsNullOrWhiteSpace(c.BookName))
                        .GroupBy(c => c.BookName!)
                        .Select(g => new BookItem { Name = g.Key, Count = g.Count() })
                        .OrderBy(b => b.Name).ToList(),
                    Words = lookups.Where(l => !string.IsNullOrWhiteSpace(l.Word))
                        .GroupBy(l => l.Word!.Trim())
                        .Select(g => new WordItem { Word = g.Key, Count = g.Count() })
                        .OrderBy(w => w.Word).ToList()
                };
            });
            _allClippings = snapshot.Clips;
            _allLookups = snapshot.Lookups;
            _allBooks = snapshot.Books;
            _allWords = snapshot.Words;
            RefreshLeftItems();
            ApplyFilter();
            StatusText = $"{Path.GetFileName(path)}: {_allBooks.Count} 本书 / {_allClippings.Count} 条标注(读取 {snapshot.ElapsedMs} ms)";
        } catch (Exception ex) {
            StatusText = $"打开失败: {ex.Message}";
            Console.WriteLine(ex);
        } finally {
            IsBusy = false;
        }
    }

    private void RefreshLeftItems() {
        LeftItems.Clear();
        if (DomainIndex == 0) {
            foreach (var book in _allBooks) LeftItems.Add(book);
            LeftCountInfo = $"{_allBooks.Count} 本";
        } else {
            foreach (var word in _allWords) LeftItems.Add(word);
            LeftCountInfo = $"{_allWords.Count} 词";
        }
        OnPropertyChanged(nameof(LeftCountInfo));
        OnPropertyChanged(nameof(BookCount));
        OnPropertyChanged(nameof(TotalClips));
        OnPropertyChanged(nameof(StatusBarText));
    }

    private void ApplyFilter() {
        if (DomainIndex == 0) RebuildClippings();
        else RebuildLookups();
    }

    private void RebuildClippings() {
        var query = _allClippings.AsEnumerable();
        if (SelectedItem is BookItem b) {
            query = query.Where(c => string.Equals(c.BookName, b.Name, StringComparison.Ordinal));
        }
        var keyword = SearchText?.Trim();
        if (!string.IsNullOrWhiteSpace(keyword)) {
            var k = keyword;
            var type = SearchType;
            query = query.Where(c => MatchClippingKeyword(c, k, type));
        }
        var rows = query.OrderByDescending(c => c.ClippingDate).Select(ClippingRow.From).ToList();
        Clippings.Clear();
        foreach (var row in rows) Clippings.Add(row);
        var scope = SelectedItem is BookItem bb ? bb.Name : "全部书籍";
        StatusText = $"{scope}: {Clippings.Count} / {_allClippings.Count} 条标注";
        ScopeInfo = $"{Clippings.Count} / {_allClippings.Count} 条";
        OnPropertyChanged(nameof(ScopeInfo));
    }

    private static bool MatchClippingKeyword(Clipping c, string k, string type) {
        bool Hit(string? s) => s != null && s.Contains(k, StringComparison.OrdinalIgnoreCase);
        return type switch {
            "书籍" => Hit(c.BookName) || Hit(c.Content),
            "作者" => Hit(c.AuthorName) || Hit(c.Content),
            "内容" => Hit(c.Content),
            "笔记" => Hit(c.Content) || (c.BriefType == (long)BriefType.Note),
            _ => Hit(c.Content) || Hit(c.BookName) || Hit(c.AuthorName)
        };
    }

    private void RebuildLookups() {
        var query = _allLookups.AsEnumerable();
        if (SelectedItem is WordItem w) {
            query = query.Where(l => string.Equals(l.Word?.Trim(), w.Word, StringComparison.Ordinal));
        }
        var keyword = SearchText?.Trim();
        if (!string.IsNullOrWhiteSpace(keyword)) {
            var k = keyword;
            query = query.Where(l => (l.Word?.Contains(k, StringComparison.OrdinalIgnoreCase) ?? false)
                                   || (l.Usage?.Contains(k, StringComparison.OrdinalIgnoreCase) ?? false));
        }
        var rows = query.OrderByDescending(l => l.Timestamp).ToList();
        Lookups.Clear();
        foreach (var row in rows) Lookups.Add(row);
        var scope = SelectedItem is WordItem ww ? ww.Word : "全部生词";
        StatusText = $"{scope}: {Lookups.Count} / {_allLookups.Count} 条查询";
        ScopeInfo = $"{Lookups.Count} / {_allLookups.Count} 条";
        OnPropertyChanged(nameof(ScopeInfo));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
