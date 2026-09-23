using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using KindleMate2.Shared.Diagnostics;
using System.Threading.Tasks;

namespace KindleMate2.Application.Services;

/// <summary>
/// 在线释义(查词)。**只在生词详情里用** —— 拿不到就当作"没有释义",由调用方决定不显示,
/// 绝不抛给上层、也不弹任何提示(与 <see cref="UpdateChecker"/> 同一条口径:连不上外网不是错误)。
///
/// 为什么是有道的网页版接口:2026-09-23 实测(直连与走系统代理各测一次)——
/// Wiktionary 与 dictionaryapi.dev 在本机网络下均不可达(HTTP 000),只有
/// <c>dict.youdao.com/jsonapi</c> 通(200)。选它的代价必须写明:
/// <list type="bullet">
///   <item><b>它是网页内部接口</b>:无公开文档、可能随时变更或被限流 ⇒ 所以解析失败一律退化为"没有释义",
///         且**不重试**、超时故意短(8 秒),不让它拖住界面;</item>
///   <item><b>每个查过的词都会发给第三方</b>(有道)。需求本身就要联网释义,但这条要有意识;
///         将来若要更稳/更私密,可换成有道智云官方 API(需 key)或本地离线词库,只需替换本类。</item>
/// </list>
///
/// ⚠️ **一次响应里有多个"释义来源",必须按查询语言挑**(2026-09-23 更正:此前只读 <c>ec</c> 就宣布
/// "中文词查不到",那是**实现缺口**不是数据源边界 —— 教训是"读响应前先列出顶层所有段"):
/// <list type="bullet">
///   <item><c>ec</c> = 有道词典的**英汉**释义(带英/美音标)<c>word[0].trs[].tr[0].l.i[]</c>;</item>
///   <item><c>newhh</c> = **《现代汉语规范词典》** ⇒ 中文词的**中文释义** <c>dataList[0].sense[].def[]</c>;</item>
///   <item><c>baike</c> = **百度百科**摘要 ⇒ 中文词也有(⚠️ 会**误命中同名条目**,见 <see cref="Parse"/>);</item>
///   <item><c>ce</c>/<c>ce_new</c> = 汉英大辞典的**英文释义**(结构与 <c>ec</c> 同);</item>
///   <item><c>simple.word[0].phone</c> = **拼音**(中文词几乎都有,单独作为音标行)。</item>
/// </list>
/// 用户真实生词本实测:190 个去重词里 **178 个含中文**(94%)⇒ 中文词必须优先走 <c>newhh</c>/<c>baike</c>,
/// 否则会拿英文释义去答一个中文词的查询。
/// (另:响应里还有个 <c>aiDefinition</c> 段,但在实测的四个词里**都没出现** ⇒ 不依赖它。)
/// </summary>
public static class WordDefinitionService {
    private const string Endpoint = "https://dict.youdao.com/jsonapi?q=";
    private const string UserAgent = "KindleMate2-Dictionary";

    /// <summary>一次最多展示几条释义 —— 详情面板只有 330px 宽,列多了读不下去。</summary>
    private const int MaxDefinitions = 8;

    /// <summary>查词超时。故意短:这是"顺手补个释义",不该让用户等网络。</summary>
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(8);

