using System.Runtime.CompilerServices;
using Xunit;

namespace KindleMate2.Tests;

/// <summary>
/// 元测试:凡调用 <see cref="ManualTestGate"/> 的测试文件,必须同时声明
/// <c>[Trait("Category", "Manual")]</c>。
///
/// <para>
/// 为什么需要:<c>build.yml</c> 的数量断言只能发现「Manual 集合整体为空 / 类别名整体写错」和
/// 「过滤器失效」—— 单独漏掉某一类的 Trait 时,<c>总计 = 非Manual + Manual</c> 这个过滤器互补的
/// 恒等式仍然成立(Manual 少 N、非Manual 多 N),该类的真机/外网用例就会在 CI 里被"执行"
/// (再被 gate 早退记成 PASSED,即假绿)。这条源码级断言把"单点漏打 Trait"补上;
/// 运行期口径仍由 build.yml 的断言负责。
/// </para>
/// <para>
/// 已知限制:按**文件**检查(类级/方法级 Trait 都算),不逐方法配对 —— 够覆盖当前
/// 「整类 gate + 类级 Trait / 单方法 gate + 方法级 Trait」两种写法。
/// </para>
/// </summary>
public sealed class ManualTraitCoverageTests {
    [Fact]
    public void EveryTestFileUsingManualTestGate_AlsoDeclaresManualTrait() {
        var dir = TestSourceDirectory();
        Assert.True(Directory.Exists(dir), $"找不到测试源码目录:{dir}(是否在源码树外运行?)");

        var gateUsers = new List<string>();
        var missingTrait = new List<string>();
        foreach (var file in Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories)) {
            // obj/ 与 bin/ 下是生成物,跳过。
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)) {
                continue;
            }
            var text = File.ReadAllText(file);
            if (!text.Contains("ManualTestGate.Require", StringComparison.Ordinal)) {
                continue;
            }
            gateUsers.Add(Path.GetFileName(file));
            var hasTrait = text.Contains("[Trait(\"Category\", \"Manual\")]", StringComparison.Ordinal)
                           || text.Contains("[Trait(\"Category\",\"Manual\")]", StringComparison.Ordinal);
            if (!hasTrait) {
                missingTrait.Add(Path.GetFileName(file));
            }
        }

        Assert.NotEmpty(gateUsers);   // 扫描逻辑本身要有效:扫不到任何调用说明正则/目录选错了
        Assert.True(missingTrait.Count == 0,
            "以下测试文件调用了 ManualTestGate 却没有声明 [Trait(\"Category\", \"Manual\")] —— "
            + "CI 的 --filter \"Category!=Manual\" 排除不掉它们,会在报告里以 PASSED 假绿:\n  "
            + string.Join("\n  ", missingTrait));
    }

    /// <summary>本文件(编译期)所在目录 = 测试工程源码目录。</summary>
    private static string TestSourceDirectory([CallerFilePath] string thisFile = "") =>
        Path.GetDirectoryName(thisFile)!;
}
