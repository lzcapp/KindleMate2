using KindleMate2.Infrastructure.Helpers;
using KindleMate2.Infrastructure.Repositories.KM2DB;
using Microsoft.Data.Sqlite;
using Xunit;

namespace KindleMate2.Tests;

/// <summary>
/// <c>--ops</c> 写操作端到端探针的**夹具**准备,兼一条"建库即可用"的集成断言。
///
/// <para>
/// 为什么需要:<c>--ops</c> 要求传入一份能打开的 KM2 库(它会在临时副本上跑
/// 导入 → 导出 → 备份 → 重命名 → … → 清空 → 重导)。而仓库按约定**不提交**任何库样本
/// (见 <c>KindleMate2.Avalonia/README.md</c>:测试夹具在仓库外),CI runner 上也没有现成的一份。
/// "启动自动建库"那条路径只走 GUI 启动或 <c>--smoke</c> 的启动探针,而探针建完就把目录删了。
/// 所以这里用**生产同款**的 <see cref="DatabaseHelper.CreateDatabase"/> 造一份空库:
/// 断言它建完立刻能被仓储读(证明不是"能建不能开"的空壳),并在设了
/// <c>KM2_OPS_FIXTURE</c> 环境变量时把这份库落一份到指定路径,供随后的 CI 步骤当输入。
/// </para>
/// <para>
/// 该环境变量在普通 <c>dotnet test</c> 下为空 —— 此时用例照样跑完整断言,只是不额外落盘。
/// </para>
/// </summary>
public sealed class OpsProbeFixtureTests {
    [Fact]
    public void CreateDatabase_ProducesAnImmediatelyUsableKm2Database() {
        var dir = Path.Combine(Path.GetTempPath(), "km2-ops-fixture-" + Guid.NewGuid().ToString("N")[..10]);
        Directory.CreateDirectory(dir);
        var db = Path.Combine(dir, "KM2.dat");
        try {
            Assert.True(DatabaseHelper.CreateDatabase(db, out var ex), "CreateDatabase failed: " + ex.Message);

            // ① 表真的建出来了(不是只落了个文件)
            using (var conn = new SqliteConnection(DatabaseHelper.GetConnectionString(db))) {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT count(*) FROM sqlite_master WHERE type='table' AND name='clippings'";
                Assert.Equal(1L, cmd.ExecuteScalar());
            }

            // ② 仓储能直接读它(与启动后第一件事 ReloadFromSession 走的是同一条路)
            var repo = new ClippingRepository(DatabaseHelper.GetConnectionString(db));
            Assert.Empty(repo.GetAll());

            // ③ 按需为 CI 的 --ops 探针落一份夹具
            var fixture = Environment.GetEnvironmentVariable("KM2_OPS_FIXTURE");
            if (!string.IsNullOrEmpty(fixture)) {
                var fixtureDir = Path.GetDirectoryName(Path.GetFullPath(fixture));
                if (!string.IsNullOrEmpty(fixtureDir)) {
                    Directory.CreateDirectory(fixtureDir);
                }
                File.Copy(db, fixture, overwrite: true);
            }
        } finally {
            try { Directory.Delete(dir, true); } catch { /* 句柄未释放时由 TestTempCleanup 兜底 */ }
        }
    }
}
