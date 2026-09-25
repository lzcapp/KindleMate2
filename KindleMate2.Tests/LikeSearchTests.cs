using Xunit;
using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Infrastructure.Helpers;
using KindleMate2.Infrastructure.Repositories.KM2DB;
using KindleMate2.Shared.Entities;

namespace KindleMate2.Tests;

/// <summary>
/// 回归:模糊搜索的用户输入必须转义 LIKE 通配符(配合 SQL 的 <c>ESCAPE '\'</c>)。
/// 之前直接把 search 绑进 <c>LIKE '%' || @strSearch || '%'</c>,搜 <c>_</c> 会命中任意单字符、
/// 搜 <c>%</c> 几乎命中全部,搜 <c>a_b</c> 会误命中 <c>aXb</c>。
/// </summary>
public sealed class LikeSearchTests : IDisposable {
    private readonly string _dir;
    private readonly ClippingRepository _repo;

    public LikeSearchTests() {
        _dir = Path.Combine(Path.GetTempPath(), "km2-tests-" + Guid.NewGuid().ToString("N")[..10]);
        Directory.CreateDirectory(_dir);
        var db = Path.Combine(_dir, "search.db");
        Assert.True(DatabaseHelper.CreateDatabase(db, out var ex), ex.Message);
        _repo = new ClippingRepository(DatabaseHelper.GetConnectionString(db));
        _repo.Add(new Clipping { Key = "k1", Content = "foo_bar here", BookName = "B" });
        _repo.Add(new Clipping { Key = "k2", Content = "fooXbar here", BookName = "B" });
        _repo.Add(new Clipping { Key = "k3", Content = "save 50% now", BookName = "B" });
        _repo.Add(new Clipping { Key = "k4", Content = "save 5000 now", BookName = "B" });
    }

    public void Dispose() {
        try { Directory.Delete(_dir, true); } catch { /* best effort */ }
    }

    [Fact]
    public void EscapeLikePattern_EscapesWildcardsAndBackslash() {
        Assert.Equal("a\\%b\\_c\\\\d", DatabaseHelper.EscapeLikePattern("a%b_c\\d"));
        Assert.Equal(string.Empty, DatabaseHelper.EscapeLikePattern(null));
    }

    [Fact]
    public void Search_Underscore_MatchesOnlyLiteralUnderscore() {
        var hits = _repo.GetByFuzzySearch("foo_bar", AppEntities.SearchType.Content);

        var hit = Assert.Single(hits);
        Assert.Equal("foo_bar here", hit.Content);
    }

    [Fact]
    public void Search_Percent_MatchesOnlyLiteralPercent() {
        var hits = _repo.GetByFuzzySearch("50%", AppEntities.SearchType.Content);

        var hit = Assert.Single(hits);
        Assert.Equal("save 50% now", hit.Content);
    }
}
