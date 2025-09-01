using System.Globalization;
using System.Text.RegularExpressions;
using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Domain.Entities.MyClippings;
using KindleMate2.Domain.Interfaces.KM2DB;
using KindleMate2.Infrastructure.Helpers;
using KindleMate2.Shared.Constants;

namespace KindleMate2.Application.Services.KM2DB {
    public partial class KMDatabaseService {
        private readonly IClippingRepository _clippingRepository;
        private readonly ILookupRepository _lookupRepository;
        private readonly IOriginalClippingLineRepository _originalClippingLineRepository;
        private readonly IVocabRepository _vocabRepository;

        public KMDatabaseService(IClippingRepository clippingRepository, ILookupRepository lookupRepository, IOriginalClippingLineRepository originalClippingLineRepository, IVocabRepository vocabRepository) {
            _clippingRepository = clippingRepository;
            _lookupRepository = lookupRepository;
            _originalClippingLineRepository = originalClippingLineRepository;
            _vocabRepository = vocabRepository;
        }

        public void ImportFromKmDatabase(string sourceDbPath, string targetDbPath) {
            DatabaseHelper.ImportKMDatabase(sourceDbPath, targetDbPath);
        }

        public void ImportFromVocabDatabase(string sourceDbPath, string targetDbPath) {
            DatabaseHelper.ImportVocabDatabase(sourceDbPath, targetDbPath);
        }

        public void UpdateFrequency() {
            var vocabs = _vocabRepository.GetAll();
            var lookups = _lookupRepository.GetAll();

            try {
                foreach (Vocab vocab in vocabs) {
                    var wordKey = vocab.word_key;
                    var frequency = lookups.AsEnumerable().Count(lookupsRow => lookupsRow.WordKey.Trim() == wordKey);
                    _vocabRepository.UpdateFrequencyByWordKey(new Vocab {
                        word_key = wordKey,
                        frequency = frequency
                    });
                }
            } catch (Exception) {
                // ignored
            }
        }

        public bool ImportKindleClippings(string clippingsPath, out Dictionary<string, string> result) {
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

            int insertedCount;
            var myClippings = new List<MyClipping>();

            try {
                for (var i = 0; i < delimiterIndex.Count; i++) {
                    var ceilDelimiter = i == 0 ? -1 : delimiterIndex[i - 1];
                    var florDelimiter = delimiterIndex[i];

                    var line1 = lines[ceilDelimiter + 1].Trim();
                    var line2 = lines[ceilDelimiter + 2].Trim();
                    // ReSharper disable once UnusedVariable
                    var line3 = lines[ceilDelimiter + 3].Trim(); // line3 should be empty
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

                    myClippings.Add(new MyClipping() {
                        Header = line1,
                        Metadata = line2,
                        Content = line4,
                        Delimiter = line5
                    });
                }

                insertedCount = HandleClippings(myClippings);
            } catch (Exception e) {
                result = new Dictionary<string, string> {
                    { AppConstants.Exception, e.Message }
                };
                return false;
            }

            result = new Dictionary<string, string> {
                { AppConstants.ParsedCount, delimiterIndex.Count.ToString() },
                { AppConstants.InsertedCount, insertedCount.ToString() }
            };
            return true;
        }

