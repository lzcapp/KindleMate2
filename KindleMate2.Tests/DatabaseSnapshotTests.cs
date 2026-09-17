using System.Text;
using Microsoft.Data.Sqlite;
using Xunit;
using KindleMate2.Infrastructure.Helpers;

namespace KindleMate2.Tests;

/// <summary>
/// <see cref="DatabaseHelper.CreateConsistentSnapshot"/> 的用例 —— 备份与（将来的）多机同步
/// 快照共用的唯一出口。
///
/// 这组用例的重点不只是"能产出文件"，而是**失败时必须明确报错**：备份一旦产出一份
/// "看起来正常、实际不一致"的文件，用户是在真正需要恢复的那一刻才会发现。所以下面既
/// 验证产出的快照可用，也逐条钉住各个失败路径，并回归两个实测踩过的坑：
/// 目标被别的连接占用过之后的覆盖、以及并发写入时绝不留下损坏文件。
/// </summary>
public sealed class DatabaseSnapshotTests : IDisposable {
    private const int SeededRows = 500;

    private readonly string _dir;
    private readonly string _source;

    public DatabaseSnapshotTests() {
        _dir = Path.Combine(Path.GetTempPath(), "km2-snapshot-tests-" + Guid.NewGuid().ToString("N")[..10]);
        Directory.CreateDirectory(_dir);

        _source = Path.Combine(_dir, "KM2.dat");
        Assert.True(DatabaseHelper.CreateDatabase(_source, out var ex), "CreateDatabase failed: " + ex.Message);
        SeedClippings(_source, SeededRows);
    }

    public void Dispose() {
        try {
            // SQLite 的连接池会让已归还连接的文件句柄继续存活(Dispose 只是归还),
            // 不清池的话在 Windows 上删不掉这个临时目录。
            SqliteConnection.ClearAllPools();
            Directory.Delete(_dir, true);
        } catch { /* best effort */ }
    }

    // ————————————————————————— 正常路径 —————————————————————————

    [Fact]
    public void Snapshot_IsConsistent_andPreservesRowCount() {
        var snapshot = Path.Combine(_dir, "snapshot.dat");

        DatabaseHelper.CreateConsistentSnapshot(_source, snapshot);

        Assert.True(File.Exists(snapshot));
        var (integrity, rows) = Inspect(snapshot);
        Assert.Equal("ok", integrity);
        Assert.Equal(SeededRows, rows);
    }

    [Fact]
    public void Snapshot_PreservesContent_notJustRowCount() {
        var snapshot = Path.Combine(_dir, "snapshot.dat");

        DatabaseHelper.CreateConsistentSnapshot(_source, snapshot);

        // 行数一致还不够 —— 内容必须逐行相同(排序后比对,避免依赖物理顺序)。
        Assert.Equal(DumpClippings(_source), DumpClippings(snapshot));
    }

    [Fact]
    public void Snapshot_LeavesNoTempFileBehind() {
        var snapshot = Path.Combine(_dir, "snapshot.dat");

        DatabaseHelper.CreateConsistentSnapshot(_source, snapshot);

        Assert.Empty(Directory.GetFiles(_dir, "*.snapshot-tmp"));
    }

    // ————————————————————————— 目标已存在 —————————————————————————

    [Fact]
    public void Snapshot_ExistingTarget_WithoutOverwrite_Throws_andLeavesItUntouched() {
        var snapshot = Path.Combine(_dir, "snapshot.dat");
        File.WriteAllText(snapshot, "occupied");

        Assert.Throws<IOException>(() => DatabaseHelper.CreateConsistentSnapshot(_source, snapshot));

        // 关键:拒绝覆盖时,原有文件必须原封不动(它可能是用户上一次的有效备份)。
        Assert.Equal("occupied", File.ReadAllText(snapshot));
    }

    [Fact]
    public void Snapshot_ExistingTarget_WithOverwrite_ReplacesIt() {
        var snapshot = Path.Combine(_dir, "snapshot.dat");
        File.WriteAllText(snapshot, "occupied");

        DatabaseHelper.CreateConsistentSnapshot(_source, snapshot, overwrite: true);

        var (integrity, rows) = Inspect(snapshot);
        Assert.Equal("ok", integrity);
        Assert.Equal(SeededRows, rows);
    }

    /// <summary>
    /// 回归实测踩过的坑:该目标曾被本进程打开过(连接已 Dispose、但句柄还在连接池里),
    /// 此时覆盖在 Windows 上会抛 UnauthorizedAccessException —— 而它**不是** IOException 的子类,
    /// 只 catch IOException 的重试逻辑会完全失效。
    /// </summary>
    [Fact]
    public void Snapshot_Overwrite_Works_WhenTargetWasPreviouslyOpened() {
        var snapshot = Path.Combine(_dir, "snapshot.dat");
        File.Copy(_source, snapshot);
        using (var conn = new SqliteConnection($"Data Source={snapshot};Mode=ReadOnly;")) {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM clippings;";
            cmd.ExecuteScalar();
        }

        DatabaseHelper.CreateConsistentSnapshot(_source, snapshot, overwrite: true);

        var (integrity, rows) = Inspect(snapshot);
        Assert.Equal("ok", integrity);
        Assert.Equal(SeededRows, rows);
    }

