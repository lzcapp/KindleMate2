using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Shared.Constants;
using Markdig.Helpers;
using System.Runtime.InteropServices;
using System.Text;
using Lookup = KindleMate2.Domain.Entities.KM2DB.Lookup;

namespace KindleMate2.Infrastructure.Helpers {
    public static class StringHelper {
        /// <summary>
        /// Removes control characters, line feeds, and BOM (65279) from the input string.
        /// </summary>
        /// <param name="input">The input string to process</param>
        /// <returns>A cleaned string with control characters removed</returns>
        /// <exception cref="ArgumentNullException">Thrown when input is null</exception>
        public static string RemoveControlChar(string input) {
            ArgumentNullException.ThrowIfNull(input);

            if (string.IsNullOrEmpty(input)) {
                return string.Empty;
            }

            const char BOM = AppConstants.ByteOrderMark; // Use constant from AppConstants
            var output = new StringBuilder(input.Length); // Pre-allocate capacity for better performance
            
            foreach (var c in input) {
                if (!c.IsControl() && !c.IsNewLineOrLineFeed() && c != BOM) {
                    output.Append(c);
                }
            }
            return output.ToString();
        }

        /// <summary>
        /// Converts a Roman numeral string to its integer equivalent.
        /// </summary>
        /// <param name="roman">The Roman numeral string to convert</param>
        /// <returns>The integer value of the Roman numeral</returns>
        /// <exception cref="ArgumentNullException">Thrown when roman is null</exception>
        /// <exception cref="ArgumentException">Thrown when roman contains invalid characters</exception>
        public static int RomanToInteger(string roman) {
            ArgumentNullException.ThrowIfNull(roman);

            if (string.IsNullOrWhiteSpace(roman)) {
                return 0;
            }

            // ToUpperInvariant:罗马数字是固定的 ASCII 记号,与区域设置无关;
            // 走文化相关的 ToUpper 在土耳其语等区域会把 'i' 转成带点的 'İ',随后按字符查表就失败了。
            roman = roman.ToUpperInvariant();
            
            // Validate that all characters are valid Roman numerals
            if (roman.Any(c => !RomanMap.ContainsKey(c))) {
                throw new ArgumentException($"Invalid Roman numeral character found in: {roman}", nameof(roman));
            }

            var result = 0;
            var prevValue = 0;

            for (var i = roman.Length - 1; i >= 0; i--) {
                var value = RomanMap[roman[i]];

                if (value < prevValue) {
                    result -= value;
                } else {
                    result += value;
                }

                prevValue = value;
            }

            return result;
        }

        private static readonly Dictionary<char, int> RomanMap = new() {
            { 'I', 1 },
            { 'V', 5 },
            { 'X', 10 },
            { 'L', 50 },
            { 'C', 100 },
            { 'D', 500 },
            { 'M', 1000 }
        };

        /// <summary>
        /// Trims content by removing leading spaces, dots, and specific punctuation, then trims whitespace.
        /// </summary>
        /// <param name="content">The content to trim</param>
        /// <returns>Trimmed content or empty string if input is null</returns>
        public static string TrimContent(string? content) {
            if (content == null) {
                return string.Empty;
            }

            // Use more readable character array for trimming
            char[] leadingCharsToTrim = [' ', '.', '，', '。'];
            return content.TrimStart(leadingCharsToTrim).Trim();
        }

        /// <summary>
        /// <see cref="SanitizeFilename"/> 默认的 UTF-8 字节上限:240。
        /// </summary>
        /// <remarks>
        /// 多数文件系统的单段文件名上限是 **255 字节**(Linux / macOS 按字节;Windows 按 UTF-16 码元,
        /// 但中文按字节更严),这里预留 15 字节给调用方后续拼接的部分:
        /// <list type="bullet">
        /// <item>扩展名最长 <c>.html</c> = 5 字节(本 helper 各调用点里最长的那个);</item>
        /// <item>撞名消歧后缀 <c> (NN)</c> 至多 6 字节(<c> (100)</c> 起即 6 字节,千级足够);</item>
        /// <item>Windows 保留名兜底前缀 <c>_</c> = 1 字节。</item>
        /// </list>
        /// 240 + 5 + 6 + 1 = 252 ≤ 255,尚余 3 字节。正常长度的名字远达不到这个上限,故行为与改动前逐字相同。
        /// </remarks>
        public const int DefaultMaxUtf8Bytes = 240;

