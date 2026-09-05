using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Domain.Entities.MyClippings;
using KindleMate2.Shared.Constants;
using System.Globalization;
using System.Text.RegularExpressions;

namespace KindleMate2.Infrastructure.Helpers {
    public static partial class MyClippingsHelper {
        /// <summary>
        /// Parses title and author from a header string that may contain parentheses or hyphens.
        /// </summary>
        /// <param name="input">The header string containing title and author information</param>
        /// <returns>A Header object with parsed title and author</returns>
        /// <exception cref="ArgumentNullException">Thrown when input is null</exception>
        public static Header ParseTitleAndAuthor(string input) {
            ArgumentNullException.ThrowIfNull(input);

            try {
                if (string.IsNullOrEmpty(input)) {
                    throw new ArgumentException("[input] is null or empty.", nameof(input));
                }

                // Check if the string ends with a valid closing parenthesis
                if (!input.EndsWith(Symbols.ClosingParenthesis) && !input.EndsWith(Symbols.ClosingParenthesisChinese)) {
                    var indexOfHyphen = input.LastIndexOf(Symbols.Hyphen);
                    if (indexOfHyphen == -1 || indexOfHyphen >= input.Length - 1) {
                        return new Header {
                            Title = input.Trim(),
                            Author = string.Empty,
                        };
                    }
                    var author = input[(indexOfHyphen + 1)..].Trim();
                    var book = input[..indexOfHyphen].Trim();
                        
                    if (!string.IsNullOrWhiteSpace(author) && !string.IsNullOrWhiteSpace(book)) {
                        return new Header {
                            Title = book,
                            Author = author,
                        };
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

                    switch (c) {
                        case Symbols.OpeningParenthesisChinese when countNestedChineseParentheses == 0 && input.EndsWith(Symbols.ClosingParenthesisChinese): {
                            var author = input.Substring(i + 1, input.Length - i - 2).Trim();
                            var book = input[..i].Trim();
                            
                            if (!string.IsNullOrWhiteSpace(author) && !string.IsNullOrWhiteSpace(book)) {
                                return new Header {
                                    Title = book,
                                    Author = author,
                                };
                            }
                            break;
                        }
                        case Symbols.OpeningParenthesisChinese:
                            countNestedChineseParentheses--;
                            break;
                        case Symbols.OpeningParenthesis when countNestedEnglishParentheses == 0 && input.EndsWith(Symbols.ClosingParenthesis): {
                            var author = input.Substring(i + 1, input.Length - i - 2).Trim();
                            var book = input[..i].Trim();
                            
                            if (!string.IsNullOrWhiteSpace(author) && !string.IsNullOrWhiteSpace(book)) {
                                return new Header {
                                    Title = book,
                                    Author = author,
                                };
                            }
                            break;
                        }
                        case Symbols.OpeningParenthesis:
                            countNestedEnglishParentheses--;
                            break;
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
                    Title = input.Trim(),
                    Author = string.Empty,
                };
            }
        }

        /// <summary>
        /// Parses metadata information from clipping metadata string.
        /// </summary>
        /// <param name="input">The metadata string to parse</param>
        /// <returns>A Metadata object with parsed information</returns>
        // ReSharper disable once UnusedMember.Local
        private static Metadata ParseMetadata(string input) {
            var result = new Metadata();
            
            try {
                if (string.IsNullOrEmpty(input)) {
                    throw new ArgumentException("[input] is null or empty.", nameof(input));
                }
                
                var sections = BriefTypeTranslations.Dividers
                    .Aggregate(input, (str, token) => str.Replace(token, "|"))
                    .Split('|')
                    .Select(s => s.Trim())
                    .ToList();

                if (sections.Count < 2) {
                    throw new ArgumentException($@"Invalid metadata entry. Expected a page and/or location and created date entry: {input}", nameof(input));
                }

                var firstSection = sections[0];
                
                result.Type = ParseEntryType(input);
                result.DateOfCreation = ParseToUtcDate(sections.Last());

                Location location = ParseLocation(firstSection);
                result.Page = location.Page;
                result.Location = location;
            } catch (Exception e) when (e is not ArgumentException) {
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

        // ── 多语言日期解析(对齐原版 Kindle Mate 1.38 的 getLine2TypeLocationAddingTime) ──

        /// <summary>
        /// 各区域 Kindle 日期行中的前缀/星期/冗余词,解析前逐项删除(原版 StringToDelete 25 项;
        /// 另补德语 Hinzugefügt 的变体与俄语日期常用词,删除均忽略大小写)。
        /// </summary>
        private static readonly string[] KindleDateCleaningTokens = [
            "星期一", "星期二", "星期三", "星期四", "星期五", "星期六", "星期日",
            "Added on ", "添加于", "已添加至",
            "Hinzugefügt am ", "Hinzugefügt pm ", "作成日: ",
            "Añadido el ", "Ajouté le ", "Ajouté ll ",
            "日曜日", "月曜日", "火曜日", "水曜日", "木曜日", "金曜日", "土曜日",
            "Dodany w dn. ", "Toegevoegd op ", "à",
            // 意/葡真实前缀(KindleToJoplin languages.ts example: "Aggiunto il domenica…" / "Adicionado em domingo…")
            "Aggiunto il ", "Adicionado em ",
            // 德语时间短语(真实样本 "…4.46 Uhr GMT+07:29" / "…um 01:31:13 Uhr",SuzanaK·becausecurious 公开样本)。
            // 注意:删除 token 若含两侧空格会把词前后空格一并吃掉导致粘连("2012 um 01"→"201201"),
            // 故 "um " 只带单侧空格,清理后再折叠空白。
            " Uhr", "um ",
            // 各语言星期词(.NET DateTime.TryParse 对带星期前缀的非标准串并不宽松 → 一并删除)
            "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday",
            "Sonntag", "Montag", "Dienstag", "Mittwoch", "Donnerstag", "Freitag", "Samstag",
            "dimanche", "lundi", "mardi", "mercredi", "jeudi", "vendredi", "samedi",
            "domingo", "lunes", "martes", "miércoles", "jueves", "viernes", "sábado",
            "domenica", "lunedì", "martedì", "mercoledì", "giovedì", "venerdì", "sabato",
            "zondag", "maandag", "dinsdag", "woensdag", "donderdag", "vrijdag", "zaterdag",
            "niedzielę", "niedziela", "poniedziałek", "wtorek", "środa", "czwartek", "piątek", "sobota"
        ];

        /// <summary>Kindle 设备区域文化(原版 10 个 + ru-RU),用于轮询解析。</summary>
        private static readonly string[] KindleDateCultures = [
            "zh-CN", "en-US", "ja-JP", "pl-PL", "de-DE", "fr-FR", "es-ES", "it-IT", "pt-PT", "nl-NL", "ru-RU"
        ];

        private static readonly CultureInfo EnUs = CultureInfo.GetCultureInfo("en-US");
        private static readonly CultureInfo ZhCn = CultureInfo.GetCultureInfo("zh-CN");

        /// <summary>
        /// 解析 Kindle 区域化的日期行(形如 "Added on Sunday, May 19, 2025, 10:20:31 PM" /
        /// "添加于 2025年5月19日 星期一 下午10:20:31" / "作成日: 2025年5月19日 22:20:31" …)。
        /// 策略(借鉴原版):① 清洗前缀/星期词 → ② 优先尝试原有三种精确格式(零回归)→ ③ 按 11 个
        /// Kindle 文化逐轮 <see cref="DateTime.TryParse(string, IFormatProvider, DateTimeStyles, out DateTime)"/>
        /// 兜底(.NET 文化规则消化各区域长/短日期变体)。输出统一由调用方格式化为 yyyy-MM-dd HH:mm:ss。
        /// </summary>
        public static bool TryParseClippingDate(string rawDate, out DateTime parsedDate) {
            parsedDate = default;
            if (string.IsNullOrWhiteSpace(rawDate)) {
                return false;
            }

            var cleaned = rawDate;
            foreach (var token in KindleDateCleaningTokens) {
                cleaned = Regex.Replace(cleaned, Regex.Escape(token), string.Empty, RegexOptions.IgnoreCase);
            }
            // 折叠清理留下的连续空白(如 "2025 à 22" 删 à 后),并去掉星期词删除后残留的行首逗号
            // (en 真实 "Sunday, September 05, 2021…" 删 Sunday 后留 ", …"),避免影响后续解析
            cleaned = Regex.Replace(cleaned, @"\s{2,}", " ").Trim().TrimStart(',', ' ');
            // 某些区域在“日期 与 时间”之间带逗号(如 en/de/es "…2025, 10:20:31 PM"),文化解析不接受 → 归并为空格。
            cleaned = Regex.Replace(cleaned, @",\s+(?=\d{1,2}:\d{2})", " ");
            var gmtIndex = cleaned.LastIndexOf(" GMT", StringComparison.OrdinalIgnoreCase);
            if (gmtIndex >= 0) {
                cleaned = cleaned[..gmtIndex].Trim();
            }
            // 德语老设备点号时间(真实样本 "4.46 Uhr" 去 Uhr/GMT 后为 "4.46")→ 归并为冒号 "4:46"
            cleaned = Regex.Replace(cleaned, @"(?<=^|\s)(\d{1,2})\.(\d{2})(?=\s|$)", "$1:$2");

            // 与原实现一致的“去掉首逗号前段”变体(兼容 "Added on Sunday, …" 类前缀残留)
            var truncated = cleaned;
            var firstComma = cleaned.IndexOf(',');
            if (firstComma >= 0) {
                truncated = cleaned[(firstComma + 1)..].Trim();
            }

            if (TryParseExactKindleDate(truncated, out parsedDate) || TryParseExactKindleDate(cleaned, out parsedDate)) {
                return true;
            }

            foreach (var cultureName in KindleDateCultures) {
                var culture = CultureInfo.GetCultureInfo(cultureName);
                if (DateTime.TryParse(cleaned, culture, DateTimeStyles.None, out parsedDate) ||
                    DateTime.TryParseExact(cleaned, "d MMMM yyyy HH:mm:ss", culture, DateTimeStyles.None, out parsedDate) ||
                    DateTime.TryParseExact(cleaned, "d MMMM yyyy H:mm:ss", culture, DateTimeStyles.None, out parsedDate)) {
                    return true;
                }
            }
            return false;
        }

        private static bool TryParseExactKindleDate(string input, out DateTime parsedDate) {
            if (DateTime.TryParseExact(input, "MMMM d, yyyy h:m:s tt", EnUs, DateTimeStyles.None, out parsedDate) ||
                DateTime.TryParseExact(input, "d MMMM yyyy HH:mm:ss", EnUs, DateTimeStyles.None, out parsedDate)) {
                return true;
            }
            var dayOfWeekIndex = input.IndexOf("星期", StringComparison.Ordinal);
            if (dayOfWeekIndex != -1) {
                input = input.Remove(dayOfWeekIndex, 3);
            }
            return DateTime.TryParseExact(input, "yyyy年M月d日 tth:m:s", ZhCn, DateTimeStyles.None, out parsedDate);
        }

        private static DateTime? ParseToUtcDate(string serializedDate) {
            if (DateTime.TryParse(serializedDate, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTime date) || CultureInfoArray.Any(culture => DateTime.TryParse(serializedDate, new CultureInfo(culture), DateTimeStyles.AssumeUniversal, out date))) {
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
            ArgumentNullException.ThrowIfNull(input);

            var result = new Location();
            
            if (string.IsNullOrWhiteSpace(input)) {
                return result;
            }

            try {
                // Parse location range (e.g., "123-456")
                try {
                    Match matchLocation = LocationRegex().Match(input);
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
                Match matchPage = PageNumberRegex().Match(input);
                if (matchPage.Success && int.TryParse(matchPage.Groups[1].Value, out var page)) {
                    result.Page = page;
                }
            } catch (Exception e) when (e is not (ArgumentNullException or InvalidOperationException)) {
                // Wrap and re-throw with more context, but preserve specific exceptions
                throw new InvalidOperationException($"Failed to parse location from input '{input}': {e.Message}", e);
            }
            
            return result;
        }
        
        private static readonly List<string> ClippingLimitReachedWarning = [
            "You have reached the clipping limit for this item",
            "您已达到本内容的剪贴上限"
        ];
        
        private static readonly string[] CultureInfoArray = ["it-IT", "fr-FR", "es-ES", "pt-PT"];

        public static bool IsClippingLimitReached(string content) {
            return ClippingLimitReachedWarning.Any(content.Contains);
        }

        [GeneratedRegex(AppConstants.SingleNumberPattern, RegexOptions.Compiled)]
        private static partial Regex PageNumberRegex();
        
        [GeneratedRegex(AppConstants.LocationRangePattern, RegexOptions.Compiled)]
        private static partial Regex LocationRegex();
    }
}