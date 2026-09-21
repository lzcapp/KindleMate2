using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using KindleMate2.Avalonia.Charts;
using KindleMate2.Avalonia.Services;
using KindleMate2.Avalonia.ViewModels;
using KindleMate2.Infrastructure.Helpers;
using KindleMate2.Shared;
using KindleMate2.Shared.Constants;
using KindleMate2.Shared.Diagnostics;

namespace KindleMate2.Avalonia.Views;

/// <summary>
/// 统计页。桌面版是 6 个 WinForms Chart + 一张截图按钮;
/// 这里改为「同一组图表切换数据源」的紧凑布局,并保留导出图片能力
/// (用 RenderTargetBitmap 自绘落盘,Avalonia 原生,跨平台可用)。
/// </summary>
public partial class StatisticsWindow : Window {
    private StatisticsViewModel _vm = new();
    private bool _showClippings = true;

    /// <summary>年历热力图选中的年份。两个数据域的年份区间不同,各记一份 —— 来回切域不会把挑好的年份弄丢。</summary>
    private int? _clippingsYear;
    private int? _vocabsYear;

    /// <summary>
    /// 程序化改写 <c>CalendarYearBox.SelectedItem</c> 时也会走 <c>SelectionChanged</c>,
    /// 用它挡住那次回环,免得把"因为数据换了域而重设"误当成用户选择。
    /// </summary>
    private bool _syncingYear;

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
            RefreshCalendar(vm.ClippingsCalendar, clippings: true);
            SummaryText.Text = vm.ClippingSummary;
        } else {
            DateChart.Points = vm.VocabsByDate;
            HourChart.Points = vm.VocabsByHour;
            WeekdayChart.Points = vm.VocabsByWeekday;
            CompositionChart.Points = vm.VocabFrequencyBuckets;
            CompositionTitle.Text = Strings.Ui_Stats_ByFrequency;
            TopChart.Points = vm.TopWords;
            TopTitle.Text = Strings.Ui_Stats_TopWords;
            RefreshCalendar(vm.VocabsCalendar, clippings: false);
            SummaryText.Text = vm.VocabSummary;
        }

        EmptyHint.IsVisible = _showClippings
            ? vm.ClippingsByDate.Count == 0
            : vm.VocabsByDate.Count == 0;

        SetActive(TabClippings, _showClippings);
        SetActive(TabVocabs, !_showClippings);
    }

    /// <summary>
    /// 刷新年历热力图与它的年份选择器。
    ///
    /// 可选年份来自当前数据域里**真正出现过**的年份(降序),所以标注域与生词域各是各的 ——
    /// 换域时若沿用上一域的年份,就会指向一个本域根本没有的年份,热力图会整片空白。
    /// 只有一年数据时不显示选择器:单项下拉框没有意义,还会白占标题行的位置。
    /// </summary>
    private void RefreshCalendar(IReadOnlyList<ChartPoint> calendar, bool clippings) {
        var years = StatisticsViewModel.CalendarYears(calendar);

        var selected = clippings ? _clippingsYear : _vocabsYear;
        if (selected == null || !years.Contains(selected.Value)) {
            selected = years.Count > 0 ? years[0] : null;
        }
        if (clippings) _clippingsYear = selected; else _vocabsYear = selected;

        // 先换条目再定选中值,否则 SelectedItem 会短暂落在一个已不存在的条目上而被清成 null
        _syncingYear = true;
        CalendarYearBox.ItemsSource = years;
        CalendarYearBox.SelectedItem = selected;
        CalendarYearBox.IsVisible = years.Count > 1;
        _syncingYear = false;

        CalendarChart.Points = calendar;
        CalendarChart.HeatmapYear = selected;
    }

    /// <summary>用户换了年份 —— 数据不动,只让热力图改画那一年(几何由 ChartControl 自己算)。</summary>
    private void OnCalendarYearChanged(object? sender, SelectionChangedEventArgs e) {
        if (_syncingYear) return;

        var year = CalendarYearBox.SelectedItem as int?;
        if (_showClippings) _clippingsYear = year; else _vocabsYear = year;
        CalendarChart.HeatmapYear = year;
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
                Path.GetDirectoryName(_vm.Source) ?? AppPaths.DataDirectory,
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
                // 平台差异(explorer.exe / open -R / xdg-open)由 ShellHelper 承担,
                // 截图已落盘且路径就在 SummaryText 上,打开失败只记日志,
                // 不再让外层 catch 把它误报成「截图保存失败」。
                try {
                    ShellHelper.RevealFile(file);
                } catch (Exception ex) {
                    AppLog.Write(ex);
                }
            }
        } catch (Exception ex) {
            await AppDialog.AlertAsync(this, Strings.Failed, Strings.Statistics_Screenshot_Failed);
            SummaryText.Text = string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.Ui_Stats_SaveFailed, ex.Message);
        }

        await Task.CompletedTask;
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