        /// <summary>
        /// Sanitizes a filename by replacing invalid characters with underscores, and capping its UTF-8
        /// length at <paramref name="maxUtf8Bytes"/>.
        ///
        /// <para>
        /// 非法字符集是**固定**的,不取 <see cref="Path.GetInvalidFileNameChars"/> —— 后者给的是
        /// "当前平台"的规则(macOS / Linux 只有 '\0' 与 '/',Windows 有 41 个),于是同一本书在
        /// Windows 与 macOS 上会导出成**不同文件名**:macOS 侧会留下 ':',而 Finder 把 ':' 显示成
        /// 路径分隔符;把导出目录拷到 Windows / SMB 共享时这些名字更是直接非法。
        /// 这里取三平台非法字符的并集,使净化结果与运行平台无关 —— Windows 上**非法字符的替换结果**
        /// 与改动前一致,但另有两处是刻意收紧的(去掉末尾的点、保留名判定移到修剪之后),
        /// 所以少数名字会比改动前更安全,不再与旧行为逐字相同。
        /// </para>
        /// </summary>
        /// <param name="filename">The filename to sanitize</param>
        /// <param name="maxUtf8Bytes">
        /// 结果串允许的最大 **UTF-8 字节数**(默认 <see cref="DefaultMaxUtf8Bytes"/>);按字符边界截断,
        /// 绝不切出半个字符。传 <see cref="int.MaxValue"/> 可关闭截断 —— 生产代码里
        /// <see cref="ComposeTruncatedFileName"/> 就用它:那里需要"先只净化、不截断",再由调用方按
        /// 重要性分配预算统一截断(否则本方法会先按默认上限把书名截掉,预算分配就无从谈起)。
        /// </param>
        /// <returns>A sanitized filename safe for file system use</returns>
        /// <exception cref="ArgumentNullException">Thrown when filename is null</exception>
        public static string SanitizeFilename(string filename, int maxUtf8Bytes = DefaultMaxUtf8Bytes) {
            ArgumentNullException.ThrowIfNull(filename);

            if (string.IsNullOrEmpty(filename)) {
                return string.Empty;
            }

            var builder = new StringBuilder(filename.Length);
            foreach (var c in filename) {
                builder.Append(IsPortablyInvalidFileNameChar(c) ? '_' : c);
            }
            var sanitized = builder.ToString();

            sanitized = sanitized.Trim();

            // 截断:按 UTF-8 字节在字符边界截断。**必须插在"去尾点"之前** —— 截断后可能正好以点结尾,
            // 那要交给下一步去掉;若顺序反过来(先去尾点再截断),这个由截断**新产生**的尾点就漏掉了。
            sanitized = TruncateToUtf8Bytes(sanitized, maxUtf8Bytes);

            // 末尾的点在 Windows 上非法(结尾空格已由上一行的 Trim 处理)。
            // 整串都是点则保持原样:修成空名只会让调用方产出一个隐藏文件。
            var withoutTrailingDots = sanitized.TrimEnd('.');
            if (withoutTrailingDots.Length > 0) {
                sanitized = withoutTrailingDots;
            }

            // Windows 保留设备名在任意目录下都不能当文件名,且与扩展名无关 —— 故按"去掉扩展名后"判断。
            //
            // 必须放在 Trim / 截断 / 去尾点**之后**:顺序反过来时 " CON "、"CON."、"aux " 会先被判为
            // "非保留名",修剪 / 截断之后才落回 "CON"/"aux" —— 恰好生成那个非法名(原实现在 Windows 上
            // 一直如此,2026-09-21 探针实测确认)。判定的对象必须是**最终要落盘的那个字符串**。
            //
            // 基名还要再 TrimEnd(' ', '.') 一次:Windows 解析文件名时会先剥掉基名尾部的空格 / 点,
            // 于是 "CON .txt" 实际会被当成 "CON.txt" —— 仍是保留名。只按 GetFileNameWithoutExtension
            // 判(得到基名 "CON ")会漏掉「扩展名之前带空格 / 点」的这一类。
            var nameWithoutExtension = Path.GetFileNameWithoutExtension(sanitized).TrimEnd(' ', '.');
            if (ReservedFileNames.Contains(nameWithoutExtension.ToUpperInvariant())) {
                sanitized = "_" + sanitized;
            }

            return sanitized;
        }

