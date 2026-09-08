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
/// 主界面 VM —— Avalonia 迁移「阶段 1:主界面骨架」。
/// 数据全部来自现有分层(Infrastructure 仓储 / Domain 实体),UI 零业务逻辑;
/// 打开库后一次性读入内存,切换/搜索为纯内存过滤,DB 读取在 Task.Run。
/// </summary>
public class MainWindowViewModel : INotifyPropertyChanged {
    private string _dbPath = string.Empty;
    private string _statusText = "请选择 KM2.db 数据库(或把 .db 路径作为启动参数)";
    private string _searchText = string.Empty;
    private bool _isBusy;
    private int _domainIndex; // 0=标注,1=生词
    private object? _selectedItem; // 左列表选中:BookItem 或 WordItem;null=全部

    // 全量数据(打开库时装载)
    private List<Clipping> _allClippings = new();
    private List<Lookup> _allLookups = new();
    private List<BookItem> _allBooks = new();
    private List<WordItem> _allWords = new();

    // 左列表(随域切换)
    public ObservableCollection<object> LeftItems { get; } = new();
    // 右表格
    public ObservableCollection<ClippingRow> Clippings { get; } = new();
    public ObservableCollection<Lookup> Lookups { get; } = new();

    public string DbPath { get => _dbPath; set { if (_dbPath == value) return; _dbPath = value; OnPropertyChanged(); } }

    public string StatusText {
        get => _statusText;
        set { if (_statusText == value) return; _statusText = value; OnPropertyChanged(); }
    }

    public bool IsBusy {
        get => _isBusy;
        set { if (_isBusy == value) return; _isBusy = value; OnPropertyChanged(); }
    }

    /// <summary>左侧标题/搜索占位提示随域变化。</summary>
    public string LeftTitle => DomainIndex == 0 ? "书籍" : "生词";

    public int DomainIndex {
        get => _domainIndex;
        set {
            if (_domainIndex == value) return;
            _domainIndex = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(LeftTitle));
            SelectedItem = null;
            RefreshLeftItems();
            ApplyDomainFilter();
        }
    }

    public string SearchText {
        get => _searchText;
        set {
            if (_searchText == value) return;
            _searchText = value;
            OnPropertyChanged();
            ApplyDomainFilter();
        }
    }

    public object? SelectedItem {
        get => _selectedItem;
        set {
            if (ReferenceEquals(_selectedItem, value)) return;
            _selectedItem = value;
            OnPropertyChanged();
            ApplyDomainFilter();
        }
    }

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
        _allClippings = new List<Clipping>();
        _allLookups = new List<Lookup>();
        _allBooks = new List<BookItem>();
        _allWords = new List<WordItem>();
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
                    Books = clips
                        .Where(c => !string.IsNullOrWhiteSpace(c.BookName))
                        .GroupBy(c => c.BookName!)
                        .Select(g => new BookItem { Name = g.Key, Count = g.Count() })
                        .OrderBy(b => b.Name)
                        .ToList(),
                    Words = lookups
                        .Where(l => !string.IsNullOrWhiteSpace(l.Word))
                        .GroupBy(l => l.Word!.Trim())
                        .Select(g => new WordItem { Word = g.Key, Count = g.Count() })
                        .OrderBy(w => w.Word)
                        .ToList()
                };
            });

            _allClippings = snapshot.Clips;
            _allLookups = snapshot.Lookups;
            _allBooks = snapshot.Books;
            _allWords = snapshot.Words;
            RefreshLeftItems();
            ApplyDomainFilter();
            StatusText = $"{Path.GetFileName(path)}: {_allBooks.Count} 本书 / {_allClippings.Count} 条标注 / {_allWords.Count} 个生词(读取 {snapshot.ElapsedMs} ms)";
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
        } else {
            foreach (var word in _allWords) LeftItems.Add(word);
        }
    }

    /// <summary>按「域 + 左选中 + 搜索词」重建右侧表格。</summary>
    private void ApplyDomainFilter() {
        if (DomainIndex == 0) {
            RebuildClippings();
        } else {
            RebuildLookups();
        }
    }

    private void RebuildClippings() {
        var query = _allClippings.AsEnumerable();
        if (SelectedItem is BookItem book) {
            query = query.Where(c => string.Equals(c.BookName, book.Name, StringComparison.Ordinal));
        }
        var keyword = SearchText?.Trim();
        if (!string.IsNullOrWhiteSpace(keyword)) {
            query = query.Where(c =>
                (c.Content?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (c.BookName?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (c.AuthorName?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        var rows = query
            .OrderByDescending(c => c.ClippingDate)
            .Select(ClippingRow.From)
            .ToList();
        Clippings.Clear();
        foreach (var row in rows) Clippings.Add(row);

        var scope = SelectedItem is BookItem b ? $"{b.Name}" : "全部书籍";
        StatusText = $"{scope}: {Clippings.Count} / {_allClippings.Count} 条标注";
    }

    private void RebuildLookups() {
        var query = _allLookups.AsEnumerable();
        if (SelectedItem is WordItem word) {
            query = query.Where(l => string.Equals(l.Word?.Trim(), word.Word, StringComparison.Ordinal));
        }
        var keyword = SearchText?.Trim();
        if (!string.IsNullOrWhiteSpace(keyword)) {
            query = query.Where(l =>
                (l.Word?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (l.Usage?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        var rows = query
            .OrderByDescending(l => l.Timestamp)
            .ToList();
        Lookups.Clear();
        foreach (var row in rows) Lookups.Add(row);

        var scope = SelectedItem is WordItem w ? $"{w.Word}" : "全部生词";
        StatusText = $"{scope}: {Lookups.Count} / {_allLookups.Count} 条查询记录";
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
