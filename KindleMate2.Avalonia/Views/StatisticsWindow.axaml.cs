using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using KindleMate2.Avalonia.Charts;
using KindleMate2.Avalonia.ViewModels;
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
        if (_showClippings) {
            DateChart.Points = vm.ClippingsByDate;
            HourChart.Points = vm.ClippingsByHour;
            WeekdayChart.Points = vm.ClippingsByWeekday;
            SummaryText.Text = vm.ClippingSummary;
        } else {
            DateChart.Points = vm.VocabsByDate;
            HourChart.Points = vm.VocabsByHour;
            WeekdayChart.Points = vm.VocabsByWeekday;
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
            SummaryText.Text = $"已保存统计图:{file}";
        } catch (Exception ex) {
            SummaryText.Text = $"保存失败:{ex.Message}";
        }

        await Task.CompletedTask;
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