        /// <summary>
        /// 把 <paramref name="value"/> 按 UTF-8 字节截断到至多 <paramref name="maxUtf8Bytes"/> 字节。
        /// </summary>
        /// <remarks>
        /// 逐**码点**累计字节数、在码点边界收尾:中文是 3 字节,emoji 等补充平面字符是 4 字节且由一对
        /// surrogate(UTF-16)组成 —— 若按 <c>char</c> 截断,极易把一对 surrogate 劈成两半,写出一个
        /// 非法 UTF-8 的文件名。孤立 surrogate(数据损坏时可能出现)按编码器会替换成的 U+FFFD 记 3 字节,
        /// 与 <see cref="Encoding.UTF8"/> 的 <c>GetByteCount</c> 口径一致。
        ///
        /// <para>
        /// 设为 <c>public</c>:文件名长度控制不止 <see cref="SanitizeFilename"/> 一处需要它 ——
        /// 分享图文件名要"按重要性分配预算"地分段截断(<see cref="ComposeTruncatedFileName"/>),
        /// 共用这一份实现,避免"同一判定两份实现"再次漂移。
        /// </para>
        /// </remarks>
        public static string TruncateToUtf8Bytes(string value, int maxUtf8Bytes) {
            if (maxUtf8Bytes <= 0) {
                return string.Empty;
            }
            if (Encoding.UTF8.GetByteCount(value) <= maxUtf8Bytes) {
                return value;
            }

            var bytes = 0;
            var length = 0;
            while (length < value.Length) {
                var c = value[length];
                int charCount;
                int byteCount;
                if (char.IsHighSurrogate(c) && length + 1 < value.Length && char.IsLowSurrogate(value[length + 1])) {
                    charCount = 2;
                    byteCount = 4;
                } else if (char.IsSurrogate(c)) {
                    charCount = 1;
                    byteCount = 3;
                } else if (c < 0x80) {
                    charCount = 1;
                    byteCount = 1;
                } else if (c < 0x800) {
                    charCount = 1;
                    byteCount = 2;
                } else {
                    charCount = 1;
                    byteCount = 3;
                }

                if (bytes + byteCount > maxUtf8Bytes) {
                    break;
                }
                bytes += byteCount;
                length += charCount;
            }
            return value[..length];
        }

        /// <summary>
        /// 组合后的**整路径**(目录 + 文件名 + 扩展名)允许的最大 UTF-8 字节数。
        /// </summary>
        /// <remarks>
        /// <see cref="SanitizeFilename"/> 管的是**单段文件名**的上限(<see cref="DefaultMaxUtf8Bytes"/>);
        /// 但整条路径另有一层上限:目录越长,留给文件名的实际预算越少 —— Windows 的经典 MAX_PATH 是
        /// 260 个 UTF-16 码元(含盘符),导出目录常位于较深的用户目录下,极端长的书名会把整条路径撑过上限,
        /// 落盘时抛 <see cref="PathTooLongException"/>。这里取 255 字节(与多数文件系统的单段 NAME_MAX 同值)
        /// 作为**保守**门槛:ASCII 下约等于 255 个码元,中文按字节更早触发截断。正常长度的目录 / 书名远达不到,
        /// 故常规导出行为与改动前逐字相同。
        /// </remarks>
        public const int DefaultMaxPathUtf8Bytes = 255;

        /// <summary>
        /// 截短 <paramref name="fileName"/>,使 <c>目录 + 文件名 + 扩展名</c> 的**整路径**不超过
        /// <paramref name="maxPathUtf8Bytes"/> 字节;按码点边界截断,绝不切出半个字符。
        /// </summary>
        /// <param name="directory">目标目录(可带或不带结尾分隔符)。</param>
        /// <param name="fileName">文件名(不含扩展名);超出剩余预算时被截短。</param>
        /// <param name="extension">扩展名(含前导点,如 <c>.md</c>)。</param>
        /// <param name="maxPathUtf8Bytes">整路径 UTF-8 字节上限(默认 <see cref="DefaultMaxPathUtf8Bytes"/>)。</param>
        /// <remarks>
        /// 目录(含其与文件名之间的那一枚分隔符)与扩展名都**原样计入**,只截文件名 —— 目录由用户 /
        /// 调用方决定,不该被本 helper 改写。剩余预算不足 1 字节时返回**空串**,由调用方判为"无法落盘"
        /// (而不是写一个退化名)。截断复用 <see cref="TruncateToUtf8Bytes"/>,与单段文件名同一套字节口径。
        /// </remarks>
        /// <exception cref="ArgumentNullException">任一参数为 null 时抛出。</exception>
        public static string TruncateFileNameToFitPath(
            string directory,
            string fileName,
            string extension,
            int maxPathUtf8Bytes = DefaultMaxPathUtf8Bytes) {
            ArgumentNullException.ThrowIfNull(directory);
            ArgumentNullException.ThrowIfNull(fileName);
            ArgumentNullException.ThrowIfNull(extension);

            // +1:Path.Combine 在目录与文件名之间补的那一枚路径分隔符。
            var overhead = Encoding.UTF8.GetByteCount(directory) + Encoding.UTF8.GetByteCount(extension) + 1;
            var budget = maxPathUtf8Bytes - overhead;
            return budget <= 0 ? string.Empty : TruncateToUtf8Bytes(fileName, budget);
        }

