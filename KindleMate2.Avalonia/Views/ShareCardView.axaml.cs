using Avalonia.Controls;

namespace KindleMate2.Avalonia.Views;

/// <summary>
/// 分享卡片的视图 —— 纯版式,不含任何逻辑。
///
/// 渲染由 <see cref="Services.ShareCardRenderer"/> 负责:它把这棵可视树挂进一扇不显示的窗口
/// (为了拿到应用的主题令牌与字体族),再 <c>RenderTargetBitmap</c> 落成 PNG。
/// </summary>
public partial class ShareCardView : UserControl {
    public ShareCardView() => InitializeComponent();
}
