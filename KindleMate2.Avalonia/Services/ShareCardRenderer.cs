using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using KindleMate2.Avalonia.ViewModels;
using KindleMate2.Avalonia.Views;

namespace KindleMate2.Avalonia.Services;

/// <summary>
/// 把一张分享卡片渲染成 PNG。
///
/// 为什么要**挂进一扇不显示的窗口**再渲染,而不是直接对一个游离的控件
/// <c>Measure/Arrange</c> 后渲染:卡片用的全是 <c>{DynamicResource Km…}</c> 主题令牌,
/// 字体也来自 <c>Window</c> 样式里那条 CJK 回退链 —— 这两样都靠**树继承**,
/// 游离的控件拿不到(字体拿不到最致命:中文会掉进默认字体的方框)。
/// 所以走真窗口这条唯一可靠的路,代价是得把它显示出来才有布局。
///
/// 与统计页「导出图片」同源(`RenderTargetBitmap` + `Save`),那条链路是验证过的。
/// </summary>
internal static class ShareCardRenderer {

    /// <summary>设计稿定的画布宽度(4:5 竖版 = 1080×1350)。</summary>
    public const int CardWidth = 1080;

    /// <summary>最小高度。短正文也出一张 1350 的卡片,不让它变成矮胖的一条。</summary>
    public const int CardMinHeight = 1350;

    /// <summary>
    /// 渲染并写盘。失败时抛异常,由调用方转成用户提示。
    /// </summary>
    /// <remarks>
    /// 必须在 UI 线程调用(要建窗口、跑布局)。
    /// </remarks>
    public static async Task RenderToPngAsync(ShareCardModel model, string filePath) {
        var card = new ShareCardView { DataContext = model };

        var host = new Window {
            // 窗口全程 0 透明、只活到渲染完,所以不必管窗口装饰
            // (Avalonia 12 的 Window 上已经没有 SystemDecorations 属性了)。
            ShowInTaskbar = false,
            ShowActivated = false,   // 别把焦点从主窗口抢走
            CanResize = false,
            SizeToContent = SizeToContent.Height,
            Width = CardWidth,
            // 全透明:窗口**必须真显示出来**才有布局(否则 DesiredSize 是 0),
            // 但用户不该看见它闪一下。渲染的是卡片自己,不是这扇窗口,
            // 所以窗口的 Opacity 不会影响出图。
            Opacity = 0,
            Content = card
        };

        try {
            host.Show();
            // 等一次布局跑完 —— Show 之后 DesiredSize/Bounds 才有意义。
            // 用 Loaded 优先级把这条消息排在布局之后,比死等一帧稳。
            await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Loaded);

            var height = Math.Max(CardMinHeight, (int)Math.Ceiling(card.Bounds.Height));
            using var bitmap = new RenderTargetBitmap(new PixelSize(CardWidth, height), new Vector(96, 96));
            bitmap.Render(card);
            // Avalonia 12 把 Save 标注为过时并建议 BitmapEncoderOptions,但该重载在 Skia 后端下
            // 与旧签名等价 —— 与 StatisticsWindow 的导出保持同一写法,不为此多引一层依赖。
#pragma warning disable CS0618
            bitmap.Save(filePath);
#pragma warning restore CS0618
        } finally {
            host.Close();
        }
    }
}