        /// <summary>
        /// 把 <paramref name="head"/> 与 <paramref name="tail"/> 各段用 '_' 拼接,并按**重要性分配预算**
        /// 截断,使整个结果(含 <paramref name="extension"/>)不超过 <paramref name="maxTotalUtf8Bytes"/> 字节。
        /// </summary>
        /// <param name="head">可截断的主段(分享图里是书名);为空则不参与拼接(不留前导下划线)。</param>
        /// <param name="tail">必须**完整保留**的尾段(分享图里是位置 + 时间),按序用 '_' 拼接;空段跳过。</param>
        /// <param name="extension">扩展名(含前导点,如 <c>.png</c>)。</param>
        /// <param name="maxTotalUtf8Bytes">整个文件名(含扩展名)的 UTF-8 字节上限。</param>
        /// <remarks>
        /// <para>
        /// **为什么不从右往左砍**:分享图文件名是 <c>&lt;书名&gt;_&lt;位置&gt;_&lt;时间&gt;.png</c>,
        /// 位置与时间短,却是区分「同一本书同一天不同标注」的**唯一**来源;书名可长。若按字符从尾部
        /// 截断,书名一长就把位置与时间整段砍掉 ⇒ 同书同日的两条标注会得到同一个建议名,连存两张就
        /// 互相覆盖 —— 与文件名带这两段的理由直接冲突。故这里先算出尾段(含它与书名之间那一个 '_')
        /// 的字节数整段保留,把**剩余预算**留给主段。
        /// </para>
        /// <para>
        /// 截断走 <see cref="TruncateToUtf8Bytes"/>:按 UTF-8 字节在字符边界收尾 —— 全 emoji 的书名
        /// 每个码点 4 字节,按 <c>char</c> 截断既会低估长度(80 码点 = 320 字节,早超 255),又会劈开
        /// 代理对产出非法 UTF-8。故上限按**字节**算。
        /// </para>
        /// <para>
        /// 尾段由标注主键派生、天然很短(位置是区间、时间是固定格式时间戳),整段保留后仍远低于上限,
        /// 因此"保留尾段"与"总长 ≤ 上限"两个要求可同时成立。
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException">任一参数为 null 时抛出。</exception>
        public static string ComposeTruncatedFileName(
            string head,
            IReadOnlyList<string> tail,
            string extension,
            int maxTotalUtf8Bytes) {
            ArgumentNullException.ThrowIfNull(head);
            ArgumentNullException.ThrowIfNull(tail);
            ArgumentNullException.ThrowIfNull(extension);

            // 各段先净化,但**关掉截断**(int.MaxValue)—— 否则 SanitizeFilename 会先把书名截到默认
            // 上限,后面的"按重要性分配预算"就无从谈起。净化保留非法字符替换、去尾点、保留名兜底。
            var headPart = head.Length > 0 ? SanitizeFilename(head, int.MaxValue) : string.Empty;
            var tailParts = new List<string>(tail.Count);
            foreach (var segment in tail) {
                if (segment.Length == 0) {
                    continue; // 空段跳过 —— 不留连续下划线
                }
                var sanitized = SanitizeFilename(segment, int.MaxValue);
                if (sanitized.Length > 0) {
                    tailParts.Add(sanitized);
                }
            }
            var tailJoined = string.Join("_", tailParts);

            // 主干(不含扩展名)的预算 = 整个文件名上限 − 扩展名占用的字节。
            var stemBudget = maxTotalUtf8Bytes - Encoding.UTF8.GetByteCount(extension);

            string stem;
            if (headPart.Length == 0) {
                // 没有主段:主干就是尾段(位置 / 时间)。
                stem = tailJoined;
            } else if (tailJoined.Length == 0) {
                // 没有尾段:主段是唯一内容,直接按预算截断。
                stem = TruncateToUtf8Bytes(headPart, stemBudget);
            } else {
                // 尾段(含其前导那一个 '_')整段保留,剩余预算才给主段。
                var reserved = Encoding.UTF8.GetByteCount(tailJoined) + 1;
                var headBudget = Math.Max(0, stemBudget - reserved);
                var headTruncated = TruncateToUtf8Bytes(headPart, headBudget);
                // 主段被截到空时连分隔符一起省掉,避免留下前导下划线。
                stem = headTruncated.Length > 0 ? headTruncated + "_" + tailJoined : tailJoined;
            }

            // 截断可能恰好以点 / 空格结尾(Windows 上文件名不能以点或空格结尾)。
            // 整串都是点时保持原样:修成空名只会让调用方产出一个隐藏文件。
            var trimmed = stem.TrimEnd(' ', '.');
            if (trimmed.Length > 0) {
                stem = trimmed;
            }

            return stem + extension;
        }

