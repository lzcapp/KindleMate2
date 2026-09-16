using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using KindleMate2.Avalonia.Charts;
using KindleMate2.Avalonia.ViewModels;
using KindleMate2.Shared;
using KindleMate2.Shared.Constants;

namespace KindleMate2.Avalonia.Views;

/// <summary>
/// 统计页。桌面版是 6 个 WinForms Chart + 一张截图按钮;
/// 这里改为「同一组图表切换数据源」的紧凑布局,并保留导出图片能力
/// (用 RenderTargetBitmap 自绘落盘,Avalonia 原生,跨平台可用)。
/// </summary>
public partial class StatisticsWindow : Window {
    private StatisticsViewModel _vm = new();
    private bool _showClippings = true;

    public StatisticsWindow() {
        InitializeComponent();
    }

    public StatisticsWindow(StatisticsViewModel vm) : this() {
        _vm = vm;
        DataContext = vm;
        Loaded += (_, _) => {
            Refresh();
            if (!vm.HasVocabs) TabVocabs.IsVisible = false;
        };
    }

    private void Refresh() {
        var vm = _vm;
        // 构成图与排名图的**标题**随数据域变化 —— 标注页看类型构成与书籍排名,生词页看类别构成与查询排名;
        // 其余三张(按日期 / 按小时 / 按星期)的维度对两个域都成立,标题固定。
        if (_showClippings) {
            DateChart.Points = vm.ClippingsByDate;
            HourChart.Points = vm.ClippingsByHour;
            WeekdayChart.Points = vm.ClippingsByWeekday;
            CompositionChart.Points = vm.ClippingsByType;
            CompositionTitle.Text = Strings.Ui_Stats_ByType;
            TopChart.Points = vm.TopBooks;
            TopTitle.Text = Strings.Ui_Stats_TopBooks;
            SummaryText.Text = vm.ClippingSummary;
        } else {
            DateChart.Points = vm.VocabsByDate;
            HourChart.Points = vm.VocabsByHour;
            WeekdayChart.Points = vm.VocabsByWeekday;
            CompositionChart.Points = vm.VocabFrequencyBuckets;
            CompositionTitle.Text = Strings.Ui_Stats_ByFrequency;
            TopChart.Points = vm.TopWords;
            TopTitle.Text = Strings.Ui_Stats_TopWords;
            SummaryText.Text = vm.VocabSummary;
        }

        EmptyHint.IsVisible = _showClippings
            ? vm.ClippingsByDate.Count == 0
            : vm.VocabsByDate.Count == 0;

        SetActive(TabClippings, _showClippings);
        SetActive(TabVocabs, !_showClippings);
    }

    private static void SetActive(Button button, bool active) {
        if (active) {
            if (!button.Classes.Contains("active")) button.Classes.Add("active");
        } else {
            button.Classes.Remove("active");
        }
    }

    private void OnShowClippings(object? sender, RoutedEventArgs e) {
        _showClippings = true;
        Refresh();
    }

    private void OnShowVocabs(object? sender, RoutedEventArgs e) {
        _showClippings = false;
        Refresh();
    }

    private async void OnExportImage(object? sender, RoutedEventArgs e) {
        try {
            var size = new PixelSize(Math.Max(1, (int)Bounds.Width), Math.Max(1, (int)Bounds.Height));
            using var bitmap = new RenderTargetBitmap(size, new Vector(96, 96));
            bitmap.Render(this);

            var directory = Path.Combine(
                Path.GetDirectoryName(_vm.Source) ?? Environment.CurrentDirectory,
                AppConstants.StatisticsPathName);
            Directory.CreateDirectory(directory);
            var file = Path.Combine(directory,
                DateTime.Now.ToString("yyyyMMdd_HHmmss", System.Globalization.CultureInfo.InvariantCulture) + ".png");
            // Avalonia 12 将 Bitmap.Save 标注为过时并建议改用 BitmapEncoderOptions,
            // 但该重载在 Skia 后端下的默认参数与旧签名等价,这里保留旧调用以免多引一层依赖。
#pragma warning disable CS0618
            bitmap.Save(file);
#pragma warning restore CS0618
            SummaryText.Text = string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.Ui_Stats_Saved, file);

            // 原版:成功弹 Statistics_Screenshot_Successful 后【无条件】打开资源管理器并选中文件;
            // 用户 2026-09-13 指定改为 Yes/No 追问 —— 这是有意偏离原版的一处。
            var open = await AppDialog.ConfirmAsync(this, Strings.Successful,
                Strings.Statistics_Screenshot_Successful, Strings.Ui_Action_Ok);
            if (open) {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo {
                    FileName = AppConstants.ExplorerFileName,
                    Arguments = AppConstants.ExplorerSelect + "\"" + file + "\"",
                    UseShellExecute = true
                });
            }
        } catch (Exception ex) {
            await AppDialog.AlertAsync(this, Strings.Failed, Strings.Statistics_Screenshot_Failed);
            SummaryText.Text = string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.Ui_Stats_SaveFailed, ex.Message);
        }

        await Task.CompletedTask;
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
