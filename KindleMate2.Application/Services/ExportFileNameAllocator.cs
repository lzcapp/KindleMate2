using System.Globalization;
using System.Text;
using KindleMate2.Domain.Models.Export;
using KindleMate2.Infrastructure.Helpers;

namespace KindleMate2.Application.Services;

/// <summary>
/// 导出文件名分配:把「净化 + 撞名消歧」这套**确定性**规则收敛到一处,供批量导出与单书导出共用。
/// </summary>
/// <remarks>
/// <para>
/// 为什么要抽出来:同一套判定此前存在**两份实现** —— Obsidian 批量导出里有一份(候选序 +
/// <see cref="HashSet{T}"/> 占位),而「按书导出 Markdown」那条路径是「净化后直接写」,**没有任何消歧**。
/// 后者会**静默覆盖**:「A/B」与「A:B」两本书净化后同为 <c>A_B</c>,先导出的那本被后导出的无声吃掉,
/// <c>.md</c> 与 <c>.html</c> 一起丢。把判定收敛到本类,既补上缺失的保护,也消除"同一判定两处实现"的隐患。
/// </para>
/// <para>
/// 两类场景的消歧方式**刻意不同**:<see cref="AssignFileNames"/> 面向「一次导出整批书」——
/// 整批在同一份输入里可见,用内存 <see cref="HashSet{T}"/> 占位即可;<see cref="AssignSingleFileName"/>
/// 面向「一次只导一本书」——看不见其他书,只能看**目标目录里已有什么**。详见各方法 remarks。
/// </para>
/// </remarks>
internal static class ExportFileNameAllocator {
    /// <summary>文件名撞名判据用**大小写不敏感**。</summary>
    /// <remarks>
    /// 动机与 <see cref="StringHelper.SanitizeFilename"/>「按三平台非法字符并集净化」一致:
    /// Windows / macOS 的文件系统默认大小写不敏感,导出目录又常被拷到 Windows / SMB 共享 ——
    /// 若在 Linux 上按大小写敏感判重,"Book" 与 "book" 会各自成文件,拷到 Windows 上就互相覆盖,
    /// 其中一个被静默吞掉。这里宁可**保守**(在 Linux 上也把二者当撞名)以保证产物跨平台一致。
    /// </remarks>
    internal static readonly StringComparer FileNameComparer = StringComparer.OrdinalIgnoreCase;

    /// <summary>
    /// 我们自己的 Markdown 产物里**标识身份**的标题行前缀。
    /// </summary>
    /// <remarks>
    /// 单书消歧时要靠"读已有文件判是不是同一本书"。clippings 侧正文首个标题是
    /// <c>## 📖 &lt;书名&gt;</c>(见 <see cref="StringHelper.BuildMarkdownWithClippings"/>),
    /// 生词侧是 <c>## 📖 &lt;词&gt;</c>(见 <see cref="StringHelper.BuildMarkdownWithLookups"/>)——
    /// 二者同形,故一个前缀够用。emoji 写成转义序列,与既有源码(同一 helper)保持一致。
    /// </remarks>
    internal const string IdentityHeadingPrefix = "## \ud83d\udcd6 ";

    /// <summary>
    /// 读文件判身份时最多扫描的行数。
    /// </summary>
    /// <remarks>
    /// 身份行恒在前几行(产物固定是「一级标题 → 空行 → 身份行」,整批导出时前面另有一段
    /// <c>[TOC]</c>)。设上限是为了**不把整个大文件读进来**:一本几 MB 的导出不该为判身份被全量读一遍。
    /// </remarks>
    private const int IdentityScanLineLimit = 20;

    /// <summary>身份文件即 <c>.md</c>(<c>.html</c> 与它同基名,只换扩展名)。</summary>
    private const string MarkdownExtension = ".md";

