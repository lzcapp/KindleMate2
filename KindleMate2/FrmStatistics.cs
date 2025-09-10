using System.Diagnostics;
using System.Drawing.Imaging;
using System.Globalization;
using System.Windows.Forms.DataVisualization.Charting;
using DarkModeForms;
using KindleMate2.Application.Services.KM2DB;
using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Infrastructure.Helpers;
using KindleMate2.Infrastructure.Repositories.KM2DB;
using KindleMate2.Shared;
using KindleMate2.Shared.Constants;

namespace KindleMate2 {
    public partial class FrmStatistics : Form {
        private readonly ClippingService _clippingService;
        private readonly VocabService _vocabService;

        private List<Clipping> _clippings = [];
        private List<Vocab> _vocabs = [];

        public FrmStatistics() {
            InitializeComponent();

            var clippingRepository = new ClippingRepository(AppConstants.ConnectionString);
            _clippingService = new ClippingService(clippingRepository);

            var vocabRepository = new VocabRepository(AppConstants.ConnectionString);
            _vocabService = new VocabService(vocabRepository);

            SetTheme();
        }

        private void SetTheme() {
            var settingRepository = new SettingRepository(AppConstants.ConnectionString);
            var themeService = new ThemeService(settingRepository);
            if (!themeService.IsDarkTheme()) {
                return;
            }
            _ = new DarkModeCS(this, false);
            
            SetControlColor(chartBooksTime);
            SetControlColor(chartBooksWeek);
            SetControlColor(chartBooksHistory);
            SetControlColor(chartVocabsTime);
            SetControlColor(chartVocabsWeek);
            SetControlColor(chartVocabsHistory);

            SetUIText();
        }

        private void SetUIText() {
            Text = Strings.Statistics;
            tabPageBooks.Text = Strings.Clippings;
            tabPageVocabs.Text = Strings.Vocabulary_List;

            tabPageBooks.Text = Strings.Clippings;
            tabPageVocabs.Text = Strings.Vocabulary_List;
        }

        private static bool SetControlColor(Chart chart) {
            try {
                chart.BackColor = Colors.DarkGray;
                chart.ChartAreas[0].AxisX.LabelStyle.ForeColor = Color.White;
                return true;
            } catch (Exception e) {
                Console.WriteLine(StringHelper.GetExceptionMessage(nameof(SetControlColor), e));
                return false;
            }
        }

