using Xunit;
using KindleMate2.Infrastructure.Helpers;

namespace KindleMate2.Tests;

/// <summary>
/// Multi-language clipping-date parsing (MyClippingsHelper.TryParseClippingDate), mirroring the
/// original Kindle Mate 1.38 approach: prefix/weekday cleaning + per-culture DateTime.TryParse fallback.
/// </summary>
public sealed class ClippingDateParsingTests {
    [Theory]
    // zh-CN (baseline regression)
    [InlineData("添加于 2025年5月19日 星期一 下午10:20:31", "2025-05-19 22:20:31")]
    [InlineData("已添加至 2025年5月19日 上午8:05:09", "2025-05-19 08:05:09")]
    // en-US (baseline regression)
    [InlineData("Added on Sunday, May 19, 2025, 10:20:31 PM", "2025-05-19 22:20:31")]
    [InlineData("Added on Tuesday, May 20, 2025, 8:00:00 AM", "2025-05-20 08:00:00")]
    // de-DE
    [InlineData("Hinzugefügt am Sonntag, 19. Mai 2025, 22:20:31", "2025-05-19 22:20:31")]
    // ja-JP
    [InlineData("作成日: 2025年5月19日 22:20:31", "2025-05-19 22:20:31")]
    // fr-FR
    [InlineData("Ajouté le dimanche 19 mai 2025 à 22:20:31", "2025-05-19 22:20:31")]
    // es-ES
    [InlineData("Añadido el domingo, 19 de mayo de 2025, 22:20:31", "2025-05-19 22:20:31")]
    // nl-NL
    [InlineData("Toegevoegd op zondag 19 mei 2025 22:20:31", "2025-05-19 22:20:31")]
    public void TryParseClippingDate_ParsesRegionalDates(string raw, string expected) {
        Assert.True(MyClippingsHelper.TryParseClippingDate(raw, out var date), $"should parse: {raw}");
        Assert.Equal(expected, date.ToString("yyyy-MM-dd HH:mm:ss"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("kein datum hier")]
    [InlineData("位置 #123-125")]
    public void TryParseClippingDate_InvalidInput_ReturnsFalse(string raw) {
        Assert.False(MyClippingsHelper.TryParseClippingDate(raw, out _));
    }
}
