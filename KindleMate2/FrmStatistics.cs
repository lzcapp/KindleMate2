using DarkModeForms;
using System.Data;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Globalization;

namespace KindleMate2 {
    public partial class FrmStatistics : Form {
        private readonly StaticData _staticData = new();

        private DataTable _clippingsDataTable = new();

        private DataTable _vocabDataTable = new();

        public FrmStatistics() {
            InitializeComponent();

            if (_staticData.IsDarkTheme()) {
                _ = new DarkModeCS(this, false);
                chartBooksTime.BackColor = ColorTranslator.FromHtml("#202020");
                chartBooksTime.ChartAreas[0].AxisX.LabelStyle.ForeColor = Color.White;
                chartBooksWeek.BackColor = ColorTranslator.FromHtml("#202020");
                chartBooksWeek.ChartAreas[0].AxisX.LabelStyle.ForeColor = Color.White;
                chartBooksHistory.BackColor = ColorTranslator.FromHtml("#202020");
                chartBooksHistory.ChartAreas[0].AxisX.LabelStyle.ForeColor = Color.White;
                chartVocabsTime.BackColor = ColorTranslator.FromHtml("#202020");
                chartVocabsTime.ChartAreas[0].AxisX.LabelStyle.ForeColor = Color.White;
                chartVocabsWeek.BackColor = ColorTranslator.FromHtml("#202020");
                chartVocabsWeek.ChartAreas[0].AxisX.LabelStyle.ForeColor = Color.White;
                chartVocabsHistory.BackColor = ColorTranslator.FromHtml("#202020");
                chartVocabsHistory.ChartAreas[0].AxisX.LabelStyle.ForeColor = Color.White;
            }
        }

        private void FrmStatistic_Load(object sender, EventArgs e) {
            try {
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
                        DateTime date = ParseDateTime(row.Field<string>("clippingdate") ?? "");
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
            } catch (Exception exception) {
                Messenger.MessageBox(exception.Message, Strings.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static DateTime ParseDateTime(string field) {
            try {
                if (!string.IsNullOrWhiteSpace(field)) {
                    DateTime date = DateTime.Parse(field);
                    return date;
                }
                return DateTime.MinValue;
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
                var unixTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds() + ".png";
                var filePath = Path.Combine(Environment.CurrentDirectory, "Statistics", unixTimestamp);
                var directoryPath = Path.Combine(Environment.CurrentDirectory, "Statistics");
                if (!Directory.Exists(directoryPath)) {
                    Directory.CreateDirectory(directoryPath);
                }

                bitmap.Save(filePath, ImageFormat.Png);
                bitmap.Dispose();

                btnSave.Visible = true;
                WindowState = FormWindowState.Normal;

                MessageBox.Show(Strings.Statistics_Screenshot_Successful, Strings.Successful, MessageBoxButtons.OK, MessageBoxIcon.Information);
                Process.Start("Explorer.exe", "/select," + filePath);
            } catch (Exception) {
                MessageBox.Show(Strings.Statistics_Screenshot_Failed, Strings.Failed, MessageBoxButtons.OK, MessageBoxIcon.Information);
            } finally {
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
            switch (selectedIndex) {
                case 0:
                    var clippings = 0;
                    var books = 0;
                    var authors = 0;
                    var bookDays = 0;
                    try {
                        if (_clippingsDataTable.Rows.Count > 0) {
                            clippings = _clippingsDataTable.Rows.Count;
                            books = _clippingsDataTable.AsEnumerable().Select(row => row.Field<string>("bookname")).Distinct().Count();
                            authors = _clippingsDataTable.AsEnumerable().Select(row => row.Field<string>("authorname")).Distinct().Count();
                            var bookTimes = _clippingsDataTable.AsEnumerable().Select(row => DateTime.Parse(row.Field<string>("clippingdate") ?? string.Empty));
                            bookDays = (bookTimes.Max() - bookTimes.Min()).Days;
                        } else {
                            throw new Exception();
                        }
                    } catch {
                        // ignored
                    } finally {
                        lblStatistics.Text = Strings.In + Strings.Space + bookDays + Strings.Space + Strings.X_Days + Strings.Symbol_Comma + Strings.Totally + Strings.Space + clippings + Strings.Space + Strings.X_Clippings + Strings.Symbol_Comma + books + Strings.Space + Strings.X_Books + Strings.Symbol_Comma + authors + Strings.Space + Strings.X_Authors;
                    }
                    break;
                case 1:
                    var lookups = 0;
                    var words = 0;
                    var vocabDays = 0;
                    try {
                        if (_vocabDataTable.Rows.Count > 0) {
                            lookups = _vocabDataTable.Rows.Cast<DataRow>().Sum(row => Convert.ToInt32(row["frequency"]));
                            words = _vocabDataTable.AsEnumerable().Select(row => row.Field<string>("word")).Distinct().Count();
                            var vocabTimes = _vocabDataTable.AsEnumerable().Select(row => DateTime.Parse(row.Field<string>("timestamp") ?? string.Empty));
                            vocabDays = (vocabTimes.Max() - vocabTimes.Min()).Days;
                        } else {
                            throw new Exception();
                        }
                    } catch {
                        // ignored
                    } finally {
                        lblStatistics.Text = Strings.In + Strings.Space + vocabDays + Strings.Space + Strings.X_Days + Strings.Symbol_Comma + Strings.Totally + Strings.Space + lookups + Strings.Space + Strings.X_Lookups + Strings.Symbol_Comma + words + Strings.Space + Strings.X_Vocabs;
                    }
                    break;
            }
        }
    }
}