using System.Data;
using System.Drawing.Imaging;
using System.Globalization;

namespace KindleMate2 {
    public partial class FrmStatistics : Form {
        private DataTable _clippingsDataTable = new();

        private DataTable _vocabDataTable = new();

        private readonly StaticData _staticData = new();

        public FrmStatistics() {
            InitializeComponent();
        }

        private void FrmStatistic_Load(object sender, EventArgs e) {
            Text = Strings.Statistics;
            tabPageBooks.Text = Strings.Clippings;
            tabPageVocabs.Text = Strings.Vocabulary_List;

            tabPageBooks.Text = Strings.Clippings;
            tabPageVocabs.Text = Strings.Vocabulary_List;

            _clippingsDataTable = _staticData.GetClipingsDataTable();
            _staticData.GetOriginClippingsDataTable();
            _vocabDataTable = _staticData.GetVocabDataTable();
            _staticData.GetLookupsDataTable();

            var enumBooks = _clippingsDataTable.AsEnumerable()
                .GroupBy(row => {
                    DateTime date = DateTime.Parse(row.Field<string>("clippingdate") ?? string.Empty);
                    return new {
                        date.Year, date.Month, date.Day
                    };
                })
                .Select(group => new {
                    group.Key.Year,
                    group.Key.Month,
                    group.Key.Day,
                    Count = group.Count()
                });
            var listBooks = enumBooks
                .OrderBy(x => x.Year)
                .ThenBy(x => x.Month)
                .ThenBy(x => x.Day)
                .ToList();
            foreach (var dataPoint in listBooks) {
                chartBooksHistory.Series[0].Points.AddXY(dataPoint.Year + "." + dataPoint.Month + "." + dataPoint.Day, dataPoint.Count);
            }

            var enumBooksTime = _clippingsDataTable.AsEnumerable().GroupBy(row => DateTime.Parse(row.Field<string>("clippingdate") ?? string.Empty).TimeOfDay.Hours).Select(g => new {
                ClippingHour = g.Key, ClippingCount = g.Count()
            });

            var listBooksTime = enumBooksTime.ToList();
            foreach (var dataPoint in listBooksTime) {
                chartBooksTime.Series[0].Points.AddXY(dataPoint.ClippingHour, dataPoint.ClippingCount);
            }

            var enumBooksWeek = _clippingsDataTable.AsEnumerable().GroupBy(row => (int)DateTime.Parse(row.Field<string>("clippingdate") ?? string.Empty).DayOfWeek).Select(g => new {
                Weekday = g.Key, ClippingCount = g.Count()
            });

            var listBooksWeek = enumBooksWeek.ToList().OrderBy(x => x.Weekday);
            foreach (var dataPoint in listBooksWeek) {
                chartBooksWeek.Series[0].Points.AddXY(DateTimeFormatInfo.CurrentInfo.AbbreviatedDayNames[dataPoint.Weekday], dataPoint.ClippingCount);
            }

            var enumVocabs = _vocabDataTable.AsEnumerable()
                .GroupBy(row => {
                    DateTime date = DateTime.Parse(row.Field<string>("timestamp") ?? string.Empty);
                    return new {
                        date.Year, date.Month, date.Day
                    };
                })
                .Select(group => new { 
                    group.Key.Year, 
                    group.Key.Month, 
                    group.Key.Day, 
                    Count = group.Count()
                });
            var listVocabs = enumVocabs
                .OrderBy(x => x.Year)
                .ThenBy(x => x.Month)
                .ThenBy(x => x.Day)
                .ToList();
            foreach (var dataPoint in listVocabs) {
                chartVocabsHistory.Series[0].Points.AddXY(dataPoint.Year + "." + dataPoint.Month + "." + dataPoint.Day, dataPoint.Count);
            }

            var enumVocabsTime = _vocabDataTable.AsEnumerable().GroupBy(row => DateTime.Parse(row.Field<string>("timestamp") ?? string.Empty).TimeOfDay.Hours).Select(g => new {
                ClippingHour = g.Key, ClippingCount = g.Count()
            });

            var listVocabsTime = enumVocabsTime.ToList();
            foreach (var dataPoint in listVocabsTime) {
                chartVocabsTime.Series[0].Points.AddXY(dataPoint.ClippingHour, dataPoint.ClippingCount);
            }

            var enumVocabsWeek = _vocabDataTable.AsEnumerable().GroupBy(row => (int)DateTime.Parse(row.Field<string>("timestamp") ?? string.Empty).DayOfWeek).Select(g => new {
                Weekday = g.Key, ClippingCount = g.Count()
            });

            var listVocabsWeek = enumVocabsWeek.ToList().OrderBy(x => x.Weekday);
            foreach (var dataPoint in listVocabsWeek) {
                chartVocabsWeek.Series[0].Points.AddXY(DateTimeFormatInfo.CurrentInfo.AbbreviatedDayNames[dataPoint.Weekday], dataPoint.ClippingCount);
            }

            SetLblStatistics();
        }