        public int HandleClippings(List<MyClipping> clippings, bool isRebuild = false) {
            var insertResult = 0;
            
            foreach (MyClipping clipping in clippings) {
                var entityClipping = new Clipping();

                var header = clipping.Header;
                var metadata = clipping.Metadata;
                var content = clipping.Content;
                
                var brieftype = BriefType.Highlight;
                if (metadata.Contains("笔记") || metadata.Contains("Note")) {
                    brieftype = BriefType.Note;
                } else if (metadata.Contains("书签") || metadata.Contains("Bookmark")) {
                    brieftype = BriefType.Bookmark;
                } else if (metadata.Contains("文章剪切") || metadata.Contains("Cut")) {
                    brieftype = BriefType.Cut;
                }
                entityClipping.brieftype = brieftype;

                if (entityClipping.brieftype == BriefType.Bookmark) {
                    return 0;
                }

                if (content.Contains("您已达到本内容的剪贴上限")) {
                    return 0;
                }

                var split_b = metadata.Split('|');

                var clippingtypelocation = string.Empty;
                metadata = metadata.Replace("- ", "", StringComparison.InvariantCultureIgnoreCase);
                var indexOf = metadata.LastIndexOf('|');
                if (indexOf >= 0) {
                    clippingtypelocation = metadata[..(indexOf - 1)];
                }
                indexOf = clippingtypelocation.LastIndexOf('|');
                var pageStr = indexOf >= 0 ? clippingtypelocation[(indexOf)..] : clippingtypelocation;
                var pagenumber = -1;
                var pagenPattern = @"\d+(-\d+)?";
                var isPagenIsMatch = Regex.IsMatch(pageStr, pagenPattern);
                var romanPattern = @"^(M{0,3})(CM|CD|D?C{0,3})(XC|XL|L?X{0,3})(IX|IV|V?I{0,3})$";
                var isRomanMatched = Regex.IsMatch(pageStr, romanPattern);
                var isPageParsed = false;
                if (isPagenIsMatch) {
                    var regex = new Regex(pagenPattern);
                    var strMatched = regex.Matches(pageStr)[0].Value;
                    var split = strMatched.Split("-");
                    if (split.Length > 1) {
                        strMatched = strMatched.Split("-")[1];
                    }
                    strMatched = strMatched.Replace("#", "");
                    strMatched = strMatched.Split("）")[0];
                    isPageParsed = int.TryParse(strMatched, out pagenumber);
                } else if (isRomanMatched) {
                    var strMatched = StringHelper.RomanToInteger(pageStr).ToString();
                    isPageParsed = int.TryParse(strMatched, out pagenumber);
                }
                if (!isPageParsed || pagenumber == -1 || pagenumber == 0) {
                    return 0;
                }
                entityClipping.clippingtypelocation = clippingtypelocation;
                entityClipping.pagenumber = pagenumber;

                string clippingdate;
                var datetime = split_b[^1].Replace("Added on", "").Replace("添加于", "").Trim();
                datetime = datetime[(datetime.IndexOf(',') + 1)..].Trim();
                // ReSharper disable once InlineOutVariableDeclaration
                var isDateParsed = DateTime.TryParseExact(datetime, "MMMM d, yyyy h:m:s tt", CultureInfo.GetCultureInfo("en-US"), DateTimeStyles.None, out DateTime parsedDate);
                if (!isDateParsed) {
                    var dayOfWeekIndex = datetime.IndexOf("星期", StringComparison.Ordinal);
                    if (dayOfWeekIndex != -1) {
                        datetime = datetime.Remove(dayOfWeekIndex, 3);
                    }
                    isDateParsed = DateTime.TryParseExact(datetime, "yyyy年M月d日 tth:m:s", CultureInfo.GetCultureInfo("zh-CN"), DateTimeStyles.None, out parsedDate);
                }
                if (isDateParsed && parsedDate != DateTime.MinValue) {
                    clippingdate = parsedDate.ToString("yyyy-MM-dd HH:mm:ss");
                } else {
                    return 0;
                }
                entityClipping.clippingdate = clippingdate;

                var key = clippingdate + "|" + clippingtypelocation;
                if (!isRebuild) {
                    if (_originalClippingLineRepository.GetByKey(key) != null) {
                        return 0;
                    }
                    _originalClippingLineRepository.Add(new OriginalClippingLine {
                        key = key, 
                        line1 = header, 
                        line2 = metadata,
                        line4 = content
                    });
                }

                entityClipping.key = key;

                string bookname;
                string authorname;
                Match match = BookNameRegex().Match(header);
                if (match.Success) {
                    authorname = match.Groups[1].Value.Trim();
                    bookname = header.Replace(match.Groups[0].Value.Trim(), "").Trim();
                } else {
                    authorname = string.Empty;
                    bookname = header;
                }
                bookname = bookname.Trim();
                entityClipping.bookname = bookname;
                entityClipping.authorname = authorname;

                if (brieftype == BriefType.Note) {
                    SetClippingsBriefTypeHide(bookname, pagenumber);
                }

                if (_clippingRepository.GetByKeyAndContent(key, content) != null) {
                    return 0;
                }

                _clippingRepository.Add(entityClipping);
                insertResult += 1;
            }


            return insertResult;
        }

        public bool SetClippingsBriefTypeHide(string bookname, int pagenumber) {
            try {
                switch (bookname) {
                    case null:
                    case "":
                        return true;
                }

                var clippings = _clippingRepository.GetByBookNameAndPageNumber(bookname, pagenumber);

                if (clippings.Count <= 0) {
                    return false;
                }
                Clipping clipping = clippings[0];
                var book = clipping.bookname;
                var page = clipping.pagenumber;
                if (!bookname.Equals(book) || !pagenumber.Equals(page)) {
                    return false;
                }
                _clippingRepository.UpdateBriefTypeByKey(new Clipping {
                    key = clipping.key,
                    brieftype = BriefType.Hide
                });
                return true;
            } catch (Exception e) {
                Console.WriteLine(e);
                return false;
            }
        }
        

        [GeneratedRegex(@"\(([^()]+)\)[^(]*$")]
        private static partial Regex BookNameRegex();
    }
}