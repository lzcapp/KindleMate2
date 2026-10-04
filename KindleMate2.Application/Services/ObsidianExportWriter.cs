using System.Globalization;
using System.Text;
using KindleMate2.Domain.Models.Export;
using KindleMate2.Infrastructure.Helpers;

namespace KindleMate2.Application.Services;

/// <summary>
/// 把导出中间模型(见 <see cref="ExportModelBuilder"/>)拼装成一份**可直接丢进 Obsidian vault** 的目录。
/// </summary>
/// <remarks>
/// <para>
/// 为什么单独抽成 writer、而不塞进 <see cref="ExportManager"/>:与 <c>WriteClippingsJson</c> 同理 ——
/// 拼装是**纯函数**(输入两份 DTO、输出若干文件),不碰库、不联网、不依赖设备,单测可直接喂固定
/// 时刻与固定数据。上游的分组 / 跳过 / 省略口径全部复用 <see cref="ExportModelBuilder"/>,本类
/// **不重新遍历实体、不重写那套口径** —— 那正是 PR1 抽出中间模型的目的。
/// </para>
/// <para>
/// 产物结构刻意把书文件放进 <c>Books/</c> 子目录:<c>index.md</c> 与 <c>Vocabs.md</c> 是固定的顶层
/// 文件名,若书文件平铺在同一层,一本恰好叫「index」的书就会与之撞名 —— 靠"给书改名规避"既脆弱
/// 又难解释,分目录从**结构上**让这种撞名不可能发生。
/// </para>
/// </remarks>
internal static class ObsidianExportWriter {
    /// <summary>vault 根目录名(建在 Exports 之下,避免与既有的 Clippings.md/json/csv 混在一起)。</summary>
    internal const string ObsidianDirectoryName = "Obsidian";

    /// <summary>书文件所在子目录名。</summary>
    internal const string BooksDirectoryName = "Books";

    /// <summary>索引文件名。</summary>
    internal const string IndexFileName = "index.md";

    /// <summary>生词文件名。</summary>
    internal const string VocabsFileName = "Vocabs.md";

    /// <summary>Markdown 扩展名。</summary>
    internal const string MarkdownExtension = ".md";

    /// <summary>生词没有来源书时的归类标题。</summary>
    internal const string UnknownSourceLabel = "未知来源";

    /// <summary>
    /// 行分隔符固定用 LF,不用 <see cref="Environment.NewLine"/>。
    /// </summary>
    /// <remarks>
    /// 产物是**跨平台搬运的数据文件**(vault 会被拷到别的机器 / 同步盘),LF 是最通用的写法;
    /// 用 <c>Environment.NewLine</c> 会让同一份库在 Windows 与 macOS 上导出成**逐字节不同**的文件,
    /// diff 噪音大、也不便于测试固定断言。
    /// </remarks>
    private const string NewLine = "\n";

    /// <summary>
    /// 书文件名撞名判据用**大小写不敏感**。
    /// </summary>
    /// <remarks>
    /// 动机与 <see cref="StringHelper.SanitizeFilename"/>「按三平台非法字符并集净化」一致:
    /// Windows / macOS 的文件系统默认大小写不敏感,导出目录又常被拷到 Windows / SMB 共享 ——
    /// 若在 Linux 上按大小写敏感判重,"Book" 与 "book" 会各自成文件,拷到 Windows 上就互相覆盖,
    /// 其中一个被静默吞掉。这里宁可**保守**(在 Linux 上也把二者当撞名)以保证产物跨平台一致。
    /// </remarks>
    private static readonly StringComparer FileNameComparer = StringComparer.OrdinalIgnoreCase;

