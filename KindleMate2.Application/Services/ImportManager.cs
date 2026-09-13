using KindleMate2.Application.Services.KM2DB;
using KindleMate2.Infrastructure.Helpers;
using KindleMate2.Application.Models;
using KindleMate2.Shared;
using KindleMate2.Shared.Constants;

namespace KindleMate2.Application.Services;

/// <summary>
/// Centralizes all data import operations: Kindle clippings, Kindle words, and KM database imports.
/// </summary>
public class ImportManager : IImportManager {
    private readonly IKm2DatabaseService _km2DatabaseService;
    private readonly IClippingService _clippingService;
    private readonly IVocabService _vocabService;
    private readonly IOriginalClippingLineService _originalClippingLineService;
    private readonly ILookupService _lookupService;
    private readonly IVocabDatabaseServiceFactory _vocabDatabaseServiceFactory;
    private readonly IKmDatabaseServiceFactory _kmDatabaseServiceFactory;
    private readonly IKmateDatabaseServiceFactory _kmateDatabaseServiceFactory;
    private readonly string _importPath;

    public ImportManager(IKm2DatabaseService km2DatabaseService, IClippingService clippingService, IVocabService vocabService,
        IOriginalClippingLineService originalClippingLineService, ILookupService lookupService,
        IVocabDatabaseServiceFactory vocabDatabaseServiceFactory, IKmDatabaseServiceFactory kmDatabaseServiceFactory,
        IKmateDatabaseServiceFactory kmateDatabaseServiceFactory,
        string importPath) {
        _km2DatabaseService = km2DatabaseService;
        _clippingService = clippingService;
        _vocabService = vocabService;
        _originalClippingLineService = originalClippingLineService;
        _lookupService = lookupService;
        _vocabDatabaseServiceFactory = vocabDatabaseServiceFactory;
        _kmDatabaseServiceFactory = kmDatabaseServiceFactory;
        _kmateDatabaseServiceFactory = kmateDatabaseServiceFactory;
        _importPath = importPath;
    }

    /// <summary>
    /// Imports both Kindle clippings and Kindle words from file paths.
    /// On any failure, throws <see cref="InvalidOperationException"/> with the underlying message —
    /// the UI layer distinguishes success/failure by result presence vs thrown exception.
    /// </summary>
    /// <summary>
    /// 合并导入:标注 + 生词。原版「从设备导入」把文件拉回本地后走的就是这条。
    /// 两者都返回空串即视为失败(与 <c>RunBackgroundTask</c> 的契约一致)。
    /// </summary>
    public string Import(string kindleClippingsPath, string kindleWordsPath,
        IProgress<OperationProgress>? progress = null) {
        // 耗时主要在标注解析,把进度透传下去
        var clippingsResult = ImportKindleClippings(kindleClippingsPath, progress);
        var wordResult = ImportKindleWords(kindleWordsPath);

        if (string.IsNullOrWhiteSpace(clippingsResult) && string.IsNullOrWhiteSpace(wordResult)) {
            return string.Empty;
        }
        if (string.IsNullOrWhiteSpace(clippingsResult)) {
            return wordResult;
        }
        if (string.IsNullOrWhiteSpace(wordResult)) {
            return clippingsResult;
        }
        return clippingsResult + Environment.NewLine + wordResult;
    }

    /// <summary>导入 Kindle 标注(My Clippings.txt)。<paramref name="progress"/> 供界面展示阶段与进度。</summary>
    public string ImportKindleClippings(string clippingsPath, IProgress<OperationProgress>? progress = null) {
        if (!_km2DatabaseService.ImportKindleClippings(clippingsPath, out var result, progress)) {
            var exception = result[AppConstants.Exception];
            throw new InvalidOperationException(exception);
        }
        var parsedCount = result[AppConstants.ParsedCount];
        var insertedCount = result[AppConstants.InsertedCount];
        return Strings.Parsed_X + Strings.Space + parsedCount + Strings.Space + Strings.X_Clippings + Strings.Symbol_Comma +
               Strings.Imported_X + Strings.Space + insertedCount + Strings.Space + Strings.X_Clippings;
    }

