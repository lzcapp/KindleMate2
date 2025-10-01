using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Domain.Entities.MyClippings;
using KindleMate2.Shared.Constants;
using System.Globalization;
using System.Text.RegularExpressions;

namespace KindleMate2.Infrastructure.Helpers {
    public static class MyClippingsHelper {
        /// <summary>
        /// Parses title and author from a header string that may contain parentheses or hyphens.
        /// </summary>
        /// <param name="input">The header string containing title and author information</param>
        /// <returns>A Header object with parsed title and author</returns>
        /// <exception cref="ArgumentNullException">Thrown when input is null</exception>
        public static Header ParseTitleAndAuthor(string input) {
            if (input == null) {
                throw new ArgumentNullException(nameof(input));
            }

            try {
                if (string.IsNullOrEmpty(input)) {
                    throw new ArgumentException("Input is null or empty.", nameof(input));
                }

                // Check if the string ends with a valid closing parenthesis
                if (!input.EndsWith(Symbols.ClosingParenthesis) && !input.EndsWith(Symbols.ClosingParenthesisChinese)) {
                    var indexOfHyphen = input.LastIndexOf(Symbols.Hyphen);
                    if (indexOfHyphen != -1 && indexOfHyphen < input.Length - 1) {
                        var author = input.Substring(indexOfHyphen + 1).Trim();
                        var book = input[..indexOfHyphen].Trim();
                        
                        if (!string.IsNullOrWhiteSpace(author) && !string.IsNullOrWhiteSpace(book)) {
                            return new Header {
                                Title = book,
                                Author = author,
                            };
                        }
                    }
                    
                    // If hyphen parsing fails, treat entire input as title
                    return new Header {
                        Title = input.Trim(),
                        Author = string.Empty,
                    };
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

                    if (c == Symbols.OpeningParenthesisChinese) {
                        if (countNestedChineseParentheses == 0 && input.EndsWith(Symbols.ClosingParenthesisChinese)) {
                            var author = input.Substring(i + 1, input.Length - i - 2).Trim();
                            var book = input[..i].Trim();
                            
                            if (!string.IsNullOrWhiteSpace(author) && !string.IsNullOrWhiteSpace(book)) {
                                return new Header {
                                    Title = book,
                                    Author = author,
                                };
                            }
                        } else {
                            countNestedChineseParentheses--;
                        }
                    } else if (c == Symbols.OpeningParenthesis) {
                        if (countNestedEnglishParentheses == 0 && input.EndsWith(Symbols.ClosingParenthesis)) {
                            var author = input.Substring(i + 1, input.Length - i - 2).Trim();
                            var book = input[..i].Trim();
                            
                            if (!string.IsNullOrWhiteSpace(author) && !string.IsNullOrWhiteSpace(book)) {
                                return new Header {
                                    Title = book,
                                    Author = author,
                                };
                            }
                        } else {
                            countNestedEnglishParentheses--;
                        }
                    }
                }
                
                // If parsing fails, treat entire input as title
                return new Header {
                    Title = input.Trim(),
                    Author = string.Empty,
                };
            } catch (Exception e) when (!(e is ArgumentNullException || e is ArgumentException)) {
                // For any unexpected errors, return a fallback header instead of logging to console
                // This provides more resilient behavior while preserving the input
                return new Header {
                    Title = input?.Trim() ?? string.Empty,
                    Author = string.Empty,
                };
            }
        }

        /// <summary>
        /// Parses metadata information from clipping metadata string.
        /// </summary>
        /// <param name="input">The metadata string to parse</param>
        /// <returns>A Metadata object with parsed information</returns>
        private static Metadata ParseMetadata(string input) {
            var result = new Metadata();
            
            try {
                if (string.IsNullOrEmpty(input)) {
                    throw new ArgumentException("Input is null or empty.", nameof(input));
                }
                
                var sections = BriefTypeTranslations.Dividers
                    .Aggregate(input, (str, token) => str.Replace(token, "|"))
                    .Split('|')
                    .Select(s => s.Trim())
                    .ToList();

                if (sections.Count < 2) {
                    throw new ArgumentException($"Invalid metadata entry. Expected a page and/or location and created date entry: {input}", nameof(input));
                }

                var firstSection = sections[0];
                
                result.Type = ParseEntryType(input);
                result.DateOfCreation = ParseToUtcDate(sections.Last());

                var location = ParseLocation(firstSection);
                result.Page = location.Page;
                result.Location = location;
            } catch (Exception e) when (!(e is ArgumentException)) {
                // Re-throw argument exceptions as they have specific meaning
                // For other exceptions, wrap with context but don't log to console
                throw new InvalidOperationException($"Failed to parse metadata from input '{input}': {e.Message}", e);
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
            if (DateTime.TryParse(serializedDate, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date))
                return date;

            foreach (var culture in new[] { "it-IT", "fr-FR", "es-ES", "pt-PT" }) {
                if (DateTime.TryParse(serializedDate, new CultureInfo(culture), DateTimeStyles.AssumeUniversal, out date))
                    return date;
            }

            return null;
        }

        /// <summary>
        /// Parses location information from clipping metadata string.
        /// </summary>
        /// <param name="input">The input string containing location information</param>
        /// <returns>A Location object with parsed page and range information</returns>
        /// <exception cref="ArgumentNullException">Thrown when input is null</exception>
        public static Location ParseLocation(string input) {
            if (input == null) {
                throw new ArgumentNullException(nameof(input));
            }

            var result = new Location();
            
            if (string.IsNullOrWhiteSpace(input)) {
                return result;
            }

            try {
                // Parse location range (e.g., "123-456")
                try {
                    var matchLocation = Regex.Match(input, AppConstants.LocationRangePattern, RegexOptions.Compiled);
                    if (matchLocation.Success && 
                        int.TryParse(matchLocation.Groups[1].Value, out var from) &&
                        int.TryParse(matchLocation.Groups[2].Value, out var to)) {
                        
                        result.From = from;
                        result.To = to;
                        input = input.Replace(matchLocation.Value, string.Empty);
                    }
                } catch (RegexMatchTimeoutException ex) {
                    // Log regex timeout but continue processing
                    throw new InvalidOperationException($"Regex timeout while parsing location range: {ex.Message}", ex);
                }

                // Parse single page number
                var matchPage = Regex.Match(input, AppConstants.SingleNumberPattern, RegexOptions.Compiled);
                if (matchPage.Success && int.TryParse(matchPage.Groups[1].Value, out var page)) {
                    result.Page = page;
                }
            } catch (Exception e) when (!(e is ArgumentNullException || e is InvalidOperationException)) {
                // Wrap and re-throw with more context, but preserve specific exceptions
                throw new InvalidOperationException($"Failed to parse location from input '{input}': {e.Message}", e);
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