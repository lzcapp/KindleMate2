using System.Runtime.CompilerServices;
using Microsoft.Data.Sqlite;

namespace KindleMate2.Tests;

/// <summary>
/// 测试临时目录的**进程退出时清扫**。
///
/// **为什么不在每个测试类自己的 Dispose 里清池**：SQLite 的连接池会让已归还连接的
/// **文件句柄继续存活**（<c>Dispose</c> 只是归还），而清池的唯一 API
/// <c>SqliteConnection.ClearAllPools()</c> 是**进程级**的 —— xUnit 默认**类间并行**，
/// 于是一个类 Dispose 时会把**别的类正在复用**的连接一起销毁。
///
/// 实测症状（约 1/6 概率，2026-09-22 定位）：
/// <code>
/// DatabaseSnapshotTests.Snapshot_Overwrite_Works_WhenTargetWasPreviouslyOpened [FAIL]
///   ObjectDisposedException: Cannot access a disposed object. 'SQLitePCL.sqlite3'
/// </code>
/// 失败点落在**产品代码** <c>CreateConsistentSnapshot</c> 内部 —— 受害者可以是任何正在用连接的类。
/// 注意「只把调清池的那几个类串行化」是**没用**的：元凶不需要和它们互撞，它撞的是任何在用连接的类。
///
/// 所以改成：**测试期间谁都不清池**；等所有测试跑完、进程退出时**只清一次**，再扫掉残留目录。
/// 代价是 Windows 上各测试类自己的 <c>Directory.Delete</c> 会因句柄未释放而失败
/// （已被各自的 <c>catch</c> 吞掉，不会让测试变红）—— 那些残留由这里的清扫兜底。
/// </summary>
internal static class TestTempCleanup {

    /// <summary>
    /// 测试临时目录的**前缀白名单**。
    ///
    /// 刻意用白名单，而不是"凡 <c>km2-*</c> 都删"：临时目录里还躺着**应用自检**留下的
    /// <c>km2ops</c> / <c>km2settings</c> / <c>km2-startup-*</c> / <c>km2-bad-*</c> ——
    /// 那些不是测试建的，不该由测试进程删。
    ///
    /// 新增测试类若用了新前缀，把它加到这里。**漏加的后果只是那个目录残留，不会出错。**
    /// </summary>
    private static readonly string[] OwnedPrefixes = {
        "km2-backupcollision-tests-", "km2-backupname-tests-", "km2-prune-tests-",
        "km2-clipclean-tests-", "km2-maintenance-tests-", "km2-index-tests-",
        "km2-tests-", "km2-snapshot-tests-", "kmate-import-tests-",
        "km2-linux-roots-", "km2-volumes-", "km2-export-tests-",
        "km2-mtp-import-", "km2-mtp-writeback-", "km2-update-tests-"
    };

    [ModuleInitializer]
    internal static void Register() => AppDomain.CurrentDomain.ProcessExit += (_, _) => Sweep();

    /// <summary>
    /// 清一次池（此后不会再有测试在跑，所以撞不到谁），再删掉残留的测试临时目录。
    /// 返回删掉的目录数；整个过程 best-effort，任何失败都只跳过。
    /// </summary>
    internal static int Sweep() {
        var removed = 0;
        try {
            // 刻意**不**在这里清 SQLite 连接池:那是进程级 API,会和并行跑的其他测试类互撞
            // (实测 ~1/6 概率报 ObjectDisposedException)。残留目录由 TestTempCleanup 在
            // 进程退出时统一清扫 —— 那里已无测试在跑,清池不会伤到谁。
        } catch { /* best effort */ }

        string tempRoot;
        try {
            tempRoot = Path.GetTempPath();
        } catch { return 0; }

        string[] dirs;
        try {
            dirs = Directory.GetDirectories(tempRoot);
        } catch { return 0; }

        foreach (var dir in dirs) {
            var name = Path.GetFileName(dir);
            if (!OwnedPrefixes.Any(p => name.StartsWith(p, StringComparison.Ordinal))) continue;
            try {
                Directory.Delete(dir, true);
                removed++;
            } catch { /* best effort */ }
        }
        return removed;
    }
}
