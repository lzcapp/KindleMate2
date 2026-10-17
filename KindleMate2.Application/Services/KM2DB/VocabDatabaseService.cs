using System.Globalization;
using KindleMate2.Application.Models;
using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Domain.Entities.VocabDB;
using KindleMate2.Domain.Interfaces.KM2DB;
using KindleMate2.Domain.Interfaces.VocabDB;
using KindleMate2.Infrastructure.Repositories.VocabDB;
using KindleMate2.Shared.Constants;
using IVocabLookupRepository = KindleMate2.Domain.Interfaces.VocabDB.ILookupRepository;
using IKm2DbLookupRepository = KindleMate2.Domain.Interfaces.KM2DB.ILookupRepository;
using Lookup = KindleMate2.Domain.Entities.VocabDB.Lookup;
using KindleMate2.Shared.Diagnostics;

namespace KindleMate2.Application.Services.KM2DB {
    public class VocabDatabaseService {
        /// <summary>上报进度的条数间隔:构建/判重阶段每这么多行切一次 UI 线程。</summary>
        private const int ProgressReportInterval = 100;

        /// <summary>
        /// <see cref="DateTimeOffset.FromUnixTimeMilliseconds"/> 能表示的上界(9999-12-31T23:59:59.999Z)。
        /// 源库 timestamp 是 INTEGER,可能被写成 <c>long.MaxValue</c> 之类的脏值;超出它换算会抛异常,
        /// 让整份导入失败。故先做区间校验,越界按「无时间」处理并计数(见 <see cref="IsConvertibleUnixMilliseconds"/>)。
        /// </summary>
        private const long MaxUnixMilliseconds = 253402300799999L;

        private readonly IBookInfoRepository _bookInfoRepository;
        private readonly IVocabLookupRepository _vocabLookupRepository;
        private readonly IWordRepository _wordRepository;

        private readonly IKm2DbLookupRepository _km2DbLookupRepository;
        private readonly IVocabRepository _vocabRepository;

        /// <summary>源库(设备 vocab.db)的实表/实列快照;为 null 时(旧调用方)不报"逐表跳过"明细。</summary>
        private readonly VocabSourceSchema? _sourceSchema;

        public VocabDatabaseService(IBookInfoRepository bookInfoRepository, IVocabLookupRepository vocabLookupRepository, IWordRepository wordRepository, IKm2DbLookupRepository km2DbLookupRepository, IVocabRepository vocabRepository, VocabSourceSchema? sourceSchema = null) {
            _bookInfoRepository = bookInfoRepository;
            _vocabLookupRepository = vocabLookupRepository;
            _wordRepository = wordRepository;
            _km2DbLookupRepository = km2DbLookupRepository;
            _vocabRepository = vocabRepository;
            _sourceSchema = sourceSchema;
        }

