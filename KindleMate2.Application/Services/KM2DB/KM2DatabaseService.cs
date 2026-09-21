using System.Globalization;
using System.Text.RegularExpressions;
using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Domain.Entities.MyClippings;
using KindleMate2.Domain.Interfaces.KM2DB;
using KindleMate2.Infrastructure.Helpers;
using KindleMate2.Application.Models;
using KindleMate2.Shared;
using KindleMate2.Shared.Clippings;
using KindleMate2.Shared.Constants;
using KindleMate2.Shared.Diagnostics;

namespace KindleMate2.Application.Services.KM2DB {
    public class Km2DatabaseService(
        IClippingRepository clippingRepository,
        ILookupRepository lookupRepository,
        IOriginalClippingLineRepository originalClippingLineRepository,
        ISettingRepository settingRepository,
        IVocabRepository vocabRepository) : IKm2DatabaseService {
        public bool ImportKindleClippings(string clippingsPath, out Dictionary<string, string> result,
            IProgress<OperationProgress>? progress = null) {
            try {
                progress?.Report(OperationProgress.At(OperationStage.ReadingFile));
                List<string> lines = [
                    .. File.ReadAllLines(clippingsPath)
                ];
                progress?.Report(OperationProgress.At(OperationStage.Parsing));

                var delimiterIndex = new List<int>();

                for (var i = 2; i < lines.Count; i++) {
                    lines[i] = StringHelper.RemoveControlChar(lines[i]);
                    if (lines[i].StartsWith("===") && lines[i - 2].Trim().Equals("") && lines[i].EndsWith("===")) {
                        delimiterIndex.Add(i);
                    }
                }

                var myClippings = new List<MyClipping>();
                
                for (var i = 0; i < delimiterIndex.Count; i++) {
                    var ceilDelimiter = i == 0 ? -1 : delimiterIndex[i - 1];
                    var florDelimiter = delimiterIndex[i];

                    // Guard against malformed file: ensure enough lines for header + metadata + content
                    if (ceilDelimiter + 4 >= lines.Count)
                        continue;

                    var line1 = lines[ceilDelimiter + 1].Trim();
                    var line2 = lines[ceilDelimiter + 2].Trim();
                    //  line3 should be empty
                    var line4 = lines[ceilDelimiter + 4].Trim();
                    if (florDelimiter > ceilDelimiter + 5) {
                        // then line4 is the rest (multiline content)
                        for (var index = ceilDelimiter + 5; index < florDelimiter; index++) {
                            line4 += Environment.NewLine + lines[index].Trim();
                        }
                    }
                    var line5 = lines[florDelimiter].Trim(); // line 5 is "=========="

                    myClippings.Add(new MyClipping {
                        Header = line1,
                        Metadata = line2,
                        Content = line4,
                        Delimiter = line5
                    });
                }

                var insertedCount = HandleClippings(myClippings, out var skipCounts, progress: progress);

                result = new Dictionary<string, string> {
                    { AppConstants.ParsedCount, delimiterIndex.Count.ToString() },
                    { AppConstants.InsertedCount, insertedCount.ToString() },
                    { AppConstants.SkippedDateCount, skipCounts.DateFailed.ToString() },
                    { AppConstants.SkippedPageCount, skipCounts.PageFailed.ToString() },
                    { AppConstants.SkippedLimitCount, skipCounts.LimitReached.ToString() },
                    // 复用先前定义却一直没人用的 TrimmedCount:语义就是"首尾被修剪掉的条数"。
                    { AppConstants.TrimmedCount, skipCounts.CleanedNoise.ToString() }
                };
                return true;
            } catch (Exception e) {
                result = new Dictionary<string, string> {
                    { AppConstants.Exception, e.Message }
                };
                return false;
            }
        }
        
        public bool RebuildDatabase(out Dictionary<string, string> result) {
            try {
                result = new Dictionary<string, string>();
                
                var originalClippingLines = originalClippingLineRepository.GetAll();
                if (originalClippingLines.Count <= 0) {
                    throw new Exception(Strings.Database_Empty);
                }
            
                clippingRepository.DeleteAll();
            
                var myClippings = (from originalClippingLine in originalClippingLines
                    let line1 = originalClippingLine.Line1
                    let line2 = originalClippingLine.Line2
                    let line4 = originalClippingLine.Line4
                    let line5 = originalClippingLine.Line5
                    where !string.IsNullOrWhiteSpace(line1) && !string.IsNullOrWhiteSpace(line2) && !string.IsNullOrWhiteSpace(line4) && !string.IsNullOrWhiteSpace(line5)
                    select new MyClipping {
                        Header = line1,
                        Metadata = line2,
                        Content = line4,
                        Delimiter = line5
                    }).ToList();

                var insertedCount = HandleClippings(myClippings, out _, isRebuild: true);

                UpdateFrequency();

                result = new Dictionary<string, string> {
                    { AppConstants.ParsedCount, originalClippingLines.Count.ToString() },
                    { AppConstants.InsertedCount, insertedCount.ToString() }
                };
                return true;
            } catch (Exception e) {
                AppLog.Write(StringHelper.GetExceptionMessage(nameof(CleanDatabase), e));
                result = new  Dictionary<string, string> {
                    { AppConstants.Exception, e.Message }
                };
                return false;
            }
        }

        /// <summary>
        /// **只读**扫描一遍,算出"清洗会改掉哪些条目" —— 不写任何东西。
        /// 确认框要先把改了多少条、都改成什么样摆给用户看,所以写入前必须有这一步。
        /// 与 <see cref="CleanClippingTexts"/> 共用同一段判定(<see cref="ComputeCleanChanges"/>),
        /// 于是"看到的"与"实际改的"不可能对不上。
        /// </summary>
        public bool ScanClippingClean(out ClippingCleanReport report,
            IProgress<OperationProgress>? progress = null) {
            try {
                var pairs = ComputeCleanChanges(progress, out var scanned, out var allPunctuation);
                report = new ClippingCleanReport {
                    Scanned = scanned,
                    ChangedCount = pairs.Count,
                    AllPunctuationCount = allPunctuation,
                    Changes = pairs
                        .Select(p => new ClippingCleanChange(p.Clipping.Key, p.Clipping.BookName ?? string.Empty, p.Before, p.After))
                        .ToList()
                };
                return true;
            } catch (Exception e) {
                AppLog.Write(StringHelper.GetExceptionMessage(nameof(ScanClippingClean), e));
                report = new ClippingCleanReport();
                return false;
            }
        }

        /// <summary>
        /// 批量清洗已入库标注的首尾标点。
        ///
        /// 只改 <c>clippings.content</c>,**不回写** <c>original_clipping_lines.line4</c>。
        ///
        /// 之所以可以不回写:「重建数据库」会拿 line4 当 content 再走一遍 <see cref="HandleClippings"/>,
        /// 而那一遍里同样带清洗 —— 即便 line4 里还留着**老版本导入时**的噪音,重建出来的 content 仍旧是
        /// 清洗后的结果,所以这里不回写也不会被重建回退。
        /// (注意重建**只读** line4、不回写它,所以 line4 里的存量噪音会一直留着 —— 无害,但别指望它被修掉。
        /// 这一点有专门的用例钉住,见 ClipCleanTests。)
        ///
        /// 返回 false 只代表整体失败(读库/写库抛异常);"没有需要清洗的"是正常结果,
        /// 由 <see cref="ClippingCleanReport.ChangedCount"/> == 0 表达。
        /// </summary>
        public bool CleanClippingTexts(out ClippingCleanReport report,
            IProgress<OperationProgress>? progress = null) {
            try {
                var pairs = ComputeCleanChanges(progress, out var scanned, out var allPunctuation);
                var changes = new List<ClippingCleanChange>(pairs.Count);

                foreach (var (clipping, before, after) in pairs) {
                    clipping.Content = after;
                    // 只有真的写进库才计入清单 —— 否则清单会列出实际没生效的"改动",
                    // 而这份清单正是用户事后核对/回滚的凭据。
                    if (clippingRepository.Update(clipping)) {
                        changes.Add(new ClippingCleanChange(clipping.Key, clipping.BookName ?? string.Empty, before, after));
                    }
                }

                report = new ClippingCleanReport {
                    Scanned = scanned,
                    ChangedCount = changes.Count,
                    AllPunctuationCount = allPunctuation,
                    Changes = changes
                };
                return true;
            } catch (Exception e) {
                AppLog.Write(StringHelper.GetExceptionMessage(nameof(CleanClippingTexts), e));
                report = new ClippingCleanReport();
                return false;
            }
        }

        /// <summary>
        /// 清洗判定的唯一实现:扫全表,返回「要改的条目 + 改前 + 改后」。
        /// **不落库** —— 谁写谁负责,这样只读预览与真正写入走的是同一条判定。
        /// </summary>
        private List<(Clipping Clipping, string Before, string After)> ComputeCleanChanges(
            IProgress<OperationProgress>? progress, out int scanned, out int allPunctuation) {
            progress?.Report(OperationProgress.At(OperationStage.Preparing));
            var clippings = clippingRepository.GetAll();
            scanned = clippings.Count;
            allPunctuation = 0;
            var pairs = new List<(Clipping, string, string)>();
            var processed = 0;

            foreach (Clipping clipping in clippings) {
                processed++;
                if (processed % 100 == 0) {
                    progress?.Report(new OperationProgress(OperationStage.Preparing, processed, clippings.Count));
                }

                var before = clipping.Content ?? string.Empty;
                var result = ClippingCleanRules.Clean(before);
                if (result.Outcome == ClippingCleanOutcome.AllPunctuation) {
                    allPunctuation++;
                    continue;
                }
                if (!result.Changed) {
                    continue;
                }
                pairs.Add((clipping, before, result.Text));
            }

            return pairs;
        }

        private sealed class SkipCounts {
            public int LimitReached;
            public int PageFailed;
            public int DateFailed;

            /// <summary>被清掉首尾标点的条数(不是"跳过",是"改了")。</summary>
            public int CleanedNoise;

            /// <summary>整条都是标点、因而跳过未改的条数。</summary>
            public int AllPunctuation;
        }

        private int HandleClippings(List<MyClipping> clippings, out SkipCounts skipCounts, bool isRebuild = false,
            IProgress<OperationProgress>? progress = null) {
            skipCounts = new SkipCounts();
            var insertResult = 0;
            // 准备阶段按「已处理 / 解析出总数」上报确定进度(每 100 条一次,避免过于频繁)
            var processed = 0;

            var allClippings = clippingRepository.GetAll();
            var allClippingsKeys = allClippings.Select(c => c.Key).ToHashSet();
            // Pre-build key→contents map for O(1) dedup lookup instead of O(n) Any() per item
            var contentByKey = allClippings
                .Where(c => c.Content != null)
                .GroupBy(c => c.Key)
                .ToDictionary(g => g.Key, g => g.Select(c => c.Content!).ToList());
            var originalKeys = originalClippingLineRepository.GetAllKeys();
            
            var listAddClippings = new List<Clipping>();
            var listAddOriginalClippings = new List<OriginalClippingLine>();
            
            foreach (MyClipping myClipping in clippings) {
                processed++;
                if (processed % 100 == 0) {
                    progress?.Report(new OperationProgress(OperationStage.Preparing, processed, clippings.Count));
                }
                try {
                    var clipping = new Clipping {
                        Key = string.Empty
                    };

                    var header = myClipping.Header;
                    var metadata = myClipping.Metadata;
                    var content = myClipping.Content;

                    if (string.IsNullOrWhiteSpace(content)) {
                        continue;
                    }
                    if (MyClippingsHelper.IsClippingLimitReached(content)) {
                        skipCounts.LimitReached++;
                        continue;
                    }

                    // ★ 首尾标点清洗(2026-09-21 新增)。挂在这里而不是挂在导入入口,是因为
                    //   本方法**同时是导入与「重建数据库」的解析路径** —— 挂这一处,两条路都生效,
                    //   而且"重建"必然复现同样的结果,不会把清洗过的文本又还原回带噪音的样子。
                    //
                    //   content 变量之后的**三处用途拿到的都是清洗后的值** ——
                    //   `clipping.Content = content`、批内判重集合 contentByKey、以及
                    //   `Line4 = content`(original_clipping_lines)。
                    //   ⚠ 所以**本库不保留清洗前的原文** —— 想回头看原来什么样,只有清洗前那份数据库备份,
                    //   加上清洗时落盘的改动清单(BackupDirectory/ClippingClean_<stamp>.txt)。
                    //
                    //   幂等性不受影响:「重建数据库」也是走本方法,拿现有 line4 当 content 再清一遍,
                    //   而清洗本身幂等(清过的再清不变),所以**手工清洗的结果不会被重建回退**。
                    //   老版本导入的存量数据,line4 里还留着当时的噪音 —— 重建时会在解析那一遍被清掉,
                    //   出来的 content 是干净的;但 line4 本身**不回写**(重建只读它),仍留原样。
                    //   (见 ClipCleanTests 里的 rebuild / legacy 两条用例。)
                    var cleanedContent = ClippingCleanRules.Clean(content);
                    if (cleanedContent.Outcome == ClippingCleanOutcome.AllPunctuation) {
                        // 整条都是标点(例如只划到一个「。」):清下去会变成空条目,留着原文并计数。
                        skipCounts.AllPunctuation++;
                    } else if (cleanedContent.Changed) {
                        content = cleanedContent.Text;
                        skipCounts.CleanedNoise++;
                    }

                    Header headerResult = MyClippingsHelper.ParseTitleAndAuthor(header);
                    clipping.BookName = headerResult.Title;
                    clipping.AuthorName = headerResult.Author;
                
                    Location location = MyClippingsHelper.ParseLocation(metadata);
                    clipping.PageNumber = location.Page;
                    clipping.ClippingTypeLocation = string.Format(AppConstants.LocationFormat, location.From, location.To);
                
                    var clippingTypeLocation = string.Empty;
                    metadata = metadata.Replace("- ", "", StringComparison.InvariantCultureIgnoreCase);
                    var indexOf = metadata.LastIndexOf('|');
                    if (indexOf >= 0) {
                        clippingTypeLocation = metadata[..(indexOf - 1)];
                    }
                    indexOf = clippingTypeLocation.LastIndexOf('|');
                    var pageStr = indexOf >= 0 ? clippingTypeLocation[(indexOf)..] : clippingTypeLocation;
                    var pageNumber = -1;
                    var pagePattern = @"\d+(-\d+)?";
                    Match pageMatch = Regex.Match(pageStr, pagePattern);
                    var isPageMatched = pageMatch.Success;
                    var pageRomanPattern = @"^(M{0,3})(CM|CD|D?C{0,3})(XC|XL|L?X{0,3})(IX|IV|V?I{0,3})$";
                    var isRomanMatched = Regex.IsMatch(pageStr, pageRomanPattern);
                    var isPageParsed = false;
                    if (isPageMatched && pageMatch.Success) {
                        var strMatched = pageMatch.Value;
                        var split = strMatched.Split("-");
                        if (split.Length > 1) {
                            strMatched = strMatched.Split("-")[1];
                        }
                        strMatched = strMatched.Replace("#", "");
                        strMatched = strMatched.Split("）")[0];
                        isPageParsed = int.TryParse(strMatched, out pageNumber);
                    } else if (isRomanMatched) {
                        var strMatched = StringHelper.RomanToInteger(pageStr).ToString();
                        isPageParsed = int.TryParse(strMatched, out pageNumber);
                    }
                    if (!isPageParsed || pageNumber == -1 || pageNumber == 0) {
                        skipCounts.PageFailed++;
                        continue;
                    }
                    clipping.PageNumber = pageNumber;
                    clipping.ClippingTypeLocation = clippingTypeLocation;

                    // 多语言日期解析(MyClippingsHelper.TryParseClippingDate):清洗前缀/星期词 →
                    // 原有三种精确格式优先(零回归)→ 11 个 Kindle 文化轮询兜底(原版 Kindle Mate 做法)。
                    var metaSplit = metadata.Split('|');
                    if (!MyClippingsHelper.TryParseClippingDate(metaSplit[^1], out var parsedDate)) {
                        skipCounts.DateFailed++;
                        continue;
                    }
                    var clippingDate = parsedDate.ToString("yyyy-MM-dd HH:mm:ss");
                    clipping.ClippingDate = clippingDate;

                    var key = clippingDate + "|" + clippingTypeLocation;
                    if (allClippingsKeys.Contains(key) || !isRebuild && originalKeys.Contains(key)) {
                        continue;
                    }

                    clipping.Key = key;

                    if (contentByKey.TryGetValue(key, out var contents) && 
                        contents.Any(c => c.Contains(content))) {
                        continue;
                    }
                
                    clipping.BriefType = (long)MyClippingsHelper.ParseEntryType(metadata);
                    if (clipping.BriefType == (long)BriefType.Bookmark) {
                        continue;
                    }
                    if (clipping.BriefType == (long)BriefType.Note) {
                        _ = SetClippingsBriefTypeHide(clipping.BookName, pageNumber);
                    }
                
                    clipping.Content = content;

                    listAddClippings.Add(clipping);

                    // ★ 同步判重集合:此前这三个集合只在开批前从库里取一次,批内不再更新,
                    //   于是同一份源文件里出现**重复 key** 时两条都会进批次,插入时撞
                    //   UNIQUE(clippings.key) 导致**整批导入失败**(用户可见「导入失败」弹窗)。
                    //   key = 日期|位置,两条内容不同但同日期同位置的条目就会算出同一个 key。
                    //   KMate 导入路径(KMDatabaseService)早已这么做,这里补齐。
                    allClippingsKeys.Add(key);
                    if (contentByKey.TryGetValue(key, out var acceptedContents)) {
                        acceptedContents.Add(content);
                    } else {
                        contentByKey[key] = [content];
                    }
                    if (!isRebuild) {
                        originalKeys.Add(key);
                    }

                    if (!isRebuild) {
                        listAddOriginalClippings.Add(new OriginalClippingLine {
                            Key = key, 
                            Line1 = header, 
                            Line2 = metadata,
                            Line3 = string.Empty,
                            Line4 = content,
                            Line5 = myClipping.Delimiter
                        });
                    }
                } catch (Exception e) {
                    AppLog.Write(StringHelper.GetExceptionMessage(nameof(HandleClippings), e));
                }
            }

            if (listAddClippings.Count > 0) {
                progress?.Report(new OperationProgress(OperationStage.Writing, 0, listAddClippings.Count));
                insertResult = clippingRepository.Add(listAddClippings);
                progress?.Report(new OperationProgress(OperationStage.Writing, listAddClippings.Count, listAddClippings.Count));
            }

            if (listAddOriginalClippings.Count > 0) {
                originalClippingLineRepository.Add(listAddOriginalClippings);
            }

            return insertResult;
        }
        
        private bool SetClippingsBriefTypeHide(string bookName, int pageNumber) {
            try {
                if (string.IsNullOrEmpty(bookName))
                    return true;

                var clippings = clippingRepository.GetByBookNameAndPageNumber(bookName, pageNumber);

                if (clippings.Count <= 0) {
                    return false;
                }
                Clipping clipping = clippings[0];
                var book = clipping.BookName;
                var page = clipping.PageNumber;
                if (!bookName.Equals(book) || !pageNumber.Equals(page)) {
                    return false;
                }
                clippingRepository.UpdateBriefTypeByKey(new Clipping {
                    Key = clipping.Key,
                    BriefType = (long)BriefType.Hide
                });
                return true;
            } catch (Exception e) {
                AppLog.Write(StringHelper.GetExceptionMessage(nameof(SetClippingsBriefTypeHide), e));
                return false;
            }
        }

        public bool UpdateFrequency(IProgress<OperationProgress>? progress = null) {
            // 读全表 + 批量回写,单条开销稳定,整体报一次阶段即可
            progress?.Report(OperationProgress.At(OperationStage.Preparing));
            var lookups = lookupRepository.GetAll();
            var frequencyMap = lookups
                .Where(l => !string.IsNullOrWhiteSpace(l.WordKey))
                .GroupBy(l => l.WordKey!.Trim())
                .ToDictionary(g => g.Key, g => g.Count());

            // Collect every row first, then push frequencies in ONE batched,
            // transaction-wrapped call instead of one connection + UPDATE per vocab.
            var vocabs = vocabRepository.GetAll();
            var updates = new List<Vocab>(vocabs.Count);
            foreach (Vocab vocab in vocabs) {
                if (vocab.WordKey == null) {
                    continue;
                }
                frequencyMap.TryGetValue(vocab.WordKey, out var frequency);
                updates.Add(new Vocab {
                    WordKey = vocab.WordKey,
                    Frequency = frequency,
                    Id = vocab.Id,
                    Word = vocab.Word,
                });
            }
            if (updates.Count > 0) {
                progress?.Report(new OperationProgress(OperationStage.Writing, 0, updates.Count));
                vocabRepository.UpdateFrequencyByWordKey(updates);
                progress?.Report(new OperationProgress(OperationStage.Writing, updates.Count, updates.Count));
            }
            return true;
        }
        
        // —— 回收站(2026-09-13 新增功能;原版无此概念) ——
        //
        // 语义:回收站 = 「原始行仍在、但 clippings 里已不存在」的条目 ——
        // 与原版状态栏"已删除 N 条"的统计口径**完全一致**(origin 行数 − clippings 行数)。
        // 因此无需新增 schema:original_clipping_lines 本身就是每条目的原始 5 行快照。

        /// <summary>回收站内容(已删除、可恢复的条目)。</summary>
        public List<OriginalClippingLine> GetDeletedOriginalLines() {
            var liveKeys = clippingRepository.GetAll().Select(c => c.Key).ToHashSet(StringComparer.Ordinal);
            return originalClippingLineRepository.GetAll()
                .Where(line => !liveKeys.Contains(line.Key))
                .ToList();
        }

        /// <summary>
        /// 从回收站恢复一条标注。
        /// **复用导入的解析路径**(<c>isRebuild: true</c>)—— 这样解析口径与导入完全一致,
        /// 且不会重复写入原始行(它本来就在,这正是"已删除"的判据)。
        /// 返回 true 表示确实插回了一条。
        /// </summary>
        public bool RestoreFromOriginalLine(OriginalClippingLine originalLine) {
            if (originalLine == null) return false;

            var entries = new List<MyClipping> {
                new() {
                    Header = originalLine.Line1 ?? string.Empty,
                    Metadata = originalLine.Line2 ?? string.Empty,
                    Content = originalLine.Line4 ?? string.Empty,
                    Delimiter = originalLine.Line5 ?? "=========="
                }
            };
            return HandleClippings(entries, out _, isRebuild: true) > 0;
        }

        /// <summary>彻底删除回收站中的条目(只删传入的 key,不影响仍在使用的原始行)。</summary>
        public int PurgeDeletedOriginalLines(IEnumerable<string> keys) {
            var removed = 0;
            foreach (var key in keys) {
                if (string.IsNullOrWhiteSpace(key)) continue;
                originalClippingLineRepository.Delete(key);
                removed++;
            }
            return removed;
        }

        public bool CleanDatabase(string databaseFilePath, out Dictionary<string, string> result,
            IProgress<OperationProgress>? progress = null) {
            // 清理最耗时的是判重扫描与随后的 VACUUM,都在这两个阶段里上报
            progress?.Report(OperationProgress.At(OperationStage.Preparing));
            var clippings = clippingRepository.GetAll();

            try {
                // 路径可能为空:导入流程收尾的清理只关心数据卫生(空内容/重复项),不关心文件体积,
                // 调用方传的是 string.Empty。此前直接 new FileInfo("") 会抛
                // "The path is empty",导致导入在最后一步整个失败。
                // 文件不存在时同样跳过体积统计,但不影响清理本身。
                FileInfo? fileInfo = null;
                long originFileSize = 0;
                if (!string.IsNullOrWhiteSpace(databaseFilePath)) {
                    var candidate = new FileInfo(databaseFilePath);
                    if (candidate.Exists) {
                        fileInfo = candidate;
                        originFileSize = candidate.Length;
                    }
                }
                
                progress?.Report(OperationProgress.At(OperationStage.Writing));
                var emptyClippings = clippings.Where(c => string.IsNullOrWhiteSpace(c.Content) || string.IsNullOrWhiteSpace(c.BookName)).ToList();
                var emptyCount = clippingRepository.Delete(emptyClippings);
                
                // Duplicate detection: a clipping (non-blank key + content) is "duplicated"
                // when more than one row's content CONTAINS its content as a substring
                // (the row itself always matches, so one exact copy or one longer
                // container is enough to flag it). The old implementation re-scanned the
                // whole list with a Contains() count per candidate — O(n²) string scans.
                // Equivalent result, but: exact duplicates resolved by grouping (O(n)),
                // containment checks run only on remaining unique contents with length
                // pruning and early exit.
                var duplicatedClippings = FindDuplicatedClippings(clippings, progress);

                var duplicatedCount = clippingRepository.Delete(duplicatedClippings);

                // 让「回收体积」成为真实值:
                // SQLite 的 DELETE 只把页归还空闲列表,文件大小通常不变 —— 必须 VACUUM 才会真正收缩。
                // 仅在确有删除、且路径有效时才执行,避免无谓的全库重写(大库上这步不便宜);
                // "无需清理"的路径保持瞬时。注意此处必须只 VACUUM 一次 ——
                // 重复 VACUUM 会白白重写两遍整个库文件。
                if (fileInfo != null && emptyCount + duplicatedCount > 0) {
                    DatabaseHelper.VacuumDatabase(databaseFilePath);
                    // FileInfo.Length 首次读取后会缓存,不 Refresh 就永远读到旧值 —— 这正是此前恒为 0 的原因。
                    fileInfo.Refresh();
                }

                var newFileSize = fileInfo?.Length ?? 0;
                var fileSizeDelta = originFileSize - newFileSize;

                if (emptyCount == 0 && duplicatedCount == 0) {
                    throw new Exception(AppConstants.DatabaseNoNeedCleaning);
                }
                
                result = new Dictionary<string, string> {
                    { AppConstants.EmptyCount, emptyCount.ToString() },
                    { AppConstants.DuplicatedCount, duplicatedCount.ToString() },
                    { AppConstants.FileSizeDelta, StringHelper.FormatFileSize(fileSizeDelta) }
                };
                return true;
            } catch (Exception e) {
                AppLog.Write(StringHelper.GetExceptionMessage(nameof(CleanDatabase), e));
                result = new Dictionary<string, string> {
                    { AppConstants.Exception, e.Message }
                };
                return false;
            }
        }
        
        /// <summary>
        /// Equivalent to the previous O(n²) predicate
        /// <c>clippings.Count(x =&gt; x.Content != null &amp;&amp; x.Content.Contains(c.Content)) &gt; 1</c>
        /// but without rescanning the whole list per candidate:
        /// 1. exact-content duplicates are found by grouping (O(n));
        /// 2. remaining unique contents only need ONE strictly-longer container row to be
        ///    flagged, checked over distinct contents with length pruning + early exit.
        /// Semantics are preserved: every row with non-null content can serve as the
        /// "other row" that makes a candidate duplicated (including rows with a blank key
        /// that are themselves never deleted).
        /// </summary>
        private static List<Clipping> FindDuplicatedClippings(List<Clipping> clippings,
            IProgress<OperationProgress>? progress = null) {
            var candidates = clippings
                .Where(c => !string.IsNullOrWhiteSpace(c.Key) && !string.IsNullOrWhiteSpace(c.Content))
                .ToList();
            var duplicated = new List<Clipping>();
            if (candidates.Count == 0) {
                return duplicated;
            }

            var rowsByContent = candidates
                .GroupBy(c => c.Content!, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

            // Equality count over ALL non-null-content rows (not just candidates).
            var poolCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (Clipping c in clippings) {
                if (c.Content == null) continue;
                poolCounts.TryGetValue(c.Content, out var count);
                poolCounts[c.Content] = count + 1;
            }

            var remaining = new List<KeyValuePair<string, List<Clipping>>>();
            foreach (var group in rowsByContent) {
                if (poolCounts.TryGetValue(group.Key, out var equalCount) && equalCount > 1) {
                    duplicated.AddRange(group.Value);
                } else {
                    remaining.Add(group);
                }
            }
            if (remaining.Count == 0) {
                return duplicated;
            }

            // Distinct container contents, longest first — iteration stops as soon as a
            // container is no longer longer than the target.
            var containers = poolCounts.Keys
                .OrderByDescending(s => s.Length)
                .ToArray();

            // Shortest targets first so shallow hits are found quickly.
            remaining.Sort((a, b) => a.Key.Length.CompareTo(b.Key.Length));
            var scanned = 0;
            foreach (var (content, rows) in remaining) {
                scanned++;
                // 判重扫描是清理里最耗时的一段(每条候选都要与更长的内容做包含判断),
                // 每 200 条上报一次确定进度。
                if (scanned % 200 == 0) {
                    progress?.Report(new OperationProgress(OperationStage.Preparing, scanned, remaining.Count));
                }
                foreach (var container in containers) {
                    if (container.Length <= content.Length) {
                        break; // descending order: nothing after this can contain the target
                    }
                    if (container.IndexOf(content, StringComparison.Ordinal) >= 0) {
                        duplicated.AddRange(rows); // one containing row ⇒ count > 1
                        break;
                    }
                }
            }
            return duplicated;
        }

        public bool IsDatabaseEmpty() {
            try {
                var result = 0;
                result += clippingRepository.GetCount();
                result += originalClippingLineRepository.GetCount();
                result += lookupRepository.GetCount();
                result += vocabRepository.GetCount();
                return result == 0;
            } catch (Exception e) {
                AppLog.Write(StringHelper.GetExceptionMessage(nameof(IsDatabaseEmpty), e));
                throw;
            }
        }
        
        public bool DeleteAllData() {
            try {
                var table = new List<string>();
                if (!clippingRepository.DeleteAll()) {
                    table.Add("clippings");
                }
                if (!lookupRepository.DeleteAll()) {
                    table.Add("lookups");
                }
                if (!originalClippingLineRepository.DeleteAll()) {
                    table.Add("original_clipping_lines");
                }
                if (!settingRepository.DeleteAll()) {
                    table.Add("settings");
                }
                if (!vocabRepository.DeleteAll()) {
                    table.Add("vocab");
                }
                return table.Count == 0 ? true : throw new Exception($"Clear table [{string.Join(", ", table)}] failed.");
            } catch (Exception e) {
                AppLog.Write(StringHelper.GetExceptionMessage(nameof(DeleteAllData), e));
                return false;
            }
        }
    }
}