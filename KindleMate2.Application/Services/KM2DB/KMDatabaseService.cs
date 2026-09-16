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
        /// 这条链路的写入是**逐条** <c>Add</c>(每行一次连接 + 一次事务),几千条的源库上是主要耗时,
        /// 故按「已处理源行数 / 源总行数」上报确定进度;每 <see cref="ProgressReportInterval"/> 条报一次,
        /// 避免过于频繁地切回 UI 线程。
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

                    // 两条写入循环共用一条进度轴(分母 = 源库标注 + 原始行),进度单调递增不倒退
                    var writeTotal = kmClippings.Count + kmOriginalClippingLines.Count;
                    var processedRows = 0;

                    foreach (Clipping kmClipping in kmClippings) {
                        if (processedRows % ProgressReportInterval == 0) {
                            progress?.Report(new OperationProgress(OperationStage.Writing, processedRows, writeTotal));
                        }
                        processedRows++;

                        if (string.IsNullOrEmpty(kmClipping.Content)) {
                            continue;
                        }
                        if (!targetClippingKeys.Contains(kmClipping.Key) &&
                            !targetClippingBookContents.Contains(KmateDedup.BookContentKey(kmClipping))) {
                            if (_clippingRepository.Add(kmClipping)) {
                                // Keep the dedup sets in sync so a duplicate key/content later
                                // in the same source file cannot trip the PRIMARY KEY constraint.
                                targetClippingKeys.Add(kmClipping.Key);
                                targetClippingBookContents.Add(KmateDedup.BookContentKey(kmClipping));
                            }
                        }
                    }
                    var targetOriginalKeys = _originalClippingLineRepository.GetAllKeys();
                    foreach (OriginalClippingLine kmOriginalClippingLine in kmOriginalClippingLines) {
                        if (processedRows % ProgressReportInterval == 0) {
                            progress?.Report(new OperationProgress(OperationStage.Writing, processedRows, writeTotal));
                        }
                        processedRows++;

                        if (!targetOriginalKeys.Contains(kmOriginalClippingLine.Key)) {
                            if (_originalClippingLineRepository.Add(kmOriginalClippingLine)) {
                                targetOriginalKeys.Add(kmOriginalClippingLine.Key);
                            }
                        }
                    }
                    progress?.Report(new OperationProgress(OperationStage.Writing, writeTotal, writeTotal));
                }
            
                var kmLookups = _kmLookupRepository.GetAll();
                var kmVocabs = _kmVocabRepository.GetAll();
                if (kmLookups.Count > 0 || kmVocabs.Count > 0) {
                    var targetLookupWordKeys = _lookupRepository.GetWordKeysList().ToHashSet();
                    var targetVocabIds = _vocabRepository.GetAll().Select(v => v.Id).ToHashSet();

                    // 生词同样是逐条 Add,沿用与标注一致的进度轴
                    var vocabWriteTotal = kmLookups.Count + kmVocabs.Count;
                    var processedVocabRows = 0;

                    foreach (Lookup kmLookup in kmLookups) {
                        if (processedVocabRows % ProgressReportInterval == 0) {
                            progress?.Report(new OperationProgress(OperationStage.Writing, processedVocabRows, vocabWriteTotal));
                        }
                        processedVocabRows++;

                        if (!string.IsNullOrWhiteSpace(kmLookup.WordKey) &&
                            !targetLookupWordKeys.Contains(kmLookup.WordKey)) {
                            if (_lookupRepository.Add(kmLookup)) {
                                targetLookupWordKeys.Add(kmLookup.WordKey);
                            }
                        }
                    }
                    foreach (Vocab kmVocab in kmVocabs) {
                        if (processedVocabRows % ProgressReportInterval == 0) {
                            progress?.Report(new OperationProgress(OperationStage.Writing, processedVocabRows, vocabWriteTotal));
                        }
                        processedVocabRows++;

                        if (!targetVocabIds.Contains(kmVocab.Id)) {
                            if (_vocabRepository.Add(kmVocab)) {
                                targetVocabIds.Add(kmVocab.Id);
                            }
                        }
                    }
                    progress?.Report(new OperationProgress(OperationStage.Writing, vocabWriteTotal, vocabWriteTotal));
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