        private void FrmStatistic_Load(object sender, EventArgs e) {
            try {
                _clippings = _clippingService.GetAllClippings();
                _vocabs = _vocabService.GetAllVocabs();

                if (SetBookTab() | SetVocabTab()) {
                    SetLblStatistics();
                }
            } catch (Exception exception) {
                Messenger.MessageBox(exception.Message, Strings.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private bool SetBookTab() {
            try {
                if (_clippings.Count == 0) {
                    return false;
                }

                var validClippings = _clippings.AsEnumerable().Where(row => !string.IsNullOrEmpty(row.ClippingDate)).Select(row => ParseDateTime(row.ClippingDate!)).ToList();

                var listClippingsByDate = validClippings.GroupBy(date => new {
                    date.Year,
                    date.Month,
                    date.Day
                }).Select(group => new {
                    group.Key.Year,
                    group.Key.Month,
                    group.Key.Day,
                    Count = group.Count()
                }).OrderBy(x => x.Year).ThenBy(x => x.Month).ThenBy(x => x.Day).ToList();

                foreach (var dataPoint in listClippingsByDate) {
                    var label = $"{dataPoint.Year}.{dataPoint.Month}.{dataPoint.Day}";
                    chartBooksHistory.Series[0].Points.AddXY(label, dataPoint.Count);
                }

                var listClippingsByHour = validClippings.GroupBy(date => date.Hour).Select(group => new {
                    ClippingHour = group.Key,
                    ClippingCount = group.Count()
                }).OrderBy(x => x.ClippingHour).ToList();

                foreach (var dataPoint in listClippingsByHour) {
                    chartBooksTime.Series[0].Points.AddXY(dataPoint.ClippingHour, dataPoint.ClippingCount);
                }

                var listClippingsByWeekday = validClippings.GroupBy(date => (int)date.DayOfWeek).Select(group => new {
                    Weekday = group.Key,
                    ClippingCount = group.Count()
                }).OrderBy(x => x.Weekday).ToList();

                foreach (var dataPoint in listClippingsByWeekday) {
                    var label = DateTimeFormatInfo.CurrentInfo.AbbreviatedDayNames[dataPoint.Weekday];
                    chartBooksWeek.Series[0].Points.AddXY(label, dataPoint.ClippingCount);
                }

                return true;
            } catch (Exception e) {
                Console.WriteLine(StringHelper.GetExceptionMessage(nameof(SetBookTab), e));
                return false;
            }
        }

        private bool SetVocabTab() {
            try {
                if (_vocabs.Count == 0) {
                    tabControl.TabPages.Remove(tabPageVocabs);
                    return false;
                }

                var validVocabs = _vocabs.AsEnumerable().Where(row => !string.IsNullOrEmpty(row.Timestamp)).Select(row => DateTime.Parse(row.Timestamp!)).ToList();

                var listVocabsByDate = validVocabs.GroupBy(date => new {
                    date.Year,
                    date.Month,
                    date.Day
                }).Select(group => new {
                    group.Key.Year,
                    group.Key.Month,
                    group.Key.Day,
                    Count = group.Count()
                }).OrderBy(x => x.Year).ThenBy(x => x.Month).ThenBy(x => x.Day).ToList();
                
                foreach (var dataPoint in listVocabsByDate) {
                    var label = $"{dataPoint.Year}.{dataPoint.Month}.{dataPoint.Day}";
                    chartVocabsHistory.Series[0].Points.AddXY(label, dataPoint.Count);
                }

                var listVocabsByHour = validVocabs.GroupBy(date => date.Hour).Select(group => new {
                    ClippingHour = group.Key,
                    ClippingCount = group.Count()
                }).OrderBy(x => x.ClippingHour).ToList();

                foreach (var dataPoint in listVocabsByHour) {
                    chartVocabsTime.Series[0].Points.AddXY(dataPoint.ClippingHour, dataPoint.ClippingCount);
                }
                
                var listVocabsByWeekday = validVocabs.GroupBy(date => (int)date.DayOfWeek).Select(group => new {
                    Weekday = group.Key,
                    ClippingCount = group.Count()
                }).OrderBy(x => x.Weekday).ToList();

                foreach (var dataPoint in listVocabsByWeekday) {
                    var label = DateTimeFormatInfo.CurrentInfo.AbbreviatedDayNames[dataPoint.Weekday];
                    chartVocabsWeek.Series[0].Points.AddXY(label, dataPoint.ClippingCount);
                }
                
                return true;
            } catch (Exception e) {
                Console.WriteLine(StringHelper.GetExceptionMessage(nameof(SetVocabTab), e));
                return false;
            }
        }

        private static DateTime ParseDateTime(string field) {
            try {
                if (string.IsNullOrWhiteSpace(field)) {
                    return DateTime.MinValue;
                }
                DateTime date = DateTime.Parse(field);
                return date;
            } catch (Exception) {
                return DateTime.MinValue;
            }
        }

        private void BtnSave_Click(object sender, EventArgs e) {
            try {
                btnSave.Visible = false;
                WindowState = FormWindowState.Maximized;

                var bitmap = new Bitmap(Width, Height);
                DrawToBitmap(bitmap, new Rectangle(0, 0, Width, Height));
                var directoryPath = Path.Combine(Environment.CurrentDirectory, AppConstants.StatisticsPathName);
                var filePath = Path.Combine(directoryPath, DateTimeHelper.GetCurrentTimestamp() + FileExtension.PNG);
                if (!Directory.Exists(directoryPath)) {
                    Directory.CreateDirectory(directoryPath);
                }

                bitmap.Save(filePath, ImageFormat.Png);
                bitmap.Dispose();

                btnSave.Visible = true;
                WindowState = FormWindowState.Normal;

                MessageBox.Show(Strings.Statistics_Screenshot_Successful, Strings.Successful, MessageBoxButtons.OK, MessageBoxIcon.Information);
                Process.Start(AppConstants.ExplorerFileName, arguments: AppConstants.ExplorerSelect + filePath);
            } catch (Exception) {
                MessageBox.Show(Strings.Statistics_Screenshot_Failed, Strings.Failed, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            finally {
                btnSave.Visible = true;
                WindowState = FormWindowState.Normal;
            }
        }

        private void BtnSave_MouseEnter(object sender, EventArgs e) {
            Cursor = Cursors.Hand;
        }

        private void BtnSave_MouseLeave(object sender, EventArgs e) {
            Cursor = Cursors.Default;
        }

        private void TabControl_SelectedIndexChanged(object sender, EventArgs e) {
            SetLblStatistics();
        }

        private void SetLblStatistics() {
            var selectedIndex = tabControl.SelectedIndex;
            var text = string.Empty;
            switch (selectedIndex) {
                case 0:
                    if (_clippings.Count > 0) {
                        var clippings = _clippings.Count;
                        var books = _clippings.AsEnumerable().Select(row => row.BookName).Distinct().Count();
                        var authors = _clippings.AsEnumerable().Select(row => row.AuthorName).Distinct().Count();
                        var bookTimes = _clippings.AsEnumerable().Where(row => !string.IsNullOrEmpty(row.ClippingDate)).Select(row => DateTime.Parse(row.ClippingDate!)).ToList();
                        var bookDays = (bookTimes.Max() - bookTimes.Min()).Days;
                        text = Strings.In + Strings.Space + bookDays + Strings.Space + Strings.X_Days + Strings.Symbol_Comma + Strings.Totally + Strings.Space + clippings + Strings.Space + Strings.X_Clippings + Strings.Symbol_Comma +
                               books + Strings.Space + Strings.X_Books + Strings.Symbol_Comma + authors + Strings.Space + Strings.X_Authors;
                    }
                    break;
                case 1:
                    if (_vocabs.Count > 0) {
                        var lookups = _vocabs.Sum(row => Convert.ToInt32(row.Frequency));
                        var words = _vocabs.AsEnumerable().Select(row => row.Word).Distinct().Count();
                        var vocabTimes = _vocabs.AsEnumerable().Where(row => !string.IsNullOrEmpty(row.Timestamp)).Select(row => DateTime.Parse(row.Timestamp!)).ToList();
                        var vocabDays = (vocabTimes.Max() - vocabTimes.Min()).Days;
                        text = lblStatistics.Text = Strings.In + Strings.Space + vocabDays + Strings.Space + Strings.X_Days + Strings.Symbol_Comma + Strings.Totally + Strings.Space + lookups + Strings.Space + Strings.X_Lookups +
                                                    Strings.Symbol_Comma + words + Strings.Space + Strings.X_Vocabs;
                    }
                    break;
            }
            lblStatistics.Text = text;
        }
    }
}