using DatabaseClassLibraryFW;
using System;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Windows.Forms;
using KindleMate2;

namespace KindleMate2_FW {
    public partial class FrmMain : Form {
        private DataTable _clippingsDataTable;

        private DataTable _originClippingsDataTable = new DataTable();

        private DataTable _vocabDataTable = new DataTable();

        private DataTable _lookupsDataTable = new DataTable();

        private readonly StaticData _staticData = new StaticData();

        private readonly string _programsDirectory;

        private readonly string _filePath;

        private string _kindleDrive;

        private readonly string _kindleClippingsPath;

        private readonly string _kindleWordsPath;

        private readonly string _kindleVersionPath;

        private string _selectedBook;

        private string _selectedWord;

        private int _selectedIndex;

        public FrmMain() {
            InitializeComponent();

            _clippingsDataTable = _staticData.GetClipingsDataTable();
        }

        private void FrmMain_Load(object sender, EventArgs e) {
            dataGridView.DataSource = _clippingsDataTable;
        }

        private void RefreshData() {
            try {
                if (dataGridView.CurrentRow != null) {
                    _selectedIndex = dataGridView.CurrentRow.Index;
                }
                
                _clippingsDataTable = _staticData.GetClipingsDataTable();
                _originClippingsDataTable = _staticData.GetOriginClippingsDataTable();
                _vocabDataTable = _staticData.GetVocabDataTable();
                _lookupsDataTable = GetLookupsDataTable();

                DisplayData();
                CountRows();
                SelectRow();
            } catch (Exception) {
                // ignored
            }
        }

