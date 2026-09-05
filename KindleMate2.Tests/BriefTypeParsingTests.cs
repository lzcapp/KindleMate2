using Xunit;
using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Infrastructure.Helpers;

namespace KindleMate2.Tests;

/// <summary>
/// Multi-language entry-type parsing (BriefTypeTranslations). The tag tables were extended to the
/// full language set used by the original Kindle Mate (modClippingsPatterns): zh/en/ja/de/fr/es/it/
/// pt/pl/nl + ru. Metadata strings here mimic real Kindle My Clippings type lines across regions.
/// </summary>
public sealed class BriefTypeParsingTests {
    [Theory]
    [InlineData("- 标注 | 第 12 页 · 位置 #123-125 | 添加于 2025年5月19日 下午10:20:31", BriefType.Highlight)]
    [InlineData("- 您的笔记 | 第 12 页 · 位置 #126 | 添加于 2025年5月19日 下午10:21:00", BriefType.Note)]
    [InlineData("- 书签 | 第 3 页 · 位置 #55 | 添加于 2025年5月20日 上午8:00:00", BriefType.Bookmark)]
    // en-US (baseline regression)
    [InlineData("- Highlight | Loc. 123-125 | Added on Sunday, May 19, 2025, 10:20:31 PM", BriefType.Highlight)]
    [InlineData("- Note | Loc. 126 | Added on Monday, May 19, 2025, 10:21:00 PM", BriefType.Note)]
    [InlineData("- Bookmark | Page 3 | Added on Tuesday, May 20, 2025, 8:00:00 AM", BriefType.Bookmark)]
    // de-DE
    [InlineData("- Markierung | Seite 12 | Hinzugefügt am Sonntag, 19. Mai 2025, 22:20:31", BriefType.Highlight)]
    [InlineData("- Notiz | Seite 12 | Hinzugefügt am Montag, 19. Mai 2025, 22:21:00", BriefType.Note)]
    [InlineData("- Lesezeichen | Seite 3 | Hinzugefügt am Dienstag, 20. Mai 2025, 08:00:00", BriefType.Bookmark)]
    // ja-JP
    [InlineData("- ハイライト | 12 ページ | 作成日: 2025年5月19日 22:20:31", BriefType.Highlight)]
    [InlineData("- メモ | 12 ページ | 作成日: 2025年5月19日 22:21:00", BriefType.Note)]
    [InlineData("- ブックマーク | 3 ページ | 作成日: 2025年5月20日 08:00:00", BriefType.Bookmark)]
    // es-ES / fr-FR / it-IT / pt-PT / pl-PL / nl-NL / ru
    [InlineData("- Subrayado | Pág. 12 | Añadido el domingo, 19 de mayo de 2025, 22:20:31", BriefType.Highlight)]
    [InlineData("- nota | Pág. 12 | Añadido el lunes, 19 de mayo de 2025, 22:21:00", BriefType.Note)]
    [InlineData("- marcador | Pág. 3 | Añadido el martes, 20 de mayo de 2025, 08:00:00", BriefType.Bookmark)]
    [InlineData("- surlignement | Page 12 | Ajouté le dimanche 19 mai 2025 à 22:20:31", BriefType.Highlight)]
    [InlineData("- signet | Page 3 | Ajouté le mardi 20 mai 2025 à 08:00:00", BriefType.Bookmark)]
    [InlineData("- evidenziazione | Pag. 12 | 19 maggio 2025 22:20:31", BriefType.Highlight)]
    [InlineData("- segnalibro | Pag. 3 | 20 maggio 2025 08:00:00", BriefType.Bookmark)]
    [InlineData("- destaque | Pág. 12 | 19 de maio de 2025, 22:20:31", BriefType.Highlight)]
    [InlineData("- podkreślenie | Str. 12 | Dodany w niedzielę, 19 maja 2025, 22:20:31", BriefType.Highlight)]
    [InlineData("- notatka | Str. 12 | Dodany w poniedziałek, 19 maja 2025, 22:21:00", BriefType.Note)]
    [InlineData("- zakładka | Str. 3 | Dodany we wtorek, 20 maja 2025, 08:00:00", BriefType.Bookmark)]
    [InlineData("- notitie | Pagina 12 | Toegevoegd op zondag 19 mei 2025 22:20:31", BriefType.Note)]
    [InlineData("- bladwijzer | Pagina 3 | Toegevoegd op dinsdag 20 mei 2025 08:00:00", BriefType.Bookmark)]
    [InlineData("- отрывок | Стр. 12 | 19 мая 2025 г., 22:20:31", BriefType.Highlight)]
    [InlineData("- заметка | Стр. 12 | 19 мая 2025 г., 22:21:00", BriefType.Note)]
    [InlineData("- закладка | Стр. 3 | 20 мая 2025 г., 08:00:00", BriefType.Bookmark)]
    // 真实公开德语样本(becausecurious/kindle_clippings_parser;SuzanaK kindle_clippings_processor 注释)——类型行带 "Ihre"/"Pos." 变体
    [InlineData("- Markierung Pos. 2919-28", BriefType.Highlight)]
    [InlineData("- Ihre Markierung auf Seite 262", BriefType.Highlight)]
    public void ParseEntryType_RecognizesRegionalTypeTags(string metadata, BriefType expected) {
        Assert.Equal(expected, MyClippingsHelper.ParseEntryType(metadata));
    }

    [Fact]
    public void ParseEntryType_UnknownMetadata_ReturnsUnknown() {
        Assert.Equal(BriefType.Unknown, MyClippingsHelper.ParseEntryType("- etwas unbekanntes | Seite 9"));
    }
}
