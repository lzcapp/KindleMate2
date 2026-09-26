using System.Globalization;
using System.Net.Http;
using System.Text;
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
    /// 导出生词 CSV。按词联网查释义(有道);查不到则 Definition 留空。
    /// </summary>
    public async Task<bool> ExportVocabsToCsvAsync(CancellationToken cancellationToken = default) {
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

            var words = lookups
                .Select(l => l.Word)
                .Where(w => !string.IsNullOrWhiteSpace(w))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var definitions = await LookupDefinitionsAsync(words, cancellationToken).ConfigureAwait(false);
            WriteLookupsCsv(lookups, Path.Combine(dir, "Vocabs.csv"), definitions);
            return true;
        } catch (Exception ex) {
            AppLog.Write($"[VocabsToCsv] {ex}");
            return false;
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

    private static void WriteClippingsCsv(IEnumerable<Clipping> clippings, string filePath) {
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

    private static void WriteLookupsCsv(
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

    private static string LanguageOfWordKey(string? wordKey) {
        if (string.IsNullOrEmpty(wordKey)) {
            return string.Empty;
        }
        var index = wordKey.IndexOf(':');
        return index > 0 ? wordKey[..index] : string.Empty;
    }

    private static string EscapeCsv(string value) {
        if (value.IndexOfAny([',', '"', '\r', '\n']) < 0) {
            return value;
        }
        return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
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
    /// </remarks>
    public bool BackupClippings(out Exception? exception) {
        var fileName = "MyClippings_" + DateTimeHelper.GetCurrentTimestamp() + FileExtension.TXT;
        return _originalClippingLineService.Export(_backupPath, fileName, out exception);
    }

    /// <summary>
    /// Exports original clippings to a specific path.
    /// </summary>
    public bool ExportOriginalClippings(string path, string fileName, out Exception? exception) {
        return _originalClippingLineService.Export(path, fileName, out exception);
    }

    /// <summary>
    /// Syncs clippings back to the connected Kindle device.
    /// </summary>
    public void SyncToKindle() {
        var backupClippingsPath = Path.Combine(_backupPath, "MyClippings_" + DateTimeHelper.GetCurrentTimestamp() + FileExtension.TXT);
        var backupWordsPath = Path.Combine(_backupPath, "vocab_" + DateTimeHelper.GetCurrentTimestamp() + FileExtension.DB);

        if (!Directory.Exists(_backupPath)) {
            Directory.CreateDirectory(_backupPath);
        }

        if (!_deviceManager.ImportFilesFromDevice(backupClippingsPath, backupWordsPath, out Exception? exception) ||
            !_originalClippingLineService.Export(_tempPath, AppConstants.ClippingsFileName, out exception)) {
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
