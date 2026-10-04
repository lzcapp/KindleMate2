using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using KindleMate2.Domain.Models.Export;
using Microsoft.Data.Sqlite;

namespace KindleMate2.Application.Services;

/// <summary>
/// 把生词中间模型(见 <see cref="ExportModelBuilder.BuildVocabsDocument"/>)拼装成一份
/// **Anki 可直接导入的 <c>.apkg</c> 牌组**。
/// </summary>
/// <remarks>
/// <para>
/// 与 <see cref="ObsidianExportWriter"/> 同理:拼装是**纯函数**(输入一份 DTO,输出一个文件),
/// 不碰库、不联网、不依赖设备,单测可直接喂固定时刻与固定数据。上游的「空词跳过 / 换行占位符还原 /
/// 语言前缀解析」口径全部复用中间模型,本类**不重新遍历实体**。
/// </para>
/// <para>
/// <b>格式来源(全部查证自 Anki 官方源码,而非凭记忆):</b>
/// <list type="bullet">
///   <item>表结构 / 索引:<c>rslib/src/storage/schema11.sql</c>(col / notes / cards / revlog / graves)。</item>
///   <item>新库 <c>col</c> 行的写入:<c>rslib/src/storage/sqlite.rs</c> 的 <c>open_or_create</c> ——
///     <c>crt</c> 用 <c>TimestampSecs(v1_creation_date())</c>(<b>秒</b>),<c>scm</c> 用
///     <c>TimestampMillis::now()</c>(<b>毫秒</b>),<c>ver</c> = <c>SCHEMA_STARTING_VERSION</c> = <c>11</c>。</item>
///   <item>默认 <c>conf</c>:<c>rslib/src/config/schema11.rs</c> 的 <c>schema11_config_as_string</c>。</item>
///   <item><c>models</c> / <c>decks</c> 的 schema-11 JSON 结构:<c>rslib/src/notetype/schema11.rs</c>
///     与 <c>rslib/src/decks/schema11.rs</c>。</item>
///   <item>各字段取值(<c>notes</c> / <c>cards</c> / <c>sfld</c> / <c>csum</c>):交叉验证了
///     genanki(Kerrick Staley 的成熟 <c>.apkg</c> 生成器)的 <c>package.py</c> / <c>note.py</c> /
///     <c>card.py</c>,以及 Anki 的 <c>rslib/src/notes/mod.rs</c> 里 <c>field_checksum</c>。</item>
/// </list>
/// </para>
/// <para>
/// <b>刻意生成 legacy 的 <c>collection.anki2</c>(而非 <c>collection.anki21</c>):</b>
/// 后者是 zstd 压缩的,新老 Anki 都能读 legacy,反之不然 —— 兼容性优先。
/// </para>
/// </remarks>
internal static class AnkiApkgWriter {
    /// <summary>牌组名。来源书作为 tag,而不是一本书一个牌组(那会炸出几十个牌组)。</summary>
    internal const string DeckName = "KindleMate2";

    /// <summary>ZIP 里 SQLite 库的条目名。</summary>
    internal const string CollectionEntryName = "collection.anki2";

    /// <summary>ZIP 里媒体清单的条目名。</summary>
    internal const string MediaEntryName = "media";

    /// <summary>导出文件名(与 CSV / JSON 的 <c>Vocabs.*</c> 并列,同为「生词」这一内容)。</summary>
    internal const string AnkiDeckFileName = "Vocabs.apkg";

    /// <summary>
    /// Anki 的 schema 版本。新库以 <c>11</c> 起始,再由 Anki 自身升级到最新
    /// (见 <c>rslib/src/storage/upgrades/mod.rs</c> 的 <c>SCHEMA_STARTING_VERSION</c>)。
    /// </summary>
    internal const int SchemaVersion = 11;

    /// <summary>
    /// note type / 牌组 / 牌组预设的 id。用**固定常量**(而非时间戳)以保证同一份输入
    /// 导出的产物逐字节可复现。取 13 位是为了贴近 Anki 用毫秒时间戳当 id 的惯例(避免与用户
    /// 既有对象撞号时过于"显眼");三者互不相同。
    /// </summary>
    internal const long NotetypeId = 1_700_000_000_001L;
    internal const long DeckId = 1_700_000_000_002L;
    internal const long DeckConfigId = 1_700_000_000_003L;

    /// <summary>note 的 5 个字段名,顺序即 <c>flds</c> 里的顺序、也是字段 ordinal。</summary>
    internal static readonly string[] FieldNames = ["Word", "Definition", "Stem", "Usage", "Source"];