        private void BtnSave_Click(object sender, EventArgs e) {
            try {
                btnSave.Visible = false;

                var bitmap = new Bitmap(Width, Height);
                DrawToBitmap(bitmap, new Rectangle(0, 0, Width, Height));
                var unixTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds() + ".png";
                var filePath = Path.Combine(Environment.CurrentDirectory, "Statistics", unixTimestamp);
                var directoryPath = Path.Combine(Environment.CurrentDirectory, "Statistics");
                if (!Directory.Exists(directoryPath)) {
                    Directory.CreateDirectory(directoryPath);
                }

                bitmap.Save(filePath, ImageFormat.Png);
                bitmap.Dispose();

                MessageBox.Show(Strings.Statistics_Screenshot_Successful, Strings.Successful, MessageBoxButtons.OK, MessageBoxIcon.Information);
            } catch (Exception) {
                MessageBox.Show(Strings.Statistics_Screenshot_Failed, Strings.Failed, MessageBoxButtons.OK, MessageBoxIcon.Information);
            } finally {
                btnSave.Visible = true;
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
            switch (selectedIndex) {
                case 0:
                    var clippings = _clippingsDataTable.Rows.Count + Strings.Space + Strings.X_Clippings;
                    var books = _clippingsDataTable.AsEnumerable().Select(row => row.Field<string>("bookname")).Distinct().Count() + Strings.Space + Strings.X_Books;
                    var authors = _clippingsDataTable.AsEnumerable().Select(row => row.Field<string>("authorname")).Distinct().Count() + Strings.Space + Strings.X_Authors;
                    var bookTimes = _clippingsDataTable.AsEnumerable().Select(row => DateTime.Parse(row.Field<string>("clippingdate") ?? string.Empty));
                    var bookDays = (bookTimes.Max() - bookTimes.Min()).Days;
                    lblStatistics.Text = Strings.In + Strings.Space + bookDays + Strings.Space + Strings.X_Days + Strings.Symbol_Comma + Strings.Totally + Strings.Space + clippings + Strings.Symbol_Comma + books + Strings.Symbol_Comma + authors;
                    break;
                case 1:
                    var lookups = _vocabDataTable.Rows.Cast<DataRow>().Sum(row => Convert.ToInt32(row["frequency"])) + Strings.Space + Strings.X_Lookups;
                    var words = _vocabDataTable.AsEnumerable().Select(row => row.Field<string>("word")).Distinct().Count() + Strings.Space + Strings.X_Vocabs;
                    var vocabTimes = _vocabDataTable.AsEnumerable().Select(row => DateTime.Parse(row.Field<string>("timestamp") ?? string.Empty));
                    var vocabDays = (vocabTimes.Max() - vocabTimes.Min()).Days;
                    lblStatistics.Text = Strings.In + Strings.Space + vocabDays + Strings.Space + Strings.X_Days + Strings.Symbol_Comma + Strings.Totally + Strings.Space + lookups + Strings.Symbol_Comma + words;
                    break;
            }
        }
    }
}