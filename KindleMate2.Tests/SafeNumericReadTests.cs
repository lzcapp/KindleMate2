using Microsoft.Data.Sqlite;
using Xunit;
using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Infrastructure.Helpers;
using KindleMate2.Infrastructure.Repositories.KM2DB;

namespace KindleMate2.Tests;

/// <summary>
/// 回归:<see cref="DatabaseHelper.GetSafeInt(SqliteDataReader, int)"/> /
/// <see cref="DatabaseHelper.GetSafeLong(SqliteDataReader, int)"/> 面对**非整数**列值
/// (历史库里把数值存成 TEXT,或存了非数字文本/REAL)时返回 <c>null</c>,而不是:
/// ① 静默转换成一个与"缺值"不可区分的有损值(旧实现 <c>'abc'</c>→0、<c>'3.5'</c>→3);
/// ② 在 <c>GetInt32</c> 上因超出 int32 抛 <c>OverflowException</c> 打死整条读取。
/// 新口径与 <c>KmateSourceReader.ParseLong/ParseInt</c> 一致。
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
        // 列 0=整数、1=数字文本、2=非数字文本、3=小数文本、4=NULL、5=超出 int32 的大整数
        cmd.CommandText = "SELECT 42, '7', 'abc', '3.5', NULL, 9999999999;";
        using var reader = cmd.ExecuteReader();
        Assert.True(reader.Read());

        Assert.Equal(42, DatabaseHelper.GetSafeInt(reader, 0));
        Assert.Equal(7, DatabaseHelper.GetSafeInt(reader, 1));
        Assert.Null(DatabaseHelper.GetSafeInt(reader, 2));   // 'abc' 无法解析 → null(旧实现静默转 0)
        Assert.Null(DatabaseHelper.GetSafeInt(reader, 3));   // '3.5' 非整数文本 → null(旧实现静默转 3)
        Assert.Null(DatabaseHelper.GetSafeInt(reader, 4));   // NULL
        Assert.Null(DatabaseHelper.GetSafeInt(reader, 5));   // 超出 int32 → null(旧实现 GetInt32 抛 OverflowException)
    }

    [Fact]
    public void GetSafeLong_TextAndNullValues_DoNotThrow() {
        using var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT 9999999999, '123', 'xyz', NULL, 3.5;";
        using var reader = cmd.ExecuteReader();
        Assert.True(reader.Read());

        Assert.Equal(9999999999L, DatabaseHelper.GetSafeLong(reader, 0));
        Assert.Equal(123L, DatabaseHelper.GetSafeLong(reader, 1));
        Assert.Null(DatabaseHelper.GetSafeLong(reader, 2));  // 'xyz' → null(旧实现静默转 0)
        Assert.Null(DatabaseHelper.GetSafeLong(reader, 3));  // NULL
        Assert.Null(DatabaseHelper.GetSafeLong(reader, 4));  // REAL 3.5 → null(旧实现静默截断成 3)
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
    /// 端到端:clippings 的 <c>brieftype</c> / <c>colorRGB</c> 都是 INTEGER 列,但 SQLite 动态类型允许
    /// 其存 TEXT。旧实现会把 <c>'abc'</c> 静默转换成 0(与"真的 0"不可区分);新实现解析为 <c>null</c>,
    /// 再由实体 setter 归一(<c>BriefType</c> 保持 null、<c>ColorRgb</c> 归一为 -1)。
    /// 断言选这两个字段而不是 <c>PageNumber</c>:后者的 setter 也把 null 归一为 0,新旧行为在它身上
    /// 观测不到差异(那个字段只用于验证"整行仍可读")。
    /// </summary>
    [Fact]
    public void ClippingRepository_ReadsRowWithTextInIntegerColumns_CoercesToNullNotZero() {
        var db = Path.Combine(_dir, "KM2.dat");
        Assert.True(DatabaseHelper.CreateDatabase(db, out var ex), ex.Message);

        using (var conn = new SqliteConnection(DatabaseHelper.GetConnectionString(db))) {
            conn.Open();
            using var cmd = conn.CreateCommand();
            // INTEGER 亲和度转不了的非数字文本会按 TEXT 存
            cmd.CommandText =
                "INSERT INTO clippings ([key],[content],[bookname],[pagenumber],[brieftype],[colorRGB]) " +
                "VALUES ('k1','c1','Book','abc','abc','abc');";
            cmd.ExecuteNonQuery();
        }

        var repo = new ClippingRepository(DatabaseHelper.GetConnectionString(db));
        var clipping = repo.GetByKey("k1");

        // 关键:不抛异常、整行读得出来;脏值解析为 null 而不是静默的 0。
        Assert.NotNull(clipping);
        Assert.Equal("k1", clipping!.Key);
        Assert.Equal(0, clipping.PageNumber);    // 两边都是 0,仅证明整行可读
        Assert.Null(clipping.BriefType);         // 旧实现=0(静默转换);新实现=null
        Assert.Equal(-1, clipping.ColorRgb);     // 旧实现=0;新实现=null → setter 归一为 -1
    }
}