        /// <summary>
        /// 三平台非法字符的并集:控制字符(含 '\0')+ Windows 的 9 个保留字符 + POSIX 的 '/'。
        /// 刻意写成显式判断而非查表,一眼能看出它不受运行平台影响。
        /// </summary>
        private static bool IsPortablyInvalidFileNameChar(char c) =>
            c < 0x20 || c is '<' or '>' or ':' or '"' or '/' or '\\' or '|' or '?' or '*';

        /// <summary>Windows 保留设备名(比较用 Ordinal,调用方先转 UpperInvariant)。</summary>
        private static readonly HashSet<string> ReservedFileNames = new(StringComparer.Ordinal) {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };

        /// <summary>
        /// Formats a file size in bytes to a human-readable string with appropriate unit.
        /// </summary>
        /// <param name="fileSize">The file size in bytes</param>
        /// <returns>A formatted string with size and unit (e.g., "1.5 MB")</returns>
        public static string FormatFileSize(long fileSize) {
            if (fileSize < 0) {
                return "0 B";
            }

            string[] sizes = ["B", "KB", "MB", "GB", "TB"];
            const int BytesInKilobyte = AppConstants.BytesInKilobyte;

            var order = 0;
            double size = fileSize;

            while (size >= BytesInKilobyte && order < sizes.Length - 1) {
                order++;
                size /= BytesInKilobyte;
            }

            return $"{size:0.##} {sizes[order]}";
        }

        /// <summary>
        /// Parses Kindle version text to extract version number.
        /// </summary>
        /// <param name="versionText">The raw version text from Kindle</param>
        /// <returns>Parsed version string</returns>
        /// <exception cref="ArgumentNullException">Thrown when versionText is null</exception>
        public static string ParseKindleVersion(string versionText) {
            ArgumentNullException.ThrowIfNull(versionText);

            return versionText.Trim().Split('(')[0].Replace(AppConstants.Kindle, "", StringComparison.OrdinalIgnoreCase).Trim();
        }

        /// <summary>
        /// Formats an exception message with context information.
        /// </summary>
        /// <param name="className">The class/method name where the exception occurred</param>
        /// <param name="e">The exception to format</param>
        /// <returns>A formatted exception message string</returns>
        /// <exception cref="ArgumentNullException">Thrown when className or exception is null</exception>
        public static string GetExceptionMessage(string className, Exception e) {
            ArgumentNullException.ThrowIfNull(className);
            ArgumentNullException.ThrowIfNull(e);

            return $"{className}: {Environment.NewLine}{e.Message}{Environment.NewLine}Stack Trace: {e.StackTrace}";
        }

        public static string GetRuntimeArchitecture() {
            var is64Bit = Environment.Is64BitProcess;

            var runtime = RuntimeInformation.FrameworkDescription.Contains(".NET") ? "runtime" : string.Empty;

            return is64Bit switch {
                true when runtime == "runtime" => "x64_runtime",
                false when runtime == "runtime" => "x86_runtime",
                true => "x64",
                _ => "x86"
            };
        }