    /// <summary>
    /// 在 <paramref name="exportsDirectory"/> 下产出整份 Obsidian vault。
    /// </summary>
    /// <param name="clippings">标注文档(通常来自 <see cref="ExportModelBuilder.BuildClippingsDocument"/>)。</param>
    /// <param name="vocabs">生词文档(通常来自 <see cref="ExportModelBuilder.BuildVocabsDocument"/>)。</param>
    /// <param name="exportsDirectory">Exports 根目录;本方法在其下新建 <c>Obsidian/</c>。</param>
    /// <remarks>
    /// 只**新建 / 覆盖**本程序产出的文件,**不删除** vault 里已有的其他内容 —— 用户可能在
    /// <c>Books/</c> 里加了自己的笔记或附件,清空目录会把它们一起删掉。代价是:改了书名后旧文件
    /// 会残留成孤儿(需用户自行清理),这是刻意用"多一个孤儿文件"换"绝不误删用户数据"。
    /// </remarks>
    internal static void WriteVault(ExportClippingsDocument clippings, ExportVocabsDocument vocabs,
        string exportsDirectory) {
        var vaultDirectory = Path.Combine(exportsDirectory, ObsidianDirectoryName);
        var booksDirectory = Path.Combine(vaultDirectory, BooksDirectoryName);
        // 先建 Books/ 再写书文件:空集合时也要留下一个**空的 Books/** 目录,而不是根本不建。
        Directory.CreateDirectory(booksDirectory);

        // 文件名分配必须**整批**做:某本书该退化成什么名字,取决于前面已经占用了哪些名字,
        // 逐本独立分配会让两本撞名的书各自都拿到同一个"首选名"。
        var fileNames = AssignBookFileNames(clippings.Books);
        for (var i = 0; i < clippings.Books.Count; i++) {
            WriteTextNoBom(
                Path.Combine(booksDirectory, fileNames[i] + MarkdownExtension),
                BuildBookMarkdown(clippings.Books[i], clippings.ExportedAt));
        }

        WriteTextNoBom(Path.Combine(vaultDirectory, IndexFileName),
            BuildIndexMarkdown(clippings, fileNames));
        WriteTextNoBom(Path.Combine(vaultDirectory, VocabsFileName),
            BuildVocabsMarkdown(vocabs));
    }

    /// <summary>
    /// 为每本书分配一个**确定性**的文件名(不含扩展名),顺序与 <paramref name="books"/> 一致。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 消歧规则(按优先级):① <c>净化(书名)</c>;② 冲突时退化为 <c>净化(作者 - 书名)</c>;
    /// ③ 仍冲突则在上一个候选后追加 <c> (2)</c>、<c> (3)</c>… 直到落空。
    /// </para>
    /// <para>
    /// 刻意**不用随机数 / 时间戳**消歧:那会让同一份库每次导出得到不同文件名,<c>index.md</c> 的
    /// 双链随之漂移,Obsidian 里昨天建立的链接今天全断。确定性 = 同名输入恒得同套文件名。
    /// </para>
    /// </remarks>
    internal static List<string> AssignBookFileNames(IReadOnlyList<ExportBook> books) {
        var used = new HashSet<string>(FileNameComparer);
        var names = new List<string>(books.Count);
        foreach (var book in books) {
            names.Add(AssignOneFileName(book, used));
        }
        return names;
    }

    private static string AssignOneFileName(ExportBook book, HashSet<string> used) {
        var title = SanitizeOrUnknown(book.Title);

        var candidates = new List<string> { title };
        var author = (book.Author ?? string.Empty).Trim();
        if (author.Length > 0) {
            var withAuthor = SanitizeOrUnknown(author + " - " + book.Title);
            // 作者限定名与书名同名(如作者为空串已被排除,这里是净化后恰好相等)时不必重复试探。
            if (!FileNameComparer.Equals(withAuthor, title)) {
                candidates.Add(withAuthor);
            }
        }

        foreach (var candidate in candidates) {
            // HashSet.Add 同时完成"查重"与"占位":返回 true 即此前不存在,当场登记。
            if (used.Add(candidate)) {
                return candidate;
            }
        }

        // 全部候选都被占用 → 在**最后一个候选**(优先是作者限定名,更可能唯一)后叠加序号。
        var baseName = candidates[^1];
        for (var n = 2; ; n++) {
            var numbered = baseName + " (" + n.ToString(CultureInfo.InvariantCulture) + ")";
            if (used.Add(numbered)) {
                return numbered;
            }
        }
    }

