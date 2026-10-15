using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;

namespace KindleMate2.Tests;

/// <summary>
/// 元测试:扫描测试源码里所有「以 <see cref="Path.GetTempPath"/> 为根、第二个参数是字符串字面量
/// 的目录前缀」,断言它们都登记在 <see cref="TestTempCleanup.OwnedPrefixes"/> 里。
///
/// 动机:白名单是**手工维护**的,而漏登记不会报错 —— 只是那个临时目录在进程退出时不被清扫、
/// 在开发机上持续累积(2026-10 审计发现已漏 8 个前缀)。这类"漏了也不红"的约定正好适合用
/// 一条元测试钉死:以后新增测试类只要用了新前缀而忘了登记,这里立刻红。
/// </summary>
public sealed class TempDirPrefixTests {
    /// <summary>
    /// 以 <see cref="Path.GetTempPath"/> 为根拼出、但**不是测试临时目录**的字面量:
    /// 它只是被当作纯字符串用于路径判断,测试从不 <c>CreateDirectory</c>,
    /// 因此不该(也不必)进清扫白名单。见 <c>UpdateInstallerTests.ResolveAppBundlePath_FindsTheNearestAppBundle</c>。
    /// </summary>
    private static readonly string[] NonTempDirLiterals = { "Kindle Mate 2.app" };

    [Fact]
    public void EveryTempDirPrefix_UsedInTests_IsRegisteredForCleanup() {
        var dir = TestSourceDirectory();
        Assert.True(Directory.Exists(dir), $"找不到测试源码目录:{dir}(是否在源码树外运行?)");

        // 只认「GetTempPath() 后面紧跟一个字符串字面量」这一种写法 —— 全仓库的测试都用它造临时目录。
        // 模式用字符串拼接构造,否则本文件里的模式文本本身会被自己匹配到(自匹配陷阱)。
        var pattern = "Path" + @"\.Combine\(\s*Path\.GetTempPath\(\),\s*""([^""]+)""";
        var regex = new Regex(pattern);

        var seen = new Dictionary<string, string>(StringComparer.Ordinal);   // 前缀 -> 首个出现的文件
        foreach (var file in Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories)) {
            // obj/ 下是生成物,可能含有指向源码的路径但不会有这些字面量;跳过以免扫到无关内容。
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)) {
                continue;
            }
            foreach (Match m in regex.Matches(File.ReadAllText(file))) {
                seen.TryAdd(m.Groups[1].Value, Path.GetFileName(file));
            }
        }

        Assert.NotEmpty(seen);   // 扫描逻辑本身要有效:扫不到任何前缀说明正则/目录选错了

        var unregistered = seen
            .Where(kv => !TestTempCleanup.OwnedPrefixes.Contains(kv.Key, StringComparer.Ordinal)
                         && !NonTempDirLiterals.Contains(kv.Key, StringComparer.Ordinal))
            .Select(kv => $"'{kv.Key}'(首见于 {kv.Value})")
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToArray();

        Assert.True(unregistered.Length == 0,
            "以下临时目录前缀未登记进 TestTempCleanup.OwnedPrefixes,其目录在进程退出时不会被清扫:\n  "
            + string.Join("\n  ", unregistered));
    }

    /// <summary>本文件(编译期)所在目录 = 测试工程源码目录。</summary>
    private static string TestSourceDirectory([CallerFilePath] string thisFile = "") =>
        Path.GetDirectoryName(thisFile)!;
}
