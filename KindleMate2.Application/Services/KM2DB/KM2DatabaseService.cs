using System.Globalization;
using System.Text.RegularExpressions;
using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Domain.Entities.MyClippings;
using KindleMate2.Domain.Interfaces.KM2DB;
using KindleMate2.Infrastructure.Helpers;
using KindleMate2.Shared;
using KindleMate2.Shared.Constants;

namespace KindleMate2.Application.Services.KM2DB {
    public class Km2DatabaseService(
        IClippingRepository clippingRepository,
        ILookupRepository lookupRepository,
        IOriginalClippingLineRepository originalClippingLineRepository,
        ISettingRepository settingRepository,
        IVocabRepository vocabRepository) : IKm2DatabaseService {
        public bool ImportKindleClippings(string clippingsPath, out Dictionary<string, string> result) {
            try {
                List<string> lines = [
                    .. File.ReadAllLines(clippingsPath)
                ];

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

                var insertedCount = HandleClippings(myClippings, out var skipCounts);

                result = new Dictionary<string, string> {
                    { AppConstants.ParsedCount, delimiterIndex.Count.ToString() },
                    { AppConstants.InsertedCount, insertedCount.ToString() },
                    { AppConstants.SkippedDateCount, skipCounts.DateFailed.ToString() },
                    { AppConstants.SkippedPageCount, skipCounts.PageFailed.ToString() },
                    { AppConstants.SkippedLimitCount, skipCounts.LimitReached.ToString() }
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
                Console.WriteLine(StringHelper.GetExceptionMessage(nameof(CleanDatabase), e));
                result = new  Dictionary<string, string> {
                    { AppConstants.Exception, e.Message }
                };
                return false;
            }
        }

        private sealed class SkipCounts {
            public int LimitReached;
            public int PageFailed;
            public int DateFailed;
        }

        private int HandleClippings(List<MyClipping> clippings, out SkipCounts skipCounts, bool isRebuild = false) {
            skipCounts = new SkipCounts();
            var insertResult = 0;

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
                    Console.WriteLine(StringHelper.GetExceptionMessage(nameof(HandleClippings), e));
                }
            }

            if (listAddClippings.Count > 0) {
                insertResult = clippingRepository.Add(listAddClippings);
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
                Console.WriteLine(StringHelper.GetExceptionMessage(nameof(SetClippingsBriefTypeHide), e));
                return false;
            }
        }

        public bool UpdateFrequency() {
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
                vocabRepository.UpdateFrequencyByWordKey(updates);
            }
            return true;
        }
        
        public bool CleanDatabase(string databaseFilePath, out Dictionary<string, string> result) {
            var clippings = clippingRepository.GetAll();

            try {
                var fileInfo = new FileInfo(databaseFilePath);
                var originFileSize = fileInfo.Length;
                
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
                var duplicatedClippings = FindDuplicatedClippings(clippings);

                var duplicatedCount = clippingRepository.Delete(duplicatedClippings);
                
                DatabaseHelper.VacuumDatabase(databaseFilePath);
                
                var newFileSize = fileInfo.Length;
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
                Console.WriteLine(StringHelper.GetExceptionMessage(nameof(CleanDatabase), e));
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
        private static List<Clipping> FindDuplicatedClippings(List<Clipping> clippings) {
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
            foreach (var (content, rows) in remaining) {
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
                Console.WriteLine(StringHelper.GetExceptionMessage(nameof(IsDatabaseEmpty), e));
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
                Console.WriteLine(StringHelper.GetExceptionMessage(nameof(DeleteAllData), e));
                return false;
            }
        }
    }
}