    /// <summary>字段分隔符(U+001F),Anki 用它把一条 note 的多个字段拼进单个 <c>flds</c> 文本列。</summary>
    private const char FieldSeparator = '\u001f';

    /// <summary>
    /// Anki 的 guid 是 base91 编码的随机数(见 <c>rslib</c> 的 <c>guid64</c>)。
    /// 这里沿用 genanki 的那张表,编码出来的 guid 与 Anki 自身的形态一致。
    /// </summary>
    private static readonly char[] Base91Alphabet = [
        'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's',
        't', 'u', 'v', 'w', 'x', 'y', 'z', 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L',
        'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z', '0', '1', '2', '3', '4',
        '5', '6', '7', '8', '9', '!', '#', '$', '%', '&', '(', ')', '*', '+', ',', '-', '.', '/', ':',
        ';', '<', '=', '>', '?', '@', '[', ']', '^', '_', '`', '{', '|', '}', '~'
    ];

    /// <summary>
    /// note type 的正面模板:只放 <c>Word</c>。卡片是否生成取决于 <c>req</c>(见 <see cref="BuildModelsJson"/>)。
    /// </summary>
    private const string FrontTemplate = "<div class=\"word\">{{Word}}</div>";

    /// <summary>
    /// note type 的反面模板:<c>{{FrontSide}}</c> 复现正面(即 Word),再依次给释义 / 词干 / 原句 / 来源。
    /// 空字段渲染为空,Anki 不会因此报错。
    /// </summary>
    private const string BackTemplate =
        "{{FrontSide}}\n\n<hr id=answer>\n\n" +
        "<div class=\"definition\">{{Definition}}</div>\n\n" +
        "<div class=\"stem\">{{Stem}}</div>\n\n" +
        "<div class=\"usage\">{{Usage}}</div>\n\n" +
        "<div class=\"source\">{{Source}}</div>";

    /// <summary>
    /// note type 样式。同时覆盖 <c>nightMode</c>(Anki 2.1.50+ 给 body 加的类)与旧版的
    /// <c>night_mode</c>,保证夜间模式可读 —— 两个类名都写是刻意的,只认其一会在某些版本上失去样式。
    /// </summary>
    private const string Css = """
.card {
  font-family: -apple-system, "Segoe UI", Roboto, "Helvetica Neue", Arial, "PingFang SC", "Microsoft YaHei", sans-serif;
  font-size: 20px;
  line-height: 1.5;
  text-align: left;
  color: #1a1a1a;
  background: #ffffff;
}
.word { font-size: 28px; font-weight: 600; }
.definition { margin-top: 4px; }
.stem { color: #666666; font-style: italic; }
.usage { margin-top: 8px; padding-left: 10px; border-left: 3px solid #cccccc; color: #444444; }
.source { margin-top: 12px; font-size: 14px; color: #888888; }
.nightMode.card, .night_mode.card { color: #e8e8e8; background: #2b2b2b; }
.nightMode .stem, .night_mode .stem { color: #aaaaaa; }
.nightMode .usage, .night_mode .usage { border-left-color: #555555; color: #cccccc; }
.nightMode .source, .night_mode .source { color: #999999; }
""";

    /// <summary>
    /// Anki 默认的 LaTeX 前言 / 后记。生词卡不用 LaTeX,但 Anki 的 stock note type 都带这套值,
    /// 补齐可让导入后的 note type 与 Anki 自建的形态一致(留空也是合法的,此处取"更像原生")。
    /// </summary>
    private const string LatexPre =
        "\\documentclass[12pt]{article}\n\\special{papersize=3in,5in}\n\\usepackage[utf8]{inputenc}\n" +
        "\\usepackage{amssymb,amsmath}\n\\pagestyle{empty}\n\\setlength{\\parindent}{0in}\n" +
        "\\begin{document}\n";

    private const string LatexPost = "\\end{document}";

