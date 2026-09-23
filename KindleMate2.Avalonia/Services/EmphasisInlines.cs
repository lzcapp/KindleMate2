using System.Collections.Generic;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using KindleMate2.Application.Services;

namespace KindleMate2.Avalonia.Services;

/// <summary>
/// 把 <see cref="WordEmphasis"/> 切出来的分段拼成一份 <see cref="InlineCollection"/>,
/// 命中的那几段加粗 —— 这样"在一整句里只加粗生词"才画得出来
/// (普通 <c>Text</c> 是纯字符串,加粗不了其中一部分)。
///
/// 为什么单独抽一层、而不是把 Inlines 直接塞进 ViewModel:ViewModel 与
/// <c>ListItem</c>/<c>DetailModel</c> 的数据里只能有<em>纯数据</em>
/// (哪一段是命中的),<c>InlineCollection</c> 是 Avalonia 控件层的东西。
/// 分开之后"怎么切"能在无头用例里验,"怎么画"只在这里一行。
///
/// ⚠️ 每次调用**新建**一份,不复用、不缓存:一个 <see cref="Run"/> 只能属于一个父级,
/// 缓存下来被两处绑定同时取到就会抛"已有父级"。一行的开销只是几个对象,不值得冒险。
/// </summary>
internal static class EmphasisInlines {

    /// <summary>命中的生词用粗体。<b>只加粗、不改颜色</b> ——
    /// 颜色得跟着深浅主题走,而 <c>Run</c> 上写死的颜色在另一套主题下就是错的;
    /// 粗体两套主题下都成立。</summary>
    private static readonly FontWeight Emphasis = FontWeight.Bold;

    /// <summary>
    /// 拼装。<paramref name="segments"/> 为空时退回 <paramref name="fallback"/> 整段 ——
    /// 保证**任何情况下都不会渲染出一个空白的 TextBlock**:正文该显示什么,
    /// 不能取决于"有没有做高亮"。
    /// </summary>
    public static InlineCollection Build(IReadOnlyList<EmphasisSegment>? segments, string fallback) {
        var inlines = new InlineCollection();
        if (segments is null || segments.Count == 0) {
            inlines.Add(new Run(fallback));
            return inlines;
        }
        foreach (var segment in segments) {
            // 空段不产出 Run:空 Run 不报错,但白白多一层内联对象
            if (segment.Text.Length == 0) continue;
            var run = new Run(segment.Text);
            if (segment.IsMatch) run.FontWeight = Emphasis;
            inlines.Add(run);
        }
        // 兜底同上:分段全是空串(畸形数据)也不能让这一行变成空白
        if (inlines.Count == 0) inlines.Add(new Run(fallback));
        return inlines;
    }
}
