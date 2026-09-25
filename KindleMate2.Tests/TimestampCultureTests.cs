using System.Globalization;
using Xunit;
using KindleMate2.Application.Services.KM2DB;
using KindleMate2.Infrastructure.Helpers;
using KindleMate2.Infrastructure.Repositories.KM2DB;

namespace KindleMate2.Tests;

/// <summary>
/// 回归:持久化时间戳(标注的 clippingDate、生词的 timestamp)必须走 InvariantCulture。
/// 之前用不带 culture 的 <c>ToString("yyyy-MM-dd HH:mm:ss")</c>,在泰历/回历等区域会给出
/// 非公历年,同一份数据在不同机器上生成不同的 key —— 判重与唯一约束都会错。
/// </summary>
public sealed class TimestampCultureTests : IDisposable {
    private readonly string _dir;

    public TimestampCultureTests() {
        _dir = Path.Combine(Path.GetTempPath(), "km2-tests-" + Guid.NewGuid().ToString("N")[..10]);
        Directory.CreateDirectory(_dir);
    }

    public void Dispose() {
        try { Directory.Delete(_dir, true); } catch { /* best effort */ }
    }

    [Fact]
    public void ImportClipping_KeepsGregorianYear_UnderBuddhistCalendarCulture() {
        var path = Path.Combine(_dir, "clippings.db");
        Assert.True(DatabaseHelper.CreateDatabase(path, out var ex), ex.Message);
        var connectionString = DatabaseHelper.GetConnectionString(path);
        var repo = new ClippingRepository(connectionString);
        var service = new Km2DatabaseService(repo,
            new LookupRepository(connectionString),
            new OriginalClippingLineRepository(connectionString),
            new SettingRepository(connectionString),
            new VocabRepository(connectionString));

        var clippings = Path.Combine(_dir, "My Clippings.txt");
        File.WriteAllText(clippings, """
Book (Author)
- Your Highlight on page 1 | Location 10-11 | Added on Sunday, May 19, 2025, 10:20:31 PM

hello
==========
""");

        var original = CultureInfo.CurrentCulture;
        try {
            // 泰历(佛历)默认把 2025 显示成 2568;若格式化没带 InvariantCulture,key 会变。
            var thai = (CultureInfo)CultureInfo.GetCultureInfo("th-TH").Clone();
            thai.DateTimeFormat.Calendar = new ThaiBuddhistCalendar();
            CultureInfo.CurrentCulture = thai;

            Assert.True(service.ImportKindleClippings(clippings, out _));
        } finally {
            CultureInfo.CurrentCulture = original;
        }

        var stored = Assert.Single(repo.GetAll());
        Assert.StartsWith("2025-05-19", stored.ClippingDate, StringComparison.Ordinal);
    }
}