        /// <summary>
        /// 把正文转义成 Markdown **字面量**,使其不会改变导出文档的结构。
        /// </summary>
        /// <remarks>
        /// <para>
        /// 修的缺陷:正文原样写入时,行首的 <c>#</c> 会被 Markdig 渲染成一个标题、行首 <c>[1]: url</c>
        /// 会被当成链接引用定义而整行消失、任意位置的 <c>|</c> 会拆掉表格、裸 <c>&lt;tag&gt;</c>
        /// 会被当成 HTML 原样透传 —— 导出的排版因此取决于标注内容本身。本方法把这几点转义成字面量
        /// (其中 <c>|</c> 的写法与 Obsidian 导出对**表格单元格**的处理
        /// (<c>ObsidianExportWriter.EscapeTableCell</c>)一致;Obsidian 的正文本身仍未转义,见
        /// <see cref="BuildMarkdownWithLookups"/> 的说明)。
        /// </para>
        /// <para>
        /// 刻意**只**转义会引发结构变化的字符,不做"全 ASCII 标点转义"(那会把普通标点也写成
        /// <c>\,</c> <c>\.</c> 之类,可读性尽失):
        /// <list type="bullet">
        /// <item>行首(至多 3 个前导空格,CommonMark 的块级标记缩进上限)的块级标记:<c>#</c>
        ///   <c>&gt;</c> <c>-</c> <c>+</c> <c>*</c> <c>_</c> <c>=</c> <c>~</c> <c>`</c>;</item>
        /// <item>行首的有序列表:数字原样保留、只转义分隔符(<c>1\.</c> / <c>12\)</c>) ——
        ///   反斜杠转义只对 ASCII 标点有效,转义数字(<c>\1.</c>)会把反斜杠本身渲染出来;</item>
        /// <item>行首的链接引用定义(<c>[1]: url</c>):转义开头的 <c>[</c>,否则整行会被 Markdig 当定义
        ///   吃掉、完全不渲染;</item>
        /// <item>任意位置的行内标记:<c>|</c>(表格)、<c>&lt;</c>(裸 HTML)、<c>\</c>(转义前缀)、
        ///   <c>`</c>(行内代码)。</item>
        /// </list>
        /// 换行原样保留(正文的段落结构不该被压掉)。
        /// </para>
        /// <para>
        /// **已知限制**:4 个及以上前导空格的行是 CommonMark 的缩进代码块,代码块内反斜杠**不生效**,
        /// 行内转义(<c>\</c> <c>\|</c> <c>\&lt;</c> <c>\`</c>)在那种行里会原样显示反斜杠。正文里的
        /// 缩进代码块极少见(标注正文通常从行首开始),这里不做"逐行判断是否处于代码块内"的上下文分析,
        /// 以免引入状态机;块级标记判定已限制在 ≤3 个前导空格,至少不会在代码块里额外转义 <c>#</c> 等。
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="content"/> 为 null 时抛出。</exception>
        public static string EscapeMarkdownContent(string content) {
            ArgumentNullException.ThrowIfNull(content);

            if (content.Length == 0) {
                return content;
            }

            // 按 '\n' 切行,行尾可能残留的 '\r' 由行内分支原样带过(不参与块级判定)。
            var lines = content.Split('\n');
            var sb = new StringBuilder(content.Length + 16);
            for (var i = 0; i < lines.Length; i++) {
                if (i > 0) {
                    sb.Append('\n');
                }
                AppendEscapedMarkdownLine(sb, lines[i]);
            }
            return sb.ToString();
        }

        /// <summary>转义单行并追加到 <paramref name="sb"/>(见 <see cref="EscapeMarkdownContent"/>)。</summary>
        private static void AppendEscapedMarkdownLine(StringBuilder sb, string line) {
            // 前导空格原样保留:CommonMark 允许块级标记前有**至多 3 个**空格;4 个以上就是缩进代码块,
            // 块内反斜杠不生效(转义反而会显示反斜杠,见 EscapeMarkdownContent 的已知限制说明)。
            var start = 0;
            while (start < line.Length && line[start] == ' ') {
                start++;
            }
            if (start > 0) {
                sb.Append(line, 0, start);
            }

            var i = start;
            if (start <= 3 && i < line.Length) {
                var delimiter = FindOrderedListDelimiter(line, i);
                if (delimiter >= 0) {
                    // 有序列表:数字原样输出、**只转义分隔符**("1\." / "12\)")。
                    // 不能转义首位数字:"\1" 不是 CommonMark 转义(反斜杠只对 ASCII 标点生效),
                    // 会把反斜杠原样渲染出来(2026-10 审阅实测)。
                    sb.Append(line, i, delimiter - i);
                    sb.Append('\\').Append(line[delimiter]);
                    i = delimiter + 1;
                } else if (IsMarkdownBlockMarker(line, i)) {
                    // 先单独转义首字符再跳过:否则它会在下面的行内分支里被**二次**转义(如行首的 '`')。
                    sb.Append('\\').Append(line[i]);
                    i++;
                } else if (IsLinkReferenceDefinition(line, i)) {
                    // 行首 "[1]: url" 会被解析成链接引用定义,整行不出现在渲染结果里 —— 转义开头的 '['。
                    sb.Append("\\[");
                    i++;
                }
            }
            for (; i < line.Length; i++) {
                switch (line[i]) {
                    case '\\': sb.Append("\\\\"); break;
                    case '`': sb.Append("\\`"); break;
                    case '|': sb.Append("\\|"); break;
                    case '<': sb.Append("\\<"); break;
                    default: sb.Append(line[i]); break;
                }
            }
        }