        /// <summary>
        /// 导入设备上 vocab.db 里的生词与查询记录。<paramref name="progress"/> 供界面展示阶段与进度。
        /// <paramref name="originBatch"/> 为本次导入在 import_batches 里的 id,写入各行的 origin_batch 溯源列;
        /// 传 null 时不落溯源(旧调用方兼容)。
        /// </summary>
        public bool ImportKindleWords(string sourceFilePath, out Dictionary<string, string> result,
            IProgress<OperationProgress>? progress = null, long? originBatch = null) {
            try {
                // 读的是另一份库(vocab.db),词量大时有可感知耗时
                progress?.Report(OperationProgress.At(OperationStage.ReadingFile));
                var words = _wordRepository.GetAll();
                var lookups = _vocabLookupRepository.GetAll();
                var bookInfos = _bookInfoRepository.GetAll();

                var lookupCount = lookups.Count;
                var insertedVocabCount = 0;
                var insertedLookupCount = 0;
                // 时间戳非法/越界(按「无时间」处理)而被忽略的记录数 —— 与"逐表跳过"一并呈现给用户。
                var skippedTimestampCount = 0;
                // 缺表/缺必需列而整表跳过的表数(源库快照为空时按 0 计,不虚报)。
                var skippedTableCount = _sourceSchema?.UnusableTables().Count ?? 0;

                // 源库 book_key 理论唯一,但重复/空 id 会让 ToDictionary 抛 ArgumentException、
                // 整份导入失败。这里容忍重复(取首条),空 id 兜底为 ""。
                var bookInfoMap = new Dictionary<string, BookInfo>(StringComparer.OrdinalIgnoreCase);
                foreach (var bookInfo in bookInfos) {
                    bookInfoMap.TryAdd(bookInfo.Id ?? string.Empty, bookInfo);
                }

                // Dedup against one in-memory snapshot of existing vocab ids instead of a
                // GetById round-trip (new connection + query) per candidate row.
                // 必须含 tombstone(GetAllIncludingDeleted):删除是软删,若只读活表,
                // "删过的词再导一次"会被判为新行插回来 —— 正是这次要修的复活缺陷。
                var existingVocabIds = _vocabRepository.GetAllIncludingDeleted()
                    .Where(v => v.Id != null)
                    .Select(v => v.Id!)
                    .ToHashSet(StringComparer.Ordinal);

                // 判重与构建是纯内存操作,但要走完两份源表 —— 共用一条进度轴,分母 = 词 + 查询记录
                var prepareTotal = words.Count + lookups.Count;
                var processedRows = 0;
                progress?.Report(new OperationProgress(OperationStage.Preparing, 0, prepareTotal));

                var newVocabs = new List<Vocab>();
                foreach (Word item in words) {
                    ReportProgress(progress, OperationStage.Preparing, processedRows, prepareTotal);
                    processedRows++;

                    var id = item.Id;
                    var word = item.WordText;
                    var stem = item.Stem;
                    var category = item.Category;
                    var timestamp = item.Timestamp;

                    // 时间戳无效即跳过。注意**不能只判 null**:源库 schema 里
                    // WORDS.timestamp 是 INTEGER DEFAULT 0,所以"没有时间"的行读出来是 0 而不是 NULL,
                    // 只判 null 会把它们按 1970-01-01 导进来。0 与负数一并视为无效
                    // (unix 毫秒里负数不可能是合法的阅读时间);超出可表示范围(如 long.MaxValue)同样无效。
                    if (word == null) {
                        continue;
                    }
                    if (!IsConvertibleUnixMilliseconds(timestamp)) {
                        skippedTimestampCount++;
                        continue;
                    }
                    DateTimeOffset dateTimeOffset;
                    try {
                        dateTimeOffset = DateTimeOffset.FromUnixTimeMilliseconds(timestamp!.Value);
                    } catch (ArgumentOutOfRangeException) {
                        // 区间校验后理论上到不了这里;留作最后一道防线,避免一条脏值弄挂整份导入。
                        skippedTimestampCount++;
                        continue;
                    }
                    DateTime dateTime = dateTimeOffset.LocalDateTime;
                    // 持久化键:必须用 InvariantCulture。CurrentCulture 在泰历/回历等区域会给出
                    // 非公历年,同一份数据在不同机器上算出不同的 (word_key, timestamp),判重与唯一约束都会错。
                    var formattedDateTime = dateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

                    if (!existingVocabIds.Add(word + timestamp)) {
                        continue;
                    }
                    newVocabs.Add(new Vocab {
                        Id = word + timestamp,
                        WordKey = id,
                        Word = word,
                        Stem = stem,
                        Category = category,
                        Timestamp = formattedDateTime,
                        Frequency = 0,
                        OriginBatch = originBatch
                    });
                }
                if (newVocabs.Count > 0) {
                    // 批量插入各自是单事务,内部无法细分 —— 前后各报一次
                    progress?.Report(new OperationProgress(OperationStage.Writing, 0, newVocabs.Count));
                    insertedVocabCount = _vocabRepository.Add(newVocabs);
                    progress?.Report(new OperationProgress(OperationStage.Writing, newVocabs.Count, newVocabs.Count));
                }

                var newLookups = new List<Domain.Entities.KM2DB.Lookup>();
                // KM2DB.lookups enforces UNIQUE(word_key, timestamp): several words looked
                // up within the same second are legitimate, but the same word looked up at
                // the exact same formatted timestamp must not be inserted twice. Dedup
                // in-memory on that composite key so a single import pass never trips the
                // constraint and rolls back the whole batch. The set is seeded from the
                // existing destination rows so the previous per-row existence query
                // (one connection each) disappears too.
                // 同样必须含 tombstone:被删的 (word_key, timestamp) 仍占着 UNIQUE 键,
                // 只读活表会让删过的查询记录被重新导回来。
                var seenLookupKeys = _km2DbLookupRepository.GetAllIncludingDeleted()
                    .Where(l => l.WordKey != null && l.Timestamp != null)
                    .Select(l => l.WordKey + "\u0000" + l.Timestamp)
                    .ToHashSet(StringComparer.Ordinal);
                foreach (Lookup item in lookups) {
                    ReportProgress(progress, OperationStage.Preparing, processedRows, prepareTotal);
                    processedRows++;

                    var wordKey = item.WordKey;
                    var bookKey = item.BookKey;
                    var usage = item.Usage;
                    var timestamp = item.Timestamp;

                    var title = string.Empty;
                    var authors = string.Empty;
                    if (bookKey != null && bookInfoMap.TryGetValue(bookKey, out var bookInfo)) {
                        title = bookInfo.Title;
                        authors = bookInfo.Authors;
                    }

                    // 同 WORDS:LOOKUPS.timestamp 也是 INTEGER DEFAULT 0,只判 null 会把
                    // 默认值当成 1970-01-01。越界(如 long.MaxValue)同样按「无时间」处理。
                    if (!IsConvertibleUnixMilliseconds(timestamp)) {
                        skippedTimestampCount++;
                        continue;
                    }
                    DateTimeOffset dateTimeOffset;
                    try {
                        dateTimeOffset = DateTimeOffset.FromUnixTimeMilliseconds(timestamp!.Value);
                    } catch (ArgumentOutOfRangeException) {
                        skippedTimestampCount++;
                        continue;
                    }
                    DateTime dateTime = dateTimeOffset.LocalDateTime;
                    // 持久化键:必须用 InvariantCulture。CurrentCulture 在泰历/回历等区域会给出
                    // 非公历年,同一份数据在不同机器上算出不同的 (word_key, timestamp),判重与唯一约束都会错。
                    var formattedDateTime = dateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

                    if (string.IsNullOrWhiteSpace(wordKey)) {
                        // Rows without a word key cannot be linked to vocabulary — skip.
                        continue;
                    }

                    var lookupKey = wordKey + "\u0000" + formattedDateTime;
                    if (!seenLookupKeys.Add(lookupKey)) {
                        continue;
                    }

                    newLookups.Add(new Domain.Entities.KM2DB.Lookup {
                        WordKey = wordKey,
                        Usage = usage,
                        Title = title,
                        Authors = authors,
                        Timestamp = formattedDateTime,
                        OriginBatch = originBatch
                    });
                }
                if (newLookups.Count > 0) {
                    progress?.Report(new OperationProgress(OperationStage.Writing, 0, newLookups.Count));
                    insertedLookupCount = _km2DbLookupRepository.Add(newLookups);
                    progress?.Report(new OperationProgress(OperationStage.Writing, newLookups.Count, newLookups.Count));
                }

                UpdateFrequency(progress);

                result = new Dictionary<string, string> {
                    { AppConstants.LookupCount, lookupCount.ToString() },
                    { AppConstants.InsertedLookupCount, insertedLookupCount.ToString() },
                    { AppConstants.InsertedVocabCount, insertedVocabCount.ToString() },
                    { AppConstants.SkippedTableCount, skippedTableCount.ToString() },
                    { AppConstants.SkippedTimestampCount, skippedTimestampCount.ToString() }
                };

                return true;
            } catch (Exception e) {
                AppLog.Write(e.Message);
                result = new Dictionary<string, string> {
                    { AppConstants.Exception, e.Message }
                };

                return false;
            }
        }

