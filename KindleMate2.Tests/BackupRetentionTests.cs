using KindleMate2.Infrastructure.Helpers;
using Xunit;

namespace KindleMate2.Tests;

/// <summary>
/// 退出备份的保留策略:目录里只留最新 N 份(见 <c>DatabaseHelper.PruneBackups</c>)。
///
/// 背景:退出备份每次关闭都产生一份,不收敛的话 Backups 里会堆成一长串时间戳文件。
/// 现在它落在 <c>Backups/OnExit/</c> 并只保留最新 3 份,清理逻辑因此必须满足三点:
/// <list type="number">
///   <item>按**文件名里的时间戳**判新旧 —— 而不是 mtime,后者会被复制 / 云同步改写;</item>
///   <item>只动**本库**的备份(<c>KM2_backup_*.dat</c>),目录里其它东西一概不碰;</item>
///   <item>**绝不抛异常** —— 它跑在进程退出路径上,抛出去只会变成日志里的噪音。</item>
/// </list>
///
/// 本文件直接造文件名来验证策略本身(不真的跑 VACUUM INTO):同秒内连续备份会因文件名撞车而
/// 失败,靠真实备份来凑份数既慢又不稳,而这里要钉住的只是"选哪几份删"。
/// </summary>
public sealed class BackupRetentionTests : IDisposable {
    private readonly string _dir;

    public BackupRetentionTests() {
        _dir = Path.Combine(Path.GetTempPath(), "km2-prune-tests-" + Guid.NewGuid().ToString("N")[..10]);
        Directory.CreateDirectory(_dir);
    }

    public void Dispose() {
        try {
            Directory.Delete(_dir, true);
        } catch { /* best effort */ }
    }

    private void Make(string fileName) => File.WriteAllText(Path.Combine(_dir, fileName), "x");

    private string[] Remaining() {
        var names = new List<string>();
        foreach (var path in Directory.GetFiles(_dir)) {
            names.Add(Path.GetFileName(path));
        }
        names.Sort(StringComparer.Ordinal);
        return names.ToArray();
    }

    [Fact]
    public void PruneBackups_KeepsNewestThree_AndDeletesOlder() {
        Make("KM2_backup_20260918_090000.dat");
        Make("KM2_backup_20260919_090000.dat");
        Make("KM2_backup_20260920_090000.dat");
        Make("KM2_backup_20260921_090000.dat");
        Make("KM2_backup_20260921_221834.dat");

        var deleted = DatabaseHelper.PruneBackups(_dir, 3, "KM2.dat");

        Assert.Equal(2, deleted);
        Assert.Equal(new[] {
            "KM2_backup_20260920_090000.dat",
            "KM2_backup_20260921_090000.dat",
            "KM2_backup_20260921_221834.dat",
        }, Remaining());
    }

    /// <summary>份数还没到上限时不该动任何东西(否则"保留 3 份"会退化成"每次只留 1 份")。</summary>
    [Fact]
    public void PruneBackups_LeavesEverything_WhenAtOrBelowKeepCount() {
        Make("KM2_backup_20260920_090000.dat");
        Make("KM2_backup_20260921_090000.dat");

        Assert.Equal(0, DatabaseHelper.PruneBackups(_dir, 3, "KM2.dat"));
        Assert.Equal(2, Remaining().Length);
    }

    /// <summary>
    /// 目录里还住着别的东西:另一个库的备份、用户手写的说明、快照临时文件。
    /// 清理必须只认 <c>KM2_backup_*</c>,否则会误删用户数据。
    /// </summary>
    [Fact]
    public void PruneBackups_TouchesOnlyMatchingBackupsOfTheSameDatabase() {
        for (var i = 0; i < 5; i++) {
            Make($"KM2_backup_2026092{i}_090000.dat");
        }
        Make("KM3_backup_20260901_090000.dat");   // 别的库的备份
        Make("KM2.dat");                          // BackupClippings 产出的可读文本副本
        Make("readme.txt");
        Make("KM2_backup_20260925_090000.dat.snapshot-tmp");   // 快照残留

        var deleted = DatabaseHelper.PruneBackups(_dir, 3, "KM2.dat");

        Assert.Equal(2, deleted);
        var remaining = Remaining();
        Assert.Contains("KM3_backup_20260901_090000.dat", remaining);
        Assert.Contains("KM2.dat", remaining);
        Assert.Contains("readme.txt", remaining);
        Assert.Contains("KM2_backup_20260925_090000.dat.snapshot-tmp", remaining);
        Assert.Equal(4 + 3, remaining.Length);   // 3 份 KM2 备份 + 上面 4 个"不该动"的
    }

    /// <summary>目录还不存在(从没备份过)不是异常。</summary>
    [Fact]
    public void PruneBackups_MissingDirectory_ReturnsZero() {
        var missing = Path.Combine(_dir, "OnExit");
        Assert.False(Directory.Exists(missing));

        Assert.Equal(0, DatabaseHelper.PruneBackups(missing, 3, "KM2.dat"));
    }

    /// <summary>
    /// keepCount ≤ 0 按 1 处理:调用方传 0 的意图不可能是"把备份全删光" ——
    /// 真删光就等于用户在退出时丢掉了唯一的回滚点。
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void PruneBackups_NonPositiveKeepCount_StillKeepsOne(int keepCount) {
        Make("KM2_backup_20260920_090000.dat");
        Make("KM2_backup_20260921_090000.dat");

        Assert.Equal(1, DatabaseHelper.PruneBackups(_dir, keepCount, "KM2.dat"));
        Assert.Equal(new[] { "KM2_backup_20260921_090000.dat" }, Remaining());
    }

    /// <summary>
    /// 排序按名字里的时间戳,与文件 mtime 无关。
    /// 这里把**最旧**的那份文件的 mtime 改成最新 —— 若实现改用 mtime 排序,它就会被留下、真正的新备份被删。
    /// (复制 / 云同步 / 解压都会重写 mtime,所以这个场景在真实环境里很常见。)
    /// </summary>
    [Fact]
    public void PruneBackups_OrdersByFileNameTimestamp_NotByWriteTime() {
        Make("KM2_backup_20260918_090000.dat");
        Make("KM2_backup_20260920_090000.dat");
        Make("KM2_backup_20260921_090000.dat");
        Make("KM2_backup_20260922_090000.dat");

        // 把最旧那份的 mtime 设成"未来",制造"它看起来最新"的假象
        File.SetLastWriteTimeUtc(Path.Combine(_dir, "KM2_backup_20260918_090000.dat"), DateTime.UtcNow.AddDays(1));

        Assert.Equal(1, DatabaseHelper.PruneBackups(_dir, 3, "KM2.dat"));
        Assert.Equal(new[] {
            "KM2_backup_20260920_090000.dat",
            "KM2_backup_20260921_090000.dat",
            "KM2_backup_20260922_090000.dat",
        }, Remaining());
    }

    /// <summary>
    /// 名字里的时间戳是 <c>yyyyMMdd_HHmmss</c> 定长零填充 —— 字典序即时间序,这是上面排序的前提。
    /// 一旦有人把格式改成不定长(例如去掉前导零),这里会先炸。
    /// </summary>
    [Fact]
    public void BackupFileName_TimestampIsLexicographicallySortable() {
        Make("KM2_backup_20260101_000000.dat");
        Make("KM2_backup_20260921_221834.dat");

        Assert.Equal(new[] {
            "KM2_backup_20260101_000000.dat",
            "KM2_backup_20260921_221834.dat",
        }, Remaining());
    }
}
