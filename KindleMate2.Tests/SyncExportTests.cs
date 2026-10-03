using System.Text;
using Xunit;
using KindleMate2.Application.Models;
using KindleMate2.Application.Services;
using KindleMate2.Application.Services.KM2DB;
using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Domain.Interfaces.KM2DB;
using KindleMate2.Infrastructure.Helpers;
using KindleMate2.Infrastructure.Repositories.KM2DB;
using KindleMate2.Shared.Entities;

namespace KindleMate2.Tests;

/// <summary>
/// 「写回设备 / 备份」这条出口的用例 —— 钉住一个真 bug。
///
/// <para>
/// 背景:<c>original_clipping_lines</c> 同时充当回收站 —— 「原始行仍在、但 <c>clippings</c> 里
/// 已无该 key」= 已删除(<c>KM2DatabaseService.GetDeletedOriginalLines</c> 用的是同一判据)。
/// 「删除标注」与「清理重复 / 清理空条目」都只删 <c>clippings</c> 的行,原始行一律保留。
/// </para>
/// <para>
/// 缺陷:导出此前把 <c>original_clipping_lines</c> **整表**写出,于是
/// 「删除标注 → 同步到设备」会把删掉的条目重新写回设备(条目复活),
/// 备份出来的 <c>MyClippings_&lt;时间戳&gt;.txt</c> 里也混着已删除的条目。
/// </para>
/// <para>
/// 因此这里走**真实导入路径**造数据(两张表靠同一个 key 关联,手工分别塞数据几乎必然对不上),
/// 删除只走 <c>ClippingService.DeleteClipping</c> —— 这正是用户点「删除」时发生的事。
/// </para>
/// </summary>
public sealed class SyncExportTests : IDisposable {
    private const string ClippingsFileName = "My Clippings.txt";

    private readonly string _dir;
    private readonly string _db;
    private readonly IClippingRepository _clippingRepo;
    private readonly ILookupRepository _lookupRepo;
    private readonly IOriginalClippingLineRepository _originalRepo;
    private readonly ISettingRepository _settingRepo;
    private readonly IVocabRepository _vocabRepo;
    private readonly ClippingService _clippingService;
    private readonly Km2DatabaseService _km2;
    private readonly FakeDeviceManager _device;
    private readonly ExportManager _export;

    public SyncExportTests() {
        _dir = Path.Combine(Path.GetTempPath(), "km2-sync-export-" + Guid.NewGuid().ToString("N")[..10]);
        Directory.CreateDirectory(_dir);
        _db = Path.Combine(_dir, "KM2.dat");
        Assert.True(DatabaseHelper.CreateDatabase(_db, out var ex), "CreateDatabase failed: " + ex.Message);

        // 手工组装一套指向同一个库的仓储 —— 与 Avalonia 壳的 DatabaseSession 同构,
        // 但按路径参数化,避免依赖进程工作目录。
        var cs = DatabaseHelper.GetConnectionString(_db);
        _clippingRepo = new ClippingRepository(cs);
        _lookupRepo = new LookupRepository(cs);
        _originalRepo = new OriginalClippingLineRepository(cs);
        _settingRepo = new SettingRepository(cs);
        _vocabRepo = new VocabRepository(cs);

        _clippingService = new ClippingService(_clippingRepo);
        var lookupService = new LookupService(_lookupRepo);
        var vocabService = new VocabService(_vocabRepo);
        var originalService = new OriginalClippingLineService(_originalRepo);

        _km2 = new Km2DatabaseService(_clippingRepo, _lookupRepo, _originalRepo, _settingRepo, _vocabRepo);
        _device = new FakeDeviceManager();
        _export = new ExportManager(
            _clippingService, lookupService, vocabService, originalService, _device,
            Path.Combine(_dir, "Work"), Path.Combine(_dir, "Backups"), Path.Combine(_dir, "Temp"));
    }

    public void Dispose() {
        try {
            // 刻意**不**在这里清 SQLite 连接池:那是进程级 API,会和并行跑的其他测试类互撞
            // (实测 ~1/6 概率报 ObjectDisposedException)。残留目录由 TestTempCleanup 在
            // 进程退出时统一清扫 —— 那里已无测试在跑,清池不会伤到谁。
            Directory.Delete(_dir, true);
        } catch { /* best effort */ }
    }

    // ————————————————————— 核心回归:已删除的条目不写回设备 —————————————————————

    /// <summary>
    /// **核心回归**:造 2 条 → 删掉 1 条 → 同步到设备 → 设备上那份文件里**不该**有被删的那条。
    /// 修复前这条会红(条目复活)。
    /// </summary>
    [Fact]
    public void SyncToKindle_DoesNotWriteBackDeletedClipping() {
        ImportClippings("保留的内容", "被删除的内容");
        Assert.True(_clippingService.DeleteClipping(KeyOfContent("被删除的内容")));

        _export.SyncToKindle();

        var written = SyncText();
        Assert.Contains("保留的内容", written);
        Assert.DoesNotContain("被删除的内容", written);
        Assert.Equal(1, CountEntries(written));
        Assert.Equal(ClippingsFileName, _device.SyncedTargetName);
    }

