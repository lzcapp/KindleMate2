using System.ComponentModel;
using System.Data;
using System.Data.SQLite;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using KindleMate2.DarkModeForms;
using Markdig;

namespace KindleMate2 {
    public partial class FrmMain : Form {
        private DataTable _clippingsDataTable = new();

        private DataTable _originClippingsDataTable = new();

        private DataTable _vocabDataTable = new();

        private DataTable _lookupsDataTable = new();

        private readonly StaticData _staticData = new();

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

            if (_staticData.IsDarkTheme()) {
                _ = new DarkModeCS(this, false);
                menuTheme.Image = Properties.Resources.sun;
            } else {
                _staticData.SetTheme("light");
                menuTheme.Image = Properties.Resources.new_moon;
            }

            var name = _staticData.GetLanguage();
            if (!string.IsNullOrWhiteSpace(name)) {
                var culture = new CultureInfo(name);
                Thread.CurrentThread.CurrentUICulture = culture;

                switch (name.ToLowerInvariant()) {
                    case "en":
                        menuLangEN.Visible = false;
                        break;
                    case "zh-hans":
                        menuLangSC.Visible = false;
                        break;
                    case "zh-hant":
                        menuLangTC.Visible = false;
                        break;
                }
            } else {
                menuLangAuto.Visible = false;
                CultureInfo currentCulture = CultureInfo.CurrentCulture;
                if (currentCulture.EnglishName.Contains("English") || currentCulture.TwoLetterISOLanguageName.Equals("en", StringComparison.OrdinalIgnoreCase)) {
                    menuLangEN.Visible = false;
                } else if (string.Equals(currentCulture.Name, "zh-CN", StringComparison.OrdinalIgnoreCase) || string.Equals(currentCulture.Name, "zh-SG", StringComparison.OrdinalIgnoreCase) || string.Equals(currentCulture.Name, "zh-Hans", StringComparison.OrdinalIgnoreCase)) {
                    menuLangSC.Visible = false;
                } else if (string.Equals(currentCulture.Name, "zh-TW", StringComparison.OrdinalIgnoreCase) || string.Equals(currentCulture.Name, "zh-HK", StringComparison.OrdinalIgnoreCase) || string.Equals(currentCulture.Name, "zh-MO", StringComparison.OrdinalIgnoreCase) || string.Equals(currentCulture.Name, "zh-Hant", StringComparison.OrdinalIgnoreCase)) {
                    menuLangTC.Visible = false;
                }
            }

            AppDomain.CurrentDomain.ProcessExit += (_, _) => {
                BackupDatabase();
                _staticData.CloseConnection();
                _staticData.DisposeConnection();
            };

            _programsDirectory = Environment.CurrentDirectory;
            _filePath = Path.Combine(_programsDirectory, "KM2.dat");
            _kindleClippingsPath = Path.Combine("documents", "My Clippings.txt");
            _kindleWordsPath = Path.Combine("system", "vocabulary", "vocab.db");
            _kindleVersionPath = Path.Combine("system", "version.txt");
            _kindleDrive = string.Empty;
            _selectedBook = string.Empty;
            _selectedWord = string.Empty;
            _selectedIndex = 0;

            menuFile.Text = Strings.Files + @"(&F)";
            menuRefresh.Text = Strings.Refresh;
            menuStatistic.Text = Strings.Statistics;
            menuRestart.Text = Strings.Restart;
            menuExit.Text = Strings.Exit;
            menuManage.Text = Strings.Management + @"(&M)";
            menuImportKindle.Text = Strings.Import_Kindle_Clippings;
            menuImportKindleWords.Text = Strings.Import_Kindle_Vocabs;
            menuImportKindleMate.Text = Strings.Import_Kindle_Mate_Database;
            menuSyncFromKindle.Text = Strings.Import_Kindle_Clippings_From_Kindle;
            menuExportMd.Text = Strings.Export_To_Markdown;
            menuClean.Text = Strings.Clean_Database;
            menuBackup.Text = Strings.Backup;
            menuClear.Text = Strings.Clear_Data;
            menuHelp.Text = Strings.Help + @"(&H)";
            menuAbout.Text = Strings.About;
            menuRepo.Text = Strings.GitHub_Repo;
            menuLang.Text = Strings.Language + @"(&L)";
            menuLangEN.Text = Strings.English;
            menuLangSC.Text = Strings.SC;
            menuLangTC.Text = Strings.TC;
            menuLangAuto.Text = Strings.AutomaticDetection;

            tabPageBooks.Text = Strings.Clippings;
            tabPageWords.Text = Strings.Vocabulary_List;

            menuBookRefresh.Text = Strings.Refresh;
            menuBooksDelete.Text = Strings.Delete;
            menuRename.Text = Strings.Rename;

            menuClippingsRefresh.Text = Strings.Refresh;
            menuClippingsCopy.Text = Strings.Copy;
            menuClippingsDelete.Text = Strings.Delete;

            dataGridView.ColumnHeadersHeight = 23;
        }

