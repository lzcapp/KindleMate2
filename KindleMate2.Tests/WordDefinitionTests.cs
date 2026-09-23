using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using KindleMate2.Application.Services;
using Xunit;

namespace KindleMate2.Tests;

/// <summary>
/// 在线释义的用例 —— **不碰网络**:解析是纯函数,http 那一层用假 handler 喂固定响应/固定异常。
/// 样本取自 2026-09-23 对 <c>dict.youdao.com/jsonapi</c> 的真实响应(截取关键字段),
/// 并刻意包含**用户真实生词本里的词**(买办 / 针指 / 潜龙勿用 / 戚本禹)—— 他的生词 94% 是中文。
/// </summary>
public sealed class WordDefinitionTests {

    /// <summary>英文词的真实形态:<c>ec.word[0]</c> 带英/美音标 + <c>trs[].tr[0].l.i[]</c> 释义文本。</summary>
    private const string AppleSample = """
        {
          "input": "apple",
          "lang": "eng",
          "ec": {
            "source": { "name": "有道词典" },
            "word": [
              {
                "return-phrase": "apple",
                "ukphone": "ˈæp(ə)l",
                "usphone": "ˈæp(ə)l",
                "trs": [
                  { "tr": [ { "l": { "i": ["n. 苹果；苹果树；苹果公司（Apple Inc.）"] } } ] }
                ]
              }
            ]
          }
        }
        """;

    /// <summary>中文词的真实形态:<c>newhh</c> = 《现代汉语规范词典》,释义在 <c>dataList[0].sense[].def[]</c>;
    /// 拼音另有 <c>simple.word[0].phone</c>(比 newhh 自带的 "mǎibàn" 更好读)。样本即「买办」。</summary>
    private const string ChineseDictionarySample = """
        {
          "input": "买办",
          "lang": "eng",
          "newhh": {
            "source": { "name": "《现代汉语规范词典》" },
            "word": "买办",
            "dataList": [
              {
                "word": "买办",
                "pinyin": "mǎibàn",
                "cat": "名词",
                "sense": [
                  { "cat": "名词",
                    "def": ["殖民地、半殖民地国家中，替外国资本家在本国市场上经营企业、开设银行等的代理人。"] }
                ]
              }
            ]
          },
          "simple": { "query": "买办", "word": [ { "phone": "mǎi bàn", "return-phrase": "买办" } ] }
        }
        """;

    /// <summary>中文词的另一种真实形态:词典里没有,只有 <c>baike</c>(百度百科摘要)。样本即「针指」。</summary>
    private const string BaikeSample = """
        {
          "input": "针指",
          "lang": "eng",
          "baike": {
            "source": { "name": "百度百科" },
            "summarys": [ { "key": "针指", "summary": "针指，读音为zhēn zhǐ，汉语词语，意思是指针线活。 " } ]
          },
          "web_trans": { "web-translation": [ { "key": "指针指示器", "trans": [ { "value": "needle indicator" } ] } ] }
        }
        """;

    /// <summary>真实形态:查不到的词(如「戚本禹」这类人名)只有 <c>meta/input/lang</c>,**什么段都没有**。</summary>
    private const string UnknownWordSample = """
        { "input": "戚本禹", "lang": "eng", "le": "en", "meta": { "input": "戚本禹" } }
        """;

    /// <summary>真实形态:只有拼音、没有任何释义段(实测「潜龙勿用」在无 ce 段时如此)。</summary>
    private const string PinyinOnlySample = """
        { "input": "潜龙勿用", "lang": "eng", "simple": { "query": "潜龙勿用",
          "word": [ { "phone": "qián lóng wù yòng", "return-phrase": "潜龙勿用" } ] } }
        """;

    // ————————————————————— 解析：英文词（ec） —————————————————————

