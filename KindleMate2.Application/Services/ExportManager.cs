using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using KindleMate2.Application.Services.KM2DB;
using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Infrastructure.Helpers;
using KindleMate2.Shared;
using KindleMate2.Shared.Constants;
using KindleMate2.Shared.Diagnostics;

namespace KindleMate2.Application.Services;

/// <summary>
/// Manages data export operations: Markdown export and sync back to Kindle device.
/// </summary>
public class ExportManager : IExportManager {
    private readonly IClippingService _clippingService;
    private readonly ILookupService _lookupService;
    private readonly IVocabService _vocabService;
    private readonly IOriginalClippingLineService _originalClippingLineService;
    private readonly IDeviceManager _deviceManager;
    private readonly string _programPath;
    private readonly string _backupPath;
    private readonly string _tempPath;

    public ExportManager(IClippingService clippingService, ILookupService lookupService,
        IVocabService vocabService, IOriginalClippingLineService originalClippingLineService,
        IDeviceManager deviceManager, string programPath, string backupPath, string tempPath) {
        _clippingService = clippingService;
        _lookupService = lookupService;
        _vocabService = vocabService;
        _originalClippingLineService = originalClippingLineService;
        _deviceManager = deviceManager;
        _programPath = programPath;
        _backupPath = backupPath;
        _tempPath = tempPath;
    }

    /// <summary>
    /// Exports clippings to Markdown format.
    /// </summary>
    public bool ExportClippingsToMarkdown(string bookName = "") {
        try {
            return _clippingService.ClippingsToMarkdown(Path.Combine(_programPath, AppConstants.ExportsPathName), bookName);
        } catch (Exception ex) {
            AppLog.Write($"[ClippingsToMarkdown] {ex}");
            return false;
        }
    }

    /// <summary>
    /// Exports vocabulary/lookups to Markdown format.
    /// </summary>
    public bool ExportVocabsToMarkdown(string word = "") {
        try {
            return _lookupService.LookupsToMarkdown(Path.Combine(_programPath, AppConstants.ExportsPathName), word);
        } catch (Exception ex) {
            AppLog.Write($"[VocabsToMarkdown] {ex}");
            return false;
        }
    }

    /// <summary>导出标注为 CSV(Anki 可导入)。</summary>
    public bool ExportClippingsToCsv() {
        try {
            var dir = Path.Combine(_programPath, AppConstants.ExportsPathName);
            Directory.CreateDirectory(dir);
            WriteClippingsCsv(_clippingService.GetAllClippings(), Path.Combine(dir, "Clippings.csv"));
            return true;
        } catch (Exception ex) {
            AppLog.Write($"[ClippingsToCsv] {ex}");
            return false;
        }
    }

    /// <summary>
    /// 导出生词 CSV。<paramref name="includeDefinitions"/> 为 true 时**联网**按词查释义(有道),
    /// 查不到则 Definition 留空;为 false 时完全离线,Definition 列留空。
    /// 联网会**把全部生词发给第三方**,故默认关,由调用方按用户选择显式打开。
    /// </summary>
    public async Task<bool> ExportVocabsToCsvAsync(bool includeDefinitions,
        CancellationToken cancellationToken = default) {
        try {
            var dir = Path.Combine(_programPath, AppConstants.ExportsPathName);
            Directory.CreateDirectory(dir);

            var stemByKey = _vocabService.GetAllVocabs()
                .Where(v => !string.IsNullOrEmpty(v.WordKey))
                .GroupBy(v => v.WordKey!, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First().Stem ?? string.Empty, StringComparer.Ordinal);

            var lookups = _lookupService.GetAllLookups();
            foreach (var lookup in lookups) {
                if (lookup.WordKey != null && stemByKey.TryGetValue(lookup.WordKey, out var stem)) {
                    lookup.Stem = stem;
                }
            }

            var definitions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (includeDefinitions) {
                var words = lookups
                    .Select(l => l.Word)
                    .Where(w => !string.IsNullOrWhiteSpace(w))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                definitions = await LookupDefinitionsAsync(words, cancellationToken).ConfigureAwait(false);
            }

            WriteLookupsCsv(lookups, Path.Combine(dir, "Vocabs.csv"), definitions);
            return true;
        } catch (Exception ex) {
            AppLog.Write($"[VocabsToCsv] {ex}");
            return false;
        }
    }

    /// <summary>
    /// 导出标注为 JSON(<c>Clippings.json</c>,按书名分组)。
    /// </summary>
    /// <remarks>
    /// 与 CSV 并列存在而不是替换它:CSV 是给 Anki 这类"表格导入"用的,JSON 是给脚本 /
    /// Obsidian / 将来的服务集成用的,两者消费方式不同,共享的只是上游那份中间模型。
    /// </remarks>
    public bool ExportClippingsToJson() {
        try {
            var dir = Path.Combine(_programPath, AppConstants.ExportsPathName);
            Directory.CreateDirectory(dir);
            WriteClippingsJson(_clippingService.GetAllClippings(),
                Path.Combine(dir, "Clippings" + FileExtension.JSON));
            return true;
        } catch (Exception ex) {
            AppLog.Write($"[ClippingsToJson] {ex}");
            return false;
        }
    }

