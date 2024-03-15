using System.Data;
using System.Windows.Forms.DataVisualization.Charting;

namespace KindleMate2 {
    public partial class FrmStatistic : Form {
        private DataTable _clippingsDataTable = new();

        private DataTable _vocabDataTable = new();

        private readonly StaticData _staticData = new();

        public FrmStatistic() {
            InitializeComponent();
        }

        private void FrmStatistic_Load(object sender, EventArgs e) {
            tabPageBooks.Text = Strings.Clippings;
            tabPageVocabs.Text = Strings.Vocabulary_List;

            _clippingsDataTable = _staticData.GetClipingsDataTable();
            _staticData.GetOriginClippingsDataTable();
            _vocabDataTable = _staticData.GetVocabDataTable();
            _staticData.GetLookupsDataTable();

            var enumBooks = _clippingsDataTable.AsEnumerable().GroupBy(row => new {
                Time = DateTime.Parse(row.Field<string>("clippingdate") ?? string.Empty).ToString("yyyy-M-d")
            }).Select(group => new {
                group.Key.Time, Count = group.Count()
            });

            var listBooks = enumBooks.ToList().OrderBy(x => x.Time);
            foreach (var dataPoint in listBooks) {
                chartBooksHistory.Series[0].Points.AddXY(dataPoint.Time, dataPoint.Count);
            }

            //lblBooksCount.Text = listBooks.Count() + Strings.Space + Strings.X_Books;

            var enumBooksTime = _clippingsDataTable.AsEnumerable().GroupBy(row => DateTime.Parse(row.Field<string>("clippingdate") ?? string.Empty).TimeOfDay.Hours).Select(g => new {
                ClippingHour = g.Key, ClippingCount = g.Count()
            });

            var listBooksTime = enumBooksTime.ToList();
            foreach (var dataPoint in listBooksTime) {
                chartBooksTime.Series[0].Points.AddXY(dataPoint.ClippingHour, dataPoint.ClippingCount);
            }

            var enumBooksWeek = _clippingsDataTable.AsEnumerable().GroupBy(row => DateTime.Parse(row.Field<string>("clippingdate") ?? string.Empty).DayOfWeek.ToString()[..3].ToUpper()).Select(g => new {
                Weekday = g.Key, ClippingCount = g.Count()
            });

            var listBooksWeek = enumBooksWeek.ToList().OrderBy(x => x.Weekday);
            foreach (var dataPoint in listBooksWeek) {
                chartBooksWeek.Series[0].Points.AddXY(dataPoint.Weekday, dataPoint.ClippingCount);
            }

            //----------------------------//

            var enumVocabs = _vocabDataTable.AsEnumerable().GroupBy(row => new {
                Time = DateTime.Parse(row.Field<string>("timestamp") ?? string.Empty).ToString("yyyy-M-d")
            }).Select(group => new {
                group.Key.Time, Count = group.Count()
            });

            var listVocabs = enumVocabs.ToList().OrderBy(x => x.Time);
            foreach (var dataPoint in listVocabs) {
                chartVocabsHistory.Series[0].Points.AddXY(dataPoint.Time, dataPoint.Count);
            }

            //lblBooksCount.Text = listBooks.Count() + Strings.Space + Strings.X_Books;

            var enumVocabsTime = _vocabDataTable.AsEnumerable().GroupBy(row => DateTime.Parse(row.Field<string>("timestamp") ?? string.Empty).TimeOfDay.Hours).Select(g => new {
                ClippingHour = g.Key, ClippingCount = g.Count()
            });

            var listVocabsTime = enumVocabsTime.ToList();
            foreach (var dataPoint in listVocabsTime) {
                chartVocabsTime.Series[0].Points.AddXY(dataPoint.ClippingHour, dataPoint.ClippingCount);
            }

            var enumVocabsWeek = _vocabDataTable.AsEnumerable().GroupBy(row => DateTime.Parse(row.Field<string>("timestamp") ?? string.Empty).DayOfWeek.ToString()[..3].ToUpper()).Select(g => new {
                Weekday = g.Key, ClippingCount = g.Count()
            });

            var listVocabsWeek = enumVocabsWeek.ToList().OrderBy(x => x.Weekday);
            foreach (var dataPoint in listVocabsWeek) {
                chartVocabsWeek.Series[0].Points.AddXY(dataPoint.Weekday, dataPoint.ClippingCount);
            }
        }
    }
}