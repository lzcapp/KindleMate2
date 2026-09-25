using Xunit;
using KindleMate2.Application.Services.KM2DB;
using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Infrastructure.Helpers;
using KindleMate2.Infrastructure.Repositories.KM2DB;

namespace KindleMate2.Tests;

/// <summary>
/// 回归:「重命名生词」撞名时静默并入(<c>MergeWordKey</c>)的去重口径。
///
/// 旧口径只丢**同 timestamp** 的源行 —— 用户实测(2026-09-25)把两个同名词条并起来后,
/// 中栏出现两条肉眼完全相同的查询(同句、同书、词频同为 1,时间戳只差 3 秒),
/// 看上去就是"合并完出了重复"。同句同书同作者而 timestamp 不同的源行,本来就是
/// 同一次阅读被两个词条各记了一条,并入时应当只留目标键下那一条。
///
/// 反面同样要钉住:句子不同、或句子为空(NULL/空串,无从判断是不是同一条)的行,
/// 一条都不许丢 —— 合并不是清理,静默删数据只限于"有把握是同一条记录"的情形。
/// </summary>
public sealed class MergeWordKeyTests : IDisposable {
    private readonly string _dir;

    public MergeWordKeyTests() {
        _dir = Path.Combine(Path.GetTempPath(), "km2-tests-" + Guid.NewGuid().ToString("N")[..10]);
        Directory.CreateDirectory(_dir);
    }

    public void Dispose() {
        try { Directory.Delete(_dir, true); } catch { /* best effort */ }
    }

    private LookupRepository CreateRepo(string name) {
        var path = Path.Combine(_dir, name);
        Assert.True(DatabaseHelper.CreateDatabase(path, out var ex), ex.Message);
        return new LookupRepository(DatabaseHelper.GetConnectionString(path));
    }

    [Fact]
    public void MergeWordKey_SameSentenceDifferentTimestamps_KeepsOnlyTheTargetsRow() {
        var repo = CreateRepo("content-dup.db");
        var service = new LookupService(repo);
        const string sentence = "何美之叫浑家煮了一只母鸡，把火腿切了，酒舀出来烫着。";

        repo.Add(new Lookup { WordKey = "en:fire", Usage = sentence, Title = "儒林外史", Authors = "吴敬梓",
            Timestamp = "2025-09-12 15:39:35" });
        repo.Add(new Lookup { WordKey = "en:ham", Usage = sentence, Title = "儒林外史", Authors = "吴敬梓",
            Timestamp = "2025-09-12 15:39:32" });

        // 返回值 = 实际搬走的行数;源行按"同句同书同作者"被丢弃,没有可搬的 ⇒ 0。
        Assert.Equal(0, service.MergeWordKey("en:ham", "en:fire"));

        var targetRows = repo.GetAll().Where(l => l.WordKey == "en:fire").ToList();
        var sourceRows = repo.GetAll().Where(l => l.WordKey == "en:ham").ToList();
        Assert.Single(targetRows);
        Assert.Empty(sourceRows);
        // 留的是目标键下原有的那一条(用户的既有视图不动)。
        Assert.Equal("2025-09-12 15:39:35", targetRows[0].Timestamp);
    }

    [Fact]
    public void MergeWordKey_DifferentSentences_KeepsBothRows() {
        var repo = CreateRepo("distinct.db");
        var service = new LookupService(repo);

        repo.Add(new Lookup { WordKey = "en:target", Usage = "sentence A", Title = "Book", Authors = "A",
            Timestamp = "2026-01-01 10:00:00" });
        repo.Add(new Lookup { WordKey = "en:source", Usage = "sentence B", Title = "Book", Authors = "A",
            Timestamp = "2026-01-01 10:00:03" });

        Assert.Equal(1, service.MergeWordKey("en:source", "en:target"));

        var targetRows = repo.GetAll().Where(l => l.WordKey == "en:target").ToList();
        Assert.Equal(2, targetRows.Count);
        Assert.DoesNotContain(repo.GetAll(), l => l.WordKey == "en:source");
    }

    [Fact]
    public void MergeWordKey_ExactTimestampCollision_DropsEvenWhenSentenceDiffers() {
        // 旧规则保留:(word_key, timestamp) 撞唯一约束,搬过去必失败 ⇒ 与句子内容无关,一律丢源行。
        var repo = CreateRepo("ts-collision.db");
        var service = new LookupService(repo);

        repo.Add(new Lookup { WordKey = "en:target", Usage = "target sentence", Title = "Book", Authors = "A",
            Timestamp = "2026-01-01 10:00:00" });
        repo.Add(new Lookup { WordKey = "en:source", Usage = "source sentence", Title = "Book", Authors = "A",
            Timestamp = "2026-01-01 10:00:00" });

        Assert.Equal(0, service.MergeWordKey("en:source", "en:target"));

        var targetRows = repo.GetAll().Where(l => l.WordKey == "en:target").ToList();
        Assert.Single(targetRows);
        Assert.Equal("target sentence", targetRows[0].Usage);
        Assert.DoesNotContain(repo.GetAll(), l => l.WordKey == "en:source");
    }

    [Fact]
    public void MergeWordKey_NullOrEmptyUsage_IsNeverDroppedAsContentDuplicate() {
        // 句子为空时无从判断是不是同一条 —— 不许以"看起来重复"为由静默删。
        var repo = CreateRepo("empty-usage.db");
        var service = new LookupService(repo);

        repo.Add(new Lookup { WordKey = "en:target", Usage = null, Title = "Book", Authors = "A",
            Timestamp = "2026-01-01 10:00:00" });
        repo.Add(new Lookup { WordKey = "en:source", Usage = null, Title = "Book", Authors = "A",
            Timestamp = "2026-01-01 10:00:03" });
        repo.Add(new Lookup { WordKey = "en:target", Usage = "", Title = "Book", Authors = "A",
            Timestamp = "2026-01-01 10:00:06" });
        repo.Add(new Lookup { WordKey = "en:source", Usage = "", Title = "Book", Authors = "A",
            Timestamp = "2026-01-01 10:00:09" });

        Assert.Equal(2, service.MergeWordKey("en:source", "en:target"));

        Assert.Equal(4, repo.GetAll().Count(l => l.WordKey == "en:target"));
        Assert.DoesNotContain(repo.GetAll(), l => l.WordKey == "en:source");
    }

    [Fact]
    public void MergeWordKey_DifferentAuthorOrBook_KeepsBothRows() {
        // 内容重复的判定必须"同句 + 同书 + 同作者"三者齐备;元数据对不上就保守保留。
        var repo = CreateRepo("metadata-mismatch.db");
        var service = new LookupService(repo);
        const string sentence = "same sentence";

        repo.Add(new Lookup { WordKey = "en:target", Usage = sentence, Title = "Book One", Authors = "A",
            Timestamp = "2026-01-01 10:00:00" });
        repo.Add(new Lookup { WordKey = "en:source", Usage = sentence, Title = "Book Two", Authors = "A",
            Timestamp = "2026-01-01 10:00:03" });
        repo.Add(new Lookup { WordKey = "en:source", Usage = sentence, Title = "Book One", Authors = "B",
            Timestamp = "2026-01-01 10:00:06" });

        Assert.Equal(2, service.MergeWordKey("en:source", "en:target"));

        Assert.Equal(3, repo.GetAll().Count(l => l.WordKey == "en:target"));
        Assert.DoesNotContain(repo.GetAll(), l => l.WordKey == "en:source");
    }
}