    /// <summary>
    /// 导出生词为 JSON(<c>Vocabs.json</c>)。完全**离线**:不像 CSV 那条路径提供联网查释义,
    /// 释义既不在 schema 里、也没有可靠的离线词源,宁可不给,也不给一份来源不明的。
    /// </summary>
    public bool ExportVocabsToJson() {
        try {
            var dir = Path.Combine(_programPath, AppConstants.ExportsPathName);
            Directory.CreateDirectory(dir);

            var lookups = _lookupService.GetAllLookups();
            FillStemsFromVocabs(lookups);
            WriteLookupsJson(lookups, Path.Combine(dir, "Vocabs" + FileExtension.JSON));
            return true;
        } catch (Exception ex) {
            AppLog.Write($"[VocabsToJson] {ex}");
            return false;
        }
    }

    /// <summary>
    /// 用 Vocab 表(<c>vocab.db</c> 的 WORDS)里的 stem 补齐 lookups 缺失的词形。
    /// </summary>
    /// <remarks>
    /// <c>LOOKUPS</c> 表多数行的 stem 是空的,词形只存在于 WORDS 表;CSV 导出
    /// (<see cref="ExportVocabsToCsvAsync"/>)就是这么取的,JSON 对同一份库不该给出另一个答案,
    /// 故沿用同一口径。
    /// <para>
    /// 两处**刻意**的差异,说明如下:一是这里只在 <c>lookup.Stem</c> 为空时才补 —— CSV 那边是
    /// 无条件覆盖(既有行为,本次不改动),于是 WORDS 里 stem 为空时会把 LOOKUPS 自带的非空
    /// stem 抹掉;二是 CSV 那段代码没有被抽出来共用,因为那属于既有导出路径,本次不碰它。
    /// </para>
    /// </remarks>
    private void FillStemsFromVocabs(IEnumerable<Lookup> lookups) {
        var stemByKey = _vocabService.GetAllVocabs()
            .Where(v => !string.IsNullOrEmpty(v.WordKey))
            .GroupBy(v => v.WordKey!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().Stem ?? string.Empty, StringComparer.Ordinal);

        foreach (var lookup in lookups) {
            if (lookup.WordKey == null) {
                continue;
            }
            if (!string.IsNullOrEmpty(lookup.Stem)) {
                continue;
            }
            if (stemByKey.TryGetValue(lookup.WordKey, out var stem)) {
                lookup.Stem = stem;
            }
        }
    }

    private static async Task<Dictionary<string, string>> LookupDefinitionsAsync(
        IReadOnlyList<string> words, CancellationToken cancellationToken) {
        var result = new Dictionary<string, string>(words.Count, StringComparer.OrdinalIgnoreCase);
        if (words.Count == 0) {
            return result;
        }

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("KindleMate2-Dictionary");
        using var gate = new SemaphoreSlim(4);
        var tasks = words.Select(async word => {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try {
                var def = await WordDefinitionService.LookupAsync(word, http, cancellationToken)
                    .ConfigureAwait(false);
                return (word, text: def?.ToDisplayText() ?? string.Empty);
            } finally {
                gate.Release();
            }
        });

        foreach (var (word, text) in await Task.WhenAll(tasks).ConfigureAwait(false)) {
            if (text.Length > 0) {
                result[word] = text;
            }
        }
        return result;
    }

