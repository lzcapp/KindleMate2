using Xunit;
using KindleMate2.Application.Services.KM2DB;
using KindleMate2.Infrastructure.Helpers;
using KindleMate2.Infrastructure.Repositories.KM2DB;

namespace KindleMate2.Tests;

/// <summary>
/// 回归:导入解析时 <c>PageNumber</c> 必须取「页码」而不是「位置段末端的数字」。
/// 旧实现从最后一个 <c>|</c> **之后**取串(<c>clippingTypeLocation[(indexOf)..]</c>),于是
/// <c>page 5 | Location 100-101</c> 会把 101 当成页码写库、真实页码 5 丢失;
/// 只有位置没有页码的条目则靠这段数字兜底(供同页笔记关联),不能改为 0,
/// 否则会在 <c>PageFailed</c> 分支被整条跳过、凭空丢数据。
/// </summary>
public sealed class ClippingPageNumberTests : IDisposable {
    private readonly string _dir;

    public ClippingPageNumberTests() {
        _dir = Path.Combine(Path.GetTempPath(), "km2-tests-" + Guid.NewGuid().ToString("N")[..10]);
        Directory.CreateDirectory(_dir);
    }

    public void Dispose() {
        try { Directory.Delete(_dir, true); } catch { /* best effort */ }
    }

    private (Km2DatabaseService Service, ClippingRepository Repo) NewService(string name) {
        var path = Path.Combine(_dir, name);
        Assert.True(DatabaseHelper.CreateDatabase(path, out var ex), "CreateDatabase failed: " + ex.Message);
        var connectionString = DatabaseHelper.GetConnectionString(path);
        var repo = new ClippingRepository(connectionString);
        var service = new Km2DatabaseService(repo,
            new LookupRepository(connectionString),
            new OriginalClippingLineRepository(connectionString),
            new SettingRepository(connectionString),
            new VocabRepository(connectionString));
        return (service, repo);
    }

    private string WriteClippings(string name, string body) {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, body);
        return path;
    }

    [Fact]
    public void Import_PageAndLocation_UsesPageNotLocationEnd() {
        var (service, repo) = NewService("page.db");
        var path = WriteClippings("page.txt", """
Book R (Author R)
- Your Highlight on page 5 | Location 100-101 | Added on Thursday, January 1, 2026, 12:00:00 AM

a highlight
=========
""");

        Assert.True(service.ImportKindleClippings(path, out _));

        var stored = Assert.Single(repo.GetAll());
        Assert.Equal(5, stored.PageNumber);
    }

    [Fact]
    public void Import_LocationOnly_FallsBackToLocationEndWithoutSkipping() {
        var (service, repo) = NewService("loc.db");
        var path = WriteClippings("loc.txt", """
Book R (Author R)
- Your Highlight on Location 100-101 | Added on Thursday, January 1, 2026, 12:00:00 AM

a highlight
=========
""");

        Assert.True(service.ImportKindleClippings(path, out _));

        // 没有页码的位置型条目不得被跳过;PageNumber 沿用位置末端作兜底(旧行为)。
        var stored = Assert.Single(repo.GetAll());
        Assert.Equal(101, stored.PageNumber);
    }
}
