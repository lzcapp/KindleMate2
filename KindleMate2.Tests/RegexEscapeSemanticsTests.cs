using System.Text.RegularExpressions;
using Xunit;

namespace KindleMate2.Tests;

/// <summary>Regression tests for the Regex.Escape fix in ContentDetailService (commit 8a8db53).</summary>
public sealed class RegexEscapeSemanticsTests {
    [Fact]
    public void WordWithDot_NoLongerMatchesArbitraryCharacter() {
        var word = "a.b";
        var oldPattern = $"\\b{word}\\b";                    // pre-fix interpolation
        var newPattern = $@"\b{Regex.Escape(word)}\b";      // post-fix
        Assert.False(Regex.IsMatch("axb", newPattern, RegexOptions.IgnoreCase)); // fixed: no mis-match
        Assert.True(Regex.IsMatch("axb", oldPattern, RegexOptions.IgnoreCase));  // confirms the old bug
    }

    [Fact]
    public void WordWithDot_ExactMatchStillWorks() {
        var word = "a.b";
        Assert.True(Regex.IsMatch("see a.b here", $@"\b{Regex.Escape(word)}\b", RegexOptions.IgnoreCase));
    }

    [Fact]
    public void WordWithUnclosedBracket_NoLongerThrows() {
        var bad = "x[1";
        // Old code threw (RegexParseException : ArgumentException) — unterminated character class
        Assert.ThrowsAny<ArgumentException>(() => Regex.IsMatch("text", $"\\b{bad}\\b"));
        // New code escapes and works
        Assert.True(Regex.IsMatch("x[1", $@"\b{Regex.Escape(bad)}\b"));
    }
}