    /// <summary>
    /// <c>col.conf</c> 的 JSON 编码选项。
    /// </summary>
    /// <remarks>
    /// 用放宽的编码器(与 <c>ExportManager.WriteJson</c> 同理由):默认编码器会把非 ASCII 写成
    /// <c>\uXXXX</c>,而这些 JSON 里会出现中文书名(仅当它进到 conf/decks 时)。compact(不缩进)是
    /// 刻意的 —— 这几段是给 Anki 解析的机器数据,不是给人读的,缩进只会白占空间。
    /// </remarks>
    private static readonly JsonSerializerOptions JsonOptions = new() {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// 生成 <c>.apkg</c>。产物是一个 ZIP,含 <c>collection.anki2</c>(SQLite)与 <c>media</c>(无媒体时为 <c>{}</c>)。
    /// </summary>
    /// <param name="document">生词文档(通常来自 <see cref="ExportModelBuilder.BuildVocabsDocument"/>)。</param>
    /// <param name="definitionsByWord">「词 → 释义」;离线导出时传空字典,Definition 字段即为空。</param>
    /// <param name="filePath">目标 <c>.apkg</c> 路径;父目录须已存在。</param>
    /// <param name="exportedAt">导出时刻;不传则取当前时间(仅单测需要显式固定它)。</param>
    internal static void WriteApkg(ExportVocabsDocument document, IReadOnlyDictionary<string, string> definitionsByWord,
        string filePath, DateTimeOffset? exportedAt = null) {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(definitionsByWord);
        ArgumentNullException.ThrowIfNull(filePath);

        var stamp = exportedAt ?? DateTimeOffset.Now;

        // 先建 SQLite 库到临时文件,再把它的字节塞进 ZIP —— Anki 读的是 ZIP 里的这个文件。
        // 用**系统临时目录**而不是 Exports 目录:这里只读它的字节、不做改名(不需要同卷),
        // 万一进程在导出中途崩溃,残留物落在临时目录里(会被系统清理)而不是用户眼皮底下的 Exports。
        var tempDb = Path.Combine(Path.GetTempPath(),
            "km2-anki-" + Guid.NewGuid().ToString("N") + ".anki2");
        try {
            WriteCollectionDatabase(tempDb, document, definitionsByWord, stamp);

            using var zip = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
            using var archive = new ZipArchive(zip, ZipArchiveMode.Create);
            AddFileEntry(archive, CollectionEntryName, tempDb);
            AddTextEntry(archive, MediaEntryName, "{}");
        } finally {
            // 临时库无论如何都要清掉;清不掉也只是残留一个随机名文件,不该影响导出结果。
            TryDelete(tempDb);
        }
    }

    /// <summary>
    /// 写出 <c>collection.anki2</c>:建标准表 / 索引 → 写唯一一行 <c>col</c> → 逐条写 <c>notes</c> 与 <c>cards</c>。
    /// </summary>
    /// <remarks>
    /// 时间口径(均查证自 <c>rslib/src/storage/sqlite.rs</c> 与 genanki):
    /// <list type="bullet">
    ///   <item><c>crt</c>(库创建时间)= **秒**。Anki 自身写的就是秒(<c>TimestampSecs</c>)。</item>
    ///   <item><c>col.mod</c> / <c>col.scm</c> = **毫秒**(<c>TimestampMillis::now()</c>)。</item>
    ///   <item><c>notes.mod</c> / <c>cards.mod</c> / <c>decks.mod</c> / <c>models.mod</c> = **秒**。</item>
    /// </list>
    /// 混用秒 / 毫秒不是笔误,是 Anki 的历史遗留;照抄才能与 Anki 自身产物一致。
    /// </remarks>
    private static void WriteCollectionDatabase(string dbPath, ExportVocabsDocument document,
        IReadOnlyDictionary<string, string> definitionsByWord, DateTimeOffset stamp) {
        var crtSeconds = stamp.ToUnixTimeSeconds();
        var modMillis = stamp.ToUnixTimeMilliseconds();
        var modSeconds = crtSeconds;

        // Pooling=false:默认连接池会让"已归还"的连接继续持有文件句柄,于是下面把库文件读进 ZIP 前
        // 无法确定句柄已释放(Windows 上尤其明显)。这里是一次性的临时库,不值得为它承担这个不确定性。
        var connectionString = new SqliteConnectionStringBuilder {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
        }.ToString();

        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        Execute(connection, SchemaSql);
        InsertCollectionRow(connection, crtSeconds, modMillis, modSeconds);
        InsertNotesAndCards(connection, document, definitionsByWord, modSeconds, modMillis);
    }

    /// <summary>
    /// 标准 schema(表 + 索引),逐字对应 <c>rslib/src/storage/schema11.sql</c>。
    /// </summary>
    /// <remarks>
    /// <b>不要"顺手精简":</b>表少一张、索引少一个,Anki 打开时会报
    /// "database disk image is malformed" 或直接拒绝导入;而 <c>sfld</c> 刻意声明成 <c>integer</c>
    /// (Anki 注释说明:这样纯数字的首字段会按数值排序)—— 改成 <c>text</c> 不报错,但排序语义就变了。
    /// </remarks>
    private const string SchemaSql = """
        CREATE TABLE col (
            id integer PRIMARY KEY,
            crt integer NOT NULL,
            mod integer NOT NULL,
            scm integer NOT NULL,
            ver integer NOT NULL,
            dty integer NOT NULL,
            usn integer NOT NULL,
            ls integer NOT NULL,
            conf text NOT NULL,
            models text NOT NULL,
            decks text NOT NULL,
            dconf text NOT NULL,
            tags text NOT NULL
        );
        CREATE TABLE notes (
            id integer PRIMARY KEY,
            guid text NOT NULL,
            mid integer NOT NULL,
            mod integer NOT NULL,
            usn integer NOT NULL,
            tags text NOT NULL,
            flds text NOT NULL,
            sfld integer NOT NULL,
            csum integer NOT NULL,
            flags integer NOT NULL,
            data text NOT NULL
        );
        CREATE TABLE cards (
            id integer PRIMARY KEY,
            nid integer NOT NULL,
            did integer NOT NULL,
            ord integer NOT NULL,
            mod integer NOT NULL,
            usn integer NOT NULL,
            type integer NOT NULL,
            queue integer NOT NULL,
            due integer NOT NULL,
            ivl integer NOT NULL,
            factor integer NOT NULL,
            reps integer NOT NULL,
            lapses integer NOT NULL,
            left integer NOT NULL,
            odue integer NOT NULL,
            odid integer NOT NULL,
            flags integer NOT NULL,
            data text NOT NULL
        );
        CREATE TABLE revlog (
            id integer PRIMARY KEY,
            cid integer NOT NULL,
            usn integer NOT NULL,
            ease integer NOT NULL,
            ivl integer NOT NULL,
            lastIvl integer NOT NULL,
            factor integer NOT NULL,
            time integer NOT NULL,
            type integer NOT NULL
        );
        CREATE TABLE graves (
            usn integer NOT NULL,
            oid integer NOT NULL,
            type integer NOT NULL
        );
        CREATE INDEX ix_notes_usn ON notes (usn);
        CREATE INDEX ix_cards_usn ON cards (usn);
        CREATE INDEX ix_revlog_usn ON revlog (usn);
        CREATE INDEX ix_cards_nid ON cards (nid);
        CREATE INDEX ix_cards_sched ON cards (did, queue, due);
        CREATE INDEX ix_revlog_cid ON revlog (cid);
        CREATE INDEX ix_notes_csum ON notes (csum);
        """;

    /// <summary>
    /// 写唯一一行 <c>col</c>。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>必须有且只有一行</b> —— Anki 用 <c>select … from col</c>(单值查询)读它,多一行会拿到不确定的行。
    /// <c>id</c> 用 <c>1</c>(Anki 的既定值)。
    /// </para>
    /// <para>
    /// <c>conf</c> / <c>dconf</c> / <c>tags</c> / <c>models</c> / <c>decks</c> **都不能留空 JSON**
    /// (<c>{}</c> 或 <c>[]</c>):Anki 解析后会拿不到必需的键而报错或导入成空白卡片。故
    /// <c>conf</c> 用 Anki 的真实默认值(见 <c>schema11_config_as_string</c>),
    /// <c>dconf</c> / <c>models</c> / <c>decks</c> 填我们自己的定义,<c>tags</c> 是"标签 → usn"的映射
    /// (无标签时是合法的空对象 <c>{}</c> —— 这是唯一一个空对象合法的)。
    /// </para>
    /// </remarks>
    private static void InsertCollectionRow(SqliteConnection connection, long crtSeconds, long modMillis, long modSeconds) {
        using var command = connection.CreateCommand();
        command.CommandText =
            "INSERT INTO col (id, crt, mod, scm, ver, dty, usn, ls, conf, models, decks, dconf, tags) " +
            "VALUES ($id, $crt, $mod, $scm, $ver, 0, 0, 0, $conf, $models, $decks, $dconf, $tags);";
        command.Parameters.AddWithValue("$id", 1);
        command.Parameters.AddWithValue("$crt", crtSeconds);
        command.Parameters.AddWithValue("$mod", modMillis);
        command.Parameters.AddWithValue("$scm", modMillis);
        command.Parameters.AddWithValue("$ver", SchemaVersion);
        // 用参数绑定而不是拼字符串:models/decks 的 JSON 里含 {{字段}} 模板与 CSS,
        // 直接拼接一旦遇到单引号就会破坏 SQL(Anki 自己写库时同样是对 JSON 字符串做转义)。
        command.Parameters.AddWithValue("$conf", BuildConfJson());
        command.Parameters.AddWithValue("$models", BuildModelsJson(modSeconds));
        command.Parameters.AddWithValue("$decks", BuildDecksJson(modSeconds));
        command.Parameters.AddWithValue("$dconf", BuildDconfJson());
        command.Parameters.AddWithValue("$tags", "{}");
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// 逐条写 <c>notes</c> 与其对应的 <c>cards</c>。
    /// </summary>
    /// <remarks>
    /// 一条生词 = 一个 note + 一张 card(单模板)。note id / card id 用**导出的毫秒时刻**起算的
    /// 递增序列(与 genanki 一致):这样不同批次的导出不会复用同一批 id,降低与目标库里既有对象撞号的概率。
    /// 真正的"重复导入去重"靠 <c>guid</c>(见 <see cref="BuildGuid"/>),不靠 id。
    /// </remarks>
    private static void InsertNotesAndCards(SqliteConnection connection, ExportVocabsDocument document,
        IReadOnlyDictionary<string, string> definitionsByWord, long modSeconds, long modMillis) {
        using var noteCommand = connection.CreateCommand();
        noteCommand.CommandText =
            "INSERT INTO notes (id, guid, mid, mod, usn, tags, flds, sfld, csum, flags, data) " +
            "VALUES ($id, $guid, $mid, $mod, -1, $tags, $flds, $sfld, $csum, 0, '');";
        var noteId = noteCommand.Parameters.Add("$id", SqliteType.Integer);
        var guid = noteCommand.Parameters.Add("$guid", SqliteType.Text);
        var tags = noteCommand.Parameters.Add("$tags", SqliteType.Text);
        var flds = noteCommand.Parameters.Add("$flds", SqliteType.Text);
        var sfld = noteCommand.Parameters.Add("$sfld", SqliteType.Text);
        var csum = noteCommand.Parameters.Add("$csum", SqliteType.Integer);
        noteCommand.Parameters.AddWithValue("$mid", NotetypeId);
        noteCommand.Parameters.AddWithValue("$mod", modSeconds);

        using var cardCommand = connection.CreateCommand();
        cardCommand.CommandText =
            "INSERT INTO cards (id, nid, did, ord, mod, usn, type, queue, due, ivl, factor, reps, lapses, left, odue, odid, flags, data) " +
            "VALUES ($id, $nid, $did, 0, $mod, -1, 0, 0, $due, 0, 0, 0, 0, 0, 0, 0, 0, '');";
        var cardId = cardCommand.Parameters.Add("$id", SqliteType.Integer);
        var cardNid = cardCommand.Parameters.Add("$nid", SqliteType.Integer);
        var cardDue = cardCommand.Parameters.Add("$due", SqliteType.Integer);
        cardCommand.Parameters.AddWithValue("$did", DeckId);
        cardCommand.Parameters.AddWithValue("$mod", modSeconds);

        // 单条导出走一个事务:几万条生词逐条自动提交会慢到不可接受(每次都要 fsync)。
        using var transaction = connection.BeginTransaction();
        noteCommand.Transaction = transaction;
        cardCommand.Transaction = transaction;

        var nextId = modMillis;
        var position = 0;
        foreach (var word in document.Words) {
            var definition = definitionsByWord.TryGetValue(word.Word, out var text) ? text : string.Empty;
            var source = BuildSource(word.Book, word.Author);
            var fields = new[] { word.Word, definition, word.Stem, word.Usage, source };

            var noteIdentifier = nextId++;
            noteId.Value = noteIdentifier;
            guid.Value = BuildGuid(word);
            tags.Value = BuildTags(word.Book);
            flds.Value = string.Join(FieldSeparator, fields);
            // sfld = 首个字段(排序用);csum = 首字段的数字校验和(Anki 的 field_checksum)。
            sfld.Value = word.Word;
            csum.Value = FieldChecksum(word.Word);
            noteCommand.ExecuteNonQuery();

            // due:新卡(queue=0)的 due 是**位置**(1 起算的正整数),不是日期 —— Anki 用它在
            // 牌组内排序新卡。genanki 默认给 0,这里给递增位置,让导入后卡片保持导出顺序。
            position++;
            cardId.Value = nextId++;
            cardNid.Value = noteIdentifier;
            cardDue.Value = position;
            cardCommand.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    /// <summary>
    /// 拼装 <c>models</c>(note type 定义)的 JSON。结构对应 <c>rslib/src/notetype/schema11.rs</c>。
    /// </summary>
    /// <remarks>
    /// <c>req</c> = <c>[[cardOrd, "any", [fieldOrds]]]</c>,声明"生成卡片所必需的字段"。
    /// 我们的正面只引用 <c>Word</c>(ordinal 0),故 <c>[[0, "any", [0]]]</c> —— 词非空即出卡。
    /// 这一项若写错(如 <c>[]</c>),Anki 会认为该模板不需要任何字段,可能不生成卡片。
    /// </remarks>
    private static string BuildModelsJson(long modSeconds) {
        var fields = new JsonArray();
        for (var i = 0; i < FieldNames.Length; i++) {
            fields.Add(new JsonObject {
                ["name"] = FieldNames[i],
                ["ord"] = i,
                ["sticky"] = false,
                ["rtl"] = false,
                ["font"] = "Arial",
                ["size"] = 20,
                ["media"] = new JsonArray(),
            });
        }

        var template = new JsonObject {
            ["name"] = "Card 1",
            ["ord"] = 0,
            ["qfmt"] = FrontTemplate,
            ["afmt"] = BackTemplate,
            ["bqfmt"] = "",
            ["bafmt"] = "",
            ["did"] = null,
            ["bfont"] = "",
            ["bsize"] = 0,
        };

        var model = new JsonObject {
            ["id"] = NotetypeId,
            ["name"] = DeckName,
            ["type"] = 0,
            ["mod"] = modSeconds,
            ["usn"] = -1,
            ["sortf"] = 0,
            ["did"] = DeckId,
            ["tmpls"] = new JsonArray { template },
            ["flds"] = fields,
            ["css"] = Css,
            ["latexPre"] = LatexPre,
            ["latexPost"] = LatexPost,
            ["latexsvg"] = false,
            ["req"] = new JsonArray { new JsonArray { 0, "any", new JsonArray { 0 } } },
            ["tags"] = new JsonArray(),
            ["vers"] = new JsonArray(),
        };

        var models = new JsonObject {
            [NotetypeId.ToString(CultureInfo.InvariantCulture)] = model,
        };
        return models.ToJsonString(JsonOptions);
    }

    /// <summary>
    /// 拼装 <c>decks</c>(牌组定义)的 JSON。结构对应 <c>rslib/src/decks/schema11.rs</c>。
    /// </summary>
    /// <remarks>
    /// 只放**一个**牌组 <see cref="DeckName"/>:来源书走 tag,不做"一本书一个牌组"。
    /// <c>conf</c> 指向 <see cref="DeckConfigId"/>(与 <c>dconf</c> 里的键一致),
    /// 而**不是** Anki 内建的 <c>1</c> —— 后者会让导入时覆盖用户既有的「Default」预设。
    /// </remarks>
    private static string BuildDecksJson(long modSeconds) {
        var deck = new JsonObject {
            ["id"] = DeckId,
            ["mod"] = modSeconds,
            ["name"] = DeckName,
            ["usn"] = -1,
            // 四个 today 计数是 [day, amount] 元组(schema-11 用 tuple 序列化),不是对象。
            ["lrnToday"] = new JsonArray { 0, 0 },
            ["revToday"] = new JsonArray { 0, 0 },
            ["newToday"] = new JsonArray { 0, 0 },
            ["timeToday"] = new JsonArray { 0, 0 },
            ["collapsed"] = false,
            ["browserCollapsed"] = false,
            ["desc"] = "",
            ["dyn"] = 0,
            ["conf"] = DeckConfigId,
            ["extendNew"] = 10,
            ["extendRev"] = 50,
        };

        var decks = new JsonObject {
            [DeckId.ToString(CultureInfo.InvariantCulture)] = deck,
        };
        return decks.ToJsonString(JsonOptions);
    }

    /// <summary>
    /// 拼装 <c>dconf</c>(牌组预设)的 JSON。
    /// </summary>
    /// <remarks>
    /// 用 Anki 内建预设的那套默认值(与 genanki 一致)。<b>不能留空</b>:空对象会让 Anki
    /// 在解析牌组预设时拿不到 <c>new</c> / <c>rev</c> / <c>lapse</c> 而报错。
    /// </remarks>
    private static string BuildDconfJson() {
        var config = new JsonObject {
            ["id"] = DeckConfigId,
            ["name"] = "Default",
            ["mod"] = 0,
            ["usn"] = 0,
            ["maxTaken"] = 60,
            ["autoplay"] = true,
            ["timer"] = 0,
            ["replayq"] = true,
            ["new"] = new JsonObject {
                ["delays"] = new JsonArray { 1, 10 },
                ["ints"] = new JsonArray { 1, 4, 7 },
                ["initialFactor"] = 2500,
                ["separate"] = true,
                ["order"] = 1,
                ["perDay"] = 20,
                ["bury"] = true,
            },
            ["rev"] = new JsonObject {
                ["perDay"] = 100,
                ["ease4"] = 1.3,
                ["ivlFct"] = 1,
                ["maxIvl"] = 36500,
                ["bury"] = true,
                ["minSpace"] = 1,
                ["fuzz"] = 0.05,
            },
            ["lapse"] = new JsonObject {
                ["delays"] = new JsonArray { 10 },
                ["mult"] = 0,
                ["minInt"] = 1,
                ["leechFails"] = 8,
                ["leechAction"] = 0,
            },
        };

        var dconf = new JsonObject {
            [DeckConfigId.ToString(CultureInfo.InvariantCulture)] = config,
        };
        return dconf.ToJsonString(JsonOptions);
    }

    /// <summary>
    /// 拼装 <c>col.conf</c>(集合配置)的 JSON,取值即 Anki 的默认
    /// (见 <c>rslib/src/config/schema11.rs</c> 的 <c>schema11_config_as_string</c>)。
    /// </summary>
    /// <remarks>
    /// <c>activeDecks</c> / <c>curDeck</c> 指向**我们自己的牌组**(而不是 Anki 内建的 <c>1</c>),
    /// 免得这份配置引用一个包里并不存在的牌组。<c>creationOffset</c> 留 <c>null</c>:
    /// 它是"创建时的时区偏移",由**导入方**按本地时区补齐更合适(Anki 服务端创建的库同样是 null)。
    /// </remarks>
    private static string BuildConfJson() {
        var conf = new JsonObject {
            ["activeDecks"] = new JsonArray { DeckId },
            ["curDeck"] = DeckId,
            ["newSpread"] = 0,
            ["collapseTime"] = 1200,
            ["timeLim"] = 0,
            ["estTimes"] = true,
            ["dueCounts"] = true,
            ["curModel"] = null,
            ["nextPos"] = 1,
            ["sortType"] = "noteFld",
            ["sortBackwards"] = false,
            ["addToCur"] = true,
            ["dayLearnFirst"] = false,
            ["schedVer"] = 2,
            ["creationOffset"] = null,
            ["sched2021"] = true,
        };
        return conf.ToJsonString(JsonOptions);
    }

    /// <summary>
    /// 从生词派生**确定性**的 note guid —— 同一份输入导出两次,guid 逐字相同,于是重复导入不会产生重复卡片。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 编码方式与 Anki 自身一致:SHA-256 取前 8 字节当大端 64 位整数,再 base91 编码
    /// (与 genanki 的 <c>guid_for</c> 同款,产物形态与 Anki 的 <c>guid64</c> 相同)。
    /// </para>
    /// <para>
    /// <b>为什么输入是「WordKey + 时间戳」而不是单一个 WordKey:</b>
    /// <c>lookups</c> 表的唯一约束是 <c>(word_key, timestamp)</c>(见 <c>DatabaseHelper</c>)——
    /// 同一个词可以在不同书 / 不同时间被查多次,是**多行**。只用 WordKey 派生会让这些行撞成同一个
    /// guid,Anki 导入时把它们当成同一条 note,note 数就少于输入生词数了。带上时间戳后与库里的
    /// 自然键一一对应,既唯一又确定。刻意**不**掺入 Definition / Stem:那两个字段会随"是否联网查释义"
    /// 或词表变化而变,掺进去会让同一份库在不同导出参数下得到不同 guid,重复导入就防不住了。
    /// </para>
    /// </remarks>
    internal static string BuildGuid(ExportWord word) {
        ArgumentNullException.ThrowIfNull(word);

        var wordKey = word.Language.Length > 0 ? word.Language + ":" + word.Word : word.Word;
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(wordKey + FieldSeparator + word.Timestamp));

        ulong value = 0;
        for (var i = 0; i < 8; i++) {
            value = (value << 8) | hash[i];
        }

        // base91 编码(低位先出,再反转)—— 与 Anki / genanki 的 guid 形态一致。
        var builder = new StringBuilder(11);
        while (value > 0) {
            builder.Append(Base91Alphabet[(int)(value % (ulong)Base91Alphabet.Length)]);
            value /= (ulong)Base91Alphabet.Length;
        }

        if (builder.Length == 0) {
            // 理论不可达(SHA-256 前 8 字节全 0);留一个非空 guid 以防万一,空 guid 会被 Anki 拒绝。
            return "0";
        }

        var chars = builder.ToString().ToCharArray();
        Array.Reverse(chars);
        return new string(chars);
    }

    /// <summary>
    /// 拼装 <c>notes.tags</c>:Anki 的格式是**空格分隔、整体用空格包起来**(<c>" tag1 tag2 "</c>),
    /// 无标签时是空串。
    /// </summary>
    /// <remarks>
    /// Anki 的 tag 不能含空格(空格是分隔符)。书名常带空格,故必须先把空白换成 <c>_</c>,
    /// 否则「The Great Gatsby」会被拆成三个 tag。空书名则不加 tag。
    /// </remarks>
    internal static string BuildTags(string? book) {
        var tag = SanitizeTag(book);
        return tag.Length == 0 ? string.Empty : " " + tag + " ";
    }

    /// <summary>
    /// 把书名净化成**单个**合法的 Anki tag:空白折叠成 <c>_</c>,并去掉 Anki 视为非法的字符。
    /// </summary>
    /// <remarks>
    /// Anki 的非法 tag 字符(见其 tag 规范化逻辑):<c>" # + * / : &lt; &gt; = ? ^ ` { | } ~</c>。
    /// 这里把它们一并替换成 <c>_</c>,让 tag 在导入后保持为**一个**标签而不是被拆开或被丢弃。
    /// </remarks>
    internal static string SanitizeTag(string? book) {
        if (string.IsNullOrWhiteSpace(book)) {
            return string.Empty;
        }

        var builder = new StringBuilder(book.Length);
        var lastWasSeparator = false;
        foreach (var c in book.Trim()) {
            var isSeparator = char.IsWhiteSpace(c) || c is '"' or '#' or '+' or '*' or '/' or ':' or '<' or '>'
                or '=' or '?' or '^' or '`' or '{' or '|' or '}' or '~';
            if (isSeparator) {
                if (!lastWasSeparator) {
                    builder.Append('_');
                    lastWasSeparator = true;
                }
                continue;
            }
            builder.Append(c);
            lastWasSeparator = false;
        }

        return builder.ToString().Trim('_');
    }

    /// <summary>
    /// Anki 的 <c>field_checksum</c>(<c>rslib/src/notes/mod.rs</c>):SHA-1 摘要的**前 4 字节**
    /// 按大端解释成 <c>u32</c>。
    /// </summary>
    /// <remarks>
    /// 用于 <c>notes.csum</c>。Anki 只在**库内**用它对首字段做去重检测(<c>ix_notes_csum</c>),
    /// 导入时不强制正确(genanki 干脆写 0),但写对能让"导入后库内的重复检测"照常工作。
    /// Anki 官方单测锚定:<c>field_checksum("test") == 2840236005</c>、<c>field_checksum("今日") == 1464653051</c>,
    /// 本实现的用例照抄了这两个值。
    /// <para>
    /// 简化:Anki 会先 <c>strip_html_preserving_media_filenames</c> 再算,而我们的首字段是纯词形、
    /// 不含 HTML / 媒体引用,故直接对原文求校验和。
    /// </para>
    /// </remarks>
    internal static long FieldChecksum(string text) {
        ArgumentNullException.ThrowIfNull(text);
        var digest = SHA1.HashData(Encoding.UTF8.GetBytes(text));
        return (long)((uint)((digest[0] << 24) | (digest[1] << 16) | (digest[2] << 8) | digest[3]));
    }

    /// <summary>来源字段 = 书名 + 作者;两者都有时用 <c> · </c> 连接,都空则为空串。</summary>
    private static string BuildSource(string book, string author) {
        var trimmedBook = (book ?? string.Empty).Trim();
        var trimmedAuthor = (author ?? string.Empty).Trim();
        if (trimmedBook.Length == 0) {
            return trimmedAuthor;
        }
        return trimmedAuthor.Length == 0 ? trimmedBook : trimmedBook + " · " + trimmedAuthor;
    }

    /// <summary>把 <paramref name="sourcePath"/> 作为 ZIP 条目 <paramref name="entryName"/> 写入。</summary>
    private static void AddFileEntry(ZipArchive archive, string entryName, string sourcePath) {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using var target = entry.Open();
        using var source = File.OpenRead(sourcePath);
        source.CopyTo(target);
    }

    /// <summary>把一段文本作为 ZIP 条目写入(UTF-8,无 BOM —— <c>media</c> 是 JSON,解析器不吃 BOM)。</summary>
    private static void AddTextEntry(ZipArchive archive, string entryName, string content) {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(content);
    }

    private static void Execute(SqliteConnection connection, string sql) {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static void TryDelete(string path) {
        try {
            if (File.Exists(path)) {
                File.Delete(path);
            }
        } catch { /* 清理是尽最大努力,不该影响导出结果 */ }
    }
}
