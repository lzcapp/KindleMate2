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
    // 真实公开样本(来源:becausecurious/kindle_clippings_parser samples;SuzanaK kindle_clippings_processor 注释)
    [InlineData("Hinzugefügt am Sonntag, 27. September 2020 4.46 Uhr GMT+07:29", "2020-09-27 04:46:00")]
    [InlineData("Hinzugefügt am Mittwoch, 18. August 2021 5.53 Uhr GMT+07:29", "2021-08-18 05:53:00")]
    [InlineData("Hinzugefügt am Sonntag, 27. Mai 2012 um 01:31:13 Uhr", "2012-05-27 01:31:13")]
    [InlineData("Added on Sunday, September 05, 2021, 04:39 PM", "2021-09-05 16:39:00")]
    // KindleToJoplin languages.ts 六语言真实 example(日期段,来源注明)
    [InlineData("Added on Sunday, January 15, 2024 10:30:45 AM", "2024-01-15 10:30:45")]
    [InlineData("Añadido el domingo, 15 de enero de 2024 10:30:45", "2024-01-15 10:30:45")]
    [InlineData("Aggiunto il domenica 15 gennaio 2024 10:30:45", "2024-01-15 10:30:45")]
    [InlineData("Ajouté le dimanche 15 janvier 2024 10:30:45", "2024-01-15 10:30:45")]
    [InlineData("Hinzugefügt am Sonntag, 15. Januar 2024 10:30:45", "2024-01-15 10:30:45")]
    [InlineData("Adicionado em domingo, 15 de janeiro de 2024 10:30:45", "2024-01-15 10:30:45")]
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