    /// <summary>备份标注(Backups/MyClippings_&lt;时间戳&gt;.txt)同样不该带上已删除的条目。</summary>
    [Fact]
    public void BackupClippings_DoesNotWriteBackDeletedClipping() {
        ImportClippings("保留的内容", "被删除的内容");
        Assert.True(_clippingService.DeleteClipping(KeyOfContent("被删除的内容")));

        Assert.True(_export.BackupClippings(out var exception));
        Assert.Null(exception);

        var file = Directory.GetFiles(Path.Combine(_dir, "Backups"), "MyClippings_*.txt").Single();
        var written = File.ReadAllText(file);
        Assert.Contains("保留的内容", written);
        Assert.DoesNotContain("被删除的内容", written);
        Assert.Equal(1, CountEntries(written));
    }

    /// <summary>「导出原始标注到指定路径」走的是同一个 <c>Export</c>,语义必须一致。</summary>
    [Fact]
    public void ExportOriginalClippings_DoesNotWriteBackDeletedClipping() {
        ImportClippings("保留的内容", "被删除的内容");
        Assert.True(_clippingService.DeleteClipping(KeyOfContent("被删除的内容")));

        var written = ExportToTempDir();
        Assert.Contains("保留的内容", written);
        Assert.DoesNotContain("被删除的内容", written);
        Assert.Equal(1, CountEntries(written));
    }

    /// <summary>
    /// 被「清理数据库」(清重复 / 清空条目)删掉的条目同样不该被导出 —— 它们与「删除标注」共用
    /// 同一个判据(原始行在、clippings 里没有)。
    /// </summary>
    [Fact]
    public void ExportOriginalClippings_DoesNotWriteBackClippingsRemovedByClean() {
        // 同书同文两条 → 精确重复,「清理」会把它们**都**删掉(与 CleanDatabase 既有语义一致);
        // 再加一条唯一的,用来证明清理没把不该删的删掉。
        ImportClippings("重复的内容", "重复的内容", "唯一的内容");
        Assert.Equal(3, _originalRepo.GetAll().Count);

        Assert.True(_km2.CleanDatabase(_db, out _, crossBookDuplicates: false), "夹具应至少删掉 1 条重复项");
        Assert.Equal("唯一的内容", Assert.Single(_clippingRepo.GetAll()).Content);
        Assert.Equal(2, _km2.GetDeletedOriginalLines().Count);   // 回收站里躺着那两条被清理掉的

        var written = ExportToTempDir();
        Assert.Contains("唯一的内容", written);
        Assert.DoesNotContain("重复的内容", written);
        // 产物条数 = 存活条数(1),而不是原始行数(3) —— 回收站里的两条一条都不许出现
        Assert.Equal(1, CountEntries(written));
    }

    // ————————————————————— 防过滤写反:没删就一条都不许丢 —————————————————————

    /// <summary>没有任何删除时,导出必须**原样**保留全部条目(防止白名单过滤写反)。</summary>
    [Fact]
    public void Export_KeepsEveryClipping_WhenNothingDeleted() {
        ImportClippings("内容一", "内容二", "内容三");
        Assert.Equal(3, _originalRepo.GetAll().Count);

        var written = ExportToTempDir();
        Assert.Contains("内容一", written);
        Assert.Contains("内容二", written);
        Assert.Contains("内容三", written);
        // 回收站为空时,存活集合覆盖全部原始行 —— 导出条数应与原始行数一致。
        Assert.Equal(3, CountEntries(written));
    }

    /// <summary>库整体为空(没有任何原始行)时导出不该抛异常,产出的是空文件。</summary>
    [Fact]
    public void Export_OnEmptyDatabase_SucceedsAndWritesEmptyFile() {
        var outDir = Path.Combine(_dir, "out");
        Assert.True(_export.ExportOriginalClippings(outDir, ClippingsFileName, out var exception));
        Assert.Null(exception);
        Assert.Equal(string.Empty, File.ReadAllText(Path.Combine(outDir, ClippingsFileName)));
    }

    // ————————————————————— 换行与编码 —————————————————————

    /// <summary>
    /// 产物必须是 **CRLF** 分隔、且**不带 BOM** —— 与设备上的 <c>My Clippings.txt</c> 一致
    /// (formats.md §1.1)。修复前在 Linux / macOS 上会产出 LF-only 的文件。
    /// </summary>
    [Fact]
    public void Export_UsesCrlfAndNoBom() {
        ImportClippings("内容一");

        var outDir = Path.Combine(_dir, "out");
        Assert.True(_export.ExportOriginalClippings(outDir, ClippingsFileName, out _));
        var bytes = File.ReadAllBytes(Path.Combine(outDir, ClippingsFileName));

        // 无 BOM(.NET 的 StreamWriter 默认就是无 BOM 的 UTF-8,这里把它钉死免得被改回去)
        Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);

