using System.Text;
using Microsoft.Data.Sqlite;
using Xunit;
using KindleMate2.Application.Services.KM2DB;
using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Domain.Interfaces.KM2DB;
using KindleMate2.Infrastructure.Helpers;
using KindleMate2.Infrastructure.Repositories.KM2DB;

namespace KindleMate2.Tests;

/// <summary>
/// 破坏性维护路径的用例：回收站(删除 → 可见 → 恢复 → 彻底清除)、书籍重命名、清空数据、清理。
/// 这些操作都直接删改用户数据，且此前完全没有测试覆盖 —— 出错的代价最高。
///
/// 两个夹具上的坑，是写这组用例时才显出来的（值得记住）：
/// <list type="number">
/// <item>回收站**不加 schema 字段**，判据是「<c>original_clipping_lines</c> 里有、<c>clippings</c>
/// 里没有」。而两边靠的是**同一个 key**（导入时同时写入，key = 标注日期 + 位置）。所以回收站
/// 相关的用例必须走**真实导入路径**造数据；手工分别往两张表塞数据几乎必然写出对不上的 key，
/// 于是条目"删了却永远清不出回收站"。</item>
/// <item>导出会**静默跳过** <c>ClippingTypeLocation</c> 或内容为空的条目。</item>
/// </list>
/// </summary>
public sealed class ClippingMaintenanceTests : IDisposable {
    private readonly string _dir;
    private readonly string _db;
    private readonly IClippingRepository _clippingRepo;
    private readonly ILookupRepository _lookupRepo;
    private readonly IOriginalClippingLineRepository _originalRepo;
    private readonly ISettingRepository _settingRepo;
    private readonly IVocabRepository _vocabRepo;
    private readonly ClippingService _clippingService;
    private readonly Km2DatabaseService _km2;

    public ClippingMaintenanceTests() {
        _dir = Path.Combine(Path.GetTempPath(), "km2-maintenance-tests-" + Guid.NewGuid().ToString("N")[..10]);
        Directory.CreateDirectory(_dir);
        _db = Path.Combine(_dir, "KM2.dat");
        Assert.True(DatabaseHelper.CreateDatabase(_db, out var ex), "CreateDatabase failed: " + ex.Message);

        // 手工组装一套指向同一个库的仓储 —— 与 Avalonia 壳的 DatabaseSession 同构，
        // 但按路径参数化，避免依赖进程工作目录。
        var cs = DatabaseHelper.GetConnectionString(_db);
        _clippingRepo = new ClippingRepository(cs);
        _lookupRepo = new LookupRepository(cs);
        _originalRepo = new OriginalClippingLineRepository(cs);
        _settingRepo = new SettingRepository(cs);
        _vocabRepo = new VocabRepository(cs);

        _clippingService = new ClippingService(_clippingRepo);
        _km2 = new Km2DatabaseService(_clippingRepo, _lookupRepo, _originalRepo, _settingRepo, _vocabRepo);
    }

    public void Dispose() {
        try {
            SqliteConnection.ClearAllPools();
            Directory.Delete(_dir, true);
        } catch { /* best effort */ }
    }

    // ————————————————————————— 回收站 —————————————————————————

    [Fact]
    public void RecycleBin_ListsDeletedEntry_andItsOriginalLineSurvives() {
        var key = ImportOneClipping("内容一");

        Assert.True(_clippingService.DeleteClipping(key));

        Assert.Equal(key, Assert.Single(_km2.GetDeletedOriginalLines()).Key);
        Assert.DoesNotContain(_clippingRepo.GetAll(), c => c.Key == key);
    }

    [Fact]
    public void RecycleBin_DoesNotListLiveEntries() {
        ImportOneClipping("内容一");

        Assert.Empty(_km2.GetDeletedOriginalLines());
    }

    [Fact]
    public void RecycleBin_Restore_PutsTheClippingBack_andClearsItFromTheBin() {
        var key = ImportOneClipping("内容一");
        Assert.True(_clippingService.DeleteClipping(key));
        var deletedLine = Assert.Single(_km2.GetDeletedOriginalLines());

        Assert.True(_km2.RestoreFromOriginalLine(deletedLine));

        // 恢复走"重新解析原始行"的导入路径,而原始行与恢复出来的条目共用同一个 key,
        // 所以它应当**即时离开回收站**,而不是在回收站里再留一份。
        var restored = Assert.Single(_clippingRepo.GetAll());
        Assert.Equal("内容一", restored.Content);
        Assert.Empty(_km2.GetDeletedOriginalLines());
    }

    [Fact]
    public void RecycleBin_Purge_RemovesTheOriginalLineForGood() {
        var key = ImportOneClipping("内容一");
        Assert.True(_clippingService.DeleteClipping(key));

        Assert.Equal(1, _km2.PurgeDeletedOriginalLines([key]));

        Assert.Empty(_km2.GetDeletedOriginalLines());
        Assert.DoesNotContain(_originalRepo.GetAll(), l => l.Key == key);
    }