    [Fact]
    public void Parse_ExtractsPhoneticDefinitionAndSource() {
        var definition = WordDefinitionService.Parse(AppleSample);

        Assert.NotNull(definition);
        Assert.Equal("/ˈæp(ə)l/", definition!.Phonetic);
        Assert.Single(definition.Definitions);
        Assert.Equal("n. 苹果；苹果树；苹果公司（Apple Inc.）", definition.Definitions[0]);
        // 来源名直接取接口给的（调用方把它拼进标题,用来提示"这条是谁说的"）
        Assert.Equal("有道词典", definition.Source);

        var lines = definition.ToDisplayText().Split(Environment.NewLine);
        Assert.Equal(2, lines.Length);
        Assert.Equal("/ˈæp(ə)l/", lines[0]);
    }

    [Fact]
    public void Parse_ListsBothPhonesWhenTheyDiffer() {
        const string sample = """
            { "input": "oh", "ec": { "word": [ { "ukphone": "əʊ", "usphone": "oʊ",
              "trs": [ { "tr": [ { "l": { "i": ["int. 哦"] } } ] } ] } ] } }
            """;

        var definition = WordDefinitionService.Parse(sample);

        Assert.NotNull(definition);
        Assert.Equal("英 /əʊ/  美 /oʊ/", definition!.Phonetic);
    }

    [Fact]
    public void Parse_DoesNotDuplicatePartOfSpeech() {
        // 词性多数已经写在释义文本里;只有文本没带时才补前缀 —— 免得出现 "n. n. 苹果"
        const string withPos = """
            { "input": "apple", "ec": { "word": [ { "trs": [ { "tr": [ { "pos": "n.",
              "l": { "i": ["苹果"] } } ] } ] } ] } }
            """;
        const string posAlreadyInside = """
            { "input": "apple", "ec": { "word": [ { "trs": [ { "tr": [ { "pos": "n.",
              "l": { "i": ["n. 苹果"] } } ] } ] } ] } }
            """;

        Assert.Equal("n. 苹果", WordDefinitionService.Parse(withPos)!.Definitions[0]);
        Assert.Equal("n. 苹果", WordDefinitionService.Parse(posAlreadyInside)!.Definitions[0]);
    }

    [Fact]
    public void Parse_DeduplicatesAndCapsDefinitions() {
        const string duplicates = """
            { "input": "apple", "ec": { "word": [ { "trs": [
              { "tr": [ { "l": { "i": ["a", "a", "a"] } } ] },
              { "tr": [ { "l": { "i": ["a"] } } ] } ] } ] } }
            """;
        var many = new System.Text.StringBuilder();
        many.Append("""{ "input": "apple", "ec": { "word": [ { "trs": [ { "tr": [ { "l": { "i": [ """);
        for (var i = 0; i < 12; i++) {
            if (i > 0) many.Append(", ");
            many.Append('"').Append("def-").Append(i).Append('"');
        }
        many.Append(""" ] } } ] } ] } ] } }""");

        Assert.Single(WordDefinitionService.Parse(duplicates)!.Definitions);
        Assert.Equal(8, WordDefinitionService.Parse(many.ToString())!.Definitions.Count);
    }

    [Fact]
    public void Parse_ToleratesMissingSourceName() {
        // 接口哪天不再给 source.name:来源给空串,调用方会退回"在线释义"这种不带来源的说法
        const string sample = """
            { "input": "apple", "ec": { "word": [ { "trs": [ { "tr": [ { "l": { "i": ["苹果"] } } ] } ] } ] } }
            """;

        var definition = WordDefinitionService.Parse(sample);

        Assert.NotNull(definition);
        Assert.Equal(string.Empty, definition!.Source);
    }

    // ————————————————————— 解析：中文词（newhh / baike / 拼音） —————————————————————

    [Fact]
    public void Parse_ReadsChineseDefinitionFromModernChineseDictionary() {
        // 这是本次最关键的用例:用户生词 94% 是中文词。此前只读 ec 段 ⇒ 这类词永远查不到。
        var definition = WordDefinitionService.Parse(ChineseDictionarySample);

        Assert.NotNull(definition);
        Assert.Equal("《现代汉语规范词典》", definition!.Source);
        Assert.Equal("殖民地、半殖民地国家中，替外国资本家在本国市场上经营企业、开设银行等的代理人。",
            definition.Definitions[0]);
        // 中文词的音标行用拼音(simple 给的更易读:"mǎi bàn" 而不是 newhh 的 "mǎibàn")
        Assert.Equal("mǎi bàn", definition.Phonetic);
        Assert.Equal("mǎi bàn", definition.ToDisplayText().Split(Environment.NewLine)[0]);
    }