        private void DisplayData() {
            ClearDataGridView();

            if (IsClippings()) {
                if (_clippingsDataTable.Rows.Count <= 0) {
                    return;
                }
                if (string.IsNullOrWhiteSpace(_selectedBook) || _selectedBook == Strings.Select_All) {
                    dataGridView.DataSource = _clippingsDataTable;
                    SetColumnVisible(dataGridView.Columns["key"], false);
                    dataGridView.Columns["content"].HeaderText = Strings.Content;
                    dataGridView.Columns["bookname"].HeaderText = Strings.Books;
                    dataGridView.Columns["authorname"].HeaderText = Strings.Author;
                    SetColumnVisible(dataGridView.Columns["brieftype"], false);
                    SetColumnVisible(dataGridView.Columns["clippingtypelocation"], false);
                    dataGridView.Columns["clippingdate"].HeaderText = Strings.Time;
                    SetColumnVisible(dataGridView.Columns["read"], false);
                    SetColumnVisible(dataGridView.Columns["clipping_importdate"], false);
                    SetColumnVisible(dataGridView.Columns["tag"], false);
                    SetColumnVisible(dataGridView.Columns["sync"], false);
                    SetColumnVisible(dataGridView.Columns["newbookname"], false);
                    SetColumnVisible(dataGridView.Columns["colorRGB"], false);
                    dataGridView.Columns["pagenumber"].HeaderText = Strings.Page;

                    dataGridView.Columns["content"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                    dataGridView.Columns["bookname"].Width = 150;
                    dataGridView.Columns["authorname"].Width = 100;
                    dataGridView.Columns["clippingdate"].Width = 175;
                    dataGridView.Columns["pagenumber"].Width = 75;

                    dataGridView.Sort(dataGridView.Columns["clippingdate"], ListSortDirection.Descending);
                } else {
                    DataTable filteredBooks = _clippingsDataTable.AsEnumerable().Where(row => row.Field<string>("bookname") == _selectedBook).CopyToDataTable();
                    lblCount.Text = Strings.Total_Clippings + Strings.Space + filteredBooks.Rows.Count + Strings.Space + Strings.X_Clippings;
                    lblCount.Image = Properties.Resources.open_book;
                    lblCount, true;
                    dataGridView.DataSource = filteredBooks;
                    dataGridView.Columns["key"], false;
                    dataGridView.Columns["content"].HeaderText = Strings.Content;
                    dataGridView.Columns["bookname"], false;
                    dataGridView.Columns["authorname"], false;
                    dataGridView.Columns["brieftype"], false;
                    dataGridView.Columns["clippingtypelocation"], false;
                    dataGridView.Columns["clippingdate"].HeaderText = Strings.Time;
                    dataGridView.Columns["read"], false;
                    dataGridView.Columns["clipping_importdate"], false;
                    dataGridView.Columns["tag"], false;
                    dataGridView.Columns["sync"], false;
                    dataGridView.Columns["newbookname"], false;
                    dataGridView.Columns["colorRGB"], false;
                    dataGridView.Columns["pagenumber"].HeaderText = Strings.Page;

                    dataGridView.Columns["content"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                    dataGridView.Columns["clippingdate"].Width = 175;
                    dataGridView.Columns["pagenumber"].Width = 75;

                    dataGridView.Sort(dataGridView.Columns["pagenumber"], ListSortDirection.Ascending);
                }

                dataGridView.Columns["pagenumber"].DisplayIndex = 4;
            } else {
                if (_vocabDataTable.Rows.Count <= 0) {
                    return;
                }

                dataGridView.DataSource = _lookupsDataTable;

                dataGridView.Columns["word"].DisplayIndex = 0;
                dataGridView.Columns["stem"].DisplayIndex = 1;

                if (string.IsNullOrWhiteSpace(_selectedWord) || _selectedWord == Strings.Select_All) {
                    dataGridView.Columns["word"].HeaderText = Strings.Vocabulary;
                    dataGridView.Columns["usage"], false;
                    dataGridView.Columns["title"], false;
                    dataGridView.Columns["authors"], false;
                    dataGridView.Columns["timestamp"].HeaderText = Strings.Time;
                    dataGridView.Columns["stem"].HeaderText = Strings.Stem;
                    dataGridView.Columns["frequency"].HeaderText = Strings.Frequency;

                    dataGridView.Columns["word_key"], false;
                    dataGridView.Columns["word"].MinimumWidth = 150;
                    dataGridView.Columns["word"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                    dataGridView.Columns["stem"].MinimumWidth = 150;
                    dataGridView.Columns["stem"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                    dataGridView.Columns["title"].Width = 200;
                    dataGridView.Columns["authors"].Width = 150;
                    dataGridView.Columns["timestamp"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                    dataGridView.Columns["frequency"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                } else {
                    DataTable filteredWords = _lookupsDataTable.AsEnumerable().Where(row => row.Field<string>("word_key")?[3..] == _selectedWord).CopyToDataTable();
                    lblCount.Text = Strings.Totally_Vocabs + Strings.Space + filteredWords.Rows.Count + Strings.Space + Strings.X_Lookups;
                    lblCount.Image = Properties.Resources.input_latin_uppercase;
                    lblCount, true;
                    dataGridView.DataSource = filteredWords;

                    dataGridView.Columns["word"].HeaderText = Strings.Vocabulary;
                    dataGridView.Columns["usage"].HeaderText = Strings.Content;
                    dataGridView.Columns["title"].HeaderText = Strings.Books;
                    dataGridView.Columns["authors"].HeaderText = Strings.Author;
                    dataGridView.Columns["timestamp"].HeaderText = Strings.Time;

                    dataGridView.Columns["word_key"], false;
                    dataGridView.Columns["word"].MinimumWidth = 150;
                    dataGridView.Columns["word"].AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells;
                    dataGridView.Columns["stem"].MinimumWidth = 150;
                    dataGridView.Columns["stem"].AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells;
                    dataGridView.Columns["usage"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                    dataGridView.Columns["title"], false;
                    dataGridView.Columns["authors"], false;
                    dataGridView.Columns["timestamp"].Width = 175;
                    dataGridView.Columns["frequency"].Width = 75;
                }

                dataGridView.Sort(dataGridView.Columns["timestamp"], ListSortDirection.Descending);
            }

            var books = _clippingsDataTable.AsEnumerable().Select(row => new {
                BookName = row.Field<string>("bookname")
            }).Distinct().OrderBy(book => book.BookName);

            var rootNodeBooks = new TreeNode(Strings.Select_All) {
                ImageIndex = 2, SelectedImageIndex = 2
            };
        }

        private DataTable GetLookupsDataTable() {
            DataTable lookupsDataTable = _staticData.GetLookupsDataTable();
            lookupsDataTable.Columns.Add("word", typeof(string));
            lookupsDataTable.Columns.Add("stem", typeof(string));
            lookupsDataTable.Columns.Add("frequency", typeof(string));

            foreach (DataRow row in lookupsDataTable.Rows) {
                var word_key = row["word_key"].ToString();
                var word = "";
                var stem = "";
                var frequency = "";
                foreach (DataRow vocabRow in _vocabDataTable.Rows) {
                    if (vocabRow["word_key"].ToString() != word_key) {
                        continue;
                    }
                    word = vocabRow["word"].ToString();
                    stem = vocabRow["stem"].ToString();
                    frequency = vocabRow["frequency"].ToString();
                    break;
                }
                row["word"] = word;
                row["stem"] = stem;
                row["frequency"] = frequency;
            }
            return lookupsDataTable;
        }

        private void ClearDataGridView() {
            dataGridView.DataSource = null;
            dataGridView.Rows.Clear();
            dataGridView.Columns.Clear();
        }

        private bool IsClippings() {
            return !btnVocabulary.Checked;
        }

        private string GetValue() {
            
        }

        private void SetColumnVisible(DataGridViewColumn column, bool visible) {
            if (column != null) {
                column.Visible = visible;
            }
        }
    }
}