using KindleMate2.Application.Models;
using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Domain.Interfaces.KM2DB;
using KindleMate2.Infrastructure.Helpers;
using KindleMate2.Shared.Diagnostics;

namespace KindleMate2.Application.Services.KM2DB {
    public class KmDatabaseService {
        /// <summary>上报进度的条数间隔:逐条写入时每这么多行切一次 UI 线程,兼顾平滑与开销。</summary>
        private const int ProgressReportInterval = 100;

        private readonly IClippingRepository _clippingRepository;
        private readonly ILookupRepository _lookupRepository;
        private readonly IOriginalClippingLineRepository _originalClippingLineRepository;
        private readonly ISettingRepository _settingRepository;
        private readonly IVocabRepository _vocabRepository;
        
        private readonly IClippingRepository _kmClippingRepository;
        private readonly ILookupRepository _kmLookupRepository;
        private readonly IOriginalClippingLineRepository _kmOriginalClippingLineRepository;
        private readonly ISettingRepository _kmSettingRepository;
        private readonly IVocabRepository _kmVocabRepository;

        public KmDatabaseService(IClippingRepository clippingRepository, ILookupRepository lookupRepository, IOriginalClippingLineRepository originalClippingLineRepository, ISettingRepository settingRepository, IVocabRepository vocabRepository, IClippingRepository kmClippingRepository, ILookupRepository kmLookupRepository, IOriginalClippingLineRepository kmOriginalClippingLineRepository, ISettingRepository kmSettingRepository, IVocabRepository kmVocabRepository) {
            _clippingRepository = clippingRepository;
            _lookupRepository = lookupRepository;
            _originalClippingLineRepository = originalClippingLineRepository;
            _settingRepository = settingRepository;
            _vocabRepository = vocabRepository;
            
            _kmClippingRepository = kmClippingRepository;
            _kmLookupRepository = kmLookupRepository;
            _kmOriginalClippingLineRepository = kmOriginalClippingLineRepository;
            _kmSettingRepository = kmSettingRepository;
            _kmVocabRepository = kmVocabRepository;
        }

