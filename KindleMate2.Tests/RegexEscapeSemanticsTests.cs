using System.Text.RegularExpressions;
using Xunit;

namespace KindleMate2.Tests;

/// <summary>
/// Regression tests for the Regex.Escape fix (commit 8a8db53)。
/// 该修复原本位于 ContentDetailService,而那个类后来因零调用被删除 —— 这里保留测试
/// 是为了把"正则字面量必须转义"这一语义本身固定住:将来任何重新实现相同匹配的代码,
/// 仍会被这组用例约束。
/// </summary>
public sealed class RegexEscapeSemanticsTests {
    [Fact]
    public void WordWithDot_NoLongerMatchesArbitraryCharacter() {
        var word = "a.b";
        var oldPattern = $"\\b{word}\\b";                    // pre-fix interpolation
        var newPattern = $@"\b{Regex.Escape(word)}\b";      // post-fix
        // 注意 Assert.Matches/DoesNotMatch 的参数顺序是「模式, 实际值」,与 Regex.IsMatch 相反;
        // 且需要 RegexOptions 时只能用 Regex 对象重载(字符串重载不接收选项)。
        // 用它们而非 Assert.True(Regex.IsMatch(...)):失败时会把模式与实际字符串一并打印出来(xUnit2008)。
        Assert.DoesNotMatch(new Regex(newPattern, RegexOptions.IgnoreCase), "axb"); // fixed: no mis-match
        Assert.Matches(new Regex(oldPattern, RegexOptions.IgnoreCase), "axb");      // confirms the old bug
    }

    [Fact]
    public void WordWithDot_ExactMatchStillWorks() {
        var word = "a.b";
        Assert.Matches(new Regex($@"\b{Regex.Escape(word)}\b", RegexOptions.IgnoreCase), "see a.b here");
    }

    [Fact]
    public void WordWithUnclosedBracket_NoLongerThrows() {
        var bad = "x[1";
        // Old code threw (RegexParseException : ArgumentException) — unterminated character class
        Assert.ThrowsAny<ArgumentException>(() => Regex.IsMatch("text", $"\\b{bad}\\b"));
        // New code escapes and works
        Assert.Matches(new Regex($@"\b{Regex.Escape(bad)}\b"), "x[1");
    }
}