    /// <summary>写标注 CSV。<c>internal</c> 供单测(纯函数,不碰库)。</summary>
    internal static void WriteClippingsCsv(IEnumerable<Clipping> clippings, string filePath) {
        using var writer = new StreamWriter(filePath, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        writer.WriteLine("Content,Type,Book,Author,Page,Location,Date");
        foreach (var clipping in clippings) {
            if (string.IsNullOrWhiteSpace(clipping.Content)) {
                continue;
            }
            var type = clipping.BriefType is { } brief && Enum.IsDefined(typeof(BriefType), (int)brief)
                ? ((BriefType)brief).ToString()
                : string.Empty;
            writer.WriteLine(string.Join(',',
                EscapeCsv(clipping.Content),
                EscapeCsv(type),
                EscapeCsv(clipping.BookName ?? string.Empty),
                EscapeCsv(clipping.AuthorName ?? string.Empty),
                EscapeCsv(clipping.PageNumber?.ToString(CultureInfo.InvariantCulture) ?? string.Empty),
                EscapeCsv(clipping.ClippingTypeLocation ?? string.Empty),
                EscapeCsv(clipping.ClippingDate ?? string.Empty)));
        }
    }

    /// <summary>写生词 CSV。<c>internal</c> 供单测(纯函数,不碰库)。</summary>
    internal static void WriteLookupsCsv(
        IEnumerable<Lookup> lookups,
        string filePath,
        IReadOnlyDictionary<string, string> definitionsByWord) {
        using var writer = new StreamWriter(filePath, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        writer.WriteLine("Word,Stem,Definition,Usage,Book,Author,Language,Timestamp");
        foreach (var lookup in lookups) {
            var word = lookup.Word;
            if (string.IsNullOrWhiteSpace(word)) {
                continue;
            }
            var usage = (lookup.Usage ?? string.Empty)
                .Replace(AppConstants.SpaceForNewLine, Environment.NewLine, StringComparison.Ordinal);
            definitionsByWord.TryGetValue(word, out var definition);
            writer.WriteLine(string.Join(',',
                EscapeCsv(word),
                EscapeCsv(lookup.Stem ?? string.Empty),
                EscapeCsv(definition ?? string.Empty),
                EscapeCsv(usage),
                EscapeCsv(lookup.Title ?? string.Empty),
                EscapeCsv(lookup.Authors ?? string.Empty),
                EscapeCsv(LanguageOfWordKey(lookup.WordKey)),
                EscapeCsv(lookup.Timestamp ?? string.Empty)));
        }
    }

    /// <summary>从 <c>word_key</c> 取语言前缀(<c>en:word</c> → <c>en</c>);无前缀返回空。</summary>
    internal static string LanguageOfWordKey(string? wordKey) {
        if (string.IsNullOrEmpty(wordKey)) {
            return string.Empty;
        }
        var index = wordKey.IndexOf(':');
        return index > 0 ? wordKey[..index] : string.Empty;
    }

    /// <summary>按 RFC4180 转义:含 <c>,</c> <c>"</c> CR LF 时整体加引号并把 <c>"</c> 加倍。</summary>
    internal static string EscapeCsv(string value) {
        if (value.IndexOfAny([',', '"', '\r', '\n']) < 0) {
            return value;
        }
        return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }

    /// <summary>
    /// 取出「**库里仍然存在**」的标注 key 集合,供写回设备/备份时过滤掉回收站条目。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>original_clipping_lines</c> 同时充当回收站:「原始行仍在、但 <c>clippings</c> 里已无该
    /// key」= 已删除(<c>KM2DatabaseService.GetDeletedOriginalLines</c> 用的是同一判据)。
    /// 若导出不过滤这些行,则「删除标注 → 同步到设备」会把它**重新写回设备**,条目复活;
    /// 备份出来的 <c>MyClippings_&lt;时间戳&gt;.txt</c> 里同样会混入已删除的条目。
    /// </para>
    /// <para>
    /// 用「存活 key 白名单」而不是「排除回收站」的写法:前者是**正面**表述,天然也覆盖
    /// 「清理重复 / 清理空条目」删掉的那些行(它们同样是原始行在、clippings 里没有)。
    /// </para>
    /// <para>
    /// 比较器用 <see cref="StringComparer.Ordinal"/> —— 与 <c>KM2DatabaseService</c> 里回收站
    /// 的判据保持同一口径,避免两处对同一个 key 得出不同结论。
    /// </para>
    /// </remarks>
    private HashSet<string> GetLiveClippingKeys() {
        return _clippingService.GetAllClippings()
            .Select(c => c.Key)
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// JSON 序列化选项(UTF-8、缩进、宽松转义)。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 刻意**不设** <c>PropertyNamingPolicy</c>:属性名由 DTO 上的 <c>[JsonPropertyName]</c>
    /// 逐个显式给出。全局命名策略会波及项目里其他走 <see cref="JsonSerializer"/> 的调用点
    /// (那些地方用的是另一套契约),改动面比它省下的几行代码大得多。
    /// </para>
    /// <para>
    /// <c>Encoder</c> 用放宽的那一个:默认编码器会把中文、以及 <c>&lt; &gt; &amp;</c> 一类的
    /// 非 ASCII 字符统统写成 <c>\uXXXX</c>,而本程序导出的主体就是中文标注 —— 那样出来的文件
    /// 是一整片转义序列,人眼没法看,diff 也全无意义。"Unsafe" 说的是**嵌进 HTML** 时的注入
    /// 风险;这里的目标是本地磁盘上的数据文件、由 JSON 解析器读取,不在 HTML 上下文里,故可放宽。
    /// </para>
    /// </remarks>
    private static readonly JsonSerializerOptions JsonOptions = new() {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// 把任意导出中间模型写成 JSON 文件(UTF-8 **无 BOM**)。
    /// </summary>
    /// <remarks>
    /// 走 <see cref="JsonSerializer.SerializeToUtf8Bytes"/> 而不是
    /// <c>new StreamWriter(path, false, new UTF8Encoding(true))</c>:后者是我们 CSV 导出的写法
    /// (带 BOM 是为了让 Excel 认出 UTF-8),但 JSON 解析器读到开头的 BOM 会直接报
    /// "unexpected character"。字节数组直写则天然没有 BOM 这一回事。
    /// </remarks>
    internal static void WriteJson<TDocument>(TDocument document, string filePath) {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(document, JsonOptions);
        File.WriteAllBytes(filePath, bytes);
    }

    /// <summary>写标注 JSON。<c>internal</c> 供单测(纯函数,不碰库)。</summary>
    internal static void WriteClippingsJson(IEnumerable<Clipping> clippings, string filePath) {
        WriteJson(ExportModelBuilder.BuildClippingsDocument(clippings), filePath);
    }

    /// <summary>写生词 JSON。<c>internal</c> 供单测(纯函数,不碰库)。</summary>
    internal static void WriteLookupsJson(IEnumerable<Lookup> lookups, string filePath) {
        WriteJson(ExportModelBuilder.BuildVocabsDocument(lookups), filePath);
    }

    /// <summary>
    /// 把原始标注行导出到 Backups 目录，作为数据库备份之外的一份可读副本。
    /// </summary>
    /// <remarks>
    /// 文件名此前误用了 <see cref="AppConstants.DatabaseFileName"/>（"KM2.dat"），于是
    /// Backups 里会出现一个名为 KM2.dat 的**纯文本**文件：用 SQLite 打开报
    /// "file is not a database"，而且极易与真正的库备份（KM2_backup_&lt;时间戳&gt;.dat）
    /// 混淆 —— 用户很可能把它当数据库备份去恢复，然后打不开。此外它是固定名，每次
    /// 备份都覆盖上一份。
    /// 现改为与 <see cref="SyncToKindle"/> 一致的命名：MyClippings_&lt;时间戳&gt;.txt，
    /// 既表明这是标注文本而非数据库，也不再互相覆盖。
    /// <para>
    /// 导出内容 = **库里仍然存在的标注**(<see cref="GetLiveClippingKeys"/>),不含回收站里
    /// 那些已删除的条目 —— 备份不该把用户删掉的东西又带回来。
    /// </para>
    /// </remarks>
    public bool BackupClippings(out Exception? exception) {
        var fileName = "MyClippings_" + DateTimeHelper.GetCurrentTimestamp() + FileExtension.TXT;
        return _originalClippingLineService.Export(_backupPath, fileName, out exception, GetLiveClippingKeys());
    }

    /// <summary>
    /// Exports original clippings to a specific path.
    /// </summary>
    /// <remarks>
    /// 与 <see cref="BackupClippings"/> 同一语义:只导出**库里仍然存在的标注**,
    /// 不含已删除(回收站)的条目。
    /// </remarks>
    public bool ExportOriginalClippings(string path, string fileName, out Exception? exception) {
        return _originalClippingLineService.Export(path, fileName, out exception, GetLiveClippingKeys());
    }

    /// <summary>
    /// Syncs clippings back to the connected Kindle device.
    /// </summary>
    /// <remarks>
    /// 写回的必须是**库里仍然存在的标注**:这里传 <see cref="GetLiveClippingKeys"/> 过滤,
    /// 否则已删除的条目会被写回设备、在设备上"复活"。
    /// </remarks>
    public void SyncToKindle() {
        var backupClippingsPath = Path.Combine(_backupPath, "MyClippings_" + DateTimeHelper.GetCurrentTimestamp() + FileExtension.TXT);
        var backupWordsPath = Path.Combine(_backupPath, "vocab_" + DateTimeHelper.GetCurrentTimestamp() + FileExtension.DB);

        if (!Directory.Exists(_backupPath)) {
            Directory.CreateDirectory(_backupPath);
        }

        if (!_deviceManager.ImportFilesFromDevice(backupClippingsPath, backupWordsPath, out Exception? exception) ||
            !_originalClippingLineService.Export(_tempPath, AppConstants.ClippingsFileName, out exception, GetLiveClippingKeys())) {
            throw exception!;
        }

        var exportedClippingsPath = Path.Combine(_tempPath, AppConstants.ClippingsFileName);
        _deviceManager.SyncFileToDevice(exportedClippingsPath, AppConstants.ClippingsFileName);
    }

    /// <summary>
    /// Backs up the database file.
    /// </summary>
    public void BackupDatabase() {
        DatabaseHelper.BackupDatabase(_programPath, _backupPath, AppConstants.DatabaseFileName);
    }
}