        /// <summary>合并一个扁平 schema 源库(原版 KM / 本程序 KM2)的数据。<paramref name="progress"/> 供界面展示阶段与进度。</summary>
        /// <remarks>
        /// **先判重收集候选,再一次性批量写入**。此前是逐条 <c>Add</c> —— 每行一次连接、一次事务、
        /// 一次 fsync,几千条的源库上要十几分钟(实测:5,803 行 ≈ 15 分钟),这正是「导入数据库」卡顿的根因。
        /// 改走仓储的批量重载后,整批只开一个事务。
        /// 批量重载的失败语义已核对:Clipping / OriginalClippingLine 为「整批失败后降级逐条独立事务」,
        /// Lookup / Vocab 为「原子回滚」;两边的源库判重都保证不会撞 UNIQUE,故与逐条写入等价。
        /// 判重集合仍在收集时同步更新 —— 否则同一源库里的重复 key 会撞 PRIMARY KEY。
        /// </remarks>
        public bool ImportFromKmDatabase(IProgress<OperationProgress>? progress = null) {
            try {
                // 读源库全表(标注 / 原始行 / 生词)都在这一步,源库大时有可感知耗时
                progress?.Report(OperationProgress.At(OperationStage.ReadingFile));
                var kmClippings = _kmClippingRepository.GetAll();
                var kmOriginalClippingLines = _kmOriginalClippingLineRepository.GetAll();
                if (kmClippings.Count > 0 || kmOriginalClippingLines.Count > 0) {
                    progress?.Report(OperationProgress.At(OperationStage.Preparing));
                    // Pre-fetch target data for O(1) in-memory dedup checks
                    var targetClippings = _clippingRepository.GetAll();
                    var targetClippingKeys = targetClippings.Select(c => c.Key).ToHashSet();
                    // Dedup scope is per (book, author, content) — shared with the KMate km3.dat
                    // import (KmateDatabaseService / KmateDedup) so both import paths behave
                    // consistently: the same sentence in two different books are two distinct
                    // highlights and both are kept; a genuine duplicate of the same book still
                    // shares book+author+content and is skipped.
                    var targetClippingBookContents = targetClippings.Select(KmateDedup.BookContentKey).ToHashSet();

                    // 判重阶段(纯内存)按「已扫过行数 / 源总行数」上报,每 ProgressReportInterval 条一次
                    var scanTotal = kmClippings.Count + kmOriginalClippingLines.Count;
                    var scannedRows = 0;

                    var clippingsToAdd = new List<Clipping>();
                    foreach (Clipping kmClipping in kmClippings) {
                        if (scannedRows % ProgressReportInterval == 0) {
                            progress?.Report(new OperationProgress(OperationStage.Preparing, scannedRows, scanTotal));
                        }
                        scannedRows++;

                        if (string.IsNullOrEmpty(kmClipping.Content)) {
                            continue;
                        }
                        if (!targetClippingKeys.Contains(kmClipping.Key) &&
                            !targetClippingBookContents.Contains(KmateDedup.BookContentKey(kmClipping))) {
                            clippingsToAdd.Add(kmClipping);
                            // Keep the dedup sets in sync so a duplicate key/content later
                            // in the same source file cannot trip the PRIMARY KEY constraint.
                            targetClippingKeys.Add(kmClipping.Key);
                            targetClippingBookContents.Add(KmateDedup.BookContentKey(kmClipping));
                        }
                    }

                    var linesToAdd = new List<OriginalClippingLine>();
                    var targetOriginalKeys = _originalClippingLineRepository.GetAllKeys();
                    foreach (OriginalClippingLine kmOriginalClippingLine in kmOriginalClippingLines) {
                        if (scannedRows % ProgressReportInterval == 0) {
                            progress?.Report(new OperationProgress(OperationStage.Preparing, scannedRows, scanTotal));
                        }
                        scannedRows++;

                        if (!targetOriginalKeys.Contains(kmOriginalClippingLine.Key)) {
                            linesToAdd.Add(kmOriginalClippingLine);
                            targetOriginalKeys.Add(kmOriginalClippingLine.Key);
                        }
                    }

                    // 写入阶段:两次批量写(各自单事务),进度按已写条数推进
                    var writeTotal = clippingsToAdd.Count + linesToAdd.Count;
                    if (writeTotal > 0) {
                        progress?.Report(new OperationProgress(OperationStage.Writing, 0, writeTotal));
                    }
                    if (clippingsToAdd.Count > 0) {
                        _clippingRepository.Add(clippingsToAdd);
                        progress?.Report(new OperationProgress(OperationStage.Writing, clippingsToAdd.Count, writeTotal));
                    }
                    if (linesToAdd.Count > 0) {
                        _originalClippingLineRepository.Add(linesToAdd);
                        progress?.Report(new OperationProgress(OperationStage.Writing, writeTotal, writeTotal));
                    }
                }
            
                var kmLookups = _kmLookupRepository.GetAll();
                var kmVocabs = _kmVocabRepository.GetAll();
                if (kmLookups.Count > 0 || kmVocabs.Count > 0) {
                    var targetLookupWordKeys = _lookupRepository.GetWordKeysList().ToHashSet();
                    var targetVocabIds = _vocabRepository.GetAll().Select(v => v.Id).ToHashSet();

                    // 生词同样「先判重收集、再批量写」,与标注一致
                    var vocabScanTotal = kmLookups.Count + kmVocabs.Count;
                    var vocabScanned = 0;

                    var lookupsToAdd = new List<Lookup>();
                    foreach (Lookup kmLookup in kmLookups) {
                        if (vocabScanned % ProgressReportInterval == 0) {
                            progress?.Report(new OperationProgress(OperationStage.Preparing, vocabScanned, vocabScanTotal));
                        }
                        vocabScanned++;

                        if (!string.IsNullOrWhiteSpace(kmLookup.WordKey) &&
                            !targetLookupWordKeys.Contains(kmLookup.WordKey)) {
                            lookupsToAdd.Add(kmLookup);
                            targetLookupWordKeys.Add(kmLookup.WordKey);
                        }
                    }

                    var vocabsToAdd = new List<Vocab>();
                    foreach (Vocab kmVocab in kmVocabs) {
                        if (vocabScanned % ProgressReportInterval == 0) {
                            progress?.Report(new OperationProgress(OperationStage.Preparing, vocabScanned, vocabScanTotal));
                        }
                        vocabScanned++;

                        if (!targetVocabIds.Contains(kmVocab.Id)) {
                            vocabsToAdd.Add(kmVocab);
                            targetVocabIds.Add(kmVocab.Id);
                        }
                    }

                    var vocabWriteTotal = lookupsToAdd.Count + vocabsToAdd.Count;
                    if (vocabWriteTotal > 0) {
                        progress?.Report(new OperationProgress(OperationStage.Writing, 0, vocabWriteTotal));
                    }
                    if (lookupsToAdd.Count > 0) {
                        _lookupRepository.Add(lookupsToAdd);
                        progress?.Report(new OperationProgress(OperationStage.Writing, lookupsToAdd.Count, vocabWriteTotal));
                    }
                    if (vocabsToAdd.Count > 0) {
                        _vocabRepository.Add(vocabsToAdd);
                        progress?.Report(new OperationProgress(OperationStage.Writing, vocabWriteTotal, vocabWriteTotal));
                    }
                }
            
                var kmSettings = _kmSettingRepository.GetAll();
                if (kmSettings.Count > 0) {
                    var targetSettingNames = _settingRepository.GetAll()
                        .Where(s => s.Name != null)
                        .Select(s => s.Name!)
                        .ToHashSet();

                    foreach (Setting kmSetting in kmSettings) {
                        if (!targetSettingNames.Contains(kmSetting.Name)) {
                            _settingRepository.Add(kmSetting);
                        }
                    }
                }

                return true;
            } catch (Exception e) {
                AppLog.Write(StringHelper.GetExceptionMessage(nameof(ImportFromKmDatabase), e));
                return false;
            }
        }
    }
}