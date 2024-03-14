using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

namespace KindleMate2 {
    public partial class FrmStatistic : Form {
        private DataTable _clippingsDataTable = new();

        private DataTable _originClippingsDataTable = new();

        private DataTable _vocabDataTable = new();

        private DataTable _lookupsDataTable = new();

        private readonly StaticData _staticData = new();

        public FrmStatistic() {
            InitializeComponent();
        }

        private void FrmStatistic_Load(object sender, EventArgs e) {
            _clippingsDataTable = _staticData.GetClipingsDataTable();
            _originClippingsDataTable = _staticData.GetOriginClippingsDataTable();
            _vocabDataTable = _staticData.GetVocabDataTable();
            _lookupsDataTable = _staticData.GetLookupsDataTable();

            var enumBooks = _clippingsDataTable.AsEnumerable()
                .GroupBy(row => row.Field<string>("bookname"))
                .Select(g => new {
                    BookName = g.Key,
                    EarliestClippingDate = g.Min(row => row.Field<string>("clippingdate")),
                    LatestClippingDate = g.Max(row => row.Field<string>("clippingdate"))
                });

            chartBooksDoughnut.ChartAreas.Clear();
            chartBooksDoughnut.Titles.Clear();
            chartBooksDoughnut.Series.Clear();
            chartBooksDoughnut.Legends.Clear();

            chartBooksDoughnut.ChartAreas.Add(new ChartArea("chartArea"));
            chartBooksDoughnut.Series.Add(new Series());
            chartBooksDoughnut.Series[0].Label = "#VAL";
            chartBooksDoughnut.Series[0].ChartArea = chartBooksDoughnut.ChartAreas[0].Name;
            chartBooksDoughnut.Series[0].ChartType = SeriesChartType.Doughnut;
            chartBooksDoughnut.Series[0]["PieLabelStyle"] = "Disabled";

            var listBooks = enumBooks.ToList();
            foreach (var dataPoint in listBooks) {
                var label = dataPoint.BookName;
                var value = (DateTime.Parse(dataPoint.LatestClippingDate) - DateTime.Parse(dataPoint.EarliestClippingDate)).TotalDays;
                var idxPoint = chartBooksDoughnut.Series[0].Points.AddY(value);
                DataPoint pointA = chartBooksDoughnut.Series[0].Points[idxPoint];
                pointA.Label = label;
            }

            lblBooksCount.Text = listBooks.Count() + Strings.Space + Strings.X_Books;

            var enumBooksTime = _clippingsDataTable.AsEnumerable()
                .GroupBy(row => DateTime.Parse(row.Field<string>("clippingdate")).TimeOfDay.Hours)
                .Select(g => new
                {
                    ClippingHour = g.Key,
                    ClippingCount = g.Count()
                });

            chartBooksTime.ChartAreas.Clear();
            chartBooksTime.Titles.Clear();
            chartBooksTime.Series.Clear();
            chartBooksTime.Legends.Clear();

            chartBooksTime.ChartAreas.Add(new ChartArea("chartArea"));
            chartBooksTime.Series.Add(new Series());
            chartBooksTime.Series[0].Label = "#VAL";
            chartBooksTime.Series[0].ChartArea = chartBooksDoughnut.ChartAreas[0].Name;
            chartBooksTime.Series[0].ChartType = SeriesChartType.Column;

            var listBooksTime = enumBooksTime.ToList();
            foreach (var dataPoint in listBooksTime) {
                chartBooksTime.Series[0].Points.AddXY(dataPoint.ClippingHour, dataPoint.ClippingCount);
            }

            var enumBooksWeek = _clippingsDataTable.AsEnumerable()
                .GroupBy(row => DateTime.Parse(row.Field<string>("clippingdate")).DayOfWeek.ToString()[..3].ToUpper())
                .Select(g => new
                {
                    Weekday = g.Key,
                    ClippingCount = g.Count()
                });

            chartBooksWeek.ChartAreas.Clear();
            chartBooksWeek.Titles.Clear();
            chartBooksWeek.Series.Clear();
            chartBooksWeek.Legends.Clear();

            chartBooksWeek.ChartAreas.Add(new ChartArea("chartArea"));
            chartBooksWeek.Series.Add(new Series());
            chartBooksWeek.Series[0].Label = "";
            chartBooksWeek.Series[0].ChartArea = chartBooksDoughnut.ChartAreas[0].Name;
            chartBooksWeek.Series[0].ChartType = SeriesChartType.Radar;

            var listBooksWeek = enumBooksWeek.ToList().OrderBy(x => x.Weekday);
            foreach (var dataPoint in listBooksWeek) {
                chartBooksWeek.Series[0].Points.AddXY(dataPoint.Weekday, dataPoint.ClippingCount);
            }
        }
    }
}
