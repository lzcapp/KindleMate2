using Xunit;
using KindleMate2.Infrastructure.Helpers;

namespace KindleMate2.Tests;

/// <summary>
/// 同一秒内连续备份**不能**失败,更不能互相覆盖。
///
/// 回归背景:备份文件名的时间戳只到秒(<c>yyyyMMdd_HHmmss</c>),而底层
/// <see cref="DatabaseHelper.CreateConsistentSnapshot"/> 刻意**拒绝覆盖已存在的目标**
/// (那条守卫有用例钉住,是对的 —— 不能毁掉上一份)。但此前 <c>BackupDatabase</c> 直接
/// 把撞名交给它去抛,于是"同一秒内第二次备份"整条失败。
///
/// 真实后果不是理论上的:「维护数据库」在改数据前会无条件备份一次,若恰好与刚才的手动备份
/// 落在同一秒,整条维护就以「维护失败」收场、**还不给原因**。2026-09-22 实测 <c>--ops</c>
/// 连跑 6 次挂 2 次,且在干净 main 上同样复现。
///
/// 修法:撞名时自动加序号另存 —— "拒绝覆盖"的正确代价是**换个名字存下来**,
/// 不是把用户的备份请求整个打回。
/// </summary>
public sealed class BackupCollisionTests : IDisposable {
    private readonly string _dir;
    private readonly string _dbPath;

    public BackupCollisionTests() {
        _dir = Path.Combine(Path.GetTempPath(), "km2-backupcollision-tests-" + Guid.NewGuid().ToString("N")[..10]);
        Directory.CreateDirectory(_dir);

        _dbPath = Path.Combine(_dir, "KM2.dat");
        Assert.True(DatabaseHelper.CreateDatabase(_dbPath, out var ex), "CreateDatabase failed: " + ex.Message);
    }

    public void Dispose() {
        try {
            // 刻意**不**在这里清 SQLite 连接池:那是进程级 API,会和并行跑的其他测试类互撞
            // (实测 ~1/6 概率报 ObjectDisposedException)。残留目录由 TestTempCleanup 在
            // 进程退出时统一清扫 —— 那里已无测试在跑,清池不会伤到谁。
            Directory.Delete(_dir, true);
        } catch { /* best effort */ }
    }

    [Fact]
    public void BackupDatabase_RepeatedInQuickSuccession_KeepsEveryBackup() {
        var backupDir = Path.Combine(_dir, "Backups");
        const int times = 4;

        var produced = new List<string>();
        for (var i = 0; i < times; i++) {
            var before = Directory.Exists(backupDir)
                ? Directory.GetFiles(backupDir).ToHashSet(StringComparer.Ordinal)
                : new HashSet<string>(StringComparer.Ordinal);

            // 连着来 —— 库很小,这几次基本落在同一秒里 ⇒ 第 2 次起必然撞名。
            // 旧实现会在这里抛 IOException,整条备份失败。
            DatabaseHelper.BackupDatabase(_dir, backupDir, Path.GetFileName(_dbPath));

            var added = Directory.GetFiles(backupDir).Where(f => !before.Contains(f)).ToList();
            produced.Add(Assert.Single(added));
        }

        // ① 每一份都真的存下来了
        Assert.Equal(times, Directory.GetFiles(backupDir, "*.dat").Length);
        // ② 谁也没覆盖谁
        Assert.Equal(times, produced.Distinct(StringComparer.Ordinal).Count());
        // ③ 都是完整备份,不是 0 字节残骸
        Assert.All(produced, f => Assert.True(new FileInfo(f).Length > 0, f + " 是空的"));
        // ④ **文件名序 = 时间序**。PruneBackups 按文件名字典序判定新旧,只留最新几份 ——
        //    同一秒内的多份也必须"越晚越靠后",否则清理会先删掉较新的那一份。
        //    (后缀用 `_2` 而不是 `-2` 就是为了这条:`_`(0x5F) 排在 `.`(0x2E) 之后。)
        Assert.Equal(produced, produced.OrderBy(f => f, StringComparer.Ordinal).ToList());
    }

    [Fact]
    public void BackupDatabase_StillRefusesToOverwriteAnExistingFile() {
        var backupDir = Path.Combine(_dir, "Backups");
        Directory.CreateDirectory(backupDir);

        DatabaseHelper.BackupDatabase(_dir, backupDir, Path.GetFileName(_dbPath));
        var first = Assert.Single(Directory.GetFiles(backupDir));
        var firstLength = new FileInfo(first).Length;
        var firstWriteTime = File.GetLastWriteTimeUtc(first);

        DatabaseHelper.BackupDatabase(_dir, backupDir, Path.GetFileName(_dbPath));

        // 加序号**不是**覆盖:第一份必须原封不动(长度与写入时间都不变)。
        // 这一条与下面那条一起,才算把"不静默覆盖上一份备份"这条底线钉住。
        Assert.Equal(firstLength, new FileInfo(first).Length);
        Assert.Equal(firstWriteTime, File.GetLastWriteTimeUtc(first));
    }
}
