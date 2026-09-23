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
/// 样本取自 2026-09-23 对 <c>dict.youdao.com/jsonapi</c> 的真实响应(截取关键字段)。
/// </summary>
public sealed class WordDefinitionTests {

    /// <summary>真实形态:ec.word[0] 带音标 + trs[].tr[0].l.i[] 释义文本(词性已写在文本里)。</summary>
    private const string AppleSample = """
        {
          "input": "apple",
          "lang": "eng",
          "ec": {
            "source": { "name": "ec" },
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

    /// <summary>真实形态:查不到的词只有 meta/input/lang,**没有 ec 段**。</summary>
    private const string UnknownWordSample = """
        { "input": "zzzzqqq", "lang": "eng", "le": "en", "meta": { "input": "zzzzqqq" } }
        """;

    /// <summary>真实形态:中文词只有 simple 段(且其中没有释义文本),ec 段缺席。</summary>
    private const string ChineseWordSample = """
        {
          "input": "苹果",
          "lang": "eng",
          "simple": { "query": "苹果", "word": [ { "return-phrase": "苹果", "speech": "" } ] }
        }
        """;

    // ————————————————————— 解析 —————————————————————

    [Fact]
    public void Parse_ExtractsPhoneticAndDefinition() {
        var definition = WordDefinitionService.Parse(AppleSample);

        Assert.NotNull(definition);
        Assert.Equal("/ˈæp(ə)l/", definition!.Phonetic);
        Assert.Single(definition.Definitions);
        Assert.Equal("n. 苹果；苹果树；苹果公司（Apple Inc.）", definition.Definitions[0]);

        // 面板里那一块 = 音标一行 + 释义一行
        var lines = definition.ToDisplayText().Split(Environment.NewLine);
        Assert.Equal(2, lines.Length);
        Assert.Equal("/ˈæp(ə)l/", lines[0]);
    }

    [Fact]
    public void Parse_ReturnsNullWhenTheWordHasNoEntry() {
        // 查不到的词 ⇒ 调用方据此不显示释义块(需求原文:没有释义就不显示)
        Assert.Null(WordDefinitionService.Parse(UnknownWordSample));
    }

    [Fact]
    public void Parse_ReturnsNullForChineseWord() {
        // 已知覆盖边界:中文词查不到释义。**这条用例是"文档"** ——
        // 若将来换成能覆盖中文的源,它会红,那时应当改的是这里的期望值,不是源码。
        Assert.Null(WordDefinitionService.Parse(ChineseWordSample));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("<html><body>502 Bad Gateway</body></html>")]   // 接口挂了时可能返回 HTML
    [InlineData("{ \"ec\": ")]                                   // 截断的 JSON
    [InlineData("[]")]                                           // 顶层不是对象
    [InlineData("{\"ec\":{\"word\":[]}}")]                       // 有 ec 但没有词条
    [InlineData("{\"ec\":{\"word\":[{\"trs\":[]}]}}")]           // 有词条但没有释义文本
    public void Parse_ReturnsNullOnAnythingUnusable(string? json) {
        Assert.Null(WordDefinitionService.Parse(json));
    }

    [Fact]
    public void Parse_ListsBothPhonesWhenTheyDiffer() {
        const string sample = """
            { "ec": { "word": [ { "ukphone": "əʊ", "usphone": "oʊ",
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
            { "ec": { "word": [ { "trs": [ { "tr": [ { "pos": "n.",
              "l": { "i": ["苹果"] } } ] } ] } ] } }
            """;
        const string posAlreadyInside = """
            { "ec": { "word": [ { "trs": [ { "tr": [ { "pos": "n.",
              "l": { "i": ["n. 苹果"] } } ] } ] } ] } }
            """;

        Assert.Equal("n. 苹果", WordDefinitionService.Parse(withPos)!.Definitions[0]);
        Assert.Equal("n. 苹果", WordDefinitionService.Parse(posAlreadyInside)!.Definitions[0]);
    }

    [Fact]
    public void Parse_DeduplicatesAndCapsDefinitions() {
        // 12 条同构释义:去重后只剩 1 条;再另给 10 条不同的,验证上限为 8
        const string duplicates = """
            { "ec": { "word": [ { "trs": [
              { "tr": [ { "l": { "i": ["a", "a", "a"] } } ] },
              { "tr": [ { "l": { "i": ["a"] } } ] } ] } ] } }
            """;
        var many = new System.Text.StringBuilder();
        many.Append("""{ "ec": { "word": [ { "trs": [ { "tr": [ { "l": { "i": [ """);
        for (var i = 0; i < 12; i++) {
            if (i > 0) many.Append(", ");
            many.Append('"').Append("def-").Append(i).Append('"');
        }
        many.Append(""" ] } } ] } ] } ] } }""");

        Assert.Single(WordDefinitionService.Parse(duplicates)!.Definitions);
        Assert.Equal(8, WordDefinitionService.Parse(many.ToString())!.Definitions.Count);
    }

    [Fact]
    public void ToDisplayText_OmitsPhoneticLineWhenMissing() {
        var definition = new WordDefinition(string.Empty, new[] { "n. 苹果" });

        Assert.Equal("n. 苹果", definition.ToDisplayText());
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
