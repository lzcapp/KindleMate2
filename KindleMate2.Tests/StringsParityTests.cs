using System.Collections;
using System.Globalization;
using System.Reflection;
using Xunit;
using KindleMate2.Shared;

namespace KindleMate2.Tests;

/// <summary>
/// 资源文件之间的**一致性**(不是某条文案对不对)。
///
/// 为什么值得单独测:加一条界面文案要同时动 5 个地方 —— 4 份 .resx 与手工维护的
/// Strings.Designer.cs,而且漏一处的后果**都不是编译错**:漏在 resx 是运行时拿到空串,
/// 漏在某个语言文件是静默回退到 neutral(界面照常显示、只是语言串了)。
/// 本次就实打实踩过一次:往 Strings.en.resx 插新条目时连带啃掉了下一条的起始标签,
/// 键集合看上去一个不少,靠肉眼 diff 完全看不出来 —— 只有真的对账才拦得住。
///
/// 所以这里不列白名单,而是**全量对账**:neutral(Strings.resx) 的每个键都必须在
/// 三种语言各自的资源集里存在,且每个键都要有同名 Designer 属性(反之亦然)。
/// </summary>
public sealed class StringsParityTests {
    [Theory]
    [InlineData("zh-Hans")]
    [InlineData("zh-Hant")]
    [InlineData("en")]
    public void EveryNeutralKey_ExistsInTheLanguageFile(string cultureName) {
        var neutral = NeutralKeys();
        // tryParents: false 是关键 —— 开着回退的话,缺条目会拿到 neutral 的值,
        // 断言照样绿,等于白测(DeviceStringsTests 里对这一点有同样的注释)。
        var resourceSet = Strings.ResourceManager.GetResourceSet(
            new CultureInfo(cultureName), createIfNotExists: true, tryParents: false);

        Assert.NotNull(resourceSet);
        var missing = neutral.Where(key => resourceSet!.GetString(key) is null).OrderBy(key => key).ToList();
        Assert.True(missing.Count == 0,
            $"{cultureName} 的语言文件缺 {missing.Count} 个条目(会静默回退到 neutral):{string.Join(", ", missing)}");
    }

    [Fact]
    public void EveryNeutralKey_HasADesignerProperty_AndViceVersa() {
        var neutral = NeutralKeys();
        var properties = typeof(Strings)
            .GetProperties(BindingFlags.Public | BindingFlags.Static)
            // 生成器除了文案还会给出 ResourceManager / Culture 两个 public static 属性,
            // 它们不是 resx 条目;按 string 类型过滤才能与键集对齐(不滤会误报)。
            .Where(property => property.PropertyType == typeof(string))
            .Select(property => property.Name)
            .ToHashSet();

        // 有属性、没条目 → 编译通过、运行时空串。这正是"加文案时忘了动 resx"的样子。
        var withoutEntry = properties.Except(neutral).OrderBy(name => name).ToList();
        // 有条目、没属性 → 代码里根本取不到,只能漏在 x:Static 之外。
        var withoutProperty = neutral.Except(properties).OrderBy(name => name).ToList();

        Assert.True(withoutEntry.Count == 0,
            $"Strings.Designer.cs 里有属性但 resx 里没有条目:{string.Join(", ", withoutEntry)}");
        Assert.True(withoutProperty.Count == 0,
            $"resx 里有条目但 Strings.Designer.cs 里没有属性:{string.Join(", ", withoutProperty)}");
    }

    [Fact]
    public void LanguageFiles_CarryNoKeyThatNeutralLacks() {
        // 反向也要看:只在 zh-Hant 里加、neutral 忘了加,中文用户反而看不到。
        var neutral = NeutralKeys();
        foreach (var cultureName in new[] { "zh-Hans", "zh-Hant", "en" }) {
            var resourceSet = Strings.ResourceManager.GetResourceSet(
                new CultureInfo(cultureName), createIfNotExists: true, tryParents: false);
            Assert.NotNull(resourceSet);

            var extra = resourceSet!.Cast<DictionaryEntry>()
                .Select(entry => (string)entry.Key)
                .Where(key => !neutral.Contains(key))
                .OrderBy(key => key)
                .ToList();
            Assert.True(extra.Count == 0,
                $"{cultureName} 里有 neutral 没有的条目:{string.Join(", ", extra)}");
        }
    }

    /// <summary>
    /// neutral(Strings.resx)里的全部键。
    ///
    /// 注意**不能** Dispose 拿到的 ResourceSet:ResourceManager 返回的是它内部缓存的
    /// 同一个实例,释放它会把该 culture 的缓存关掉,之后任何 GetString 都抛
    /// ObjectDisposedException,而且还会扩散到同进程里的其他用例(踩过)。
    /// </summary>
    private static HashSet<string> NeutralKeys() {
        var resourceSet = Strings.ResourceManager.GetResourceSet(
            CultureInfo.InvariantCulture, createIfNotExists: true, tryParents: false);

        Assert.NotNull(resourceSet);
        return resourceSet!.Cast<DictionaryEntry>()
            .Select(entry => (string)entry.Key)
            .ToHashSet();
    }
}