    public string ImportKindleWords(string kindleWordsPath) {
        if (!File.Exists(kindleWordsPath)) {
            return string.Empty;
        }

        var vocabDatabaseService = _vocabDatabaseServiceFactory.Create(kindleWordsPath);

        if (!vocabDatabaseService.ImportKindleWords(kindleWordsPath, out var result)) {
            var exception = result[AppConstants.Exception];
            throw new InvalidOperationException(exception);
        }
        var lookupCount = result[AppConstants.LookupCount];
        var insertedLookupCount = result[AppConstants.InsertedLookupCount];
        var insertedVocabCount = result[AppConstants.InsertedVocabCount];
        return Strings.Parsed_X + Strings.Space + lookupCount + Strings.Space + Strings.X_Vocabs + Strings.Space + Strings.Symbol_Comma +
               Strings.Imported_X + Strings.Space + insertedLookupCount + Strings.Space + Strings.X_Lookups + Strings.Space +
               Strings.Symbol_Comma + insertedVocabCount + Strings.Space + Strings.X_Vocabs;
    }

    public string ImportKmDatabase(string filePath) => ImportFlatSchemaDatabase(filePath, "KM_");

    /// <summary>
    /// 导入另一个 <b>Kindle Mate 2</b> 数据库(本程序自己的库格式,即 KM2.dat)。
    /// 用于把别处的一份库(另一台机器 / 旧备份)的标注与生词合并进当前库,按 key 与
    /// 「书名+作者+内容」双重判重,重复的跳过。
    /// </summary>
    /// <remarks>
    /// 它与原版 Kindle Mate 的库是**同一套扁平 schema**(clippings / lookups /
    /// original_clipping_lines / settings / vocab),所以合并逻辑完全复用同一个服务;
    /// 单独留一个入口是为了让用户在菜单上一眼看清"导入的是哪种来源",并把备份文件名
    /// 区分开(KM2_ 前缀),便于事后分辨。
    /// </remarks>
    public string ImportKm2Database(string filePath) => ImportFlatSchemaDatabase(filePath, "KM2_");

    /// <summary>原版 Kindle Mate 与本程序自己的库共用同一套扁平 schema,合并逻辑写在这里一份。</summary>
    private string ImportFlatSchemaDatabase(string filePath, string backupFileNamePrefix) {
        var kmDatabaseService = _kmDatabaseServiceFactory.Create(filePath);

        var clippingsCount = _clippingService.GetCount();
        var vocabCount = _vocabService.GetCount();

        if (File.Exists(filePath)) {
            var backupFilePath = Path.Combine(_importPath, backupFileNamePrefix + DateTimeHelper.GetCurrentTimestamp() + FileExtension.DAT);
            File.Copy(filePath, backupFilePath, true);
        }

        if (!kmDatabaseService.ImportFromKmDatabase()) {
            return string.Empty;
        }

        _km2DatabaseService.CleanDatabase(string.Empty, out _);
        _km2DatabaseService.UpdateFrequency();

        clippingsCount = _clippingService.GetCount() - clippingsCount;
        vocabCount = _vocabService.GetCount() - vocabCount;
        var message = Strings.Parsed_X + Strings.Space + (clippingsCount + vocabCount) + Strings.Space + Strings.X_Records + Strings.Symbol_Comma +
                      Strings.Imported_X + Strings.Space + clippingsCount + Strings.Space + Strings.X_Clippings + Strings.Symbol_Comma +
                      vocabCount + Strings.Space + Strings.X_Vocabs;
        return message;
    }

    public string ImportKmateDatabase(string filePath) {
        var kmateDatabaseService = _kmateDatabaseServiceFactory.Create(filePath);

        var clippingsCount = _clippingService.GetCount();
        var vocabCount = _vocabService.GetCount();

        if (File.Exists(filePath)) {
            var backupFilePath = Path.Combine(_importPath, "KMate_" + DateTimeHelper.GetCurrentTimestamp() + FileExtension.DAT);
            File.Copy(filePath, backupFilePath, true);
        }

        if (!kmateDatabaseService.ImportFromKmateDatabase()) {
            return string.Empty;
        }

        _km2DatabaseService.CleanDatabase(string.Empty, out _);
        _km2DatabaseService.UpdateFrequency();

        clippingsCount = _clippingService.GetCount() - clippingsCount;
        vocabCount = _vocabService.GetCount() - vocabCount;
        var message = Strings.Parsed_X + Strings.Space + (clippingsCount + vocabCount) + Strings.Space + Strings.X_Records + Strings.Symbol_Comma +
                      Strings.Imported_X + Strings.Space + clippingsCount + Strings.Space + Strings.X_Clippings + Strings.Symbol_Comma +
                      vocabCount + Strings.Space + Strings.X_Vocabs;
        return message;
    }
}