    [Fact]
    public void RecycleBin_SurvivesDeleteAndRestoreCycles() {
        ImportOneClipping("内容一");

        // 反复删了恢复 —— 原始行始终在,所以必须能一直恢复,而不是恢复一次就没了。
        for (var i = 0; i < 3; i++) {
            var current = Assert.Single(_clippingRepo.GetAll());
            Assert.True(_clippingService.DeleteClipping(current.Key), $"第 {i + 1} 次删除失败");
            var line = Assert.Single(_km2.GetDeletedOriginalLines());
            Assert.True(_km2.RestoreFromOriginalLine(line), $"第 {i + 1} 次恢复失败");
        }

        // 三轮后仍应恰好 1 条,而不是累积出重复
        Assert.Single(_clippingRepo.GetAll());
        Assert.Empty(_km2.GetDeletedOriginalLines());
    }

    // ————————————————————————— 重命名 —————————————————————————

    [Fact]
    public void RenameBook_UpdatesEveryClippingOfThatBook_andLeavesOthersAlone() {
        SeedClipping("k1", "内容一", book: "旧书名");
        SeedClipping("k2", "内容二", book: "旧书名");
        SeedClipping("k3", "内容三", book: "别的书");

        Assert.True(_clippingService.RenameBook("旧书名", "新书名", "新作者"));

        var renamed = _clippingRepo.GetAll().Where(c => c.BookName == "新书名").ToList();
        Assert.Equal(2, renamed.Count);
        Assert.All(renamed, c => Assert.Equal("新作者", c.AuthorName));
        Assert.Single(_clippingRepo.GetAll(), c => c.BookName == "别的书");
    }

    /// <summary>原版语义：作者传空时不覆盖既有作者。</summary>
    [Fact]
    public void RenameBook_KeepsExistingAuthor_WhenNewAuthorIsBlank() {
        SeedClipping("k1", "内容一", book: "旧书名", author: "原作者");

        Assert.True(_clippingService.RenameBook("旧书名", "新书名", ""));

        var renamed = Assert.Single(_clippingRepo.GetAll());
        Assert.Equal("新书名", renamed.BookName);
        Assert.Equal("原作者", renamed.AuthorName);
    }

    [Fact]
    public void RenameBook_KeepsKeyUnchanged() {
        SeedClipping("k1", "内容一", book: "旧书名");

        Assert.True(_clippingService.RenameBook("旧书名", "新书名", "新作者"));

        // 改名不动主键 —— 将来按 key 归并的同步方案依赖这一点。
        Assert.Contains(_clippingRepo.GetAll(), c => c.Key == "k1");
    }

    // ————————————————————————— 清空与清理 —————————————————————————

    [Fact]
    public void DeleteAllData_ClearsClippings() {
        SeedClipping("k1", "内容一");
        SeedClipping("k2", "内容二");

        _km2.DeleteAllData();

        Assert.Empty(_clippingRepo.GetAll());
    }

    [Fact]
    public void CleanDatabase_RemovesRowsWithEmptyContent() {
        SeedClipping("k1", "有内容");
        SeedClipping("k2", "");

        _km2.CleanDatabase(_db, out _);

        var keys = _clippingRepo.GetAll().Select(c => c.Key).ToList();
        Assert.Contains("k1", keys);
        Assert.DoesNotContain("k2", keys);
    }

    [Fact]
    public void CleanDatabase_RemovesRowsWithEmptyBookName() {
        SeedClipping("k1", "有内容");
        SeedClipping("k2", "内容但无书名", book: "");

        _km2.CleanDatabase(_db, out _);

        var keys = _clippingRepo.GetAll().Select(c => c.Key).ToList();
        Assert.Contains("k1", keys);
        Assert.DoesNotContain("k2", keys);
    }

    [Fact]
    public void CleanDatabase_OnCleanData_KeepsEverything() {
        SeedClipping("k1", "内容一");
        SeedClipping("k2", "内容二");

        _km2.CleanDatabase(_db, out _);

        Assert.Equal(2, _clippingRepo.GetAll().Count);
    }

    // ————————————————————————— helpers —————————————————————————

    /// <summary>
    /// 走**真实导入路径**造一条标注并返回它的 key。回收站相关的用例必须用这个 ——
    /// 见类注释里的说明（两张表靠同一个 key 关联）。
    /// </summary>
    private string ImportOneClipping(string content, string book = "Book", string author = "Author") {
        var file = Path.Combine(_dir, $"My Clippings {Guid.NewGuid().ToString("N")[..6]}.txt");
        var text = new StringBuilder()
            .AppendLine($"{book} ({author})")
            .AppendLine($"- 您在第 1 页（位置 #1-1）的标注 | 添加于 2020年1月1日星期三 上午 10:00:00")
            .AppendLine()
            .AppendLine(content)
            .AppendLine("==========")
            .ToString();
        File.WriteAllText(file, text, new UTF8Encoding(false));
        Assert.True(_km2.ImportKindleClippings(file, out _), "导入夹具失败");
        return Assert.Single(_clippingRepo.GetAll()).Key;
    }

    /// <summary>
    /// 直接往 clippings 写一条(不涉及回收站判据的用例用它更省事)。
    /// 注意 <c>ClippingTypeLocation</c> 不能省 —— 导出会静默跳过它。
    /// </summary>
    private void SeedClipping(string key, string content, string book = "Book", string author = "Author") {
        _clippingRepo.Add(new Clipping {
            Key = key,
            Content = content,
            BookName = book,
            AuthorName = author,
            ClippingTypeLocation = "标注 位置 #1-1",
        });
    }
}
