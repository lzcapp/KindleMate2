using Microsoft.Data.Sqlite;
using Xunit;
using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Infrastructure.Helpers;
using KindleMate2.Infrastructure.Repositories.KM2DB;

namespace KindleMate2.Tests;

/// <summary>
/// 回归:<see cref="DatabaseHelper.GetSafeInt(SqliteDataReader, int)"/> /
/// <see cref="DatabaseHelper.GetSafeLong(SqliteDataReader, int)"/> 面对**非整数**列值
/// (历史库里把数值存成 TEXT,或存了非数字文本)必须**容错**,不能抛 <c>InvalidCastException</c>
/// 把整条读取打死 —— 防御口径与 <c>KmateSourceReader.ParseLong/ParseInt</c> 一致。
/// </summary>
public sealed class SafeNumericReadTests : IDisposable {
    private readonly string _dir;

    public SafeNumericReadTests() {
        _dir = Path.Combine(Path.GetTempPath(), "km2-safe-numeric-tests-" + Guid.NewGuid().ToString("N")[..10]);
        Directory.CreateDirectory(_dir);
    }

    public void Dispose() {
        try { Directory.Delete(_dir, true); } catch { /* best effort */ }
    }

    [Fact]
    public void GetSafeInt_TextAndNullValues_DoNotThrow() {
        using var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        using var cmd = conn.CreateCommand();
        // 列 0=整数、1=数字文本、2=非数字文本、3=小数文本、4=NULL
        cmd.CommandText = "SELECT 42, '7', 'abc', '3.5', NULL;";
        using var reader = cmd.ExecuteReader();
        Assert.True(reader.Read());

        Assert.Equal(42, DatabaseHelper.GetSafeInt(reader, 0));
        Assert.Equal(7, DatabaseHelper.GetSafeInt(reader, 1));
        Assert.Null(DatabaseHelper.GetSafeInt(reader, 2));   // 'abc' 无法解析 → null,不抛
        Assert.Null(DatabaseHelper.GetSafeInt(reader, 3));   // '3.5' 非整数文本 → null,不抛
        Assert.Null(DatabaseHelper.GetSafeInt(reader, 4));   // NULL
    }

    [Fact]
    public void GetSafeLong_TextAndNullValues_DoNotThrow() {
        using var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT 9999999999, '123', 'xyz', NULL;";
        using var reader = cmd.ExecuteReader();
        Assert.True(reader.Read());

        Assert.Equal(9999999999L, DatabaseHelper.GetSafeLong(reader, 0));
        Assert.Equal(123L, DatabaseHelper.GetSafeLong(reader, 1));
        Assert.Null(DatabaseHelper.GetSafeLong(reader, 2));  // 'xyz' → null,不抛
        Assert.Null(DatabaseHelper.GetSafeLong(reader, 3));  // NULL
    }

    [Fact]
    public void GetSafeInt_WithDefault_FallsBackInsteadOfThrowing() {
        using var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT 'abc', NULL, 5;";
        using var reader = cmd.ExecuteReader();
        Assert.True(reader.Read());

        Assert.Equal(-1, DatabaseHelper.GetSafeInt(reader, 0, -1));  // 解析失败 → 默认值
        Assert.Equal(-1, DatabaseHelper.GetSafeInt(reader, 1, -1));  // NULL → 默认值
        Assert.Equal(5, DatabaseHelper.GetSafeInt(reader, 2, -1));   // 正常整数
    }

    /// <summary>
    /// 端到端:clippings 的 <c>pagenumber</c> 是 INTEGER 列,但 SQLite 动态类型允许其存 TEXT。
    /// 旧实现下 <c>GetByKey</c> 会因 <c>GetInt32</c> 抛 <c>InvalidCastException</c> 整条读不出来;
    /// 现在应容错(脏值解析为 null,经实体 setter 归一为 0),其余字段照常,整行可读。
    /// </summary>
    [Fact]
    public void ClippingRepository_ReadsRowWithTextInIntegerColumn_WithoutThrowing() {
        var db = Path.Combine(_dir, "KM2.dat");
        Assert.True(DatabaseHelper.CreateDatabase(db, out var ex), ex.Message);

        using (var conn = new SqliteConnection(DatabaseHelper.GetConnectionString(db))) {
            conn.Open();
            using var cmd = conn.CreateCommand();
            // pagenumber 塞非数字文本(INTEGER 亲和度转不了,按 TEXT 存)
            cmd.CommandText = "INSERT INTO clippings ([key],[content],[bookname],[pagenumber]) VALUES ('k1','c1','Book','abc');";
            cmd.ExecuteNonQuery();
        }

        var repo = new ClippingRepository(DatabaseHelper.GetConnectionString(db));
        var clipping = repo.GetByKey("k1");

        // 关键:不抛异常、整行读得出来。脏值经 GetSafeInt 解析为 null,再由实体 setter 归一为 0。
        Assert.NotNull(clipping);
        Assert.Equal("k1", clipping!.Key);
        Assert.Equal(0, clipping.PageNumber);
    }
}
