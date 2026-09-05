namespace KindleMate2.Shared.Constants {
    public static class BriefTypeTranslations {
        /// <summary>
        /// Kindle 各区域 My Clippings 的类型标记(经 ToLower 后做包含匹配)。
        /// 词表对齐原版 Kindle Mate modClippingsPatterns 的 10 语言覆盖(zh/en/ja/de/fr/es/it/pt/pl/nl + ru),
        /// 2026-09-05 补齐:中文裸词(标注/笔记/书签)、德(Notiz/Markierung/Lesezeichen)、日(メモ/ハイライト/ブックマーク)、
        /// 波(notatka/podkreślenie/zakładka)、荷(notitie/bladwijzer)、俄(заметка/отрывок/закладка)、意书签(segnalibro)。
        /// </summary>
        public static readonly List<string> Note = ["note", "nota", "笔记", "的笔记", "备注", "notiz", "メモ", "notatka", "notitie", "заметка"];
        public static readonly List<string> Highlight = ["highlight", "subrayado", "surlignement", "标注", "的标注", "destaque", "evidenziazione", "markierung", "podkreślenie", "ハイライト", "отрывок"];
        public static readonly List<string> Bookmark = ["bookmark", "marcador", "signet", "书签", "的书签", "segnalibro", "zakładka", "bladwijzer", "lesezeichen", "ブックマーク", "закладка"];
        public static readonly List<string> Cut = ["cut", "文章剪切", "剪切", "剪贴"];
        public static readonly List<string> Dividers = ["页"];
    }
}