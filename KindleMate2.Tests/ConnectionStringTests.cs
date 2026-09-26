using Xunit;
using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Infrastructure.Helpers;
using KindleMate2.Infrastructure.Repositories.KM2DB;

namespace KindleMate2.Tests;

/// <summary>
/// 回归:连接串必须用 <see cref="Microsoft.Data.Sqlite.SqliteConnectionStringBuilder"/> 构造。
/// 之前字符串插值拼 <c>Data Source={path};…</c>,路径含 <c>;</c>(Unix/Windows 都合法)会被
/// 解析成额外关键字,抛 <c>ArgumentException</c>,库直接打不开。
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
}