        /// <summary>
        /// <paramref name="line"/> 从 <paramref name="index"/> 起是否为一个标记**块级**结构的起始。
        /// </summary>
        /// <remarks>
        /// 只判块级:行内 <c>*</c> / <c>_</c> 等不在其中(它们不改变文档结构,转义反而让正常正文变丑)。
        /// 有序列表按 CommonMark:1–9 位数字 + <c>.</c> 或 <c>)</c> + 空白 / 行尾;转义由调用方
        /// 改为"数字原样 + 只转义分隔符",见 <see cref="FindOrderedListDelimiter"/>。
        /// </remarks>
        private static bool IsMarkdownBlockMarker(string line, int index) {
            switch (line[index]) {
                case '#':
                case '>':
                case '-':
                case '+':
                case '*':
                case '_':
                case '=':
                case '~':
                case '`':
                    return true;
                default:
                    return FindOrderedListDelimiter(line, index) >= 0;
            }
        }

        /// <summary>
        /// 若 <paramref name="line"/> 从 <paramref name="index"/> 起是 CommonMark 有序列表标记,
        /// 返回**分隔符**(<c>.</c> 或 <c>)</c>)的下标;否则返回 -1。
        /// </summary>
        /// <remarks>
        /// 规则:1–9 位 ASCII 数字 + <c>.</c>/<c>)</c> + 空白、<c>\r</c> 或行尾。
        /// 返回分隔符位置是为了只转义它(<c>1\.</c>):反斜杠转义对数字无效,转义数字会显示反斜杠。
        /// </remarks>
        private static int FindOrderedListDelimiter(string line, int index) {
            if (!char.IsAsciiDigit(line[index])) {
                return -1;
            }
            var i = index;
            while (i < line.Length && char.IsAsciiDigit(line[i])) {
                i++;
            }
            if (i - index > 9 || i >= line.Length || (line[i] != '.' && line[i] != ')')) {
                return -1;
            }
            var after = i + 1;
            return after >= line.Length || line[after] == ' ' || line[after] == '\t' || line[after] == '\r'
                ? i
                : -1;
        }

        /// <summary>
        /// <paramref name="line"/> 从 <paramref name="index"/> 起是否像一个**链接引用定义**行
        /// (<c>[label]: destination</c>)。
        /// </summary>
        /// <remarks>
        /// 只做"够用"的近似:以 <c>[</c> 开头且本行后面出现 <c>]:</c> 即认为会被解析成定义。
        /// 转义开头的 <c>[</c> 即可让它降级为普通文本(不会再有整行被吞掉的问题)。
        /// </remarks>
        private static bool IsLinkReferenceDefinition(string line, int index) =>
            line[index] == '[' &&
            line.IndexOf("]:", index, StringComparison.Ordinal) > index;

