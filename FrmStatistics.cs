using System.Data;
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
            tabPageBooks.Text = Strings.Clippings;
            tabPageVocabs.Text = Strings.Vocabulary_List;

            _clippingsDataTable = _staticData.GetClipingsDataTable();
            _staticData.GetOriginClippingsDataTable();
            _vocabDataTable = _staticData.GetVocabDataTable();
            _staticData.GetLookupsDataTable();

            var enumBooks = _clippingsDataTable.AsEnumerable()
                .GroupBy(row => new {
                    DateTime.Parse(row.Field<string>("clippingdate") ?? string.Empty).Year, DateTime.Parse(row.Field<string>("clippingdate") ?? string.Empty).Month
                })
                .Select(group => new {
                    group.Key.Year,
                    group.Key.Month,
                    Count = group.Count()
                });
            var listBooks = enumBooks
                .OrderBy(x => x.Year)
                .ThenBy(x => x.Month)
                .ToList();
            foreach (var dataPoint in listBooks) {
                chartBooksHistory.Series[0].Points.AddXY(dataPoint.Year + "-" + dataPoint.Month, dataPoint.Count);
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
                .GroupBy(row => new { 
                    DateTime.Parse(row.Field<string>("timestamp") ?? string.Empty).Year, DateTime.Parse(row.Field<string>("timestamp") ?? string.Empty).Month
                })
                .Select(group => new { 
                    group.Key.Year, 
                    group.Key.Month, 
                    Count = group.Count()
                });
            var listVocabs = enumVocabs
                .OrderBy(x => x.Year)
                .ThenBy(x => x.Month)
                .ToList();
            foreach (var dataPoint in listVocabs) {
                chartVocabsHistory.Series[0].Points.AddXY(dataPoint.Year + "-" + dataPoint.Month, dataPoint.Count);
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
            var bitmap = new Bitmap(Width, Height);
            DrawToBitmap(bitmap, new Rectangle(0, 0, Width, Height));
            var unixTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds() + ".png";
            var filePath = Path.Combine(Environment.CurrentDirectory, "Statistics", unixTimestamp);
            var directoryPath = Path.Combine(Environment.CurrentDirectory, "Statistics");
            if (!Directory.Exists(directoryPath)) {
                Directory.CreateDirectory(directoryPath);
            }

            bitmap.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);
            bitmap.Dispose();
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
                    var bookTimes = _vocabDataTable.AsEnumerable().Select(row => DateTime.Parse(row.Field<string>("timestamp") ?? string.Empty));
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