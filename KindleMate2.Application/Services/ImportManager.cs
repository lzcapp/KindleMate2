using KindleMate2.Application.Services.KM2DB;
using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Domain.Interfaces.KM2DB;
using KindleMate2.Infrastructure.Helpers;
using KindleMate2.Application.Models;
using KindleMate2.Shared;
using KindleMate2.Shared.Constants;
using KindleMate2.Shared.Diagnostics;

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
    private readonly IImportBatchRepository _importBatchRepository;
    private readonly string _importPath;

    public ImportManager(IKm2DatabaseService km2DatabaseService, IClippingService clippingService, IVocabService vocabService,
        IOriginalClippingLineService originalClippingLineService, ILookupService lookupService,
        IVocabDatabaseServiceFactory vocabDatabaseServiceFactory, IKmDatabaseServiceFactory kmDatabaseServiceFactory,
        IKmateDatabaseServiceFactory kmateDatabaseServiceFactory,
        IImportBatchRepository importBatchRepository,
        string importPath) {
        _km2DatabaseService = km2DatabaseService;
        _clippingService = clippingService;
        _vocabService = vocabService;
        _originalClippingLineService = originalClippingLineService;
        _lookupService = lookupService;
        _vocabDatabaseServiceFactory = vocabDatabaseServiceFactory;
        _kmDatabaseServiceFactory = kmDatabaseServiceFactory;
        _kmateDatabaseServiceFactory = kmateDatabaseServiceFactory;
        _importBatchRepository = importBatchRepository;
        _importPath = importPath;
    }

    /// <summary>
    /// 在 import_batches 里开一条批次记录,返回其 id(写入各行的 origin_batch 溯源列)。
    /// 计数由 <see cref="CompleteBatch"/> 在导入成功后回填;导入中途失败的批次留在
    /// parsed=inserted=0 —— 这本身就是「尝试过但没进来」的记录,不需要额外状态列。
    /// <para>
    /// 批次是**辅助账本**,写不进去(表缺失 / 库被占 / 磁盘满)时吞掉异常返回 null,
    /// 让主导入照常进行 —— 溯源降级为「无批次」(各行 origin_batch 留 NULL),
    /// 反过来把导入弄挂就本末倒置了(与 <see cref="CompleteBatch"/> 同一口径)。
    /// </para>
    /// </summary>
    private long? BeginBatch(string sourceType, string sourcePath) {
        try {
            return _importBatchRepository.Add(new ImportBatch {
                SourceType = sourceType,
                SourcePath = sourcePath,
                ImportedAt = DateTimeHelper.GetCurrentTimestamp()
            });
        } catch (Exception e) {
            AppLog.Write($"[ImportManager.BeginBatch] 建批次失败,本次导入不记溯源: {e.Message}");
            return null;
        }
    }

    /// <summary>回填批次计数;null(建批次失败)直接跳过。仓储异常只记日志不外抛 —— 溯源是辅助信息,不能反过来弄挂主导入。</summary>
    private void CompleteBatch(long? batchId, int parsedCount, int insertedCount) {
        if (batchId is not { } id) {
            return;
        }
        try {
            _importBatchRepository.UpdateCounts(id, parsedCount, insertedCount);
        } catch (Exception e) {
            AppLog.Write($"[ImportManager.CompleteBatch] 回填批次 {id} 计数失败: {e.Message}");
        }
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
        var wordResult = ImportKindleWords(kindleWordsPath, progress);

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
        // 标注侧本轮不引入 origin_batch 列(它已由 original_clipping_lines 留底),
        // 但批次行照记 —— import_batches 是「每次导入发生了什么」的总账,不分表。
        var batchId = BeginBatch(ImportBatch.SourceKindleClippings, clippingsPath);
        if (!_km2DatabaseService.ImportKindleClippings(clippingsPath, out var result, progress)) {
            var exception = result[AppConstants.Exception];
            throw new InvalidOperationException(exception);
        }
        var parsedCount = result[AppConstants.ParsedCount];
        var insertedCount = result[AppConstants.InsertedCount];
        CompleteBatch(batchId,
            int.TryParse(parsedCount, out var parsed) ? parsed : 0,
            int.TryParse(insertedCount, out var inserted) ? inserted : 0);
        return Strings.Parsed_X + Strings.Space + parsedCount + Strings.Space + Strings.X_Clippings + Strings.Symbol_Comma +
               Strings.Imported_X + Strings.Space + insertedCount + Strings.Space + Strings.X_Clippings;
    }

    public string ImportKindleWords(string kindleWordsPath, IProgress<OperationProgress>? progress = null) {
        if (!File.Exists(kindleWordsPath)) {
            return string.Empty;
        }

        var batchId = BeginBatch(ImportBatch.SourceKindleVocab, kindleWordsPath);
        var vocabDatabaseService = _vocabDatabaseServiceFactory.Create(kindleWordsPath);

        if (!vocabDatabaseService.ImportKindleWords(kindleWordsPath, out var result, progress, batchId)) {
            var exception = result[AppConstants.Exception];
            throw new InvalidOperationException(exception);
        }
        var lookupCount = result[AppConstants.LookupCount];
        var insertedLookupCount = result[AppConstants.InsertedLookupCount];
        var insertedVocabCount = result[AppConstants.InsertedVocabCount];
        CompleteBatch(batchId,
            int.TryParse(lookupCount, out var parsed) ? parsed : 0,
            (int.TryParse(insertedLookupCount, out var insertedLookups) ? insertedLookups : 0) +
            (int.TryParse(insertedVocabCount, out var insertedVocabs) ? insertedVocabs : 0));
        return Strings.Parsed_X + Strings.Space + lookupCount + Strings.Space + Strings.X_Vocabs + Strings.Space + Strings.Symbol_Comma +
               Strings.Imported_X + Strings.Space + insertedLookupCount + Strings.Space + Strings.X_Lookups + Strings.Space +
               Strings.Symbol_Comma + insertedVocabCount + Strings.Space + Strings.X_Vocabs;
    }

    public string ImportKmDatabase(string filePath, IProgress<OperationProgress>? progress = null) =>
        ImportFlatSchemaDatabase(filePath, "KM_", ImportBatch.SourceKm, progress);

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
    public string ImportKm2Database(string filePath, IProgress<OperationProgress>? progress = null) =>
        ImportFlatSchemaDatabase(filePath, "KM2_", ImportBatch.SourceKm2, progress);

    /// <summary>原版 Kindle Mate 与本程序自己的库共用同一套扁平 schema,合并逻辑写在这里一份。</summary>
    private string ImportFlatSchemaDatabase(string filePath, string backupFileNamePrefix, string sourceType,
        IProgress<OperationProgress>? progress = null) {
        var kmDatabaseService = _kmDatabaseServiceFactory.Create(filePath);

        var clippingsCount = _clippingService.GetCount();
        var vocabCount = _vocabService.GetCount();
        var lookupCount = _lookupService.GetCount();

        if (File.Exists(filePath)) {
            // 备份是整库文件拷贝,源库大时有可感知耗时,归入「读取文件」阶段
            progress?.Report(OperationProgress.At(OperationStage.ReadingFile));
            var backupFilePath = Path.Combine(_importPath, backupFileNamePrefix + DateTimeHelper.GetCurrentTimestamp() + FileExtension.DAT);
            File.Copy(filePath, backupFilePath, true);
        }

        var batchId = BeginBatch(sourceType, filePath);
        if (!kmDatabaseService.ImportFromKmDatabase(progress, batchId)) {
            return string.Empty;
        }

        // 必须在收尾清理**之前**量本次真正新增:清理会删行,而删掉的既可能是本次导入进来的、
        // 也可能是库里原有的条目。此前拿「导入前后计数差值」当导入条数,等于把增量与清理混在一处,
        // 重复导入时就会出现「导入 -904 条」这种负数。
        var importedClippings = _clippingService.GetCount() - clippingsCount;
        var importedVocabs = _vocabService.GetCount() - vocabCount;
        // 溯源账记 vocab+lookups 增量(标注不写 origin_batch);parsed 记 0 = 扁平合并
        // 不区分「解析量」与「插入量」,两个都是活表差值,没有可诚实填写的解析分母。
        CompleteBatch(batchId, 0, importedVocabs + (_lookupService.GetCount() - lookupCount));

        // 收尾的清理与词频刷新同样在导入链路上(判重扫描 + 批量回写),不报进度末尾会留一段空白。
        // 清理结果此前被丢弃(out _),正是弹窗口径说不清的根源 —— 现在收下来单独展示。
        // 收尾清理只判**同书内**重复(crossBookDuplicates: false):跨书同文是两条独立高亮,
        // 跨书判重会把库里已有的那条一并删掉 —— 详见 FindDuplicatedClippings 的导入分支。
        _km2DatabaseService.CleanDatabase(string.Empty, out var cleanResult, progress, crossBookDuplicates: false);
        _km2DatabaseService.UpdateFrequency(progress);

        return ComposeImportMessage(importedClippings, importedVocabs, cleanResult);
    }

    /// <summary>
    /// 导入成功的正文。**口径(2026-09-17 用户指定,有意偏离原版)**:分列「本次新增」与「收尾清理删除」,
    /// 不再沿用原版那句把净变化称作「解析 / 导入 N 条」的写法(那个数字含清理删除量,重复导入时是负数)。
    /// 清理段的键名与拼接顺序复用「清理数据库」正文,两处口径保持一致。
    /// </summary>
    private static string ComposeImportMessage(int importedClippings, int importedVocabs,
        Dictionary<string, string> cleanResult) {
        var message = Strings.Imported_X + Strings.Space + importedClippings + Strings.Space + Strings.X_Clippings +
                      Strings.Symbol_Comma + importedVocabs + Strings.Space + Strings.X_Vocabs;

        // 清理为 0 时不附「空内容 0 条」这样的噪音段
        var cleanedEmpty = cleanResult.TryGetValue(AppConstants.EmptyCount, out var emptyCount) ? emptyCount : "0";
        var cleanedDuplicated = cleanResult.TryGetValue(AppConstants.DuplicatedCount, out var duplicatedCount) ? duplicatedCount : "0";
        if (cleanedEmpty is "0" && cleanedDuplicated is "0") {
            return message;
        }

        return message + Strings.Symbol_Comma +
               Strings.Cleaned + Strings.Space + Strings.Empty_Content + Strings.Space + cleanedEmpty +
               Strings.Space + Strings.X_Rows + Strings.Symbol_Comma +
               Strings.Duplicate_Content + Strings.Space + cleanedDuplicated + Strings.Space + Strings.X_Rows;
    }

    public string ImportKmateDatabase(string filePath, IProgress<OperationProgress>? progress = null) {
        var kmateDatabaseService = _kmateDatabaseServiceFactory.Create(filePath);

        var clippingsCount = _clippingService.GetCount();
        var vocabCount = _vocabService.GetCount();
        var lookupCount = _lookupService.GetCount();

        if (File.Exists(filePath)) {
            // 备份是整库文件拷贝,源库大时有可感知耗时,归入「读取文件」阶段
            progress?.Report(OperationProgress.At(OperationStage.ReadingFile));
            var backupFilePath = Path.Combine(_importPath, "KMate_" + DateTimeHelper.GetCurrentTimestamp() + FileExtension.DAT);
            File.Copy(filePath, backupFilePath, true);
        }

        var batchId = BeginBatch(ImportBatch.SourceKmate, filePath);
        if (!kmateDatabaseService.ImportFromKmateDatabase(progress, batchId)) {
            return string.Empty;
        }

        // 同 ImportFlatSchemaDatabase:先量新增、再收清理结果 —— 顺序反过来就会把净变化当成导入条数
        var importedClippings = _clippingService.GetCount() - clippingsCount;
        var importedVocabs = _vocabService.GetCount() - vocabCount;
        CompleteBatch(batchId, 0, importedVocabs + (_lookupService.GetCount() - lookupCount));

        // 收尾清理只判**同书内**重复(crossBookDuplicates: false):跨书同文是两条独立高亮,
        // 跨书判重会把库里已有的那条一并删掉 —— 详见 FindDuplicatedClippings 的导入分支。
        _km2DatabaseService.CleanDatabase(string.Empty, out var cleanResult, progress, crossBookDuplicates: false);
        _km2DatabaseService.UpdateFrequency(progress);

        return ComposeImportMessage(importedClippings, importedVocabs, cleanResult);
    }
}