        /// <summary>
        /// Builds markdown content from a list of clippings for a specific book.
        /// </summary>
        /// <param name="clippings">List of clippings to process</param>
        /// <returns>StringBuilder containing the formatted markdown</returns>
        /// <exception cref="ArgumentNullException">Thrown when clippings is null</exception>
        public static StringBuilder BuildMarkdownWithClippings(List<Clipping> clippings) {
            ArgumentNullException.ThrowIfNull(clippings);

            var markdown = new StringBuilder();

            try {
                if (clippings.Count == 0) {
                    // Return empty markdown instead of throwing exception for empty list
                    return markdown;
                }

                var bookName = clippings[0].BookName;

                if (!string.IsNullOrWhiteSpace(bookName)) {
                    markdown.AppendLine("## \ud83d\udcd6 " + bookName.Trim());
                } else {
                    markdown.AppendLine("## \ud83d\udcd6 Unknown Book");
                }

                markdown.AppendLine();

                foreach (var clipping in clippings) {
                    var clippingLocation = clipping.ClippingTypeLocation;
                    var content = clipping.Content;

                    if (string.IsNullOrWhiteSpace(clippingLocation) || string.IsNullOrWhiteSpace(content)) {
                        continue;
                    }

                    markdown.AppendLine("**" + clippingLocation + "**");
                    markdown.AppendLine();
                    // 正文转义成 Markdown 字面量:否则行首 '#' / 行首链接引用定义 / 任意 '|' / 裸 HTML
                    // 会让导出排版取决于标注内容(其中 '|' 的写法与 Obsidian 表格单元格一致)。
                    markdown.AppendLine(EscapeMarkdownContent(content));
                    markdown.AppendLine();
                }
                
                return markdown;
            } catch (Exception e) {
                // Instead of Console.WriteLine, let the caller handle the exception
                throw new InvalidOperationException($"Failed to build markdown from clippings: {e.Message}", e);
            }
        }

        /// <summary>
        /// Builds markdown content from a list of vocabulary lookups.
        /// </summary>
        /// <param name="lookups">List of lookups to process</param>
        /// <returns>StringBuilder containing the formatted markdown</returns>
        /// <remarks>
        /// **usage 正文刻意不套 <see cref="EscapeMarkdownContent"/>**(本方法会先往 usage 正文里注入
        /// <c>**`word`**</c> 高亮,若先整串转义,含 <c>|</c> <c>&lt;</c> <c>\</c> <c>`</c> 的词会匹配不到、
        /// 丢高亮;若在注入后再整串转义,注入的标记本身又会被转义掉)。代价是 usage 里若出现行首
        /// <c>#</c> / <c>|</c> / 裸 HTML 等,仍可能影响渲染 —— 属已知限制,留待后续用"分段转义 + 注入
        /// 高亮"的方式跟进(2026-10 审阅记录)。标注正文走 <see cref="BuildMarkdownWithClippings"/>,
        /// 那里**已**转义。
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when lookups is null</exception>
        public static StringBuilder BuildMarkdownWithLookups(List<Lookup> lookups) {
            ArgumentNullException.ThrowIfNull(lookups);

            var markdown = new StringBuilder();

            try {
                if (lookups.Count == 0) {
                    // Return empty markdown instead of throwing exception for empty list
                    return markdown;
                }

                var word = lookups[0].Word;

                if (!string.IsNullOrWhiteSpace(word)) {
                    markdown.AppendLine("## \ud83d\udcd6 " + word.Trim());
                } else {
                    markdown.AppendLine("## \ud83d\udcd6 Unknown Word");
                }

                markdown.AppendLine();

                foreach (var lookup in lookups) {
                    var title = lookup.Title;
                    var usage = lookup.Usage;

                    if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(usage)) {
                        continue;
                    }

                    markdown.AppendLine("**" + title + "**");
                    markdown.AppendLine();
                    
                    // Safely replace word occurrences with better error handling
                    var formattedUsage = !string.IsNullOrWhiteSpace(word) 
                        ? usage.Replace(word, " **`" + word + "`** ", StringComparison.OrdinalIgnoreCase) 
                        : usage;
                    
                    markdown.AppendLine(formattedUsage);
                    markdown.AppendLine();
                }
                
                return markdown;
            } catch (Exception e) {
                // Instead of Console.WriteLine, let the caller handle the exception
                throw new InvalidOperationException($"Failed to build markdown from lookups: {e.Message}", e);
            }
        }

        /// <summary>
        /// Formats statistics text with proper spacing and punctuation.
        /// </summary>
        /// <param name="parts">Array of text parts to join</param>
        /// <returns>A formatted string with proper spacing</returns>
        public static string FormatStatisticsText(params string[] parts) {
            return parts.Length == 0 ? string.Empty : string.Join("", parts);
        }

        /// <summary>
        /// Creates a markdown header with an emoji and title.
        /// </summary>
        /// <param name="emoji">The emoji to use</param>
        /// <param name="title">The title text</param>
        /// <returns>A formatted markdown header</returns>
        public static string CreateMarkdownHeader(string emoji, string title) {
            if (string.IsNullOrWhiteSpace(title)) {
                return string.Empty;
            }

            var emojiPart = string.IsNullOrWhiteSpace(emoji) ? "" : emoji + " ";
            return $"# {emojiPart}{title}";
        }
    }
}