    /// <summary>净化书名;<paramref name="title"/> 净化后为空时退化为 <see cref="ExportModelBuilder.UnknownBookName"/>。</summary>
    private static string SanitizeOrUnknown(string? title) {
        var sanitized = StringHelper.SanitizeFilename(title ?? string.Empty).Trim();
        // 兜底:整串被净化 / 修剪掉(如全为空白)时不能让文件名退化成空串,否则会写出一个隐藏文件。
        return sanitized.Length > 0
            ? sanitized
            : StringHelper.SanitizeFilename(ExportModelBuilder.UnknownBookName);
    }

    /// <summary>拼装单本书文件:<c>--- frontmatter ---</c> + 标题 + 作者 + 逐条标注。</summary>
    internal static string BuildBookMarkdown(ExportBook book, string exportedAt) {
        var sb = new StringBuilder();
        sb.Append("---").Append(NewLine);
        // title / author 一律走双引号标量:含 ':'、以 '#' 开头、含前后空格、含 '---' 的书名若裸写
        // 都会破坏 YAML 结构(裸标量里 ':' 会被当成键值分隔、'#' 起注释、前后空格被吃掉),
        // 统一加引号并转义才能保证"解析回来与原值逐字相同"。
        sb.Append("title: ").Append(EscapeYamlDoubleQuoted(book.Title)).Append(NewLine);
        sb.Append("author: ").Append(EscapeYamlDoubleQuoted(book.Author ?? string.Empty)).Append(NewLine);
        sb.Append("source: kindle").Append(NewLine);
        sb.Append("exported_at: ").Append(exportedAt).Append(NewLine);
        sb.Append("clippings: ").Append(book.Entries.Count.ToString(CultureInfo.InvariantCulture)).Append(NewLine);
        sb.Append("tags:").Append(NewLine);
        sb.Append("  - kindle").Append(NewLine);
        sb.Append("---").Append(NewLine);
        sb.Append(NewLine);

        // 标题里若含换行,不能原样写进 H1 —— 会变成两行,后半截跑出标题。折叠成单行显示。
        sb.Append("# ").Append(SingleLine(book.Title));

        var author = (book.Author ?? string.Empty).Trim();
        if (author.Length > 0) {
            sb.Append(NewLine).Append(NewLine);
            sb.Append("> ").Append(SingleLine(author));
        }

        for (var i = 0; i < book.Entries.Count; i++) {
            sb.Append(NewLine).Append(NewLine);
            if (i > 0) {
                // 条目之间用分隔线;第一条与标题 / 作者之间只留空行(对齐产物样例)。
                sb.Append("---").Append(NewLine).Append(NewLine);
            }
            var entry = book.Entries[i];
            var meta = BuildEntryMeta(entry);
            if (meta != null) {
                sb.Append(meta).Append(NewLine).Append(NewLine);
            }
            // 正文里的换行原样保留(Markdown 段落);仅去掉结尾多余空行,避免与分隔线之间堆叠。
            sb.Append(entry.Content.TrimEnd('\n'));
        }

        sb.Append(NewLine);
        return sb.ToString();
    }

    /// <summary>
    /// 拼装一条标注的位置行:<c>**Location 1 · 第 12 页 · 2026-01-01 10:00:00**</c>。
    /// </summary>
    /// <remarks>
    /// 位置 / 页码 / 日期**只在有值时出现**,用 <c> · </c> 连接;三者都没有时返回 null(整行省略,
    /// 不写一个空的 <c>****</c>)。页码为 null 时不输出「第 X 页」那一段 —— 库里"无页码"记 0,
    /// 中间模型已把它折成 null。
    /// </remarks>
    private static string? BuildEntryMeta(ExportEntry entry) {
        var parts = new List<string>(3);
        if (!string.IsNullOrWhiteSpace(entry.Location)) {
            parts.Add(SingleLine(entry.Location.Trim()));
        }
        if (entry.Page is { } page) {
            parts.Add("第 " + page.ToString(CultureInfo.InvariantCulture) + " 页");
        }
        if (!string.IsNullOrWhiteSpace(entry.Date)) {
            parts.Add(SingleLine(entry.Date.Trim()));
        }
        return parts.Count > 0 ? "**" + string.Join(" · ", parts) + "**" : null;
    }

