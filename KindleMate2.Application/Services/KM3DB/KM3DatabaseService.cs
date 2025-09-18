using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Domain.Entities.MyClippings;
using KindleMate2.Domain.Interfaces.KM2DB;
using KindleMate2.Infrastructure.Helpers;
using KindleMate2.Shared;
using KindleMate2.Shared.Constants;
using System.Globalization;
using System.Text.RegularExpressions;

namespace KindleMate2.Application.Services.KM3DB {
    public class KM3DatabaseService {
        private readonly IClippingRepository _clippingRepository;
        private readonly ILookupRepository _lookupRepository;
        private readonly IOriginalClippingLineRepository _originalClippingLineRepository;
        private readonly ISettingRepository _settingRepository;
        private readonly IVocabRepository _vocabRepository;

        public Km2DatabaseService(IClippingRepository clippingRepository, ILookupRepository lookupRepository, IOriginalClippingLineRepository originalClippingLineRepository, ISettingRepository settingRepository, IVocabRepository vocabRepository) {
            _clippingRepository = clippingRepository;
            _lookupRepository = lookupRepository;
            _originalClippingLineRepository = originalClippingLineRepository;
            _settingRepository = settingRepository;
            _vocabRepository = vocabRepository;
        }

        public bool ImportKindleClippings(string clippingsPath, out Dictionary<string, string> result) {
            try {
                List<string> lines = [
                    .. File.ReadAllLines(clippingsPath)
                ];

                var delimiterIndex = new List<int>();

                for (var i = 0; i < lines.Count; i++) {
                    lines[i] = StringHelper.RemoveControlChar(lines[i]);
                    if (lines[i].StartsWith("===") && lines[i - 2].Trim().Equals("") && lines[i].EndsWith("===")) {
                        delimiterIndex.Add(i);
                    }
                }

                var myClippings = new List<MyClipping>();
                
                for (var i = 0; i < delimiterIndex.Count; i++) {
                    var ceilDelimiter = i == 0 ? -1 : delimiterIndex[i - 1];
                    var florDelimiter = delimiterIndex[i];

                    var line1 = lines[ceilDelimiter + 1].Trim();
                    var line2 = lines[ceilDelimiter + 2].Trim();
                    //  line3 should be empty
                    var line4 = lines[ceilDelimiter + 4].Trim();
                    if (ceilDelimiter + 5 == ceilDelimiter) {
                        // then line4 is the rest
                        for (var index = ceilDelimiter + 3; index < florDelimiter; index++) {
                            line4 += lines[index];
                            if (index < florDelimiter - 1) {
                                line4 += Environment.NewLine;
                            }
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

                var insertedCount = HandleClippings(myClippings);

                result = new Dictionary<string, string> {
                    { AppConstants.ParsedCount, delimiterIndex.Count.ToString() },
                    { AppConstants.InsertedCount, insertedCount.ToString() }
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
                
                var originalClippingLines = _originalClippingLineRepository.GetAll();
                if (originalClippingLines.Count <= 0) {
                    throw new Exception(Strings.Database_Empty);
                }
            
                _clippingRepository.DeleteAll();
            
                var myClippings = new List<MyClipping>();
                foreach (OriginalClippingLine originalClippingLine in originalClippingLines) {
                    var line1 = originalClippingLine.line1;
                    var line2 = originalClippingLine.line2;
                    var line4 = originalClippingLine.line4;
                    var line5 = originalClippingLine.line5;
                    if (string.IsNullOrWhiteSpace(line1) || string.IsNullOrWhiteSpace(line2) || string.IsNullOrWhiteSpace(line4) || string.IsNullOrWhiteSpace(line5)) {
                        continue;
                    }
                    myClippings.Add(new MyClipping {
                        Header = line1,
                        Metadata = line2,
                        Content = line4,
                        Delimiter = line5
                    });
                }
            
                var insertedCount = HandleClippings(myClippings, isRebuild: true);

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

        private int HandleClippings(List<MyClipping> clippings, bool isRebuild = false) {
            var insertResult = 0;

            var allClippings = _clippingRepository.GetAll();
            var allClippingsKeys = allClippings.Select(c => c.Key).ToHashSet();
            var originalKeys = _originalClippingLineRepository.GetAllKeys();
            
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

                    if (string.IsNullOrWhiteSpace(content) || MyClippingsHelper.IsClippingLimitReached(content)) {
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
                    var isPageMatched = Regex.IsMatch(pageStr, pagePattern);
                    var pageRomanPattern = @"^(M{0,3})(CM|CD|D?C{0,3})(XC|XL|L?X{0,3})(IX|IV|V?I{0,3})$";
                    var isRomanMatched = Regex.IsMatch(pageStr, pageRomanPattern);
                    var isPageParsed = false;
                    if (isPageMatched) {
                        var regex = new Regex(pagePattern);
                        var strMatched = regex.Matches(pageStr)[0].Value;
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
                        continue;
                    }
                    clipping.PageNumber = pageNumber;
                    clipping.ClippingTypeLocation = clippingTypeLocation;

                    string clippingDate;
                    var metaSplit = metadata.Split('|');
                    var datetime = metaSplit[^1].Replace("Added on", "").Replace("添加于", "").Trim();
                    datetime = datetime[(datetime.IndexOf(',') + 1)..].Trim();
                    var isDateParsed = DateTime.TryParseExact(datetime, "MMMM d, yyyy h:m:s tt", CultureInfo.GetCultureInfo("en-US"), DateTimeStyles.None, out DateTime parsedDate);
                    if (!isDateParsed) {
                        var dayOfWeekIndex = datetime.IndexOf("星期", StringComparison.Ordinal);
                        if (dayOfWeekIndex != -1) {
                            datetime = datetime.Remove(dayOfWeekIndex, 3);
                        }
                        isDateParsed = DateTime.TryParseExact(datetime, "yyyy年M月d日 tth:m:s", CultureInfo.GetCultureInfo("zh-CN"), DateTimeStyles.None, out parsedDate);
                    }
                    if (isDateParsed && parsedDate != DateTime.MinValue) {
                        clippingDate = parsedDate.ToString("yyyy-MM-dd HH:mm:ss");
                    } else {
                        continue;
                    }
                    clipping.ClippingDate = clippingDate;

                    var key = clippingDate + "|" + clippingTypeLocation;
                    if (allClippingsKeys.Contains(key) || !isRebuild && originalKeys.Contains(key)) {
                        continue;
                    }

                    clipping.Key = key;

                    if (allClippings.Any(c => c.Key.Equals(key) && c.Content.Contains(content))) {
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
                            key = key, 
                            line1 = header, 
                            line2 = metadata,
                            line4 = content
                        });
                    }
                } catch (Exception e) {
                    Console.WriteLine(StringHelper.GetExceptionMessage(nameof(HandleClippings), e));
                }
            }

            if (listAddClippings.Count > 0) {
                insertResult = _clippingRepository.Add(listAddClippings);
            }

            if (listAddOriginalClippings.Count > 0) {
                _originalClippingLineRepository.Add(listAddOriginalClippings);
            }

            return insertResult;
        }
        
        private bool SetClippingsBriefTypeHide(string bookName, int pageNumber) {
            try {
                switch (bookName) {
                    case null:
                    case "":
                        return true;
                }

                var clippings = _clippingRepository.GetByBookNameAndPageNumber(bookName, pageNumber);

                if (clippings.Count <= 0) {
                    return false;
                }
                Clipping clipping = clippings[0];
                var book = clipping.BookName;
                var page = clipping.PageNumber;
                if (!bookName.Equals(book) || !pageNumber.Equals(page)) {
                    return false;
                }
                _clippingRepository.UpdateBriefTypeByKey(new Clipping {
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
            try {
                var lookups = _lookupRepository.GetAll();
                var frequencyMap = lookups
                    .Where(l => !string.IsNullOrWhiteSpace(l.WordKey))
                    .GroupBy(l => l.WordKey!.Trim())
                    .ToDictionary(g => g.Key, g => g.Count());

                var vocabs = _vocabRepository.GetAll();
                foreach (Vocab vocab in vocabs) {
                    if (vocab.WordKey == null) {
                        continue;
                    }
                    frequencyMap.TryGetValue(vocab.WordKey, out var frequency);
                    _vocabRepository.UpdateFrequencyByWordKey(new Vocab {
                        WordKey = vocab.WordKey,
                        Frequency = frequency,
                        Id = string.Empty,
                        Word = string.Empty, // frequency will be 0 if key not found
                    });
                }
                return true;
            } catch (Exception e) {
                Console.WriteLine(StringHelper.GetExceptionMessage(nameof(UpdateFrequency), e));
                return false;
            }
        }
        
        public bool CleanDatabase(string databaseFilePath, out Dictionary<string, string> result) {
            var clippings = _clippingRepository.GetAll();

            try {
                var fileInfo = new FileInfo(databaseFilePath);
                var originFileSize = fileInfo.Length;
                
                var emptyClippings = clippings.Where(c => string.IsNullOrWhiteSpace(c.Content) || string.IsNullOrWhiteSpace(c.BookName)).ToList();
                var emptyCount = _clippingRepository.Delete(emptyClippings);
                
                var duplicatedClippings = new List<Clipping>();

                foreach (Clipping clipping in clippings) {
                    var key = clipping.Key;
                    var content = clipping.Content;

                    if (string.IsNullOrWhiteSpace(key)) {
                        continue;
                    }
                    
                    if (clippings.Count(c => c.Content.Contains(content))  > 1) {
                        duplicatedClippings.Add(clipping);
                    }
                }
                
                var duplicatedCount = _clippingRepository.Delete(duplicatedClippings);
                
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
        
        public bool IsDatabaseEmpty() {
            try {
                var result = 0;
                result += _clippingRepository.GetCount();
                result += _originalClippingLineRepository.GetCount();
                result += _lookupRepository.GetCount();
                result += _vocabRepository.GetCount();
                return result == 0;
            } catch (Exception e) {
                Console.WriteLine(StringHelper.GetExceptionMessage(nameof(IsDatabaseEmpty), e));
                throw;
            }
        }
        
        public bool DeleteAllData() {
            try {
                var table = new List<string>();
                if (!_clippingRepository.DeleteAll()) {
                    table.Add("clippings");
                }
                if (!_lookupRepository.DeleteAll()) {
                    table.Add("lookups");
                }
                if (!_originalClippingLineRepository.DeleteAll()) {
                    table.Add("original_clipping_lines");
                }
                if (!_settingRepository.DeleteAll()) {
                    table.Add("settings");
                }
                if (!_vocabRepository.DeleteAll()) {
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