    // ————————————————————————— 批量场景(Obsidian) —————————————————————————

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
    internal static List<string> AssignFileNames(IReadOnlyList<ExportBook> books) {
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

    // ————————————————————————— 单书 / 单词场景(按书导出 Markdown) —————————————————————————

    /// <summary>
    /// 为**单本书 / 单个词**分配文件名(不含扩展名);返回的名字由调用方分别拼 <c>.md</c> 与 <c>.html</c>。
    /// </summary>
    /// <param name="directory">目标目录;分配时读它看已有哪些文件。</param>
    /// <param name="name">原始名(书名 / 词);文件名与身份行都由它派生。</param>
    /// <param name="author">作者(仅 clippings 侧有);撞名时用于退化成「作者 - 书名」。</param>
    /// <remarks>
    /// <para>
    /// 为什么单书场景不能照搬批量的 <see cref="HashSet{T}"/>:一次调用只看得到**一本**书,内存里没有
    /// "其他书"可撞,于是改为**看目标目录** —— 这正是批量与单书两条路唯一实质不同的地方。
    /// </para>
    /// <para>
    /// 消歧规则:① 目录下没有 <c>净化(名).md</c> → 直接用;② 有、且**读其身份行确认是同一本** →
    /// **复用该文件名**(幂等覆盖,重复导出同一本书不会堆出 <c> (2)</c>);③ 有、但不是同一本 →
    /// 退化为 <c>净化(作者 - 名)</c>,仍冲突再追加 <c> (2)</c>、<c> (3)</c>… 直到落空。
    /// </para>
    /// <para>
    /// 身份判据只读**文件首部**(<see cref="IdentityScanLineLimit"/> 行内),不读整个文件 ——
    /// 大库下"每次导出都全量读一遍已有产物"是不可接受的。
    /// </para>
    /// <para>
    /// 取舍:这套判据**依赖我们自己的产物格式**(首部有 <c>## 📖 …</c> 身份行)。格式若改,旧文件会被
    /// 判成"非同一本",从而落到下一个候选名而**不是**覆盖 —— 后果是**多一个孤儿文件**,而不是静默丢数据。
    /// 相比"在导出目录写一个隐藏映射文件"的方案,这里刻意**不引入额外持久化状态**:映射文件自身也要面对
    /// "被删 / 被改 / 拷贝时遗漏"的失效,而它一旦失效,要么覆盖、要么全变 <c> (2)</c>,都比"多一个孤儿"更糟。
    /// </para>
    /// </remarks>
    internal static string AssignSingleFileName(string directory, string name, string? author = null) {
        var candidates = BuildCandidates(name, author);
        var identity = IdentityOf(name);

        foreach (var candidate in candidates) {
            if (IsReusable(directory, candidate, identity)) {
                return candidate;
            }
        }

        // 全部候选都被**别人**占用 → 在最后一个候选后叠加序号,并在每个序号上也做身份判定:
        // 否则"重导同一本书"会每次又往后挪一位、堆出一串 (2)(3)(4)…
        var baseName = candidates[^1];
        for (var n = 2; ; n++) {
            var numbered = baseName + " (" + n.ToString(CultureInfo.InvariantCulture) + ")";
            if (IsReusable(directory, numbered, identity)) {
                return numbered;
            }
        }
    }

    /// <summary>按优先级构造候选文件名:先 <c>净化(名)</c>,有作者再退化为 <c>净化(作者 - 名)</c>。</summary>
    private static List<string> BuildCandidates(string name, string? author) {
        var title = SanitizeOrUnknown(name);
        var candidates = new List<string> { title };
        var trimmedAuthor = (author ?? string.Empty).Trim();
        if (trimmedAuthor.Length > 0) {
            var withAuthor = SanitizeOrUnknown(trimmedAuthor + " - " + name);
            // 净化后与书名相等时不必重复试探(与批量路径同一判定)。
            if (!FileNameComparer.Equals(withAuthor, title)) {
                candidates.Add(withAuthor);
            }
        }
        return candidates;
    }

    /// <summary>
    /// 该名字在目标目录里是否**可用**(可落盘而不覆盖别人):文件不存在,或存在的那个就是"我们自己、且是同一本"。
    /// </summary>
    private static bool IsReusable(string directory, string candidate, string identity) {
        var path = Path.Combine(directory, candidate + MarkdownExtension);
        if (!File.Exists(path)) {
            return true;
        }
        // 已被占用:只有当那是**同一本**的旧产物时才复用该名(幂等覆盖);否则视为"别人占着",另择他名。
        return FileIdentityMatches(path, identity);
    }

    /// <summary>
    /// 读 <paramref name="markdownPath"/> 的首部,判断它的身份行是否等于 <paramref name="identity"/>。
    /// </summary>
    /// <remarks>
    /// 读不出来 / 没有身份行时一律返回 <c>false</c>(当成"非同一本")—— 宁可不复用、落到下一个候选名,
    /// 也绝不因为"读不了就当作同一本"而覆盖掉可能是别的书的内容。导出是单线程的,故这里不做并发保护。
    /// </remarks>
    private static bool FileIdentityMatches(string markdownPath, string identity) {
        try {
            using var reader = new StreamReader(markdownPath, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            for (var i = 0; i < IdentityScanLineLimit; i++) {
                var line = reader.ReadLine();
                if (line == null) {
                    return false;
                }
                if (line.StartsWith(IdentityHeadingPrefix, StringComparison.Ordinal)) {
                    var heading = line[IdentityHeadingPrefix.Length..].Trim();
                    return string.Equals(heading, identity, StringComparison.Ordinal);
                }
            }
            return false;
        } catch (IOException) {
            return false;
        } catch (UnauthorizedAccessException) {
            return false;
        }
    }

    /// <summary>身份行里的文本:原始名 Trim 后即产物标题;空名退化为 <see cref="ExportModelBuilder.UnknownBookName"/>。</summary>
    private static string IdentityOf(string name) {
        var trimmed = name.Trim();
        return trimmed.Length > 0 ? trimmed : ExportModelBuilder.UnknownBookName;
    }

    /// <summary>净化名字;净化后为空时退化为 <see cref="ExportModelBuilder.UnknownBookName"/>。</summary>
    private static string SanitizeOrUnknown(string? name) {
        var sanitized = StringHelper.SanitizeFilename(name ?? string.Empty).Trim();
        // 兜底:整串被净化 / 修剪掉(如全为空白)时不能让文件名退化成空串,否则会写出一个隐藏文件。
        return sanitized.Length > 0
            ? sanitized
            : StringHelper.SanitizeFilename(ExportModelBuilder.UnknownBookName);
    }
}