        private void FrmMain_Load(object? sender, EventArgs e) {
            if (!File.Exists(_filePath)) {
                RefreshData();
                return;
            }

            if (_staticData.IsDatabaseEmpty()) {
                if (Directory.Exists(Path.Combine(_programsDirectory, "Backups"))) {
                    var filePath = Path.Combine(_programsDirectory, "Backups", "KM2.dat");

                    var fileSize = new FileInfo(filePath).Length / 1024;
                    if (File.Exists(filePath) && fileSize >= 20) {
                        DialogResult resultRestore = Dialog(Strings.Confirm_Restore_Database, Strings.Confirm, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                        if (resultRestore == DialogResult.Yes) {
                            File.Copy(filePath, _filePath, true);
                            RefreshData();
                            return;
                        }
                    }
                }
            }

            RefreshData();

            treeViewBooks.Focus();
        }

        private DialogResult Dialog(string message, string title, MessageBoxButtons buttons, MessageBoxIcon icon) {
            return _staticData.IsDarkTheme() ? Messenger.MessageBox(message, title, buttons, icon) : MessageBox.Show(message, title, buttons, icon);
        }

        private string Import(string kindleClippingsPath, string kindleWordsPath) {
            var clippingsResult = ImportKindleClippings(kindleClippingsPath);
            var wordResult = ImportKindleWords(kindleWordsPath);
            if (string.IsNullOrWhiteSpace(clippingsResult + wordResult)) {
                return string.Empty;
            }

            if (string.IsNullOrWhiteSpace(clippingsResult)) {
                return wordResult;
            }

            if (string.IsNullOrWhiteSpace(wordResult)) {
                return clippingsResult;
            }

            return clippingsResult + "\n" + wordResult;
        }

        private string ImportKindleWords(string kindleWordsPath) {
            if (!File.Exists(kindleWordsPath)) {
                Dialog(Strings.Kindle_Vocab_Not_Exist, Strings.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return string.Empty;
            }

            SQLiteConnection connection = new("Data Source=" + kindleWordsPath + ";Version=3;");

            var bookInfoTable = new DataTable();
            var lookupsTable = new DataTable();
            var wordsTable = new DataTable();

            connection.Open();

            using (var command = new SQLiteCommand("PRAGMA synchronous=OFF", connection)) {
                command.ExecuteNonQuery();
            }

            SQLiteTransaction? trans = connection.BeginTransaction();

            try {
                const string bookInfoQuery = "SELECT * FROM BOOK_INFO;";
                using var bookInfoCommand = new SQLiteCommand(bookInfoQuery, connection);
                using var bookInfoAdapter = new SQLiteDataAdapter(bookInfoCommand);
                bookInfoAdapter.Fill(bookInfoTable);

                const string lookupsTableQuery = "SELECT * FROM LOOKUPS;";
                using var lookupsCommand = new SQLiteCommand(lookupsTableQuery, connection);
                using var lookupsAdapter = new SQLiteDataAdapter(lookupsCommand);
                lookupsAdapter.Fill(lookupsTable);

                const string wordsLookups = "SELECT * FROM WORDS;";
                using var wordsCommand = new SQLiteCommand(wordsLookups, connection);
                using var wordsAdapter = new SQLiteDataAdapter(wordsCommand);

                wordsAdapter.Fill(wordsTable);

                trans.Commit();
            } catch (Exception) {
                trans.Rollback();
            }

            connection.Close();

            var lookupsInsertedCount = 0;
            var wordsInsertedCount = 0;

            _staticData.BeginTransaction();

            try {
                foreach (DataRow row in wordsTable.Rows) {
                    var id = row["id"].ToString() ?? string.Empty;
                    var word = row["word"].ToString() ?? string.Empty;
                    var stem = row["stem"].ToString() ?? string.Empty;
                    _ = int.TryParse(row["category"].ToString()!.Trim(), out var category);
                    _ = long.TryParse(row["timestamp"].ToString()!.Trim(), out var timestamp);

                    DateTimeOffset dateTimeOffset = DateTimeOffset.FromUnixTimeMilliseconds(timestamp);
                    DateTime dateTime = dateTimeOffset.LocalDateTime;
                    var formattedDateTime = dateTime.ToString("yyyy-MM-dd HH:mm:ss");

                    if (!_staticData.IsExistVocabById(word + timestamp)) {
                        wordsInsertedCount += _staticData.InsertVocab(word + timestamp, id, word, stem, category, formattedDateTime, 0);
                    }
                }

                foreach (DataRow row in lookupsTable.Rows) {
                    var word_key = row["word_key"].ToString() ?? string.Empty;
                    var book_key = row["book_key"].ToString() ?? string.Empty;
                    var usage = row["usage"].ToString() ?? string.Empty;
                    _ = long.TryParse(row["timestamp"].ToString()!.Trim(), out var timestamp);

                    var title = "";
                    var authors = "";
                    foreach (DataRow bookInfoRow in bookInfoTable.Rows) {
                        var id = bookInfoRow["id"].ToString() ?? string.Empty;
                        var guid = bookInfoRow["guid"].ToString() ?? string.Empty;
                        if (id != book_key && guid != book_key) {
                            continue;
                        }

                        title = bookInfoRow["title"].ToString() ?? string.Empty;
                        authors = bookInfoRow["authors"].ToString() ?? string.Empty;
                    }

                    DateTimeOffset dateTimeOffset = DateTimeOffset.FromUnixTimeMilliseconds(timestamp);
                    DateTime dateTime = dateTimeOffset.LocalDateTime;
                    var formattedDateTime = dateTime.ToString("yyyy-MM-dd HH:mm:ss");

                    if (!_staticData.IsExistLookups(formattedDateTime)) {
                        lookupsInsertedCount += _staticData.InsertLookups(word_key, usage, title, authors, formattedDateTime);
                    }
                }

                _staticData.CommitTransaction();
            } catch (Exception) {
                _staticData.RollbackTransaction();
            }

            UpdateFrequency();

            return Strings.Parsed_X + Strings.Space + lookupsTable.Rows.Count + Strings.Space + Strings.X_Vocabs + Strings.Space + Strings.Symbol_Comma + Strings.Imported_X + Strings.Space + lookupsInsertedCount + Strings.Space + Strings.X_Lookups + Strings.Space + Strings.Symbol_Comma + wordsInsertedCount + Strings.Space + Strings.X_Vocabs;
        }

        private void UpdateFrequency() {
            DataTable vocabDataTable = _staticData.GetVocabDataTable();
            DataTable lookupsDataTable = _staticData.GetLookupsDataTable();

            _staticData.BeginTransaction();
            try {
                foreach (DataRow row in vocabDataTable.Rows) {
                    var word_key = row["word_key"].ToString() ?? string.Empty;
                    var frequency = lookupsDataTable.AsEnumerable().Count(lookupsRow => lookupsRow["word_key"].ToString()?.Trim() == word_key);
                    _staticData.UpdateVocab(word_key, frequency);
                }

                _staticData.CommitTransaction();
            } catch (Exception) {
                _staticData.RollbackTransaction();
            }
        }

        private void RefreshData() {
            try {
                if (dataGridView.CurrentRow is not null) {
                    _selectedIndex = dataGridView.CurrentRow.Index;
                }

                DisplayData();
                CountRows();
                SelectRow();
            } catch (Exception) {
                // ignored
            }
        }

        private void DisplayData() {
            _clippingsDataTable = _staticData.GetClipingsDataTable();
            _originClippingsDataTable = _staticData.GetOriginClippingsDataTable();
            _vocabDataTable = _staticData.GetVocabDataTable();
            _lookupsDataTable = _staticData.GetLookupsDataTable();

            _lookupsDataTable.Columns.Add("word", typeof(string));
            _lookupsDataTable.Columns.Add("stem", typeof(string));
            _lookupsDataTable.Columns.Add("frequency", typeof(string));

            foreach (DataRow row in _lookupsDataTable.Rows) {
                var word_key = row["word_key"].ToString() ?? string.Empty;
                var word = "";
                var stem = "";
                var frequency = "";
                foreach (DataRow vocabRow in _vocabDataTable.Rows) {
                    if (vocabRow["word_key"].ToString() != word_key) {
                        continue;
                    }

                    word = vocabRow["word"].ToString() ?? string.Empty;
                    stem = vocabRow["stem"].ToString() ?? string.Empty;
                    frequency = vocabRow["frequency"].ToString() ?? string.Empty;
                    break;
                }

                row["word"] = word;
                row["stem"] = stem;
                row["frequency"] = frequency;
            }

            SetDataGridView();

            var books = _clippingsDataTable.AsEnumerable().Select(row => new {
                BookName = row.Field<string>("bookname")
            }).Distinct().OrderBy(book => book.BookName);

            var rootNodeBooks = new TreeNode(Strings.Select_All) {
                ImageIndex = 2, SelectedImageIndex = 2
            };

            treeViewBooks.Nodes.Clear();

            treeViewBooks.Nodes.Add(rootNodeBooks);

            foreach (var book in books) {
                var bookNode = new TreeNode(book.BookName) {
                    ToolTipText = book.BookName
                };

                treeViewBooks.Nodes.Add(bookNode);
            }

            treeViewBooks.ExpandAll();

            if (string.IsNullOrWhiteSpace(_selectedBook)) {
                treeViewBooks.SelectedNode = rootNodeBooks;
            } else {
                foreach (TreeNode node in treeViewBooks.Nodes) {
                    if (node.Text.Trim() == _selectedBook.Trim()) {
                        treeViewBooks.SelectedNode = node;
                        lblBookCount.Text = Strings.Total_Clippings + Strings.Space + dataGridView.Rows.Count + Strings.Space + Strings.X_Clippings;
                        lblBookCount.Image = Properties.Resources.open_book;
                        lblBookCount.Visible = true;
                        break;
                    }

                    treeViewBooks.SelectedNode = rootNodeBooks;
                }
            }

            var words = _vocabDataTable.AsEnumerable().Select(row => new {
                Word = row.Field<string>("word")
            }).Distinct().OrderBy(word => word.Word);

            var rootNodeWords = new TreeNode(Strings.Select_All) {
                ImageIndex = 2, SelectedImageIndex = 2
            };

            treeViewWords.Nodes.Clear();

            treeViewWords.Nodes.Add(rootNodeWords);

            foreach (var word in words) {
                var wordNode = new TreeNode(word.Word) {
                    ToolTipText = word.Word
                };

                treeViewWords.Nodes.Add(wordNode);
            }

            treeViewWords.ExpandAll();

            if (string.IsNullOrWhiteSpace(_selectedWord)) {
                treeViewWords.SelectedNode = rootNodeWords;
            } else {
                foreach (TreeNode node in treeViewWords.Nodes) {
                    if (node.Text.Trim().Equals(_selectedWord.Trim())) {
                        treeViewWords.SelectedNode = node;
                        lblBookCount.Text = Strings.Total_Clippings + Strings.Space + dataGridView.Rows.Count + Strings.Space + Strings.X_Vocabs;
                        lblBookCount.Image = Properties.Resources.input_latin_uppercase;
                        lblBookCount.Visible = true;
                        break;
                    }

                    treeViewWords.SelectedNode = rootNodeWords;
                }
            }
        }

        private void SetDataGridView() {
            dataGridView.DataSource = null;
            dataGridView.Rows.Clear();
            dataGridView.Columns.Clear();

            var selectedIndex = tabControl.SelectedIndex;
            switch (selectedIndex) {
                case 0:
                    if (_clippingsDataTable.Rows.Count <= 0) {
                        return;
                    }

                    if (string.IsNullOrWhiteSpace(_selectedBook) || _selectedBook == Strings.Select_All) {
                        dataGridView.DataSource = _clippingsDataTable;
                        dataGridView.Columns["key"]!.Visible = false;
                        dataGridView.Columns["content"]!.HeaderText = Strings.Content;
                        dataGridView.Columns["bookname"]!.HeaderText = Strings.Books;
                        dataGridView.Columns["authorname"]!.HeaderText = Strings.Author;
                        dataGridView.Columns["brieftype"]!.Visible = false;
                        dataGridView.Columns["clippingtypelocation"]!.Visible = false;
                        dataGridView.Columns["clippingdate"]!.HeaderText = Strings.Time;
                        dataGridView.Columns["read"]!.Visible = false;
                        dataGridView.Columns["clipping_importdate"]!.Visible = false;
                        dataGridView.Columns["tag"]!.Visible = false;
                        dataGridView.Columns["sync"]!.Visible = false;
                        dataGridView.Columns["newbookname"]!.Visible = false;
                        dataGridView.Columns["colorRGB"]!.Visible = false;
                        dataGridView.Columns["pagenumber"]!.HeaderText = Strings.Page;

                        dataGridView.Columns["content"]!.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                        dataGridView.Columns["bookname"]!.Width = 100;
                        dataGridView.Columns["authorname"]!.Width = 50;
                        dataGridView.Columns["clippingdate"]!.Width = 135;
                        dataGridView.Columns["pagenumber"]!.Width = 50;

                        dataGridView.Sort(dataGridView.Columns["clippingdate"]!, ListSortDirection.Descending);
                    } else {
                        DataTable filteredBooks = _clippingsDataTable.AsEnumerable().Where(row => row.Field<string>("bookname") == _selectedBook).CopyToDataTable();
                        lblBookCount.Text = Strings.Total_Clippings + Strings.Space + filteredBooks.Rows.Count + Strings.Space + Strings.X_Clippings;
                        lblBookCount.Image = Properties.Resources.open_book;
                        lblBookCount.Visible = true;
                        dataGridView.DataSource = filteredBooks;
                        dataGridView.Columns["key"]!.Visible = false;
                        dataGridView.Columns["content"]!.HeaderText = Strings.Content;
                        dataGridView.Columns["bookname"]!.Visible = false;
                        dataGridView.Columns["authorname"]!.Visible = false;
                        dataGridView.Columns["brieftype"]!.Visible = false;
                        dataGridView.Columns["clippingtypelocation"]!.Visible = false;
                        dataGridView.Columns["clippingdate"]!.HeaderText = Strings.Time;
                        dataGridView.Columns["read"]!.Visible = false;
                        dataGridView.Columns["clipping_importdate"]!.Visible = false;
                        dataGridView.Columns["tag"]!.Visible = false;
                        dataGridView.Columns["sync"]!.Visible = false;
                        dataGridView.Columns["newbookname"]!.Visible = false;
                        dataGridView.Columns["colorRGB"]!.Visible = false;
                        dataGridView.Columns["pagenumber"]!.HeaderText = Strings.Page;

                        dataGridView.Columns["content"]!.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                        dataGridView.Columns["clippingdate"]!.Width = 135;
                        dataGridView.Columns["pagenumber"]!.Width = 50;

                        dataGridView.Sort(dataGridView.Columns["pagenumber"]!, ListSortDirection.Ascending);
                    }

                    dataGridView.Columns["pagenumber"]!.DisplayIndex = 4;

                    break;
                case 1:
                    if (_vocabDataTable.Rows.Count <= 0) {
                        return;
                    }

                    dataGridView.DataSource = _lookupsDataTable;

                    dataGridView.Columns["word"]!.DisplayIndex = 0;
                    dataGridView.Columns["stem"]!.DisplayIndex = 1;

                    if (string.IsNullOrWhiteSpace(_selectedWord) || _selectedWord == Strings.Select_All) {
                        dataGridView.Columns["word"]!.HeaderText = Strings.Vocabulary;
                        dataGridView.Columns["usage"]!.Visible = true;
                        dataGridView.Columns["title"]!.Visible = false;
                        dataGridView.Columns["authors"]!.Visible = false;
                        dataGridView.Columns["timestamp"]!.HeaderText = Strings.Time;
                        dataGridView.Columns["stem"]!.HeaderText = Strings.Stem;
                        dataGridView.Columns["frequency"]!.HeaderText = Strings.Frequency;

                        dataGridView.Columns["word_key"]!.Visible = false;
                        dataGridView.Columns["word"]!.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                        dataGridView.Columns["stem"]!.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                        dataGridView.Columns["usage"]!.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                        dataGridView.Columns["timestamp"]!.Width = 135;
                        dataGridView.Columns["frequency"]!.Width = 50;
                    } else {
                        DataTable filteredWords = _lookupsDataTable.AsEnumerable().Where(row => row.Field<string>("word_key")?[3..] == _selectedWord).CopyToDataTable();
                        lblBookCount.Text = Strings.Totally_Vocabs + Strings.Space + filteredWords.Rows.Count + Strings.Space + Strings.X_Lookups;
                        lblBookCount.Image = Properties.Resources.input_latin_uppercase;
                        lblBookCount.Visible = true;
                        dataGridView.DataSource = filteredWords;

                        dataGridView.Columns["word"]!.HeaderText = Strings.Vocabulary;
                        dataGridView.Columns["usage"]!.HeaderText = Strings.Content;
                        dataGridView.Columns["title"]!.HeaderText = Strings.Books;
                        dataGridView.Columns["authors"]!.HeaderText = Strings.Author;
                        dataGridView.Columns["timestamp"]!.HeaderText = Strings.Time;
                        dataGridView.Columns["stem"]!.HeaderText = Strings.Stem;
                        dataGridView.Columns["frequency"]!.HeaderText = Strings.Frequency;

                        dataGridView.Columns["usage"]!.Visible = true;
                        dataGridView.Columns["title"]!.Visible = true;
                        dataGridView.Columns["authors"]!.Visible = true;
                        dataGridView.Columns["word_key"]!.Visible = false;
                        dataGridView.Columns["frequency"]!.Visible = false;

                        dataGridView.Columns["word"]!.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                        dataGridView.Columns["stem"]!.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                        dataGridView.Columns["usage"]!.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                        dataGridView.Columns["timestamp"]!.Width = 135;
                        dataGridView.Columns["frequency"]!.Width = 50;
                    }

                    dataGridView.Sort(dataGridView.Columns["timestamp"]!, ListSortDirection.Descending);

                    break;
            }
        }

        private void CountRows() {
            var index = tabControl.SelectedIndex;
            switch (index) {
                case 0:
                    var booksCount = treeViewBooks.Nodes.Count - 1;
                    var clippingsCount = _clippingsDataTable.Rows.Count;
                    var originClippingsCount = _originClippingsDataTable.Rows.Count;
                    var diff = Math.Abs(originClippingsCount - clippingsCount);
                    lblCount.Text = Strings.Totally + Strings.Space + booksCount + Strings.Space + Strings.X_Books + Strings.Symbol_Comma + clippingsCount + Strings.Space + Strings.X_Clippings + Strings.Symbol_Comma + Strings.Deleted_X + Strings.Space + diff + Strings.Space + Strings.X_Rows;

                    break;
                case 1:
                    var vocabCount = _vocabDataTable.Rows.Count;
                    var lookupsCount = _lookupsDataTable.Rows.Count;
                    lblCount.Text = Strings.Totally + Strings.Space + vocabCount + Strings.Space + Strings.X_Vocabs + Strings.Symbol_Comma + Strings.Quried_X + Strings.Space + lookupsCount + Strings.Space + Strings.X_Times;
                    break;
            }
        }

        // ReSharper disable once InconsistentNaming
        private string ImportKMDatabase(string selectedFilePath) {
            SQLiteConnection connection = new("Data Source=" + selectedFilePath + ";Version=3;");

            var clippingsDataTable = new DataTable();
            var originClippingsDataTable = new DataTable();
            var lookupsDataTable = new DataTable();
            var vocabDataTable = new DataTable();

            connection.Open();

            using (var command = new SQLiteCommand("PRAGMA synchronous=OFF", connection)) {
                command.ExecuteNonQuery();
            }

            SQLiteTransaction? trans = connection.BeginTransaction();

            try {
                const string queryClippings = "SELECT * FROM clippings;";
                using var clippingsCommand = new SQLiteCommand(queryClippings, connection);
                using var clippingAdapter = new SQLiteDataAdapter(clippingsCommand);
                clippingAdapter.Fill(clippingsDataTable);

                const string queryOriginClippings = "SELECT * FROM original_clipping_lines;";
                using var originCommand = new SQLiteCommand(queryOriginClippings, connection);
                using var originAdapter = new SQLiteDataAdapter(originCommand);
                originAdapter.Fill(originClippingsDataTable);

                const string queryLookups = "SELECT * FROM lookups;";
                using var lookupsCommand = new SQLiteCommand(queryLookups, connection);
                using var lookupsAdapter = new SQLiteDataAdapter(lookupsCommand);
                lookupsAdapter.Fill(lookupsDataTable);

                const string queryVocab = "SELECT * FROM vocab;";
                using var vocabCommand = new SQLiteCommand(queryVocab, connection);
                using var vocabAdapter = new SQLiteDataAdapter(vocabCommand);
                vocabAdapter.Fill(vocabDataTable);

                trans.Commit();
            } catch (Exception) {
                trans.Rollback();
            }

            connection.Close();

            var insertedCount = 0;
            var wordsInsertedCount = 0;

            _staticData.BeginTransaction();

            try {
                foreach (DataRow row in clippingsDataTable.Rows) {
                    if (_staticData.IsExistClippings(row["key"].ToString())) {
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(row["content"].ToString())) {
                        continue;
                    }

                    if (_staticData.IsExistClippingsOfContent(row["content"].ToString())) {
                        continue;
                    }

                    _ = int.TryParse(row["brieftype"].ToString()!.Trim(), out var brieftype);
                    _ = int.TryParse(row["read"].ToString()!.Trim(), out var read);
                    _ = int.TryParse(row["sync"].ToString()!.Trim(), out var sync);
                    _ = int.TryParse(row["colorRGB"].ToString()!.Trim(), out var colorRgb);
                    _ = int.TryParse(row["pagenumber"].ToString()!.Trim(), out var pagenumber);
                    insertedCount += _staticData.InsertClippings(row["key"].ToString()!, row["content"].ToString()!, row["bookname"].ToString()!, row["authorname"].ToString()!, brieftype, row["clippingtypelocation"].ToString()!, row["clippingdate"].ToString()!, read, row["clipping_importdate"].ToString()!, row["tag"].ToString()!, sync, row["newbookname"].ToString()!, colorRgb, pagenumber);
                }

                foreach (DataRow row in originClippingsDataTable.Rows) {
                    if (_staticData.IsExistOriginalClippings(row["key"].ToString())) {
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(row["line4"].ToString())) {
                        continue;
                    }

                    _staticData.InsertOriginClippings(row["key"].ToString()!, row["line1"].ToString()!, row["line2"].ToString()!, row["line3"].ToString()!, row["line4"].ToString()!, row["line5"].ToString()!);
                }

                foreach (DataRow row in lookupsDataTable.Rows) {
                    if (_staticData.IsExistLookups(row["timestamp"].ToString() ?? string.Empty)) {
                        continue;
                    }

                    _staticData.InsertLookups(row["word_key"].ToString()!, row["usage"].ToString()!, row["title"].ToString()!, row["authors"].ToString()!, row["timestamp"].ToString() ?? string.Empty);
                }

                wordsInsertedCount = (from DataRow row in vocabDataTable.Rows where !_staticData.IsExistVocab(row["word_key"].ToString() ?? string.Empty) select _staticData.InsertVocab(row["id"].ToString() ?? string.Empty, row["word_key"].ToString() ?? string.Empty, row["word"].ToString() ?? string.Empty, row["stem"].ToString() ?? string.Empty, int.Parse(row["category"].ToString() ?? string.Empty), row["timestamp"].ToString() ?? string.Empty, int.Parse(row["frequency"].ToString() ?? string.Empty))).Sum();

                _staticData.CommitTransaction();
            } catch (Exception) {
                _staticData.RollbackTransaction();
            }

            UpdateFrequency();

            var rowsCount = clippingsDataTable.Rows.Count + lookupsDataTable.Rows.Count;

            return Strings.Parsed_X + Strings.Space + rowsCount + Strings.Space + Strings.X_Records + Strings.Symbol_Comma + Strings.Imported_X + Strings.Space + insertedCount + Strings.Space + Strings.X_Clippings + Strings.Symbol_Comma + wordsInsertedCount + Strings.Space + Strings.X_Vocabs;
        }

        private string ImportKindleClippings(string clippingsPath) {
            var originClippingsTable = new DataTable();
            originClippingsTable.Columns.Add("key", typeof(string));
            originClippingsTable.Columns.Add("line1", typeof(string));
            originClippingsTable.Columns.Add("line2", typeof(string));
            originClippingsTable.Columns.Add("line3", typeof(string));
            originClippingsTable.Columns.Add("line4", typeof(string));
            originClippingsTable.Columns.Add("line5", typeof(string));

            var clippingsTable = new DataTable();
            clippingsTable.Columns.Add("key", typeof(string));
            clippingsTable.Columns.Add("content", typeof(string));
            clippingsTable.Columns.Add("bookname", typeof(string));
            clippingsTable.Columns.Add("authorname", typeof(string));
            clippingsTable.Columns.Add("brieftype", typeof(int));
            clippingsTable.Columns.Add("clippingtypelocation", typeof(string));
            clippingsTable.Columns.Add("clippingdate", typeof(string));
            clippingsTable.Columns.Add("read", typeof(int));
            clippingsTable.Columns.Add("clipping_importdate", typeof(string));
            clippingsTable.Columns.Add("tag", typeof(string));
            clippingsTable.Columns.Add("sync", typeof(int));
            clippingsTable.Columns.Add("newbookname", typeof(string));
            clippingsTable.Columns.Add("colorRGB", typeof(int));
            clippingsTable.Columns.Add("pagenumber", typeof(int));

            List<string> lines = [
                .. File.ReadAllLines(clippingsPath)
            ];

            var delimiterIndex = new List<int>();

            for (var i = 0; i < lines.Count; i++) {
                if (lines[i].StartsWith("===") && lines[i - 2].Trim().Equals("") && lines[i].EndsWith("===")) {
                    delimiterIndex.Add(i);
                }
            }

            var insertedCount = 0;

            _staticData.BeginTransaction();

            try {
                for (var i = 0; i < delimiterIndex.Count; i++) {
                    var ceilDelimiter = i == 0 ? -1 : delimiterIndex[i - 1];
                    var florDelimiter = delimiterIndex[i];

                    var line1 = lines[ceilDelimiter + 1].Trim();
                    var line2 = lines[ceilDelimiter + 2].Trim();
                    var line3 = lines[ceilDelimiter + 3].Trim(); // line3 should be empty
                    var line4 = lines[ceilDelimiter + 4].Trim();
                    if (ceilDelimiter + 5 == ceilDelimiter) {          // line4 is the rest
                        for (var index = ceilDelimiter + 3; index < florDelimiter; index++) {
                            line4 += lines[index];
                            if (index < florDelimiter - 1) {
                                line4 += "\n";
                            }
                        }
                    }
                    var content = line4;
                    var line5 = lines[florDelimiter].Trim();     // line 5 is "=========="

                    var brieftype = 0;
                    if (line2.Contains("笔记") || line2.Contains("Note")) {
                        brieftype = 1;
                    } else if (lines.Contains("书签") || line2.Contains("Bookmark")) {
                        // brieftype = 2;
                        continue;
                    } else if (lines.Contains("文章剪切") || line2.Contains("Cut")) {
                        brieftype = 3;
                    }

                    if (line4.Contains("您已达到本内容的剪贴上限")) {
                        continue;
                    }

                    var split_b = line2.Split('|');

                    var clippingtypelocation = "";
                    if (split_b.Length > 1) {
                        clippingtypelocation = split_b[0][1..].Trim();
                    }
                    var split_d = clippingtypelocation.Split('-');
                    if (split_d.Length > 1) {
                        clippingtypelocation = split_d[1].Trim().ToUpper();
                    }
                    var pagenumber = -1;
                    /*
                    strLoc = strLoc.Replace("您在位置 #", "")
                                   .Replace("的标注", "")
                                   .Replace("的笔记", "")
                                   .Replace("your note on location", "")
                                   .Replace("your highlight on page", "")
                                   .Trim();
                    strLoc = strLoc.Split('-', '.', '#')[0].Trim();
                    */
                    var pagenPattern = @"#?\d+(?:-\d+)?";
                    var isPagenIsMatch = Regex.IsMatch(clippingtypelocation, pagenPattern);
                    var romanPattern = @"^(M{0,3})(CM|CD|D?C{0,3})(XC|XL|L?X{0,3})(IX|IV|V?I{0,3})$";
                    var isRomanMatched = Regex.IsMatch(clippingtypelocation, romanPattern);
                    string strMatched;
                    var isPageParsed = false;
                    if (isPagenIsMatch) {
                        var split = clippingtypelocation.Split("-");
                        if (split.Length > 1) {
                            clippingtypelocation = clippingtypelocation.Split("-")[1];
                        }
                        clippingtypelocation = clippingtypelocation.Replace("#", "");
                        strMatched = Regex.Match(clippingtypelocation, pagenPattern).Value;
                        isPageParsed = int.TryParse(strMatched, out pagenumber);
                    } else if (isRomanMatched) {
                        strMatched = _staticData.RomanToInteger(clippingtypelocation).ToString();
                        isPageParsed = int.TryParse(strMatched, out pagenumber);
                    }
                    if (isPageParsed == false || pagenumber == 0) {
                        continue;
                    }
                        
                    string clippingdate;
                    var datetime = split_b[^1].Replace("Added on", "").Replace("添加于", "").Trim();
                    datetime = datetime[(datetime.IndexOf(',') + 1)..].Trim();
                    var isDateParsed = DateTime.TryParseExact(datetime, "MMMM d, yyyy h:m:s tt", CultureInfo.GetCultureInfo("en-US"), DateTimeStyles.None, out DateTime parsedDate);
                    if (!isDateParsed) {
                        var dayOfWeekIndex = datetime.IndexOf("星期", StringComparison.Ordinal);
                        if (dayOfWeekIndex != -1) {
                            datetime = datetime.Remove(dayOfWeekIndex, 3);
                        }
                        isDateParsed = DateTime.TryParseExact(datetime, "yyyy年M月d日 tth:m:s", CultureInfo.GetCultureInfo("zh-CN"), DateTimeStyles.None, out parsedDate);
                    }
                    if (isDateParsed && parsedDate != DateTime.MinValue) {
                        clippingdate = parsedDate.ToString("yyyy-MM-dd HH:mm:ss");
                    } else {
                        continue;
                    }

                    var key = clippingdate + "|" + clippingtypelocation;
                    if (_staticData.IsExistOriginalClippings(key)) {
                        continue;
                    }

                    var isOriginClippingsInserted = _staticData.InsertOriginClippings(key, line1, line2, line3, line4, line5);
                    if (!isOriginClippingsInserted) {
                        continue;
                    }

                    string bookname;
                    var authorname = "";
                    var pattern = @"\(([^()]+)\)[^(]*$";
                    Match match = Regex.Match(line1, pattern);
                    if (match.Success) {
                        authorname = match.Groups[1].Value.Trim();
                        bookname = line1.Replace(match.Groups[0].Value.Trim(), "").Trim();
                    } else {
                        bookname = line1;
                    }

                    if (_staticData.IsExistClippings(key) || _staticData.IsExistClippingsOfContent(line4)) {
                        continue;
                    }

                    var insertResult = _staticData.InsertClippings(key, content, bookname, authorname, brieftype, clippingtypelocation, clippingdate, pagenumber);
                    if (insertResult) {
                        insertedCount += 1;
                    }
                }

                _staticData.CommitTransaction();
            } catch (Exception) {
                _staticData.RollbackTransaction();
                return string.Empty;
            }

            return Strings.Parsed_X + Strings.Space + delimiterIndex.Count + Strings.Space + Strings.X_Clippings + Strings.Symbol_Comma + Strings.Imported_X + Strings.Space + insertedCount + Strings.Space + Strings.X_Clippings;
        }

        private void DataGridView_SelectionChanged(object sender, EventArgs e) {
            try {
                DataGridViewRow selectedRow;

                if (dataGridView.CurrentRow is not null) {
                    selectedRow = dataGridView.CurrentRow;
                } else {
                    return;
                }

                var selectedIndex = tabControl.SelectedIndex;
                switch (selectedIndex) {
                    case 0:
                        var bookname = selectedRow.Cells["bookname"].Value.ToString() ?? string.Empty;
                        var authorname = selectedRow.Cells["authorname"].Value.ToString() ?? string.Empty;
                        var clippinglocation = selectedRow.Cells["clippingtypelocation"].Value.ToString() ?? string.Empty;
                        var pagenumber = selectedRow.Cells["pagenumber"].Value.ToString() ?? string.Empty;
                        var content = selectedRow.Cells["content"].Value.ToString()?.Replace(" 　　", "\n") ?? string.Empty;

                        lblBook.Text = bookname;
                        if (authorname != string.Empty) {
                            lblAuthor.Text = Strings.Left_Parenthesis + authorname + Strings.Right_Parenthesis;
                        } else {
                            lblAuthor.Text = string.Empty;
                        }

                        lblLocation.Text = clippinglocation + Strings.Space + Strings.Left_Parenthesis + Strings.Page_ + Strings.Space + pagenumber + Strings.Space + Strings.X_Page + Strings.Right_Parenthesis;

                        lblContent.Text = string.Empty;
                        lblContent.SelectionBullet = false;
                        lblContent.AppendText(content);

                        break;
                    case 1:
                        var word_key = selectedRow.Cells["word_key"].Value.ToString() ?? string.Empty;
                        var word = selectedRow.Cells["word"].Value.ToString() ?? string.Empty;
                        var stem = selectedRow.Cells["stem"].Value.ToString() ?? string.Empty;
                        var frequency = selectedRow.Cells["frequency"].Value.ToString() ?? string.Empty;

                        if (string.IsNullOrWhiteSpace(word_key) || string.IsNullOrWhiteSpace(word) || string.IsNullOrWhiteSpace(stem) || string.IsNullOrWhiteSpace(frequency)) {
                            break;
                        }

                        var usage_list = (from DataRow row in _lookupsDataTable.Rows where string.Equals(row["word_key"].ToString(), word_key, StringComparison.OrdinalIgnoreCase) let str = row["word_key"].ToString() ?? string.Empty let strContent = row["usage"].ToString() ?? string.Empty where !string.IsNullOrWhiteSpace(str) && !string.IsNullOrWhiteSpace(strContent) select strContent).ToList();
                        var usage = usage_list.Aggregate("", (current, s) => current + (s + "\n").Replace(" 　　", "\n"));
                        var usage_clippings_list = new List<DataRow>();
                        if (word.Length > 1) {
                            usage_clippings_list = _clippingsDataTable.Rows.Cast<DataRow>().Where(row => (row["content"].ToString() ?? string.Empty).Contains(word)).ToList();
                        }
                        var usage_clippings = "";
                        foreach (DataRow? row in usage_clippings_list) {
                            var isContain = false;
                            var strContent = row["content"].ToString() ?? string.Empty;
                            if (string.IsNullOrWhiteSpace(strContent)) {
                                continue;
                            }
                            foreach (var unused in usage_list.Where(strUsage => strUsage.Contains(strContent) || strContent.Contains(strUsage))) {
                                isContain = true;
                            }
                            if (!isContain) {
                                usage_clippings += strContent.Replace(" 　　", "\n") + " ——《" + row["bookname"] + "》" + "\n";
                            }
                        }

                        lblBook.Text = word;
                        if (stem != string.Empty && stem != word) {
                            lblAuthor.Text = Strings.Left_Parenthesis + Strings.Stem + Strings.Symbol_Colon + stem + Strings.Space + Strings.Right_Parenthesis;
                        } else {
                            lblAuthor.Text = "";
                        }

                        lblLocation.Text = Strings.Frequency + Strings.Symbol_Colon + frequency + Strings.Space + Strings.X_Times;

                        lblContent.Text = "";
                        lblContent.SelectionBullet = true;
                        lblContent.AppendText(usage);
                        lblContent.SelectionBullet = false;
                        lblContent.AppendText("\n");
                        lblContent.SelectionBullet = true;
                        lblContent.AppendText(usage_clippings);
                        lblContent.SelectionBullet = false;

                        var index = 0;
                        while (index < lblContent.TextLength) {
                            if (word == null) {
                                continue;
                            }

                            var wordStartIndex = lblContent.Find(word, index, RichTextBoxFinds.None);
                            if (wordStartIndex == -1) {
                                break;
                            }

                            lblContent.Select(wordStartIndex, word.Length);
                            lblContent.SelectionFont = new Font(lblContent.Font, FontStyle.Bold);

                            index = wordStartIndex + word.Length;
                        }

                        break;
                }
            } catch (Exception) {
                // ignored
            }
        }

        private void TreeViewBooks_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e) {
            if (e.Node.Text == Strings.Select_All) {
                _selectedBook = Strings.Select_All;
                lblBookCount.Text = string.Empty;
                lblBookCount.Image = null;
                lblBookCount.Visible = false;
                dataGridView.DataSource = _clippingsDataTable;
                dataGridView.Columns["bookname"]!.Visible = true;
                dataGridView.Columns["authorname"]!.Visible = true;
                dataGridView.Sort(dataGridView.Columns["clippingdate"]!, ListSortDirection.Descending);
            } else {
                var selectedBookName = e.Node.Text;
                _selectedBook = selectedBookName;
                DataTable filteredBooks = _clippingsDataTable.AsEnumerable().Where(row => row.Field<string>("bookname") == selectedBookName).CopyToDataTable();
                lblBookCount.Text = Strings.Total_Clippings + Strings.Space + filteredBooks.Rows.Count + Strings.Space + Strings.X_Clippings;
                lblBookCount.Image = Properties.Resources.open_book;
                lblBookCount.Visible = true;
                dataGridView.DataSource = filteredBooks;
                dataGridView.Columns["bookname"]!.Visible = false;
                dataGridView.Columns["authorname"]!.Visible = false;
                dataGridView.Columns["bookname"]!.HeaderText = Strings.Books;
                dataGridView.Columns["authorname"]!.HeaderText = Strings.Author;
                dataGridView.Sort(dataGridView.Columns["pagenumber"]!, ListSortDirection.Ascending);
            }
        }

        private void TreeViewBooks_MouseDown(object sender, MouseEventArgs e) {
            if (e.Button != MouseButtons.Right) {
                return;
            }

            var clickPoint = new Point(e.X, e.Y);
            TreeNode currentNode = treeViewBooks.GetNodeAt(clickPoint);
            if (currentNode == null) {
                return;
            }

            if (currentNode.Text == Strings.Select_All) {
                return;
            }

            currentNode.ContextMenuStrip = menuBooks;
            treeViewBooks.SelectedNode = currentNode;
        }

        private void LblContent_MouseDoubleClick(object sender, MouseEventArgs e) {
            ShowContentEditDialog();
        }

        private void ShowContentEditDialog() {
            using var dialog = new FrmEdit();
            dialog.LblBook = lblBook.Text;
            dialog.TxtContent = lblContent.Text;
            if (dialog.ShowDialog() != DialogResult.OK) {
                return;
            }

            var key = dataGridView.CurrentRow?.Cells["key"].Value.ToString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(key)) {
                return;
            }

            if (!_staticData.UpdateClippings(key, dialog.TxtContent)) {
                Dialog(Strings.Clippings_Revised_Failed, Strings.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            Dialog(Strings.Clippings_Revised, Strings.Successful, MessageBoxButtons.OK, MessageBoxIcon.Information);
            RefreshData();
        }

        private void DataGridView_CellDoubleClick(object sender, DataGridViewCellEventArgs e) {
            if (e is not {
                RowIndex: >= 0, ColumnIndex: >= 0
            }) {
                return;
            }

            var index = tabControl.SelectedIndex;
            var columnName = dataGridView.Columns[e.ColumnIndex].HeaderText;

            switch (index) {
                case 0:
                    if (columnName.Equals(Strings.Books)) {
                        _selectedBook = dataGridView.Rows[e.RowIndex].Cells["bookname"].Value.ToString()!;
                        DataTable filteredBooks = _clippingsDataTable.AsEnumerable().Where(row => row.Field<string>("bookname") == _selectedBook).CopyToDataTable();
                        lblBookCount.Text = Strings.Total_Clippings + Strings.Space + filteredBooks.Rows.Count + Strings.Space + Strings.X_Clippings;
                        lblBookCount.Image = Properties.Resources.open_book;
                        lblBookCount.Visible = true;
                        dataGridView.DataSource = filteredBooks;
                        dataGridView.Columns["bookname"]!.Visible = false;
                        dataGridView.Columns["authorname"]!.Visible = false;
                        dataGridView.Sort(dataGridView.Columns["clippingtypelocation"]!, ListSortDirection.Ascending);
                        RefreshData();
                    } else {
                        ShowContentEditDialog();
                    }

                    break;
                case 1:
                    if (columnName.Equals(Strings.Vocabulary) && treeViewWords.SelectedNode.Index == 0) {
                        _selectedWord = dataGridView.Rows[e.RowIndex].Cells["word"].Value.ToString()!;
                        DataTable filteredWord = _lookupsDataTable.AsEnumerable().Where(row => row.Field<string>("word") == _selectedWord).CopyToDataTable();
                        lblBookCount.Text = Strings.Total_Clippings + Strings.Space + filteredWord.Rows.Count + Strings.Space + Strings.X_Clippings;
                        lblBookCount.Image = Properties.Resources.open_book;
                        lblBookCount.Visible = true;
                        dataGridView.Columns["usage"]!.Visible = true;
                        dataGridView.Columns["title"]!.Visible = true;
                        dataGridView.Columns["authors"]!.Visible = true;
                        dataGridView.Columns["frequency"]!.Visible = false;
                        dataGridView.Columns["usage"]!.HeaderText = Strings.Content;
                        dataGridView.Columns["title"]!.HeaderText = Strings.Books;
                        dataGridView.Columns["authors"]!.HeaderText = Strings.Author;
                        dataGridView.Sort(dataGridView.Columns["timestamp"]!, ListSortDirection.Descending);
                        RefreshData();
                    }
                    break;
            }
        }

        private void DataGridView_CellMouseDown(object sender, DataGridViewCellMouseEventArgs e) {
            if (e.Button != MouseButtons.Right) {
                return;
            }

            if (e.RowIndex <= -1 || e.ColumnIndex <= -1) {
                return;
            }

            dataGridView.CurrentCell = dataGridView.Rows[e.RowIndex].Cells[e.ColumnIndex];
            Point location = dataGridView.PointToClient(Cursor.Position);
            menuClippings.Show(dataGridView, location);
        }

        private void ClippingMenuDelete_Click(object sender, EventArgs e) {
            if (dataGridView.SelectedRows.Count <= 0) {
                return;
            }

            var index = tabControl.SelectedIndex;
            switch (index) {
                case 0:
                    DialogResult resultClippings = Dialog(Strings.Confirm_Delete_Selected_Clippings, Strings.Confirm, MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                    if (resultClippings != DialogResult.Yes) {
                        return;
                    }

                    _staticData.BeginTransaction();

                    try {
                        foreach (DataGridViewRow row in dataGridView.SelectedRows) {
                            if (_staticData.DeleteClippingsByKey(row.Cells["key"].Value.ToString() ?? string.Empty)) {
                                dataGridView.Rows.Remove(row);
                            } else {
                                Dialog(Strings.Delete_Failed, Strings.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }

                        _staticData.CommitTransaction();
                    } catch (Exception) {
                        _staticData.RollbackTransaction();
                    }

                    break;
                case 1:
                    DialogResult resultWords = Dialog(Strings.Confirm_Delete_Lookups, Strings.Confirm, MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                    if (resultWords != DialogResult.Yes) {
                        return;
                    }

                    _staticData.BeginTransaction();

                    try {
                        foreach (DataGridViewRow row in dataGridView.SelectedRows) {
                            if (_staticData.DeleteLookupsByTimeStamp(row.Cells["timestamp"].Value.ToString() ?? string.Empty)) {
                                dataGridView.Rows.Remove(row);
                            } else {
                                Dialog(Strings.Delete_Failed, Strings.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }

                        _staticData.CommitTransaction();
                    } catch (Exception) {
                        _staticData.RollbackTransaction();
                    }

                    break;
            }

            RefreshData();
        }

        private void ClippingMenuCopy_Click(object sender, EventArgs e) {
            if (dataGridView.CurrentRow is null) {
                return;
            }

            var index = tabControl.SelectedIndex;
            switch (index) {
                case 0:
                    var content = dataGridView.CurrentRow.Cells["content"].Value.ToString() ?? string.Empty;
                    Clipboard.SetText(content != string.Empty ? content : lblContent.Text);
                    break;
                case 1:
                    var usage = dataGridView.CurrentRow.Cells["usage"].Value.ToString() ?? string.Empty;
                    Clipboard.SetText(usage != string.Empty ? usage : lblBook.Text);
                    break;
            }
        }

        private void DataGridView_KeyDown(object sender, KeyEventArgs e) {
            // ReSharper disable once ConvertIfStatementToSwitchStatement
            if (e.KeyCode == Keys.Enter) {
                ShowContentEditDialog();
                e.Handled = true;
            } else if (e.KeyCode == Keys.Delete) {
                ClippingMenuDelete_Click(sender, e);
            }
        }

        private void BooksMenuDelete_Click(object sender, EventArgs e) {
            var index = tabControl.SelectedIndex;
            switch (index) {
                case 0:
                    if (treeViewBooks.SelectedNode is null) {
                        return;
                    }

                    DialogResult resultBooks = Dialog(Strings.Confirm_Delete_Clippings_Book, Strings.Confirm, MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                    if (resultBooks != DialogResult.Yes) {
                        return;
                    }

                    var bookname = treeViewBooks.SelectedNode.Text;
                    if (!_staticData.DeleteClippingsByBook(bookname)) {
                        Dialog(Strings.Delete_Failed, Strings.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }

                    break;
                case 1:
                    if (treeViewWords.SelectedNode is null) {
                        return;
                    }

                    DialogResult resultWords = Dialog(Strings.Confirm_Delete_Lookups_Vocabs, Strings.Confirm, MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                    if (resultWords != DialogResult.Yes) {
                        return;
                    }

                    var word = treeViewBooks.SelectedNode.Text;
                    var word_key = "";
                    foreach (DataRow row in _vocabDataTable.Rows) {
                        if (row["word"].ToString() != word) {
                            continue;
                        }

                        word_key = row["word_key"].ToString() ?? string.Empty;
                        break;
                    }

                    if (!_staticData.DeleteVocab(word) && !_staticData.DeleteLookupsByWordKey(word_key)) {
                        Dialog(Strings.Delete_Failed, Strings.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }

                    break;
            }

            RefreshData();
        }

        private void MenuExit_Click(object sender, EventArgs e) {
            Close();
        }

        private void MenuAbout_Click(object sender, EventArgs e) {
            using var dialog = new FrmAboutBox();
            dialog.ShowDialog();
        }

        private void MenuImportKindle_Click(object sender, EventArgs e) {
            var fileDialog = new OpenFileDialog {
                Title = Strings.Import_Kindle_Clipping_File + Strings.Space + @"(My Clippings.txt)",
                CheckFileExists = true,
                CheckPathExists = true,
                DefaultExt = "txt",
                Filter = Strings.Kindle_Clipping_File + Strings.Space + @"(*.txt)|*.txt",
                FilterIndex = 2,
                RestoreDirectory = true,
                ReadOnlyChecked = true,
                ShowReadOnly = true
            };

            if (fileDialog.ShowDialog() != DialogResult.OK) {
                return;
            }

            SetProgressBar(true);
            var bw = new BackgroundWorker();
            bw.DoWork += (_, workEventArgs) => {
                workEventArgs.Result = ImportKindleClippings(fileDialog.FileName);
            };
            bw.RunWorkerAsync();
            bw.RunWorkerCompleted += (_, workerCompletedEventArgs) => {
                if (workerCompletedEventArgs.Result != null && !string.IsNullOrWhiteSpace(workerCompletedEventArgs.Result.ToString())) {
                    Dialog(workerCompletedEventArgs.Result.ToString() ?? string.Empty, Strings.Successful, MessageBoxButtons.OK, MessageBoxIcon.Information);
                } else {
                    Dialog(Strings.Import_Failed, Strings.Failed, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                SetProgressBar(false);
                RefreshData();
            };

            RefreshData();
        }

        private void SetProgressBar(bool isShow) {
            progressBar.Enabled = isShow;
            progressBar.Visible = isShow;
            Enabled = !isShow;
        }

        private void MenuImportKindleMate_Click(object sender, EventArgs e) {
            var fileDialog = new OpenFileDialog {
                Title = Strings.Import_Kindle_Mate_Database_File + Strings.Space + @"(KM2.dat)",
                CheckFileExists = true,
                CheckPathExists = true,
                DefaultExt = "dat",
                Filter = Strings.Kindle_Mate_Database_File + Strings.Space + @"(*.dat)|*.dat",
                FilterIndex = 2,
                RestoreDirectory = true,
                ReadOnlyChecked = true,
                ShowReadOnly = true
            };

            if (fileDialog.ShowDialog() != DialogResult.OK) {
                return;
            }

            SetProgressBar(true);
            var bw = new BackgroundWorker();
            bw.DoWork += (_, workEventArgs) => {
                workEventArgs.Result = ImportKMDatabase(fileDialog.FileName);
            };
            bw.RunWorkerAsync();
            bw.RunWorkerCompleted += (_, workerCompletedEventArgs) => {
                if (workerCompletedEventArgs.Result != null && !string.IsNullOrWhiteSpace(workerCompletedEventArgs.Result.ToString())) {
                    Dialog(workerCompletedEventArgs.Result.ToString() ?? string.Empty, Strings.Successful, MessageBoxButtons.OK, MessageBoxIcon.Information);
                } else {
                    Dialog(Strings.Import_Failed, Strings.Failed, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                SetProgressBar(false);
                RefreshData();
            };
        }

        private void MenuRepo_Click(object sender, EventArgs e) {
            const string repoUrl = "https://github.com/lzcapp/KindleMate2";
            try {
                Process.Start(new ProcessStartInfo {
                    FileName = repoUrl, UseShellExecute = true
                });
            } catch (Exception) {
                Clipboard.SetText(repoUrl);
                Dialog(Strings.Repo_URL_Copied, Strings.Prompt, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private bool IsKindleDeviceConnected() {
            var drives = DriveInfo.GetDrives();

            foreach (DriveInfo drive in drives) {
                if (drive.DriveType != DriveType.Removable) {
                    continue;
                }

                var documentsDir = Path.Combine(drive.Name, "documents");
                if (!Directory.Exists(documentsDir)) {
                    continue;
                }

                var clippingsPath = Path.Combine(documentsDir, "My Clippings.txt");
                if (!File.Exists(clippingsPath)) {
                    continue;
                }

                _kindleDrive = drive.Name;
                return true;
            }

            _kindleDrive = string.Empty;
            return false;
        }

        private void MenuSyncFromKindle_Click(object sender, EventArgs e) {
            ImportFromKindle();
        }

        private void ImportFromKindle() {
            var kindleClippingsPath = Path.Combine(_kindleDrive, _kindleClippingsPath);
            var kindleWordsPath = Path.Combine(_kindleDrive, _kindleWordsPath);
            
            var bw = new BackgroundWorker();
            bw.DoWork += (_, e) => {
                e.Result = Import(kindleClippingsPath, kindleWordsPath);
            };
            bw.RunWorkerAsync();
            bw.RunWorkerCompleted += (_, e) => {
                if (e.Result != null && !string.IsNullOrWhiteSpace(e.Result.ToString())) {
                    Dialog(e.Result.ToString() ?? string.Empty, Strings.Successful, MessageBoxButtons.OK, MessageBoxIcon.Information);
                } else {
                    Dialog(Strings.Import_Failed, Strings.Failed, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                SetProgressBar(false);
                RefreshData();
            };
        }

        private void Timer_Tick(object sender, EventArgs e) {
            if (IsKindleDeviceConnected()) {
                var kindleVersionPath = Path.Combine(_kindleDrive, _kindleVersionPath);
                if (File.Exists(kindleVersionPath)) {
                    using var reader = new StreamReader(kindleVersionPath);

                    var kindleVersion = reader.ReadLine()?.Trim().Split('(')[0].Replace("Kindle", "").Trim();
                    if (!string.IsNullOrEmpty(kindleVersion)) {
                        menuKindle.Text = Strings.Space + Strings.Kindle_Device_Connected + Strings.Left_Parenthesis + kindleVersion + Strings.Right_Parenthesis;
                    }
                } else {
                    menuKindle.Text = Strings.Space + Strings.Kindle_Device_Connected;
                }

                menuSyncFromKindle.Visible = true;
                menuKindle.Visible = true;
            } else {
                menuSyncFromKindle.Visible = false;
                menuKindle.Visible = false;
            }
        }

        private void MenuKindle_Click(object sender, EventArgs e) {
            ImportFromKindle();
        }

        private void MenuRename_Click(object sender, EventArgs e) {
            ShowBookRenameDialog();
        }

        private void ShowBookRenameDialog() {
            using var dialog = new FrmBookRename();
            var bookname = GetBookname();
            var authorname = GetAuthorname();

            dialog.TxtBook = bookname;
            dialog.TxtAuthor = authorname;
            if (dialog.ShowDialog() != DialogResult.OK) {
                return;
            }

            var dialogBook = dialog.TxtBook.Trim();
            var dialogAuthor = dialog.TxtAuthor.Trim();
            if (string.IsNullOrWhiteSpace(dialogBook)) {
                return;
            }

            if (!string.IsNullOrWhiteSpace(authorname) && string.IsNullOrWhiteSpace(dialogAuthor)) {
                dialogAuthor = authorname;
            }

            if (bookname == dialogBook && authorname == dialogAuthor) {
                Dialog(Strings.Books_Title_Not_Changed, Strings.Prompt, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (_clippingsDataTable.AsEnumerable().Any(row => row.Field<string>("BookName") == "dialogBook")) {
                DialogResult result = Dialog(Strings.Confirm_Same_Title_Combine, Strings.Confirm, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result != DialogResult.Yes) {
                    return;
                }

                var resultRows = _clippingsDataTable.Select($"bookname = '{bookname}'");
                dialogAuthor = (resultRows.Length > 0 ? resultRows[0]["authorname"].ToString() : string.Empty) ?? string.Empty;
            }

            _staticData.UpdateLookups(bookname, dialogBook, dialogAuthor);

            if (!_staticData.UpdateClippings(bookname, dialogBook, dialogAuthor)) {
                Dialog(Strings.Book_Renamed_Failed, Strings.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            Dialog(Strings.Books_Renamed, Strings.Successful, MessageBoxButtons.OK, MessageBoxIcon.Information);

            _selectedBook = dialogBook;

            RefreshData();
        }

        private string GetBookname() {
            string bookname;
            if (!string.IsNullOrWhiteSpace(lblBook.Text)) {
                bookname = lblBook.Text;
            } else {
                bookname = dataGridView.Rows[0].Cells["bookname"].Value.ToString() ?? string.Empty;
            }

            return bookname;
        }

        private string GetAuthorname() {
            string authorname;
            if (!string.IsNullOrWhiteSpace(lblAuthor.Text)) {
                authorname = lblAuthor.Text;
                var startIndex = authorname.IndexOf(Strings.Left_Parenthesis, StringComparison.Ordinal) + 1;
                var endIndex = authorname.LastIndexOf(Strings.Right_Parenthesis, StringComparison.Ordinal) - 1;
                authorname = authorname.Substring(startIndex, endIndex - startIndex + 1);
            } else {
                authorname = dataGridView.Rows[0].Cells["authorname"].Value.ToString() ?? string.Empty;
            }

            return authorname;
        }

        private void MenuClippingsRefresh_Click(object sender, EventArgs e) {
            RefreshData();
        }

        private void SelectRow() {
            if (dataGridView.Rows.Count <= 0) {
                return;
            }

            dataGridView.ClearSelection();

            var index = tabControl.SelectedIndex;

            switch (index) {
                case 0:
                    if (_selectedIndex >= dataGridView.Rows.Count) {
                        _selectedIndex = dataGridView.Rows.Count - 1;
                    }

                    dataGridView.FirstDisplayedScrollingRowIndex = _selectedIndex;
                    dataGridView.Rows[_selectedIndex].Selected = true;

                    DataGridViewRow selectedRow = dataGridView.SelectedRows[0];

                    var bookname = selectedRow.Cells["bookname"].Value.ToString();
                    var authorname = selectedRow.Cells["authorname"].Value.ToString();
                    var clippinglocation = selectedRow.Cells["clippingtypelocation"].Value.ToString();
                    var content = selectedRow.Cells["content"].Value.ToString();

                    lblBook.Text = bookname;
                    if (authorname != string.Empty) {
                        lblAuthor.Text = Strings.Left_Parenthesis + authorname + Strings.Right_Parenthesis;
                    } else {
                        lblAuthor.Text = "";
                    }

                    lblLocation.Text = clippinglocation;
                    lblContent.Text = content;
                    break;
                case 1:
                    if (_selectedIndex < 0) {
                        _selectedIndex = 0;
                    }

                    if (_selectedIndex >= dataGridView.Rows.Count) {
                        _selectedIndex = dataGridView.Rows.Count - 1;
                    }

                    dataGridView.FirstDisplayedScrollingRowIndex = _selectedIndex;
                    dataGridView.Rows[_selectedIndex].Selected = true;
                    break;
            }
        }

        private void MenuBookRefresh_Click(object sender, EventArgs e) {
            RefreshData();
        }

        private bool BackupOriginClippings() {
            DataTable dataTable = _staticData.GetOriginClippingsDataTable();
            dataTable.DefaultView.Sort = "key ASC";
            DataTable sortedDataTable = dataTable.DefaultView.ToTable();

            var filePath = Path.Combine(_programsDirectory, "Backups", "My Clippings.txt");

            if (!Directory.Exists(Path.Combine(_programsDirectory, "Backups"))) {
                Directory.CreateDirectory(Path.Combine(_programsDirectory, "Backups"));
            }

            if (File.Exists(filePath)) {
                File.Delete(filePath);
            }

            using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
            using var writer = new StreamWriter(fileStream);
            foreach (DataRow row in sortedDataTable.Rows) {
                writer.WriteLine(row["line1"]);
                writer.WriteLine(row["line2"]);
                writer.WriteLine(row["line3"]);
                writer.WriteLine(row["line4"]);
                writer.WriteLine(row["line5"]);
            }

            writer.Close();
            fileStream.Close();

            return true;
        }

        private void MenuBackup_Click(object sender, EventArgs e) {
            BackupDatabase();

            if (_clippingsDataTable.Rows.Count <= 0) {
                Dialog(Strings.No_Data_To_Backup, Strings.Prompt, MessageBoxButtons.OK, MessageBoxIcon.Information);
            } else {
                if (!BackupOriginClippings()) {
                    Dialog(Strings.Backup_Clippings_Failed, Strings.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
                } else {
                    DialogResult result = Dialog(Strings.Backup_Successful + Strings.Open_Folder, Strings.Successful, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (result != DialogResult.Yes) {
                        return;
                    }

                    Process.Start("explorer.exe", Path.Combine(_programsDirectory, "Backups"));
                }
            }
        }

        private void BackupDatabase() {
            if (_staticData.IsDatabaseEmpty()) {
                return;
            }

            var filePath = Path.Combine(_programsDirectory, "Backups", "KM2.dat");

            if (!Directory.Exists(Path.Combine(_programsDirectory, "Backups"))) {
                Directory.CreateDirectory(Path.Combine(_programsDirectory, "Backups"));
            }

            if (File.Exists(filePath)) {
                File.Delete(filePath);
            }

            File.Copy(_filePath, filePath);
        }


        private void MenuRefresh_Click(object sender, EventArgs e) {
            RefreshData();
        }

        private void MenuClear_Click(object sender, EventArgs e) {
            if (_staticData.IsDatabaseEmpty()) {
                Dialog(Strings.No_Data_To_Clear, Strings.Prompt, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            DialogResult result = Dialog(Strings.Confirm_Clear_All_Data, Strings.Confirm, MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation);
            if (result != DialogResult.Yes) {
                return;
            }

            if (_staticData.EmptyTables()) {
                Dialog(Strings.Data_Cleared, Strings.Successful, MessageBoxButtons.OK, MessageBoxIcon.Information);
            } else {
                Dialog(Strings.Clear_Failed, Strings.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            Restart();
        }

        private static void Restart() {
            Process.Start(new ProcessStartInfo {
                FileName = Application.ExecutablePath, UseShellExecute = true
            });

            Environment.Exit(0);
        }

        private void MenuImportKindleWords_Click(object sender, EventArgs e) {
            var fileDialog = new OpenFileDialog {
                Title = Strings.Import_Kindle_Vocab_File + Strings.Space + @"(vocab.db)",
                CheckFileExists = true,
                CheckPathExists = true,
                DefaultExt = "txt",
                Filter = Strings.Kindle_Vocab_File + Strings.Space + @"(*.db)|*.db",
                FilterIndex = 2,
                RestoreDirectory = true,
                ReadOnlyChecked = true,
                ShowReadOnly = true
            };

            if (fileDialog.ShowDialog() != DialogResult.OK) {
                return;
            }

            SetProgressBar(true);
            var bw = new BackgroundWorker();
            bw.DoWork += (_, workEventArgs) => {
                workEventArgs.Result = ImportKindleWords(fileDialog.FileName);
            };
            bw.RunWorkerAsync();
            bw.RunWorkerCompleted += (_, workerCompletedEventArgs) => {
                if (workerCompletedEventArgs.Result != null && !string.IsNullOrWhiteSpace(workerCompletedEventArgs.Result.ToString())) {
                    Dialog(workerCompletedEventArgs.Result.ToString() ?? string.Empty, Strings.Successful, MessageBoxButtons.OK, MessageBoxIcon.Information);
                } else {
                    Dialog(Strings.Import_Failed, Strings.Failed, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                SetProgressBar(false);
                RefreshData();
            };

            RefreshData();
        }

        private void TabControl_SelectedIndexChanged(object sender, EventArgs e) {
            var index = tabControl.SelectedIndex;
            menuRename.Visible = index switch {
                0 => true,
                1 => false,
                _ => menuRename.Visible,
            };
            RefreshData();
        }

        private void TreeViewWords_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e) {
            if (e.Node.Text is "Select All" or "全部") {
                _selectedWord = string.Empty;
                lblBookCount.Text = string.Empty;
                lblBookCount.Image = null;
                lblBookCount.Visible = false;
                dataGridView.DataSource = _lookupsDataTable;

                dataGridView.Columns["usage"]!.Visible = false;
                dataGridView.Columns["title"]!.Visible = false;
                dataGridView.Columns["authors"]!.Visible = false;

                dataGridView.Columns["frequency"]!.Visible = true;

                dataGridView.Columns["frequency"]!.HeaderText = Strings.Frequency;
            } else {
                var selectedWord = e.Node.Text;
                _selectedWord = selectedWord;
                DataTable filteredWords = _lookupsDataTable.AsEnumerable().Where(row => row.Field<string>("word_key")?[3..] == selectedWord).CopyToDataTable();
                lblBookCount.Text = Strings.Totally_Vocabs + Strings.Space + filteredWords.Rows.Count + Strings.Space + Strings.X_Lookups;
                lblBookCount.Image = Properties.Resources.input_latin_uppercase;
                lblBookCount.Visible = true;
                dataGridView.DataSource = filteredWords;

                dataGridView.Columns["usage"]!.Visible = true;
                dataGridView.Columns["title"]!.Visible = true;
                dataGridView.Columns["authors"]!.Visible = true;

                dataGridView.Columns["frequency"]!.Visible = false;

                dataGridView.Columns["usage"]!.HeaderText = Strings.Content;
                dataGridView.Columns["title"]!.HeaderText = Strings.Books;
                dataGridView.Columns["authors"]!.HeaderText = Strings.Author;
            }
        }

        private void TreeViewWords_MouseDown(object sender, MouseEventArgs e) {
            if (e.Button != MouseButtons.Right) {
                return;
            }

            var clickPoint = new Point(e.X, e.Y);
            TreeNode currentNode = treeViewWords.GetNodeAt(clickPoint);
            if (currentNode == null) {
                return;
            }

            currentNode.ContextMenuStrip = menuBooks;
            treeViewBooks.SelectedNode = currentNode;
        }

        private void TreeViewBooks_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e) {
            if (e.Node.Text is "全选" or "Select All") {
                return;
            }

            ShowBookRenameDialog();
        }

        private void LblBook_MouseDoubleClick(object sender, MouseEventArgs e) {
            ShowBookRenameDialog();
        }

        private void LblAuthor_MouseDoubleClick(object sender, MouseEventArgs e) {
            ShowBookRenameDialog();
        }

        private void FlowLayoutPanel_MouseDoubleClick(object sender, MouseEventArgs e) {
            ShowBookRenameDialog();
        }

        private void TreeViewBooks_KeyDown(object sender, KeyEventArgs e) {
            // ReSharper disable once ConvertIfStatementToSwitchStatement
            if (e.KeyCode == Keys.Delete) {
                BooksMenuDelete_Click(sender, e);
            } else if (e.KeyCode == Keys.Enter) {
                MenuRename_Click(sender, e);
            }
        }

        private void MenuRestart_Click(object sender, EventArgs e) {
            Restart();
        }

        private void MenuClean_Click(object sender, EventArgs e) {
            _clippingsDataTable = _staticData.GetClipingsDataTable();

            if (_clippingsDataTable.Rows.Count <= 0) {
                Dialog(Strings.Database_No_Need_Clean, Strings.Prompt, MessageBoxButtons.OK, MessageBoxIcon.Information);
            } else {
                _staticData.BeginTransaction();

                try {
                    var countEmpty = 0;
                    var countTrimmed = 0;

                    foreach (DataRow row in _clippingsDataTable.Rows) {
                        var key = row["key"].ToString();
                        var content = row["content"].ToString();
                        if (string.IsNullOrWhiteSpace(key)) {
                            continue;
                        }

                        if (string.IsNullOrWhiteSpace(content)) {
                            if (_staticData.DeleteClippingsByKey(key)) {
                                countEmpty++;
                            }

                            continue;
                        }

                        var contentTrimmed = TrimContent(content);
                        if (string.IsNullOrWhiteSpace(contentTrimmed)) {
                            if (_staticData.DeleteClippingsByKey(key)) {
                                countEmpty++;
                            }

                            continue;
                        }

                        if (contentTrimmed.Equals(content)) {
                            continue;
                        }

                        if (_staticData.UpdateClippings(key, contentTrimmed)) {
                            countTrimmed++;
                        }
                    }

                    var fileInfo = new FileInfo(_filePath);
                    var originFileSize = fileInfo.Length;

                    _staticData.CommitTransaction();

                    _staticData.VacuumDatabase();

                    var newFileSize = fileInfo.Length;

                    var fileSizeDelta = originFileSize - newFileSize;

                    if (countEmpty > 0 || countTrimmed > 0 || fileSizeDelta > 0) {
                        Dialog(Strings.Cleaned + Strings.Space + Strings.Empty_Content + Strings.Space + countEmpty + Strings.Space + Strings.X_Rows + Strings.Symbol_Comma + Strings.Trimmed + Strings.Space + countTrimmed + Strings.Space + Strings.X_Rows + Strings.Symbol_Comma + Strings.Database_Cleaned + Strings.Space + FormatFileSize(fileSizeDelta), Strings.Clean_Database, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    } else {
                        Dialog(Strings.Database_No_Need_Clean, Strings.Prompt, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                } catch (Exception) {
                    _staticData.RollbackTransaction();
                }
            }
        }

        private static string TrimContent(string content) {
            var contentTrimmed = content.TrimStart(' ', '.', '，', '。').Trim();
            return contentTrimmed;
        }

        private static string FormatFileSize(long fileSize) {
            string[] sizes = [
                "B", "KB", "MB", "GB", "TB"
            ];

            var order = 0;
            double size = fileSize;

            while (size >= 1024 && order < sizes.Length - 1) {
                order++;
                size /= 1024;
            }

            return $"{size:0.##} {sizes[order]}";
        }

        private bool ClippingsToMarkdown() {
            try {
                var markdown = new StringBuilder();

                markdown.AppendLine("# \ud83d\udcda " + Strings.Books);

                markdown.AppendLine();

                foreach (TreeNode node in treeViewBooks.Nodes) {
                    var selectedBookName = node.Text;

                    if (selectedBookName.Equals(Strings.Select_All)) {
                        continue;
                    }

                    DataTable filteredBooks = _clippingsDataTable.AsEnumerable().Where(row => row.Field<string>("bookname") == selectedBookName).CopyToDataTable();

                    markdown.AppendLine("## \ud83d\udcd6 " + selectedBookName.Trim());

                    markdown.AppendLine();

                    foreach (DataRow row in filteredBooks.Rows) {
                        var clippinglocation = row["clippingtypelocation"].ToString();
                        var clippingdate = row["clippingdate"].ToString();
                        var pagenumber = row["pagenumber"].ToString();
                        var content = row["content"].ToString();

                        markdown.AppendLine("**\ud83d\udccd " + clippinglocation + Strings.Left_Parenthesis + Strings.Page_ + pagenumber + Strings.X_Page + Strings.Right_Parenthesis + " `" + clippingdate + "`**");

                        markdown.AppendLine();

                        markdown.AppendLine("> " + content);

                        markdown.AppendLine();
                    }
                }

                File.WriteAllText(Path.Combine(_programsDirectory, "Exports", "Clippings.md"), markdown.ToString());

                var htmlContent = "<html><head>\r\n<link rel=\"stylesheet\" href=\"styles.css\">\r\n</head><body>\r\n";

                htmlContent += Markdown.ToHtml(markdown.ToString());

                htmlContent += "\r\n</body>\r\n</html>";

                File.WriteAllText(Path.Combine(_programsDirectory, "Exports", "Clippings.html"), htmlContent);

                return true;
            } catch (Exception) {
                return false;
            }
        }

        private bool VocabsToMarkdown() {
            try {
                var markdown = new StringBuilder();

                markdown.AppendLine("# \ud83d\udcda " + Strings.Vocabulary_List);

                markdown.AppendLine();

                foreach (TreeNode node in treeViewWords.Nodes) {
                    var word = node.Text;

                    if (word.Equals(Strings.Select_All)) {
                        continue;
                    }

                    DataTable filteredBooks = _lookupsDataTable.AsEnumerable().Where(row => row.Field<string>("word") == word).CopyToDataTable();

                    markdown.AppendLine("## \ud83d\udd24 " + word.Trim());

                    markdown.AppendLine();

                    foreach (DataRow row in filteredBooks.Rows) {
                        var timestamp = row["timestamp"].ToString();
                        var title = row["title"].ToString();
                        var usage = row["usage"].ToString();

                        if (usage == null) {
                            continue;
                        }

                        markdown.AppendLine("**\ud83d\udccd 《" + title + "》 `" + timestamp + "`**");

                        markdown.AppendLine();

                        markdown.AppendLine("> " + usage.Replace(word, " **`" + word + "`** "));

                        markdown.AppendLine();
                    }
                }

                File.WriteAllText(Path.Combine(_programsDirectory, "Exports", "Vocabs.md"), markdown.ToString());

                var htmlContent = "<html><head>\r\n<link rel=\"stylesheet\" href=\"styles.css\">\r\n</head><body>\r\n";

                htmlContent += Markdown.ToHtml(markdown.ToString());

                htmlContent += "\r\n</body>\r\n</html>";

                File.WriteAllText(Path.Combine(_programsDirectory, "Exports", "Vocabs.html"), htmlContent);

                return true;
            } catch (Exception) {
                return false;
            }
        }

        private void MenuExportMd_Click(object sender, EventArgs e) {
            if (!Directory.Exists(Path.Combine(_programsDirectory, "Exports"))) {
                Directory.CreateDirectory(Path.Combine(_programsDirectory, "Exports"));
            }

            const string css = "@import url('https://fonts.googleapis.com/css2?family=Noto+Color+Emoji&family=Noto+Emoji:wght@300..700&display=swap');\r\n\r\n* {\r\n    font-family: -apple-system, \"Noto Sans\", \"Helvetica Neue\", Helvetica, \"Nimbus Sans L\", Arial, \"Liberation Sans\", \"PingFang SC\", \"Hiragino Sans GB\", \"Noto Sans CJK SC\", \"Source Han Sans SC\", \"Source Han Sans CN\", \"Microsoft YaHei UI\", \"Microsoft YaHei\", \"Wenquanyi Micro Hei\", \"WenQuanYi Zen Hei\", \"ST Heiti\", SimHei, \"WenQuanYi Zen Hei Sharp\", \"Noto Emoji\", sans-serif;\r\n}\r\n\r\nbody {\r\n    font-family: 'Arial', sans-serif;\r\n    background-color: #f9f9f9;\r\n    color: #333;\r\n    line-height: 1.6;\r\n    align-items: center;\r\n    width: 80vw;\r\n    margin: 20px auto;\r\n}\r\n\r\nh1 {\r\n    font-size: 30px;\r\n    text-align: center;\r\n    margin: 30px auto;\r\n    color: #333;\r\n}\r\n\r\nh2 {\r\n    font-size: 24px;\r\n    margin: 30px auto;\r\n    color: #333;\r\n}\r\n\r\np {\r\n    font-size: 16px;\r\n    margin: 20px auto;\r\n}\r\n\r\ncode {\r\n    background-color: antiquewhite;\r\n    border-radius: 10px;\r\n    padding: 2px 6px;\r\n}";

            File.WriteAllText(Path.Combine(_programsDirectory, "Exports", "styles.css"), css);

            if (!ClippingsToMarkdown() || !VocabsToMarkdown()) {
                return;
            }

            DialogResult result = Dialog(Strings.Export_Successful + Strings.Open_Folder, Strings.Successful, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result != DialogResult.Yes) {
                return;
            }

            Process.Start("explorer.exe", Path.Combine(_programsDirectory, "Exports"));
        }

        private void MenuStatistic_Click(object sender, EventArgs e) {
            using var dialog = new FrmStatistics();
            dialog.ShowDialog();
        }

        private void MenuKindle_MouseEnter(object sender, EventArgs e) {
            Cursor = Cursors.Hand;
        }

        private void MenuKindle_MouseLeave(object sender, EventArgs e) {
            Cursor = Cursors.Default;
        }

        private void MenuTheme_Click(object sender, EventArgs e) {
            _staticData.SetTheme(_staticData.IsDarkTheme() ? "light" : "dark");
            Restart();
        }

        private void MenuTheme_MouseEnter(object sender, EventArgs e) {
            Cursor = Cursors.Hand;
        }

        private void MenuTheme_MouseLeave(object sender, EventArgs e) {
            Cursor = Cursors.Default;
        }

        private void MenuLangEN_Click(object sender, EventArgs e) {
            _staticData.SetLanguage("en");
            Restart();
        }

        private void MenuLangSC_Click(object sender, EventArgs e) {
            _staticData.SetLanguage("zh-Hans");
            Restart();
        }

        private void MenuLangTC_Click(object sender, EventArgs e) {
            _staticData.SetLanguage("zh-Hant");
            Restart();
        }

        private void MenuLangAuto_Click(object sender, EventArgs e) {
            _staticData.SetLanguage("");
            Restart();
        }
    }
}