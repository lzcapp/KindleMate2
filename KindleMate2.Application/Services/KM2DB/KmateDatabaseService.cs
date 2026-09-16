using KindleMate2.Application.Models;
using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Domain.Interfaces.KM2DB;
using KindleMate2.Infrastructure.Helpers;
using KindleMate2.Infrastructure.Repositories.KM2DB;
using KindleMate2.Shared.Diagnostics;

namespace KindleMate2.Application.Services.KM2DB {
    /// <summary>
    /// Imports data from a KMate (kmate.io) <c>km3.dat</c> database into the current KM2 database.
    /// </summary>
    /// <remarks>
    /// Source rows are mapped by <see cref="KmateSourceReader"/> (the km3 schema differs from the KM2
    /// target schema, so the shared repositories cannot read it directly). Target reads (dedup pre-fetch)
    /// use the regular KM2 repositories; target writes go through <see cref="KmateAtomicWriter"/> in a
    /// single transaction (all-or-nothing). Deduplication mirrors <see cref="KmDatabaseService"/> with three
    /// km3-specific corrections:
    /// <list type="bullet">
    /// <item><c>original_clipping_lines</c> are only imported when their key belongs to an accepted
    /// <c>clippings</c> row — KMate leaves orphan lines behind after its own duplicate cleanup;</item>
    /// <item><c>vocab</c> uses the word_key-derived TEXT id produced by the reader (source id is INTEGER);</item>
    /// <item><c>lookups</c> are deduplicated on (word_key, timestamp) to respect the target UNIQUE constraint.</item>
    /// </list>
    /// The source file is only ever opened read-only.
    /// </remarks>
    public class KmateDatabaseService {
        /// <summary>上报进度的条数间隔:判重是纯内存比对,每这么多行切一次 UI 线程即可。</summary>
        private const int ProgressReportInterval = 100;

        private readonly IClippingRepository _clippingRepository;
        private readonly ILookupRepository _lookupRepository;
        private readonly IOriginalClippingLineRepository _originalClippingLineRepository;
        private readonly IVocabRepository _vocabRepository;
        private readonly string _sourcePath;
        private readonly string _targetConnectionString;

        public KmateDatabaseService(
            IClippingRepository clippingRepository,
            ILookupRepository lookupRepository,
            IOriginalClippingLineRepository originalClippingLineRepository,
            IVocabRepository vocabRepository,
            string sourcePath,
            string targetConnectionString) {
            _clippingRepository = clippingRepository;
            _lookupRepository = lookupRepository;
            _originalClippingLineRepository = originalClippingLineRepository;
            _vocabRepository = vocabRepository;
            _sourcePath = sourcePath;
            _targetConnectionString = targetConnectionString;
        }

