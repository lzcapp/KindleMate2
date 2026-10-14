using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;
using Xunit;
using KindleMate2.Infrastructure.Helpers;
using KindleMate2.Shared.Constants;

namespace KindleMate2.Tests;

/// <summary>
/// 「清空全部数据」动手前落下的那份**保底备份**,必须是一份**可被 SQLite 正常打开、内容完整**的快照。
///
/// 回归背景:<c>MainWindowViewModel.ClearAllDataAsync</c> 此前用裸 <c>File.Copy</c> 复制库文件。
/// 而 <c>DatabaseHelper.CreateConsistentSnapshot</c> 的文档(<c>DatabaseHelper.cs:291-307</c>)已经论证过:
/// 直接复制库文件会在 rollback journal 下**静默**产出部分改写的文件(实测 3272 / 应 11001 行)、
/// 在 WAL 下丢掉最近提交 —— 也就是说,备份"看起来成功了",却在真正要恢复时读不出来。
/// 清空前的这份备份是用户唯一的回头路,恰在此处最不能失效,故改用 <c>VACUUM INTO</c> 快照。
///
/// 可测边界:Avalonia 的 VM 不在测试工程引用范围内(见 AGENTS.md「视图层行为单测够不着」),
/// 因此这里直接钉住 VM 现在调用的**那条 DatabaseHelper 路径** —— 复刻 VM 的保底备份文件名与落点,
/// 再断言产出的确实是一份完整快照。VM 层的接线由 <c>--ops</c> 探针实跑 <c>ClearAllDataAsync</c> 覆盖。
/// </summary>
public sealed class ClearAllBackupTests : IDisposable {
    private const int SeededRows = 300;

    private readonly string _dir;
    private readonly string _dbPath;

    public ClearAllBackupTests() {
        _dir = Path.Combine(Path.GetTempPath(), "km2-clearall-backup-tests-" + Guid.NewGuid().ToString("N")[..10]);
        Directory.CreateDirectory(_dir);

        _dbPath = Path.Combine(_dir, AppConstants.DatabaseFileName);
        Assert.True(DatabaseHelper.CreateDatabase(_dbPath, out var ex), "CreateDatabase failed: " + ex.Message);
        SeedClippings(_dbPath, SeededRows);
    }

    public void Dispose() {
        try {
            // 刻意**不**在这里清 SQLite 连接池:那是进程级 API,会和并行跑的其他测试类互撞
            // (实测 ~1/6 概率报 ObjectDisposedException)。残留目录由 TestTempCleanup 在
            // 进程退出时统一清扫 —— 那里已无测试在跑,清池不会伤到谁。
            Directory.Delete(_dir, true);
        } catch { /* best effort */ }
    }

    /// <summary>
    /// 复刻 <c>MainWindowViewModel.ClearAllDataAsync</c> 的保底备份文件名表达式:
    /// <c>{库名去掉扩展名}_{stamp}{扩展名}</c>,<c>stamp</c> 用
    /// <see cref="AppConstants.FileTimestampFormat"/> + <see cref="CultureInfo.InvariantCulture"/>。
    /// 名字带可读时间戳是有意为之 —— 用户要能按名字找回旧备份。
    /// </summary>
    private static string ClearAllBackupFileName(string databasePath) {
        var stamp = DateTime.Now.ToString(AppConstants.FileTimestampFormat, CultureInfo.InvariantCulture);
        return $"{Path.GetFileNameWithoutExtension(databasePath)}_{stamp}{Path.GetExtension(databasePath)}";
    }

    [Fact]
    public void ClearAllBackup_IsOpenableSnapshot_WithFullContent() {
        var backupDir = Path.Combine(_dir, AppConstants.BackupsPathName);
        Directory.CreateDirectory(backupDir);
        var dest = Path.Combine(backupDir, ClearAllBackupFileName(_dbPath));

        // VM 现在走的那条路径:一致性快照(而非 File.Copy)。
        DatabaseHelper.CreateConsistentSnapshot(_dbPath, dest, overwrite: true);

        Assert.True(File.Exists(dest));
        Assert.True(new FileInfo(dest).Length > 0, "保底备份是 0 字节残骸");

        // ① 能被 SQLite 正常打开且自检通过 —— 这正是 File.Copy 做不到的。
        var (integrity, rows) = Inspect(dest);
        Assert.Equal("ok", integrity);
        // ② 行数完整(File.Copy 的失败模式是"能打开但漏行")。
        Assert.Equal(SeededRows, rows);
        // ③ 内容逐行一致,而不只是行数对得上。
        Assert.Equal(DumpClippings(_dbPath), DumpClippings(dest));
    }