    /// <summary>拼装 <c>index.md</c>:汇总行 + 每本书的双链 / 作者 / 条数。</summary>
    internal static string BuildIndexMarkdown(ExportClippingsDocument document, IReadOnlyList<string> fileNames) {
        var books = document.Books;
        var totalEntries = 0;
        foreach (var book in books) {
            totalEntries += book.Entries.Count;
        }

        var sb = new StringBuilder();
        sb.Append("# Kindle 标注索引").Append(NewLine).Append(NewLine);
        sb.Append("共 ").Append(books.Count.ToString(CultureInfo.InvariantCulture))
          .Append(" 本书 · ").Append(totalEntries.ToString(CultureInfo.InvariantCulture))
          .Append(" 条标注 · 导出时间 ").Append(document.ExportedAt)
          .Append(NewLine).Append(NewLine);

        if (books.Count == 0) {
            // 空集合也要产出**合法**的 index.md(有标题、有汇总),而不是一个空文件 ——
            // 空文件在 Obsidian 里点开是一片空白,分不清"没导出"还是"本来就没数据"。
            sb.Append("（暂无标注）").Append(NewLine);
            return sb.ToString();
        }

        sb.Append("| 书名 | 作者 | 条数 |").Append(NewLine);
        sb.Append("|---|---|---|").Append(NewLine);
        for (var i = 0; i < books.Count; i++) {
            var book = books[i];
            sb.Append("| ").Append(BuildBookLink(book.Title, fileNames[i]))
              .Append(" | ").Append(EscapeTableCell(book.Author))
              .Append(" | ").Append(book.Entries.Count.ToString(CultureInfo.InvariantCulture))
              .Append(" |").Append(NewLine);
        }
        return sb.ToString();
    }

    /// <summary>
    /// 生成指向**实际文件**的双链。
    /// </summary>
    /// <remarks>
    /// 链接目标必须是落盘的文件名(去 <c>.md</c> 后缀),而不是原始书名 —— 书名被净化过
    /// (如 <c>Sapiens: …</c> → <c>Sapiens_ …</c>)或撞名被消歧过时,用原始书名做目标会指向一个
    /// **不存在的文件**,Obsidian 里整页链接全断。文件名与书名不一致时附带别名,让链接显示回原名。
    /// </remarks>
    private static string BuildBookLink(string title, string fileName) {
        if (FileNameComparer.Equals(fileName, title)) {
            return "[[" + fileName + "]]";
        }
        return "[[" + fileName + "|" + EscapeLinkAlias(title) + "]]";
    }

    /// <summary>
    /// 转义双链别名。<c>|</c> 是目标与别名的分隔符,别名里再出现会截断链接;<c>]</c> 会提前闭合
    /// <c>]]</c>;换行会把链接拆成两行。三者都替换成安全字符。
    /// </summary>
    private static string EscapeLinkAlias(string alias) =>
        SingleLine(alias).Replace('|', '/').Replace(']', ')');

    /// <summary>
    /// 转义 Markdown 表格单元格里的 <c>|</c>(不转义会把该行拆成多列)与换行(会断表)。
    /// </summary>
    private static string EscapeTableCell(string? value) =>
        SingleLine(value ?? string.Empty).Replace("|", "\\|", StringComparison.Ordinal);