    [Fact]
    public void Parse_FallsBackToBaikeWhenTheDictionaryHasNoEntry() {
        var definition = WordDefinitionService.Parse(BaikeSample);

        Assert.NotNull(definition);
        Assert.Equal("百度百科", definition!.Source);   // ⚠️ 来源必须能看出来:百科会误命中同名条目
        Assert.Single(definition.Definitions);
        Assert.Contains("针线活", definition.Definitions[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_PrefersChineseDictionaryOverEnglishGlossForChineseQuery() {
        // 中文词同时有 newhh(中文释义) 与 ce(英文释义) 时,必须给中文的那条
        const string sample = """
            { "input": "买办",
              "ce": { "source": { "name": "有道词典" },
                      "word": [ { "trs": [ { "tr": [ { "l": { "i": ["comprador"] } } ] } ] } ] },
              "newhh": { "source": { "name": "《现代汉语规范词典》" },
                         "dataList": [ { "sense": [ { "def": ["替外国资本家经营企业的代理人。"] } ] } ] } }
            """;

        var definition = WordDefinitionService.Parse(sample);

        Assert.Equal("《现代汉语规范词典》", definition!.Source);
        Assert.Equal("替外国资本家经营企业的代理人。", definition.Definitions[0]);
    }

    [Fact]
    public void Parse_PrefersEnglishEntryForAsciiQuery() {
        // 反向:英文词同时有 ec(音标+英汉) 与 newhh 时,不能被 newhh 抢走
        const string sample = """
            { "input": "apple",
              "newhh": { "source": { "name": "《现代汉语规范词典》" },
                         "dataList": [ { "sense": [ { "def": ["不该被选中的中文条目"] } ] } ] },
              "ec": { "source": { "name": "有道词典" }, "word": [ { "ukphone": "ˈæp(ə)l",
                      "trs": [ { "tr": [ { "l": { "i": ["n. 苹果"] } } ] } ] } ] } }
            """;

        var definition = WordDefinitionService.Parse(sample);

        Assert.Equal("有道词典", definition!.Source);
        Assert.Equal("/ˈæp(ə)l/", definition.Phonetic);
    }

    [Fact]
    public void Parse_ReturnsNullWhenOnlyPinyinIsAvailable() {
        // 只有拼音、没有任何释义 ⇒ 不显示(对中文母语者,中文词的拼音几乎没有信息量)。
        // ⚠️ 这条**不是**"中文词查不到释义"——那是我 2026-09-23 之前的错误结论(只读了 ec 段);
        //    中文词的释义在 newhh / baike 段里,见上面两条用例。
        Assert.Null(WordDefinitionService.Parse(PinyinOnlySample));
    }

    [Fact]
    public void Parse_ReturnsNullWhenTheWordHasNoSectionAtAll() {
        // 人名之类查不到的词:一个释义段都没有 ⇒ 不显示
        Assert.Null(WordDefinitionService.Parse(UnknownWordSample));
    }

    [Fact]
    public void Parse_MarksBaikeAsEncyclopedia() {
        // 调用方据此把标题写成「百科摘要 · 百度百科」(而不是「在线释义 · 百度百科」)
        Assert.True(WordDefinitionService.Parse(BaikeSample)!.IsEncyclopedia);
        Assert.False(WordDefinitionService.Parse(AppleSample)!.IsEncyclopedia);
        Assert.False(WordDefinitionService.Parse(ChineseDictionarySample)!.IsEncyclopedia);
    }

    [Fact]
    public void ToDisplayText_OmitsPhoneticLineWhenMissing() {
        var definition = new WordDefinition(string.Empty, new[] { "n. 苹果" }, "有道词典");

        Assert.Equal("n. 苹果", definition.ToDisplayText());
    }

    // ————————————————————— 解析：坏输入 —————————————————————

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("<html><body>502 Bad Gateway</body></html>")]   // 接口挂了时可能返回 HTML
    [InlineData("{ \"ec\": ")]                                   // 截断的 JSON
    [InlineData("[]")]                                           // 顶层不是对象
    [InlineData("{\"ec\":{\"word\":[]}}")]                       // 有 ec 但没有词条
    [InlineData("{\"ec\":{\"word\":[{\"trs\":[]}]}}")]           // 有词条但没有释义文本
    [InlineData("{\"newhh\":{\"dataList\":[{\"sense\":[{\"def\":[]}]}]}}")]   // newhh 的 def 为空
    [InlineData("{\"baike\":{\"summarys\":[{\"summary\":\"   \"}]}}")]        // baike 摘要只有空白
    public void Parse_ReturnsNullOnAnythingUnusable(string? json) {
        Assert.Null(WordDefinitionService.Parse(json));
    }

    // ————————————————————— 联网(用假 handler,不真连) —————————————————————

    [Fact]
    public async Task LookupAsync_ReturnsNullWhenTheNetworkFails() {
        // 需求原文:没联网就不显示 —— 这一条钉的就是"断网给 null,而不是抛异常/给空块"
        using var client = new HttpClient(new ThrowingHandler());

        Assert.Null(await WordDefinitionService.LookupAsync("apple", client));
    }

    [Fact]
    public async Task LookupAsync_ReturnsNullOnHttpError() {
        using var client = new HttpClient(new FixedResponseHandler(HttpStatusCode.ServiceUnavailable, "nope"));

        Assert.Null(await WordDefinitionService.LookupAsync("apple", client));
    }

    [Fact]
    public async Task LookupAsync_SendsTheWordUrlEncodedAndParsesTheResponse() {
        var handler = new FixedResponseHandler(HttpStatusCode.OK, AppleSample);
        using var client = new HttpClient(handler);

        var definition = await WordDefinitionService.LookupAsync("run out of", client);

        Assert.NotNull(definition);
        Assert.Equal("n. 苹果；苹果树；苹果公司（Apple Inc.）", definition!.Definitions[0]);
        // 空格必须转义,否则多词查询会被截断。
        // 注意用 AbsoluteUri 而不是 ToString():后者给的是**显示形态**(会把 %20 解回空格),
        // 断言写在它上面会红,而且红得很有迷惑性 —— 真实请求其实是转义过的。
        Assert.Contains("run%20out%20of", handler.LastRequestUri!.AbsoluteUri, StringComparison.Ordinal);
        Assert.Equal("https://dict.youdao.com/jsonapi", handler.LastRequestUri.GetLeftPart(UriPartial.Path));
    }

    [Fact]
    public async Task LookupAsync_PropagatesCancellationInsteadOfReportingNoDefinition() {
        // 取消(用户换了选中项)不能退化成"没有释义" —— 否则调用方会把这个结论缓存下来,
        // 这个词在本次会话里就再也不会被查了。
        using var client = new HttpClient(new FixedResponseHandler(HttpStatusCode.OK, AppleSample));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => WordDefinitionService.LookupAsync("apple", client, cancellation.Token));
    }

    [Fact]
    public async Task LookupAsync_ReturnsNullForBlankWordWithoutTouchingTheNetwork() {
        var handler = new FixedResponseHandler(HttpStatusCode.OK, AppleSample);
        using var client = new HttpClient(handler);

        Assert.Null(await WordDefinitionService.LookupAsync("   ", client));
        Assert.Null(handler.LastRequestUri);
    }

    private sealed class ThrowingHandler : HttpMessageHandler {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new HttpRequestException("模拟断网");
    }

    private sealed class FixedResponseHandler(HttpStatusCode status, string body) : HttpMessageHandler {
        public Uri? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) {
            cancellationToken.ThrowIfCancellationRequested();
            LastRequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
        }
    }
}
