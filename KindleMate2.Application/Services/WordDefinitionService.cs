using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
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
        } catch (Exception) {
            // 断网 / DNS 失败 / TLS 失败 / 超时 / 读流失败 —— 都是"这次没有释义"。
            return null;
        }
    }

    /// <summary>
    /// 从接口返回的 JSON 里抽出音标与释义。**纯函数**,单测直接喂样本串。
    ///
    /// 判据只看 <c>ec</c> 段(英汉释义):<c>ec.word[0]</c> 里 <c>ukphone</c>/<c>usphone</c> 是音标,
    /// <c>trs[].tr[0].l.i[]</c> 是释义文本(**词性已写在文本里**,如 "n. 苹果;苹果树…")。
    /// 查不到的词(如乱串)返回的 JSON 里**根本没有 <c>ec</c> 段** —— 这正是"没有释义就不显示"的干净信号。
    ///
    /// ⚠️ 已知覆盖边界:**中文词查不到**(实测"苹果"只回 <c>simple</c> 段、且其中无释义文本)。
    /// 中文书的生词要等换数据源(或改用 <c>ce</c>/<c>baike</c> 段)才可能有释义,不是本方法的 bug。
    /// </summary>
    public static WordDefinition? Parse(string? json) {
        if (string.IsNullOrWhiteSpace(json)) return null;

        try {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            if (!root.TryGetProperty("ec", out var ec) || ec.ValueKind != JsonValueKind.Object) return null;
            if (!ec.TryGetProperty("word", out var words) || words.ValueKind != JsonValueKind.Array) return null;
            if (words.GetArrayLength() == 0) return null;

            var entry = words[0];
            var phonetic = ReadPhonetic(entry);
            var definitions = ReadDefinitions(entry);

            // ★ 没有释义文本 ⇒ 交回 null(调用方不显示那一块)
            if (definitions.Count == 0) return null;
            return new WordDefinition(phonetic, definitions);
        } catch (JsonException) {
            // 返回了 HTML 错误页 / 结构变了 —— 按"没有释义"处理,别让解析炸出去。
            return null;
        }
    }

    private static List<string> ReadDefinitions(JsonElement entry) {
        var definitions = new List<string>();
        if (!entry.TryGetProperty("trs", out var trs) || trs.ValueKind != JsonValueKind.Array) return definitions;

        foreach (var trsItem in trs.EnumerateArray()) {
            if (definitions.Count >= MaxDefinitions) break;
            if (!trsItem.TryGetProperty("tr", out var trArray) || trArray.ValueKind != JsonValueKind.Array) continue;

            foreach (var tr in trArray.EnumerateArray()) {
                if (definitions.Count >= MaxDefinitions) break;
                var pos = ReadString(tr, "pos");
                if (!tr.TryGetProperty("l", out var l) || !l.TryGetProperty("i", out var items)) continue;
                if (items.ValueKind != JsonValueKind.Array) continue;

                foreach (var item in items.EnumerateArray()) {
                    if (definitions.Count >= MaxDefinitions) break;
                    if (item.ValueKind != JsonValueKind.String) continue;

                    var text = item.GetString()?.Trim() ?? string.Empty;
                    if (text.Length == 0) continue;

                    // 词性多数已经写在文本里(如 "n. 苹果…"),只有在文本没带时才补前缀,避免出现 "n. n. 苹果"。
                    if (pos.Length > 0 && !text.StartsWith(pos, StringComparison.Ordinal)) {
                        text = pos + " " + text;
                    }
                    if (!definitions.Contains(text)) definitions.Add(text);
                }
            }
        }
        return definitions;
    }

    /// <summary>音标:英式优先;英式与美式不同则都列出来。</summary>
    private static string ReadPhonetic(JsonElement entry) {
        var uk = ReadString(entry, "ukphone");
        var us = ReadString(entry, "usphone");
        if (uk.Length == 0) return us.Length == 0 ? string.Empty : "/" + us + "/";
        if (us.Length == 0 || string.Equals(uk, us, StringComparison.Ordinal)) return "/" + uk + "/";
        return "英 /" + uk + "/  美 /" + us + "/";
    }

    private static string ReadString(JsonElement element, string name) {
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
/// <param name="Phonetic">形如 <c>/ˈæp(ə)l/</c>;没有则为空串。</param>
/// <param name="Definitions">至少一条(为空时不会构造出本对象)。</param>
public sealed record WordDefinition(string Phonetic, IReadOnlyList<string> Definitions) {
    /// <summary>
    /// 拼成详情面板里那一块文本:音标单独一行(若有),下面是各行释义。
    /// 与「用法」列表(body)区分开 —— 那一段是 Kindle 记下的原句,这一段是词典释义。
    /// </summary>
    public string ToDisplayText() {
        var lines = new List<string>(Definitions.Count + 1);
        if (Phonetic.Length > 0) lines.Add(Phonetic);
        lines.AddRange(Definitions);
        return string.Join(Environment.NewLine, lines);
    }
}