    /// <summary>
    /// 保底备份落在 <c>Backups</c> 根目录、文件名**不含** <c>_backup_</c> 中缀,因此
    /// <see cref="DatabaseHelper.PruneBackups"/>(退出备份的"只留最新 N 份"策略)不会把它当成本库备份删掉。
    /// 这条钉住一个刻意的设计取舍:保底备份是用户唯一的回头路,不该被自动清理误伤。
    /// </summary>
    [Fact]
    public void ClearAllBackup_IsNotTouchedByPruneBackups() {
        var backupDir = Path.Combine(_dir, AppConstants.BackupsPathName);
        Directory.CreateDirectory(backupDir);

        var backupName = ClearAllBackupFileName(_dbPath);
        // 可读时间戳:KM2_yyyyMMdd_HHmmss.dat(定长零填充 ⇒ 字典序 = 时间序)。
        Assert.Matches(@"^KM2_\d{8}_\d{6}\.dat$", backupName);

        File.WriteAllText(Path.Combine(backupDir, backupName), "snapshot");
        File.WriteAllText(Path.Combine(backupDir, "KM2_backup_20260101_000000.dat"), "x");
        File.WriteAllText(Path.Combine(backupDir, "KM2_backup_20260921_221834.dat"), "x");

        // 只留 1 份 —— 退出备份那两份里删掉较旧的一份,保底备份必须原封不动。
        var deleted = DatabaseHelper.PruneBackups(backupDir, 1, AppConstants.DatabaseFileName);

        Assert.Equal(1, deleted);
        Assert.True(File.Exists(Path.Combine(backupDir, backupName)), "保底备份不该被退出备份的保留策略删掉");
        Assert.Equal(
            new[] { backupName, "KM2_backup_20260921_221834.dat" },
            Directory.GetFiles(backupDir).Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal).ToArray());
    }

    // ————————————————————————— helpers —————————————————————————

    private static void SeedClippings(string dbPath, int count) {
        using var conn = new SqliteConnection(DatabaseHelper.GetConnectionString(dbPath));
        conn.Open();
        using var tx = conn.BeginTransaction();
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "INSERT INTO clippings ([key],[content],[bookname],[authorname]) VALUES (@k,@c,@b,@a)";
        var pk = cmd.CreateParameter(); pk.ParameterName = "@k"; cmd.Parameters.Add(pk);
        var pc = cmd.CreateParameter(); pc.ParameterName = "@c"; cmd.Parameters.Add(pc);
        var pb = cmd.CreateParameter(); pb.ParameterName = "@b"; cmd.Parameters.Add(pb);
        var pa = cmd.CreateParameter(); pa.ParameterName = "@a"; cmd.Parameters.Add(pa);
        for (var i = 0; i < count; i++) {
            pk.Value = $"k{i}";
            pc.Value = new string('x', 100);
            pb.Value = "book";
            pa.Value = "author";
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    private static (string Integrity, long Rows) Inspect(string dbPath) {
        using var conn = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly;");
        conn.Open();
        string integrity;
        using (var cmd = conn.CreateCommand()) {
            cmd.CommandText = "PRAGMA integrity_check;";
            integrity = cmd.ExecuteScalar()?.ToString() ?? "?";
        }
        long rows;
        using (var cmd = conn.CreateCommand()) {
            cmd.CommandText = "SELECT COUNT(*) FROM clippings;";
            rows = Convert.ToInt64(cmd.ExecuteScalar());
        }
        return (integrity, rows);
    }

    private static string DumpClippings(string dbPath) {
        using var conn = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly;");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT [key],[content],[bookname],[authorname] FROM clippings ORDER BY [key];";
        var sb = new StringBuilder();
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) {
            sb.Append(reader.GetString(0)).Append('\u0001')
              .Append(reader.GetString(1)).Append('\u0001')
              .Append(reader.GetString(2)).Append('\u0001')
              .Append(reader.IsDBNull(3) ? string.Empty : reader.GetString(3))
              .Append('\n');
        }
        return sb.ToString();
    }
}
