using Xunit;
using KindleMate2.Infrastructure.Helpers;

namespace KindleMate2.Tests;

/// <summary>
/// 文件名净化(StringHelper.SanitizeFilename)。
///
/// 回归背景:原实现用 <c>Path.GetInvalidFileNameChars()</c> 取"当前平台"的规则,macOS / Linux 只返回
/// '\0' 与 '/' 两个字符(Windows 是 41 个),于是同一本书在 macOS 上导出会留下 ':'、'?'、'*' 等 ——
/// Finder 把 ':' 显示成路径分隔符,导出目录拷到 Windows / SMB 共享时更是直接非法。
///
/// 本文件的期望值**全部硬编码**,因而在 Windows / macOS / Linux 上断言的是同一个结果 ——
/// 这正是"平台无关"的检验方式:换平台跑同一份用例,结果必须一致。
///
/// 另一类回归目标(2026-09-21 补):Windows 保留设备名的判定顺序。原实现先判保留名、再修剪,
/// 于是 <c>" CON"</c>、<c>"CON."</c> 这类输入修剪后恰好落回 <c>"CON"</c>;它同时也破坏了幂等性
/// (净化一次得 <c>"CON"</c>,再净化一次得 <c>"_CON"</c>)。
/// </summary>
public sealed class SanitizeFilenameTests {
    /// <summary>三平台非法字符的并集(测试侧独立列出,不引用被测类型内部的判断)。</summary>
    private static readonly char[] PortablyIllegal = ['<', '>', ':', '"', '/', '\\', '|', '?', '*'];

    [Theory]
    // 以下每一条在改动前于 macOS 上都会被"原样放行",在 Windows 上才会被替换。
    [InlineData("Sapiens: A Brief History", "Sapiens_ A Brief History")]
    [InlineData("What is 2 + 2? A Question", "What is 2 + 2_ A Question")]
    [InlineData("Book <Vol.1> | Author \"A\" *starred*", "Book _Vol.1_ _ Author _A_ _starred_")]
    [InlineData(@"Back\slash and / slash", "Back_slash and _ slash")]
    [InlineData("Ends with dot.", "Ends with dot")]
    [InlineData("Trailing space ", "Trailing space")]
    [InlineData("  两端空白  ", "两端空白")]
    public void SanitizeFilename_IsPlatformIndependent(string input, string expected) {
        Assert.Equal(expected, StringHelper.SanitizeFilename(input));
    }

    [Fact]
    public void SanitizeFilename_ReplacesControlCharacters() {
        Assert.Equal("a_b_c", StringHelper.SanitizeFilename("a\u0001b\u001fc"));
    }

    [Theory]
    [InlineData("非法:书名*?<>|\"\\ 含反斜杠")]
    [InlineData("../../上跳/目录")]
    [InlineData("制表\t符与\u0000空字符")]
    [InlineData("普通书名")]
    [InlineData("emoji 📚 与 : 冒号")]
    public void SanitizeFilename_OutputNeverContainsPortableIllegalChars(string input) {
        var sanitized = StringHelper.SanitizeFilename(input);

        Assert.DoesNotContain(sanitized, c => PortablyIllegal.Contains(c) || char.IsControl(c));
        Assert.False(sanitized.EndsWith('.'), $"结尾的点在 Windows 上非法:{sanitized}");
        Assert.False(sanitized.EndsWith(' '), $"结尾的空格在 Windows 上非法:{sanitized}");
    }

    [Theory]
    [InlineData("CON", "_CON")]
    [InlineData("con", "_con")]
    [InlineData("NUL", "_NUL")]
    [InlineData("LPT9", "_LPT9")]
    [InlineData("CON.txt", "_CON.txt")]
    [InlineData("Console", "Console")]      // 仅前缀相同,不算保留名
    [InlineData("MY CON", "MY CON")]
    // 下面四条是回归目标:保留名判定原本发生在 Trim / 去尾点**之前**,于是修剪后才落回保留名,
    // 恰好生成那个非法名。判定对象必须是最终要落盘的那个字符串。
    [InlineData(" CON", "_CON")]            // 前导空白
    [InlineData("CON ", "_CON")]            // 尾随空白
    [InlineData("aux ", "_aux")]            // 小写保留名 + 尾随空白(比较须大小写无关)
    [InlineData("NUL.", "_NUL")]            // 尾点被去掉后才落回保留名
    [InlineData("NUL\t", "NUL_")]           // 反例:制表符是控制字符,先被替换成 '_' → 不再是保留名
    public void SanitizeFilename_PrefixesWindowsReservedNames(string input, string expected) {
        Assert.Equal(expected, StringHelper.SanitizeFilename(input));
    }

    [Fact]
    public void SanitizeFilename_AllDots_KeptAsIs() {
        // 整串都是点时保持原样:修成空名只会让调用方产出一个隐藏文件
        Assert.Equal("...", StringHelper.SanitizeFilename("..."));
    }

    [Fact]
    public void SanitizeFilename_EmptyInput_ReturnsEmpty() {
        Assert.Equal(string.Empty, StringHelper.SanitizeFilename(string.Empty));
    }

    [Fact]
    public void SanitizeFilename_NullInput_Throws() {
        Assert.Throws<ArgumentNullException>(() => StringHelper.SanitizeFilename(null!));
    }

    [Fact]
    public void SanitizeFilename_IsIdempotent() {
        // 净化结果再净化一次不应再变 —— 否则"重名加后缀"之类的后续处理会不稳定
        // " CON" / "CON." 是回归目标:原实现在这两条上会一次变两次(先修剪出 "CON",再被判为保留名)
        foreach (var input in new[] { "非法:书名*?", @"a\b/c", "CON", " CON", "CON.", "Ends with dot.", "普通书名" }) {
            var once = StringHelper.SanitizeFilename(input);
            Assert.Equal(once, StringHelper.SanitizeFilename(once));
        }
    }
}
