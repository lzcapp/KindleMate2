using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Domain.Entities.MyClippings;
using KindleMate2.Shared.Constants;
using System.Globalization;
using System.Text.RegularExpressions;

namespace KindleMate2.Infrastructure.Helpers {
    public static class MyClippingsHelper {
        public static Header ParseTitleAndAuthor(string input) {
            try {
                if (string.IsNullOrEmpty(input)) {
                    throw new Exception("Input is null or empty.");
                }

                // 2. Check if the string ends with a valid closing parenthesis
                if (!input.EndsWith(Symbols.ClosingParenthesis) && !input.EndsWith(Symbols.ClosingParenthesisChinese)) {
                    var indexOfHyphen = input.LastIndexOf(Symbols.Hyphen);
                    if (indexOfHyphen != -1) {
                        var author = input.Substring(indexOfHyphen + 1, input.Length - indexOfHyphen - 1).Trim();
                        var book = input[..indexOfHyphen].Trim();
                        return new Header() {
                            Title = book,
                            Author = author,
                        };
                    } else {
                        throw new Exception("No valid author name found.");
                    }
                }

                var countNestedChineseParentheses = 0;
                var countNestedEnglishParentheses = 0;

                for (var i = input.Length - 2; i >= 0; i--) {
                    var c = input[i];
                    switch (c) {
                        case Symbols.ClosingParenthesisChinese:
                            countNestedChineseParentheses++;
                            break;
                        case Symbols.ClosingParenthesis:
                            countNestedEnglishParentheses++;
                            break;
                    }

                    string author;
                    string book;
                    if (c == Symbols.OpeningParenthesisChinese) {
                        if (countNestedChineseParentheses == 0 && input.EndsWith(Symbols.ClosingParenthesisChinese)) {
                            author = input.Substring(i + 1, input.Length - i - 2).Trim();
                            book = input[..i].Trim();
                            return new Header() {
                                Title = book,
                                Author = author,
                            };
                        } else {
                            countNestedChineseParentheses--;
                        }
                    } else if (c == Symbols.OpeningParenthesis) {
                        if (countNestedEnglishParentheses == 0 && input.EndsWith(Symbols.ClosingParenthesis)) {
                            author = input.Substring(i + 1, input.Length - i - 2).Trim();
                            book = input[..i].Trim();
                            return new Header() {
                                Title = book,
                                Author = author,
                            };
                        } else {
                            countNestedEnglishParentheses--;
                        }
                    }
                }
                throw new Exception("No valid author name found.");
            } catch (Exception e) {
                Console.WriteLine(StringHelper.GetExceptionMessage(nameof(ParseTitleAndAuthor) + " input: " + input, e));
                return new Header() {
                    Title = input,
                    Author = string.Empty,
                };
            }
        }

        private static Metadata ParseMetadata(string input) {
            var result = new Metadata();
            try {
                if (string.IsNullOrEmpty(input)) {
                    throw new Exception("Input is null or empty.");
                }
                var sections = BriefTypeTranslations.Dividers.Aggregate(input, (str, token) => str.Replace(token, "|")).Split('|').Select(s => s.Trim()).ToList();

                if (sections.Count < 2) {
                    throw new Exception($"Invalid metadata entry. Expected a page and/or location and created date entry: {input}");
                }

                var firstSection = sections[0];
                var secondSection = sections.Count > 1 ? sections[1] : string.Empty;

                result.Type = ParseEntryType(input);
                result.DateOfCreation = ParseToUtcDate(sections.Last());

                Location location = ParseLocation(firstSection);
                result.Page = location.Page;
                result.Location = location;
            } catch (Exception e) {
                Console.WriteLine(e);
            }
            return result;
        }

        public static BriefType ParseEntryType(string pageMetadata) {
            var pageMetaDate = pageMetadata.ToLower();
            if (BriefTypeTranslations.Note.Any(token => pageMetaDate.Contains(token))) {
                return BriefType.Note;
            } else if (BriefTypeTranslations.Highlight.Any(token => pageMetaDate.Contains(token))) {
                return BriefType.Highlight;
            } else if (BriefTypeTranslations.Bookmark.Any(token => pageMetaDate.Contains(token))) {
                return BriefType.Bookmark;
            } else if (BriefTypeTranslations.Cut.Any(token => pageMetaDate.Contains(token))) {
                return BriefType.Cut;
            }
            return BriefType.Unknown;
        }

        private static DateTime? ParseToUtcDate(string serializedDate) {
            // Remove common date prefixes from different Kindle language systems
            var cleanedDate = serializedDate
                .Replace("Added on", "")
                .Replace("添加于", "")
                .Trim();
            
            // Remove day of week prefix if present (for English format like "Monday, 19 May 2025")
            var commaIndex = cleanedDate.IndexOf(',');
            if (commaIndex > 0 && commaIndex < 20) { // Reasonable day of week length check
                cleanedDate = cleanedDate.Substring(commaIndex + 1).Trim();
            }
            
            // Remove Chinese day of week text (星期) if present
            var dayOfWeekIndex = cleanedDate.IndexOf("星期", StringComparison.Ordinal);
            if (dayOfWeekIndex != -1) {
                cleanedDate = cleanedDate.Remove(dayOfWeekIndex, 3); // Remove "星期X" (3 characters)
            }
            
            if (DateTime.TryParse(cleanedDate, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date))
                return date;

            foreach (var culture in new[] { "en-US", "it-IT", "fr-FR", "es-ES", "pt-PT", "zh-CN" }) {
                if (DateTime.TryParse(cleanedDate, new CultureInfo(culture), DateTimeStyles.AssumeUniversal, out date))
                    return date;
            }

            return null;
        }

        public static Location ParseLocation(string input) {
            var result = new Location();
            try {
                try {
                    Match matchLocation = Regex.Match(input, @"(\d+)-(\d+)");
                    if (matchLocation.Success) {
                        var from = int.Parse(matchLocation.Groups[1].Value);
                        var to = int.Parse(matchLocation.Groups[2].Value);
                        result.From = from;
                        result.To = to;
                        input = input.Replace(matchLocation.Value, string.Empty);
                    }
                } catch (Exception e) {
                    Console.WriteLine(StringHelper.GetExceptionMessage(nameof(ParseLocation) + ": match location", e));
                }
                Match matchPage = Regex.Match(input, @"(\d+)");
                if (matchPage.Success) {
                    var page = int.Parse(matchPage.Groups[1].Value);
                    result.Page = page;
                }
            } catch (Exception e) {
                Console.WriteLine(StringHelper.GetExceptionMessage(nameof(ParseLocation), e));
            }
            return result;
        }
        
        private static readonly List<string> ClippingLimitReachedWarning = [
            "You have reached the clipping limit for this item",
            "您已达到本内容的剪贴上限"
        ];
        
        public static bool IsClippingLimitReached(string content) {
            return ClippingLimitReachedWarning.Any(content.Contains);
        }
    }
}