using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Infrastructure.Helpers;
using KindleMate2.Infrastructure.Repositories.KM2DB;

namespace KindleMate2.Avalonia.ViewModels;

/// <summary>书籍树节点:书名 + 标注条数。</summary>
public class BookItem {
    public required string Name { get; init; }
    public int Count { get; init; }

    public override string ToString() => Count > 0 ? $"{Name}  ({Count})" : Name;
}

/// <summary>
/// Avalonia 壳主窗口 VM —— 演示复用现有分层:
/// 直接调用 KindleMate2.Infrastructure 的 ClippingRepository / DatabaseHelper
/// (以及 Domain 实体),UI 侧零业务逻辑。所有 DB 读取放在 Task.Run 中,保持 UI 流畅。
/// </summary>
public class MainWindowViewModel : INotifyPropertyChanged {
    private string _dbPath = string.Empty;
    private string _statusText = "请选择 KM2.db 数据库(或把 .db 路径作为启动参数)";
    private BookItem? _selectedBook;
    private bool _isBusy;

    public ObservableCollection<BookItem> Books { get; } = new();
    public ObservableCollection<Clipping> Clippings { get; } = new();

    public string DbPath {
        get => _dbPath;
        set {
            if (_dbPath == value) return;
            _dbPath = value;
            OnPropertyChanged();
        }
    }

    public string StatusText {
        get => _statusText;
        set {
            if (_statusText == value) return;
            _statusText = value;
            OnPropertyChanged();
        }
    }

    public bool IsBusy {
        get => _isBusy;
        set {
            if (_isBusy == value) return;
            _isBusy = value;
            OnPropertyChanged();
        }
    }

    public BookItem? SelectedBook {
        get => _selectedBook;
        set {
            if (_selectedBook == value) return;
            _selectedBook = value;
            OnPropertyChanged();
            _ = value is null ? Task.CompletedTask : LoadClippingsAsync(value.Name);
        }
    }

    /// <summary>启动自动尝试:命令行参数 > 常见默认位置。</summary>
    public async Task TryAutoOpenAsync() {
        var args = Environment.GetCommandLineArgs();
        if (args.Length > 1 && File.Exists(args[1])) {
            await OpenDatabaseAsync(args[1]);
            return;
        }

        // 从输出目录向上回溯查找仓库根目录的 KM2.db(bin/Debug/net8.0-windows → ../../../../KM2.db)
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
        Books.Clear();

        try {
            var connectionString = DatabaseHelper.GetConnectionString(path);
            var summaries = await Task.Run(() => {
                var sw = Stopwatch.StartNew();
                var all = new ClippingRepository(connectionString).GetAll();
                return new {
                    ElapsedMs = sw.ElapsedMilliseconds,
                    Clippings = all,
                    Books = all
                        .Where(c => !string.IsNullOrWhiteSpace(c.BookName))
                        .GroupBy(c => c.BookName!)
                        .Select(g => new BookItem { Name = g.Key, Count = g.Count() })
                        .OrderBy(b => b.Name)
                        .ToList()
                };
            });

            foreach (var book in summaries.Books) {
                Books.Add(book);
            }
            StatusText = summaries.Clippings.Count == 0
                ? $"已打开 {Path.GetFileName(path)},数据库为空(共读取 {summaries.ElapsedMs} ms)"
                : $"已打开 {Path.GetFileName(path)}: {summaries.Books.Count} 本书 / {summaries.Clippings.Count} 条标注(读取 {summaries.ElapsedMs} ms)";
        } catch (Exception ex) {
            StatusText = $"打开失败: {ex.Message}";
            Console.WriteLine(ex);
        } finally {
            IsBusy = false;
        }
    }

    private async Task LoadClippingsAsync(string bookName) {
        if (string.IsNullOrWhiteSpace(bookName) || string.IsNullOrWhiteSpace(DbPath)) return;

        IsBusy = true;
        try {
            var connectionString = DatabaseHelper.GetConnectionString(DbPath);
            var rows = await Task.Run(() => {
                var sw = Stopwatch.StartNew();
                var all = new ClippingRepository(connectionString).GetAll()
                    .Where(c => string.Equals(c.BookName, bookName, StringComparison.Ordinal))
                    .OrderByDescending(c => c.ClippingDate)
                    .ToList();
                return new { ElapsedMs = sw.ElapsedMilliseconds, Rows = all };
            });

            Clippings.Clear();
            foreach (var row in rows.Rows) {
                Clippings.Add(row);
            }
            StatusText = $"{bookName}: {rows.Rows.Count} 条标注(查询 {rows.ElapsedMs} ms)";
        } catch (Exception ex) {
            StatusText = $"加载失败: {ex.Message}";
            Console.WriteLine(ex);
        } finally {
            IsBusy = false;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