    /// <summary>拼装 <c>Vocabs.md</c>:按来源书分组,每条列词 / 词干 / 语言 / 原句 / 时间。</summary>
    internal static string BuildVocabsMarkdown(ExportVocabsDocument document) {
        var sb = new StringBuilder();
        sb.Append("# Kindle 生词").Append(NewLine).Append(NewLine);
        sb.Append("共 ").Append(document.Words.Count.ToString(CultureInfo.InvariantCulture))
          .Append(" 条生词 · 导出时间 ").Append(document.ExportedAt)
          .Append(NewLine).Append(NewLine);

        if (document.Words.Count == 0) {
            sb.Append("（暂无生词）").Append(NewLine);
            return sb.ToString();
        }

        // 分组保留库内插入顺序(与中间模型一致):同一本书的词连在一起,不按书名重排 ——
        // 重排会让 diff 里整段位移,和 JSON 导出的"保持插入顺序"口径也会对不上。
        var groups = new List<(string Book, List<ExportWord> Words)>();
        var byBook = new Dictionary<string, List<ExportWord>>(StringComparer.Ordinal);
        foreach (var word in document.Words) {
            var book = string.IsNullOrWhiteSpace(word.Book) ? UnknownSourceLabel : word.Book.Trim();
            if (!byBook.TryGetValue(book, out var list)) {
                list = [];
                byBook.Add(book, list);
                groups.Add((book, list));
            }
            list.Add(word);
        }

        foreach (var (book, words) in groups) {
            sb.Append("## ").Append(SingleLine(book)).Append(NewLine).Append(NewLine);
            foreach (var word in words) {
                sb.Append("- **").Append(word.Word).Append("**");
                if (!string.IsNullOrWhiteSpace(word.Stem)) {
                    sb.Append(" · 词干 ").Append(SingleLine(word.Stem));
                }
                if (!string.IsNullOrWhiteSpace(word.Language)) {
                    sb.Append(" · 语言 ").Append(SingleLine(word.Language));
                }
                sb.Append(NewLine);

                var usage = word.Usage.TrimEnd('\n');
                if (usage.Length > 0) {
                    // 原句可能多行(中间模型已把库里的换行占位符还原成真换行);逐行加 '> ' 保持引用块。
                    foreach (var line in usage.Split('\n')) {
                        sb.Append("  > ").Append(line).Append(NewLine);
                    }
                }
                if (!string.IsNullOrWhiteSpace(word.Timestamp)) {
                    sb.Append("  时间：").Append(SingleLine(word.Timestamp)).Append(NewLine);
                }
                sb.Append(NewLine);
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// 把值包成 YAML **双引号标量**并转义。
    /// </summary>
    /// <remarks>
    /// 为什么统一用双引号而不是"按需加引号":YAML 裸标量的边界规则很细(冒号后跟空格、行首
    /// <c>#</c>、<c>---</c>、前导 / 尾随空格、特殊首字符…),逐条判断既容易漏又难维护;
    /// 一律加引号只需处理两种转义(<c>\</c> 与 <c>"</c>)外加控制字符,行为可预测。
    /// <para>
    /// 换行必须写成 <c>\n</c> 字面量:双引号标量里**真实换行**会被折叠成空格,书名里的换行
    /// 就此丢失;写成 <c>\n</c> 才能在 YAML 解析时还原。顺序上先转义 <c>\</c>,否则后续插入的
    /// 反斜杠会被二次转义。
    /// </para>
    /// </remarks>
    internal static string EscapeYamlDoubleQuoted(string value) {
        ArgumentNullException.ThrowIfNull(value);

        var sb = new StringBuilder(value.Length + 2);
        sb.Append('"');
        foreach (var c in value) {
            switch (c) {
                case '\\': sb.Append("\\\\"); break;
                case '"': sb.Append("\\\""); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default: sb.Append(c); break;
            }
        }
        sb.Append('"');
        return sb.ToString();
    }

    /// <summary>把值折成单行(CRLF / CR / LF 一律换成空格),用于标题、作者、表格单元格等单行位置。</summary>
    private static string SingleLine(string value) =>
        value.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ');

    /// <summary>
    /// 写 UTF-8 **无 BOM** 的文本文件。
    /// </summary>
    /// <remarks>
    /// 不用 <c>StreamWriter</c> 的默认编码:那会带 BOM,而 BOM 会让部分 Markdown 工具 / 解析器
    /// 把开头的 U+FEFF 当成正文渲染出一个多余字符(JSON 导出有同样顾虑,见 <c>ExportManager.WriteJson</c>)。
    /// </remarks>
    private static void WriteTextNoBom(string path, string content) {
        File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }
}