        /// <summary>重算词频。<paramref name="progress"/> 供界面展示(读全表 + 批量回写)。</summary>
        private void UpdateFrequency(IProgress<OperationProgress>? progress = null) {
            progress?.Report(OperationProgress.At(OperationStage.Preparing));
            var vocabs = _vocabRepository.GetAll();
            var lookups = _km2DbLookupRepository.GetAll();
            var frequencyMap = lookups
                .Where(l => !string.IsNullOrWhiteSpace(l.WordKey))
                .GroupBy(l => l.WordKey!.Trim())
                .ToDictionary(g => g.Key, g => g.Count());

            // Collect every row first, then push frequencies in ONE batched,
            // transaction-wrapped call instead of one connection + UPDATE per vocab.
            var updates = new List<Vocab>(vocabs.Count);
            foreach (Vocab vocab in vocabs) {
                var wordKey = vocab.WordKey;
                if (wordKey == null) {
                    continue;
                }
                frequencyMap.TryGetValue(wordKey, out var frequency);
                updates.Add(new Vocab {
                    WordKey = wordKey,
                    Frequency = frequency,
                    Id = vocab.Id,
                    Word = vocab.Word
                });
            }
            if (updates.Count > 0) {
                progress?.Report(new OperationProgress(OperationStage.Writing, 0, updates.Count));
                _vocabRepository.UpdateFrequencyByWordKey(updates);
                progress?.Report(new OperationProgress(OperationStage.Writing, updates.Count, updates.Count));
            }
        }

        /// <summary>按条数上报(每 <see cref="ProgressReportInterval"/> 条一次),避免过于频繁地切回 UI 线程。</summary>
        private static void ReportProgress(IProgress<OperationProgress>? progress, OperationStage stage,
            int processed, int total) {
            if (total > 0 && (processed % ProgressReportInterval == 0 || processed == total)) {
                progress?.Report(new OperationProgress(stage, processed, total));
            }
        }

        /// <summary>
        /// 是否是可安全换算的 unix 毫秒:<c>&gt; 0</c> 且在 <see cref="DateTimeOffset"/> 可表示范围内。
        /// 0 / 负数(源库"没有时间"的默认值)与越界脏值都返回 false,按「无时间」处理,不抛异常。
        /// </summary>
        private static bool IsConvertibleUnixMilliseconds(long? value) =>
            value is > 0 and <= MaxUnixMilliseconds;
    }
}