        var text = Encoding.UTF8.GetString(bytes);
        Assert.Contains("\r\n", text);
        Assert.EndsWith("\r\n", text);
        // 去掉所有 CRLF 后不该再剩裸 LF / 裸 CR
        var rest = text.Replace("\r\n", string.Empty);
        Assert.DoesNotContain("\n", rest);
        Assert.DoesNotContain("\r", rest);
    }

    // ————————————————————— 顺序 —————————————————————

    /// <summary>
    /// 导出顺序 = 插入顺序(插入顺序 = 原始文件里的追加顺序),写回设备后设备上的条目次序
    /// 就等于库里的次序。仓库层的 <c>ORDER BY rowid</c> 把这件事从"实现细节"变成**契约**。
    /// </summary>
    [Fact]
    public void Export_WritesEntriesInInsertionOrder() {
        ImportClippings("内容一", "内容二", "内容三");

        // ① 仓库层确实按插入顺序返回
        var line4s = _originalRepo.GetAll().Select(l => l.Line4).ToList();
        Assert.Equal(new[] { "内容一", "内容二", "内容三" }, line4s);

        // ② 产物里的次序与之一致
        var written = ExportToTempDir();
        var i1 = written.IndexOf("内容一", StringComparison.Ordinal);
        var i2 = written.IndexOf("内容二", StringComparison.Ordinal);
        var i3 = written.IndexOf("内容三", StringComparison.Ordinal);
        Assert.True(i1 >= 0 && i2 >= 0 && i3 >= 0, $"条目缺失:一={i1}, 二={i2}, 三={i3}");
        Assert.True(i1 < i2 && i2 < i3, $"顺序错乱:一={i1}, 二={i2}, 三={i3}");
    }

    // ————————————————————— helpers —————————————————————

    /// <summary>
    /// 走**真实导入路径**造若干条标注。每条用不同的页码/位置,于是 key(日期 + 位置)互不相同 ——
    /// 否则会被导入判重掉。内容可重复(用于「清理重复」的用例)。
    /// </summary>
    private void ImportClippings(params string[] contents) {
        var text = new StringBuilder();
        for (var i = 0; i < contents.Length; i++) {
            var page = i + 1;
            text.AppendLine("Some Book (Some Author)");
            text.AppendLine($"- 您在第 {page} 页（位置 #{page}-{page}）的标注 | 添加于 2020年1月1日星期三 上午 10:00:00");
            text.AppendLine();
            text.AppendLine(contents[i]);
            text.AppendLine("==========");
        }

        var file = Path.Combine(_dir, $"My Clippings {Guid.NewGuid().ToString("N")[..6]}.txt");
        File.WriteAllText(file, text.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        Assert.True(_km2.ImportKindleClippings(file, out _), "导入夹具失败");
    }

    /// <summary>按内容取 key(仅用于内容唯一的夹具)。</summary>
    private string KeyOfContent(string content) =>
        _clippingRepo.GetAll().Single(c => c.Content == content).Key;

    /// <summary>导出到临时输出目录并返回产物文本。</summary>
    private string ExportToTempDir() {
        var outDir = Path.Combine(_dir, "out-" + Guid.NewGuid().ToString("N")[..6]);
        Assert.True(_export.ExportOriginalClippings(outDir, ClippingsFileName, out var exception));
        Assert.Null(exception);
        return File.ReadAllText(Path.Combine(outDir, ClippingsFileName));
    }

    /// <summary>数产物里有几条标注 —— 每条以一行 <c>==========</c> 收尾。</summary>
    private static int CountEntries(string text) {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf("==========", index, StringComparison.Ordinal)) >= 0) {
            count++;
            index += "==========".Length;
        }
        return count;
    }

    /// <summary>取「同步到设备」时被写出的那份文件的文本。</summary>
    private string SyncText() {
        Assert.NotNull(_device.SyncedBytes);
        return Encoding.UTF8.GetString(_device.SyncedBytes!);
    }

    /// <summary>
    /// 极简设备替身:只记录「同步文件到设备」时被写出的字节,不做任何真机操作。
    /// </summary>
    private sealed class FakeDeviceManager : IDeviceManager {
        public byte[]? SyncedBytes { get; private set; }
        public string? SyncedTargetName { get; private set; }

        public Device.Type DeviceType => Device.Type.USB;
        public string DriveLetter => "/dev/null";
        public bool IsConnected => true;
        public event Action<bool>? ConnectionChanged { add { } remove { } }

        public void StartWatching() { }
        public bool IsKindleConnected() => true;
        public string GetKindleVersionText() => "Test 1.0";
        public DeviceSummary? GetDeviceInfo() => null;

        public bool ImportFilesFromDevice(string backupClippingsPath, string backupWordsPath, out Exception? exception,
            IProgress<OperationProgress>? progress = null) {
            exception = null;
            return true;
        }

        public void SyncFileToDevice(string exportedFilePath, string targetFileName) {
            SyncedBytes = File.ReadAllBytes(exportedFilePath);
            SyncedTargetName = targetFileName;
        }

        public void Dispose() { }
    }
}
