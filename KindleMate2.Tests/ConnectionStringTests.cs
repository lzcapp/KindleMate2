using Microsoft.Data.Sqlite;
using Xunit;
using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Infrastructure.Helpers;
using KindleMate2.Infrastructure.Repositories.KM2DB;

namespace KindleMate2.Tests;

/// <summary>
/// 回归:连接串必须用 <see cref="Microsoft.Data.Sqlite.SqliteConnectionStringBuilder"/> 构造。
/// 之前字符串插值拼 <c>Data Source={path};…</c>,路径含 <c>;</c>(Unix/Windows 都合法)会被
/// 解析成额外关键字,抛 <c>ArgumentException</c>,库直接打不开。
///
/// <para>
/// 另外钉住:连接串必须**显式**带一个正的命令超时(<c>Default Timeout</c> → <c>CommandTimeout</c>),
/// 且该超时集中由 <see cref="DatabaseHelper.BuildConnectionString"/> 设置 —— 免得日后某个调用点
/// 自行拼串时漏掉,又退回"依赖库默认值"的隐式状态(见该方法的说明)。
/// </para>
/// </summary>
public sealed class ConnectionStringTests : IDisposable {
    private readonly string _dir;

    public ConnectionStringTests() {
        _dir = Path.Combine(Path.GetTempPath(), "km2-tests-" + Guid.NewGuid().ToString("N")[..10]);
        Directory.CreateDirectory(_dir);
    }

    public void Dispose() {
        try { Directory.Delete(_dir, true); } catch { /* best effort */ }
    }

    [Fact]
    public void DatabasePathContainingSemicolon_IsUsable() {
        var semicolonDir = Path.Combine(_dir, "km;dir");
        Directory.CreateDirectory(semicolonDir);
        var dbPath = Path.Combine(semicolonDir, "KM2.dat");

        Assert.True(DatabaseHelper.CreateDatabase(dbPath, out var ex), ex.Message);

        var repo = new ClippingRepository(DatabaseHelper.GetConnectionString(dbPath));
        Assert.Equal(0, repo.GetCount());
        Assert.True(repo.Add(new Clipping { Key = "k;1", Content = "x" }));
        Assert.Equal(1, repo.GetCount());
    }

    /// <summary>连接串显式带正的命令超时;路径含分号时也照常解析(超时关键字不被分号带偏)。</summary>
    [Theory]
    [InlineData("plain")]
    [InlineData("km;dir")]
    public void ConnectionString_CarriesPositiveTimeout_AndParsesSemicolonPath(string subdir) {
        var dir = Path.Combine(_dir, subdir);
        Directory.CreateDirectory(dir);
        var dbPath = Path.Combine(dir, "KM2.dat");

        var cs = DatabaseHelper.GetConnectionString(dbPath);
        Assert.Contains("Default Timeout=", cs, StringComparison.OrdinalIgnoreCase);

        var builder = new SqliteConnectionStringBuilder(cs);
        Assert.True(builder.DefaultTimeout > 0, "连接串必须显式带一个正的命令超时");
        // 路径含分号时,DataSource 仍原样往返 —— 证明超时关键字没被误当成路径的一部分。
        Assert.Equal(dbPath, builder.DataSource);
    }

    /// <summary>建库路径(ReadWriteCreate)与快照路径(ReadOnly,非共享缓存)同样带上超时。</summary>
    [Fact]
    public void BuildConnectionString_AllModes_CarryTimeout() {
        var dbPath = Path.Combine(_dir, "KM2.dat");

        foreach (var cs in new[] {
            DatabaseHelper.BuildConnectionString(dbPath, SqliteOpenMode.ReadWriteCreate, sharedCache: true),
            DatabaseHelper.BuildConnectionString(dbPath, SqliteOpenMode.ReadOnly, sharedCache: false),
        }) {
            var builder = new SqliteConnectionStringBuilder(cs);
            Assert.True(builder.DefaultTimeout > 0, "每种模式都必须带正的命令超时: " + cs);
        }
    }
}