        public bool ImportFromKmateDatabase(IProgress<OperationProgress>? progress = null) {
            try {
                // 读源文件并映射到 km2 schema 都在 TryRead 里,km3.dat 大时有可感知耗时
                progress?.Report(OperationProgress.At(OperationStage.ReadingFile));
                if (!KmateSourceReader.TryRead(_sourcePath, out var data, out _)) {
                    return false;
                }

                var clippingCandidates = new List<Clipping>();
                var lineCandidates = new List<OriginalClippingLine>();
                var lookupCandidates = new List<Lookup>();
                var vocabCandidates = new List<Vocab>();

                // 判重要走完源库每一行(纯内存比对);四个集合共用一条进度轴,
                // 分母 = 源库总行数,进度单调递增不倒退。
                var prepareTotal = data.Clippings.Count + data.OriginalClippingLines.Count
                                   + data.Lookups.Count + data.Vocabs.Count;
                var processedRows = 0;
                if (prepareTotal > 0) {
                    progress?.Report(new OperationProgress(OperationStage.Preparing, 0, prepareTotal));
                }

                if (data.Clippings.Count > 0 || data.OriginalClippingLines.Count > 0) {
                    // Pre-fetch target data for O(1) in-memory dedup checks.
                    var targetClippings = _clippingRepository.GetAll();
                    var targetClippingKeys = targetClippings.Select(c => c.Key).ToHashSet();
                    // Dedup scope is per (book, author, content), not global content: the same
                    // sentence highlighted in two different books are two distinct highlights and
                    // must both be kept. A true duplicate import (same book re-imported from a
                    // device/cloud/source) still shares book+author+content and is skipped.
                    var targetBookContents = targetClippings.Select(KmateDedup.BookContentKey).ToHashSet();

                    foreach (Clipping kmClipping in data.Clippings) {
                        if (processedRows % ProgressReportInterval == 0) {
                            progress?.Report(new OperationProgress(OperationStage.Preparing, processedRows, prepareTotal));
                        }
                        processedRows++;

                        if (string.IsNullOrEmpty(kmClipping.Content)) {
                            continue;
                        }
                        if (!targetClippingKeys.Contains(kmClipping.Key) &&
                            !targetBookContents.Contains(KmateDedup.BookContentKey(kmClipping))) {
                            clippingCandidates.Add(kmClipping);
                            // Keep the dedup sets in sync so duplicates later in the same source
                            // cannot end up in the candidate list twice.
                            targetClippingKeys.Add(kmClipping.Key);
                            targetBookContents.Add(KmateDedup.BookContentKey(kmClipping));
                        }
                    }

                    var targetOriginalKeys = _originalClippingLineRepository.GetAllKeys().ToHashSet();
                    foreach (OriginalClippingLine kmLine in data.OriginalClippingLines) {
                        if (processedRows % ProgressReportInterval == 0) {
                            progress?.Report(new OperationProgress(OperationStage.Preparing, processedRows, prepareTotal));
                        }
                        processedRows++;

                        // Skip orphan lines whose key was removed from clippings by KMate's cleanup.
                        if (!targetClippingKeys.Contains(kmLine.Key)) {
                            continue;
                        }
                        if (!targetOriginalKeys.Contains(kmLine.Key)) {
                            lineCandidates.Add(kmLine);
                            targetOriginalKeys.Add(kmLine.Key);
                        }
                    }
                }

                if (data.Lookups.Count > 0) {
                    var targetLookupPairs = _lookupRepository.GetAll()
                        .Where(l => l.WordKey != null)
                        .Select(l => ComposePair(l.WordKey!, l.Timestamp))
                        .ToHashSet();

                    foreach (Lookup kmLookup in data.Lookups) {
                        if (processedRows % ProgressReportInterval == 0) {
                            progress?.Report(new OperationProgress(OperationStage.Preparing, processedRows, prepareTotal));
                        }
                        processedRows++;

                        if (string.IsNullOrWhiteSpace(kmLookup.WordKey)) {
                            continue;
                        }
                        var pair = ComposePair(kmLookup.WordKey, kmLookup.Timestamp);
                        if (!targetLookupPairs.Contains(pair)) {
                            lookupCandidates.Add(kmLookup);
                            targetLookupPairs.Add(pair);
                        }
                    }
                }

                if (data.Vocabs.Count > 0) {
                    var targetVocabIds = _vocabRepository.GetAll().Select(v => v.Id).ToHashSet();

                    foreach (Vocab kmVocab in data.Vocabs) {
                        if (processedRows % ProgressReportInterval == 0) {
                            progress?.Report(new OperationProgress(OperationStage.Preparing, processedRows, prepareTotal));
                        }
                        processedRows++;

                        if (string.IsNullOrWhiteSpace(kmVocab.Id)) {
                            continue;
                        }
                        if (!targetVocabIds.Contains(kmVocab.Id)) {
                            vocabCandidates.Add(kmVocab);
                            targetVocabIds.Add(kmVocab.Id);
                        }
                    }
                }

                if (prepareTotal > 0) {
                    progress?.Report(new OperationProgress(OperationStage.Preparing, prepareTotal, prepareTotal));
                }

                // 写入走单连接 + 单事务,内部无法再细分进度 —— 因此前后各报一次(0 → 满),
                // 而不是拆批提交,以免破坏 all-or-nothing 语义。
                var writeTotal = clippingCandidates.Count + lineCandidates.Count + lookupCandidates.Count + vocabCandidates.Count;
                if (writeTotal > 0) {
                    progress?.Report(new OperationProgress(OperationStage.Writing, 0, writeTotal));
                }

                // All-or-nothing: candidates are written in a single connection + single transaction,
                // so a failure anywhere rolls the whole import back (no partial migration).
                KmateAtomicWriter.WriteAll(
                    _targetConnectionString,
                    clippingCandidates,
                    lineCandidates,
                    lookupCandidates,
                    vocabCandidates);

                if (writeTotal > 0) {
                    progress?.Report(new OperationProgress(OperationStage.Writing, writeTotal, writeTotal));
                }

                return true;
            } catch (Exception e) {
                AppLog.Write(StringHelper.GetExceptionMessage(nameof(ImportFromKmateDatabase), e));
                return false;
            }
        }

        private static string ComposePair(string wordKey, string? timestamp) {
            // \u0001 is used as a separator that cannot appear in a word_key.
            return wordKey + "\u0001" + (timestamp ?? string.Empty);
        }
    }
}