    /// <summary>
    /// 目标被独占占用时,<c>ReplaceFile</c> 的两次 <c>File.Move</c> 都会失败 —— 此时临时快照
    /// **不能**留在用户的 Backups 目录里:它的名字带时间戳,下一次备份不会复用同一路径,
    /// 也就永远轮不到清理,只会越积越多。这正是本 PR 要消除的那类"Backups 里的迷惑文件"。
    /// </summary>
    [Fact]
    public void Snapshot_TargetLocked_Throws_andLeavesNoTempFileBehind() {
        var snapshot = Path.Combine(_dir, "snapshot.dat");
        File.WriteAllText(snapshot, "occupied");

        using (new FileStream(snapshot, FileMode.Open, FileAccess.Read, FileShare.None)) {
            var ex = Record.Exception(() =>
                DatabaseHelper.CreateConsistentSnapshot(_source, snapshot, overwrite: true));

            Assert.NotNull(ex);
            // 目标被占用时 Windows 抛的是 UnauthorizedAccessException(不继承 IOException),
            // 两种都接受 —— 这里钉的是"明确失败"这个行为,而不是具体异常类型。
            Assert.True(ex is IOException or UnauthorizedAccessException,
                $"非预期的异常类型: {ex.GetType().Name}");
        }

        Assert.Empty(Directory.GetFiles(_dir, "*.snapshot-tmp"));
        // 被占用的原文件也不能被动过 —— 它可能是用户上一份有效备份。
        Assert.Equal("occupied", File.ReadAllText(snapshot));
    }

    // ————————————————————————— 参数与路径 —————————————————————————

    [Fact]
    public void Snapshot_MissingSource_Throws() {
        var missing = Path.Combine(_dir, "nope.dat");
        Assert.Throws<FileNotFoundException>(() =>
            DatabaseHelper.CreateConsistentSnapshot(missing, Path.Combine(_dir, "out.dat")));
    }

    [Fact]
    public void Snapshot_MissingTargetDirectory_Throws() {
        var missing = Path.Combine(_dir, "no-such-dir", "out.dat");
        Assert.Throws<DirectoryNotFoundException>(() =>
            DatabaseHelper.CreateConsistentSnapshot(_source, missing));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Snapshot_BlankSource_Throws(string blankSource) {
        Assert.Throws<ArgumentException>(() =>
            DatabaseHelper.CreateConsistentSnapshot(blankSource, Path.Combine(_dir, "out.dat")));
    }

    [Fact]
    public void Snapshot_BlankDestination_Throws() {
        Assert.Throws<ArgumentException>(() => DatabaseHelper.CreateConsistentSnapshot(_source, "  "));
    }

    /// <summary>
    /// PRAGMA busy_timeout 只能插值(不支持参数绑定),所以负值必须在托管层挡住 ——
    /// 否则 SQLite 的行为依版本而异。
    /// </summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Snapshot_NegativeBusyTimeout_Throws(int timeoutMs) {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DatabaseHelper.CreateConsistentSnapshot(_source, Path.Combine(_dir, "out.dat"), busyTimeoutMs: timeoutMs));
    }

    // ————————————————————————— 并发 —————————————————————————

    /// <summary>
    /// 有另一个连接正在写(未提交)时,快照只允许两种结局:给出一份**完好**的快照,
    /// 或者明确抛错。绝不允许"看起来成功但内容已损坏" —— 那正是 File.Copy 的失败模式。
    /// </summary>
    [Fact]
    public void Snapshot_WithConcurrentWriter_IsEitherConsistent_orFailsLoudly() {
        var snapshot = Path.Combine(_dir, "snapshot.dat");

        using var writer = new SqliteConnection(DatabaseHelper.GetConnectionString(_source));
        writer.Open();
        Exec(writer, "PRAGMA cache_size = 3;");
        Exec(writer, "BEGIN;");
        for (var i = 0; i < 2000; i++) {
            Exec(writer, $"INSERT INTO clippings ([key],[content],[bookname]) VALUES ('w{i}','{new string('w', 200)}','book');");
        }

        try {
            DatabaseHelper.CreateConsistentSnapshot(_source, snapshot, overwrite: true, busyTimeoutMs: 500);

            var (integrity, rows) = Inspect(snapshot);
            Assert.Equal("ok", integrity);
            // writer 的 2000 行是**未提交**的(COMMIT 在 finally 里),所以快照里必须恰好是种子行数。
            // 只断言"不少于"等于放过最坏的情况:读到了别处的未提交数据。
            Assert.Equal(SeededRows, rows);
        } catch (InvalidOperationException) {
            // 锁等待超时是**可接受**的结局 —— 关键是它报了错,而不是留下坏文件。
            Assert.False(File.Exists(snapshot), "明确失败时不应留下目标文件");
        } finally {
            Exec(writer, "COMMIT;");
        }
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

    private static void Exec(SqliteConnection conn, string sql) {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