    /// <summary>
    /// 查一个词的释义。**任何失败(断网/超时/接口变更/被限流/没有这个词)都返回 <c>null</c>**,
    /// 调用方据此不显示释义块。
    /// </summary>
    /// <param name="cancellationToken">
    /// 调用方主动取消(例如用户换了选中项)。**取消会原样抛出** —— 取消不是"没有释义",
    /// 若把它也吞成 null,调用方会把"这次没查成"缓存成"这个词没有释义"。
    /// </param>
    public static async Task<WordDefinition?> LookupAsync(
        string word, HttpClient? httpClient = null, CancellationToken cancellationToken = default) {

        if (string.IsNullOrWhiteSpace(word)) return null;

        using var owned = httpClient is null ? CreateHttpClient() : null;
        var client = httpClient ?? owned!;

        try {
            using var response = await client
                .GetAsync(Endpoint + Uri.EscapeDataString(word.Trim()), cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return Parse(json);
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        } catch (Exception ex) {
            // 断网 / DNS 失败 / TLS 失败 / 超时 / 读流失败 —— 都是"这次没有释义"。
            // **对用户完全静默**(需求原文:"没联网也不要报错"):界面不弹框、不显示任何东西。
            // 但**记一行日志** —— 否则将来排查"这块为什么不出现"只能靠猜(没网?接口变了?还是这个词真没释义?)。
            AppLog.Write($"[WordDefinition] '{word}' 查询失败:{ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 从接口返回的 JSON 里抽出音标与释义。**纯函数**,单测直接喂样本串。
    ///
    /// 按查询语言决定**来源优先级**:
    /// <list type="bullet">
    ///   <item>查询**含中日韩字符** ⇒ <c>newhh</c>(现代汉语规范词典) → <c>baike</c>(百科) → <c>ec</c> → <c>ce_new</c>/<c>ce</c>;</item>
    ///   <item>否则(英文等) ⇒ <c>ec</c> → <c>ce_new</c>/<c>ce</c> → <c>newhh</c> → <c>baike</c>。</item>
    /// </list>
    /// 取**第一个有释义的来源**,不混排多个来源:面板只有 330px,混排会变成一锅粥。
    ///
    /// ⚠️ **<c>baike</c> 会误命中同名条目**:实测查「谢了」命中的是**同名歌曲**而不是"谢"的释义。
    /// 所以它排在词典之后,并且**来源名会显示出来**(调用方把它拼进标题),让用户能自己判断。
    ///
    /// ⚠️ **只有拼音、没有任何释义时返回 <c>null</c>** —— 对中文母语者,一个中文词的拼音几乎没有信息量,
    /// 不值得为它单开一块面板。拼音只作为音标行**附加**在释义上方。
    /// </summary>
    public static WordDefinition? Parse(string? json) {
        if (string.IsNullOrWhiteSpace(json)) return null;

        try {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;

            foreach (var section in SectionPriority(root)) {
                var definitions = ReadDefinitions(root, section);
                if (definitions.Count == 0) continue;
                return new WordDefinition(ReadPhonetic(root), definitions, ReadSourceName(root, section),
                    section == "baike");
            }
            return null;   // ★ 没有释义就不显示
        } catch (JsonException) {
            // 返回了 HTML 错误页 / 结构变了 —— 按"没有释义"处理,别让解析炸出去。
            return null;
        }
    }

    /// <summary>按查询语言给出来源优先级。判据是"查询里有没有中日韩字符",不是"哪个段有没有内容"。</summary>
    private static IEnumerable<string> SectionPriority(JsonElement root) {
        return ContainsCjk(ReadString(root, "input"))
            ? new[] { "newhh", "baike", "ec", "ce_new", "ce" }
            : new[] { "ec", "ce_new", "ce", "newhh", "baike" };
    }

    private static bool ContainsCjk(string value) {
        foreach (var ch in value) {
            if ((ch >= '\u4e00' && ch <= '\u9fff')       // 中日韩统一表意文字
                || (ch >= '\u3400' && ch <= '\u4dbf')    // 扩展 A
                || (ch >= '\uf900' && ch <= '\ufaff')) { // 兼容表意文字
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 取一个段里的释义。三种结构:
    /// ① <c>ec</c>/<c>ce</c>/<c>ce_new</c>:<c>word[0].trs[].tr[0].l.i[]</c>(词性可能写在文本里,也可能在 <c>pos</c>);
    /// ② <c>newhh</c>:<c>dataList[0].sense[].def[]</c>;
    /// ③ <c>baike</c>:<c>summarys[0].summary</c>(单条)。
    /// </summary>
    private static List<string> ReadDefinitions(JsonElement root, string section) {
        var definitions = new List<string>();
        if (!root.TryGetProperty(section, out var value) || value.ValueKind != JsonValueKind.Object) return definitions;

        if (section == "newhh") {
            foreach (var item in EnumerateArray(value, "dataList")) {
                foreach (var sense in EnumerateArray(item, "sense")) {
                    foreach (var text in EnumerateStrings(sense, "def")) {
                        AddDefinition(definitions, text, string.Empty);
                    }
                }
            }
            return definitions;
        }

        if (section == "baike") {
            foreach (var summary in EnumerateArray(value, "summarys")) {
                AddDefinition(definitions, ReadString(summary, "summary"), string.Empty);
            }
            return definitions;
        }

        foreach (var word in EnumerateArray(value, "word")) {
            foreach (var trs in EnumerateArray(word, "trs")) {
                foreach (var tr in EnumerateArray(trs, "tr")) {
                    var pos = ReadString(tr, "pos");
                    if (!tr.TryGetProperty("l", out var l) || l.ValueKind != JsonValueKind.Object) continue;
                    foreach (var text in EnumerateStrings(l, "i")) {
                        AddDefinition(definitions, text, pos);
                    }
                }
            }
        }
        return definitions;
    }

    private static void AddDefinition(List<string> definitions, string text, string pos) {
        if (definitions.Count >= MaxDefinitions) return;
        var trimmed = text.Trim();
        if (trimmed.Length == 0) return;

        // 词性多数已经写在文本里(如 "n. 苹果…"),只在文本没带时才补前缀,避免出现 "n. n. 苹果"。
        if (pos.Length > 0 && !trimmed.StartsWith(pos, StringComparison.Ordinal)) {
            trimmed = pos + " " + trimmed;
        }
        if (!definitions.Contains(trimmed)) definitions.Add(trimmed);
    }

    /// <summary>来源名(直接取接口给的,别自己写死 —— 它比我们更清楚这段是谁的)。</summary>
    private static string ReadSourceName(JsonElement root, string section) {
        if (!root.TryGetProperty(section, out var value) || value.ValueKind != JsonValueKind.Object) return string.Empty;
        if (!value.TryGetProperty("source", out var source) || source.ValueKind != JsonValueKind.Object) return string.Empty;
        return ReadString(source, "name");
    }

    /// <summary>
    /// 音标行。**中文词优先拼音**(<c>simple.word[0].phone</c> 比 <c>newhh</c> 自带的更易读,如 "mǎi bàn");
    /// 英文词优先英/美音标;都没有则空。
    /// </summary>
    private static string ReadPhonetic(JsonElement root) {
        var pinyin = ReadSimplePhone(root);
        var english = ReadEnglishPhonetic(root);
        return ContainsCjk(ReadString(root, "input"))
            ? (pinyin.Length > 0 ? pinyin : english)
            : (english.Length > 0 ? english : pinyin);
    }

    /// <summary><c>simple.word[0].phone</c> —— 拼音,中文词几乎都有。</summary>
    private static string ReadSimplePhone(JsonElement root) {
        foreach (var word in EnumerateArray(root, "simple", "word")) return ReadString(word, "phone");
        return string.Empty;
    }

    /// <summary>英式优先;英式与美式不同则都列出来。</summary>
    private static string ReadEnglishPhonetic(JsonElement root) {
        foreach (var word in EnumerateArray(root, "ec", "word")) {
            var uk = ReadString(word, "ukphone");
            var us = ReadString(word, "usphone");
            if (uk.Length == 0) return us.Length == 0 ? string.Empty : "/" + us + "/";
            if (us.Length == 0 || string.Equals(uk, us, StringComparison.Ordinal)) return "/" + uk + "/";
            return "英 /" + uk + "/  美 /" + us + "/";
        }
        return string.Empty;
    }

    /// <summary>沿 <paramref name="path"/> 取数组;任一层缺失或类型不对则给空序列(不抛)。</summary>
    private static IEnumerable<JsonElement> EnumerateArray(JsonElement parent, params string[] path) {
        var current = parent;
        foreach (var name in path) {
            if (current.ValueKind != JsonValueKind.Object) yield break;
            if (!current.TryGetProperty(name, out var next)) yield break;
            current = next;
        }
        if (current.ValueKind != JsonValueKind.Array) yield break;
        foreach (var item in current.EnumerateArray()) yield return item;
    }

    private static IEnumerable<string> EnumerateStrings(JsonElement parent, string name) {
        if (!parent.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array) yield break;
        foreach (var item in value.EnumerateArray()) {
            if (item.ValueKind == JsonValueKind.String) yield return item.GetString() ?? string.Empty;
        }
    }

    private static string ReadString(JsonElement element, string name) {
        if (element.ValueKind != JsonValueKind.Object) return string.Empty;
        if (!element.TryGetProperty(name, out var value)) return string.Empty;
        return value.ValueKind == JsonValueKind.String ? (value.GetString()?.Trim() ?? string.Empty) : string.Empty;
    }

    private static HttpClient CreateHttpClient() {
        var client = new HttpClient { Timeout = RequestTimeout };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        return client;
    }
}

/// <summary>一次查词的结果(纯数据,便于单测)。</summary>
/// <param name="Phonetic">英文音标(<c>/ˈæp(ə)l/</c>)或中文拼音(<c>mǎi bàn</c>);没有则为空串。</param>
/// <param name="Definitions">至少一条(为空时不会构造出本对象)。</param>
/// <param name="Source">来源名,直接取接口给的,如 <c>《现代汉语规范词典》</c> / <c>百度百科</c> / <c>有道词典</c>;
/// 取不到时为空串(调用方退回"在线释义"这种不带来源的说法)。</param>
/// <param name="IsEncyclopedia">这条是不是**百科摘要**(<c>baike</c> 段)。
/// 是的话调用方会把标题写成「百科摘要 · …」而不是「在线释义 · …」——
/// 百科会误命中同名条目(实测「谢了」命中同名歌曲),标题必须让人看出这不是词义。</param>
public sealed record WordDefinition(string Phonetic, IReadOnlyList<string> Definitions, string Source,
    bool IsEncyclopedia = false) {
    /// <summary>
    /// 拼成详情面板里那一块文本:音标/拼音单独一行(若有),下面是各行释义。
    /// 与「用法」列表(body)区分开 —— 那一段是 Kindle 记下的原句,这一段是词典释义。
    /// </summary>
    public string ToDisplayText() {
        var lines = new List<string>(Definitions.Count + 1);
        if (Phonetic.Length > 0) lines.Add(Phonetic);
        lines.AddRange(Definitions);
        return string.Join(Environment.NewLine, lines);
    }
}
