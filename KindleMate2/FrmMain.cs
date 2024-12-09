using System.ComponentModel;
using System.Data;
using System.Data.SQLite;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using DarkModeForms;
using KindleMate2.Entities;
using KindleMate2.Properties;
using Markdig;
using Markdig.Helpers;

namespace KindleMate2 {
    public partial class FrmMain : Form {
        [DllImport("User32.dll")]
        static extern bool HideCaret(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern bool DestroyCaret();

        private DataTable _clippingsDataTable = new();

        private DataTable _originClippingsDataTable = new();

        private DataTable _vocabDataTable = new();

        private DataTable _lookupsDataTable = new();

        private readonly StaticData _staticData;

        private readonly string _programsDirectory;

        private readonly string _filePath;

        private string _kindleDrive;

        private readonly string _kindleClippingsPath;

        private readonly string _kindleWordsPath;

        private readonly string _kindleVersionPath;

        private string _selectedBook;

        private string _selectedWord;

        private int _selectedTreeIndex;

        private int _selectedDataGridIndex;

        private string _searchText;

        private bool _isDarkTheme;

        public FrmMain() {
            InitializeComponent();

            try {
                if (File.Exists(Path.Combine(Environment.CurrentDirectory, "KM2.dat"))) {
                    File.Delete(Path.Combine(Environment.CurrentDirectory, "KM.dat"));
                } else {
                    if (!StaticData.CreateDatabase()) {
                        _ = MessageBox(Strings.Create_Database_Failed, Strings.Error, MessageBoxButtons.OK, MsgIcon.Error);
                        Environment.Exit(0);
                    }
                }
            } catch (Exception e) {
                MessageBox(e.Message, Strings.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.Exit(0);
            }

            _staticData = new StaticData();

            SetTheme();

            SetLang();

            AppDomain.CurrentDomain.ProcessExit += (_, _) => {
                BackupDatabase();
                _staticData.CloseConnection();
                _staticData.DisposeConnection();
            };

            //CheckUpdate();

            _programsDirectory = Environment.CurrentDirectory;
            _filePath = Path.Combine(_programsDirectory, "KM2.dat");
            _kindleClippingsPath = Path.Combine("documents", "My Clippings.txt");
            _kindleWordsPath = Path.Combine("system", "vocabulary", "vocab.db");
            _kindleVersionPath = Path.Combine("system", "version.txt");
            _kindleDrive = string.Empty;
            _selectedBook = Strings.Select_All;
            _selectedWord = Strings.Select_All;
            _selectedTreeIndex = 0;
            _selectedDataGridIndex = 0;
            _searchText = GetSearchText();

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
            menuRebuild.Text = Strings.Rebuild_Database;
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

            cmbSearch.Items.Add(Strings.Select_All);
            cmbSearch.Items.Add(Strings.Book_Title);
            cmbSearch.Items.Add(Strings.Author);
            cmbSearch.Items.Add(Strings.Vocabulary);
            cmbSearch.Items.Add(Strings.Stem);
            cmbSearch.Items.Add(Strings.Content);
            cmbSearch.SelectedIndex = 0;

            dataGridView.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;

            lblContent.GotFocus += LblContent_GotFocus;
            lblContent.LostFocus += LblContent_LostFocus;
        }

        private static void CheckUpdate() {
            try {
                var bw = new BackgroundWorker();
                bw.DoWork += (_, workEventArgs) => { workEventArgs.Result = StaticData.GetRepoInfo(); };
                bw.RunWorkerCompleted += (_, workerCompletedEventArgs) => {
                    if (workerCompletedEventArgs.Result == null) {
                        return;
                    }
                    var release = (GitHubRelease)workerCompletedEventArgs.Result;
                    var tagName = string.IsNullOrWhiteSpace(release.tag_name) ? string.Empty : release.tag_name;
                    var isUpdate = StaticData.IsUpdate(tagName);
                    if (isUpdate) {
                        var updater = Path.Combine(Environment.CurrentDirectory, "Updater.exe");
                        Process.Start(updater);
                        Environment.Exit(0);
                    }
                };
                bw.RunWorkerAsync();
            } catch (Exception e) {
                Console.WriteLine(e);
            }
        }

        private void SetLang() {
            var name = _staticData.GetLanguage();
            if (!string.IsNullOrWhiteSpace(name)) {
                var culture = new CultureInfo(name);
                Thread.CurrentThread.CurrentUICulture = culture;
                Thread.CurrentThread.CurrentCulture = culture;

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
                } else if (string.Equals(currentCulture.Name, "zh-CN", StringComparison.OrdinalIgnoreCase) || string.Equals(currentCulture.Name, "zh-SG", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(currentCulture.Name, "zh-Hans", StringComparison.OrdinalIgnoreCase)) {
                    menuLangSC.Visible = false;
                } else if (string.Equals(currentCulture.Name, "zh-TW", StringComparison.OrdinalIgnoreCase) || string.Equals(currentCulture.Name, "zh-HK", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(currentCulture.Name, "zh-MO", StringComparison.OrdinalIgnoreCase) || string.Equals(currentCulture.Name, "zh-Hant", StringComparison.OrdinalIgnoreCase)) {
                    menuLangTC.Visible = false;
                }
            }
        }

        private void SetTheme() {
            _isDarkTheme = _staticData.IsDarkTheme();
            if (_isDarkTheme) {
                _ = new DarkModeCS(this, false);
            }
            menuTheme.Image = _isDarkTheme ? Resources.sun : Resources.new_moon;
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
                        DialogResult resultRestore = MessageBox(Strings.Confirm_Restore_Database, Strings.Confirm, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                        if (resultRestore == DialogResult.Yes) {
                            File.Copy(filePath, _filePath, true);
                            RefreshData();
                            return;
                        }
                        DialogResult resultDeleteBackup = MessageBox(Strings.Confirm_Delete_Backup, Strings.Confirm, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                        if (resultDeleteBackup == DialogResult.Yes) {
                            File.Delete(filePath);
                        }
                    }
                }
            }

            RefreshData();

            treeViewBooks.Focus();

            CmbSearch_SelectedIndexChanged(this, e);
        }

        private DialogResult MessageBox(string message, string title, MessageBoxButtons buttons, MessageBoxIcon icon) {
            return Messenger.MessageBox(message, title, buttons, icon, _isDarkTheme);
        }

        private DialogResult MessageBox(string message, string title, MessageBoxButtons buttons, MsgIcon icon) {
            return Messenger.MessageBox(message, title, buttons, icon, _isDarkTheme);
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
                MessageBox(Strings.Kindle_Vocab_Not_Exist, Strings.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
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

                    var title = string.Empty;
                    var authors = string.Empty;
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

            return Strings.Parsed_X + Strings.Space + lookupsTable.Rows.Count + Strings.Space + Strings.X_Vocabs + Strings.Space + Strings.Symbol_Comma + Strings.Imported_X + Strings.Space + lookupsInsertedCount + Strings.Space +
                   Strings.X_Lookups + Strings.Space + Strings.Symbol_Comma + wordsInsertedCount + Strings.Space + Strings.X_Vocabs;
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

        private void RefreshData(bool isReQuery = true) {
            try {
                lblBook.Text = string.Empty;
                lblAuthor.Text = string.Empty;
                lblLocation.Text = string.Empty;
                lblContent.Text = string.Empty;
                if (isReQuery) {
                    DisplayData();
                }
                SetDataGridView();
                SetSelection();
                CountRows();
            } catch (Exception) {
                // ignored
            }
        }

        private void DisplayData() {
            if (string.IsNullOrWhiteSpace(_searchText)) {
                _clippingsDataTable = _staticData.GetClipingsDataTable();
                _originClippingsDataTable = _staticData.GetOriginClippingsDataTable();
                _vocabDataTable = _staticData.GetVocabDataTable();
                _lookupsDataTable = _staticData.GetLookupsDataTable();
            } else {
                var type = cmbSearch.SelectedItem?.ToString() ?? string.Empty;
                _clippingsDataTable = _staticData.GetClipingsDataTableFuzzySearch(_searchText, type);
                _originClippingsDataTable = _staticData.GetOriginClippingsDataTableFuzzySearch(_searchText, type);
                _vocabDataTable = _staticData.GetVocabDataTableFuzzySearch(_searchText, type);
                _lookupsDataTable = _staticData.GetLookupsDataTableFuzzySearch(_searchText, type);
            }

            _lookupsDataTable.Columns.Add("word", typeof(string));
            _lookupsDataTable.Columns.Add("stem", typeof(string));
            _lookupsDataTable.Columns.Add("frequency", typeof(string));

            foreach (DataRow row in _lookupsDataTable.Rows) {
                var word_key = row["word_key"].ToString() ?? string.Empty;
                var word = string.Empty;
                var stem = string.Empty;
                var frequency = string.Empty;
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

            var books = _clippingsDataTable.AsEnumerable().Select(row => new {
                BookName = row.Field<string>("bookname")
            }).Distinct().OrderBy(book => book.BookName);

            var rootNodeBooks = new TreeNode(Strings.Select_All) {
                ImageIndex = 2,
                SelectedImageIndex = 2
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

            var words = _vocabDataTable.AsEnumerable().Select(row => new {
                Word = row.Field<string>("word")
            }).Distinct().OrderBy(word => word.Word);

            var rootNodeWords = new TreeNode(Strings.Select_All) {
                ImageIndex = 2,
                SelectedImageIndex = 2
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

                    if (string.IsNullOrWhiteSpace(_selectedBook) || _selectedBook.Equals(Strings.Select_All)) {
                        _selectedBook = Strings.Select_All;

                        dataGridView.DataSource = _clippingsDataTable;

                        dataGridView.Columns["content"]!.HeaderText = Strings.Content;
                        dataGridView.Columns["bookname"]!.HeaderText = Strings.Books;
                        dataGridView.Columns["authorname"]!.HeaderText = Strings.Author;
                        dataGridView.Columns["clippingdate"]!.HeaderText = Strings.Time;
                        dataGridView.Columns["pagenumber"]!.HeaderText = Strings.Page;
                        dataGridView.Columns["clippingdate"]!.HeaderText = Strings.Time;

                        dataGridView.Columns["key"]!.Visible = false;
                        dataGridView.Columns["content"]!.Visible = true;
                        dataGridView.Columns["bookname"]!.Visible = true;
                        dataGridView.Columns["authorname"]!.Visible = true;
                        dataGridView.Columns["brieftype"]!.Visible = false;
                        dataGridView.Columns["clippingtypelocation"]!.Visible = false;
                        dataGridView.Columns["clippingdate"]!.Visible = true;
                        dataGridView.Columns["read"]!.Visible = false;
                        dataGridView.Columns["clipping_importdate"]!.Visible = false;
                        dataGridView.Columns["tag"]!.Visible = false;
                        dataGridView.Columns["sync"]!.Visible = false;
                        dataGridView.Columns["newbookname"]!.Visible = false;
                        dataGridView.Columns["colorRGB"]!.Visible = false;
                        dataGridView.Columns["pagenumber"]!.Visible = true;

                        dataGridView.Columns["bookname"]!.Width = 100;
                        dataGridView.Columns["authorname"]!.Width = 50;
                        dataGridView.Columns["clippingdate"]!.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
                        dataGridView.Columns["pagenumber"]!.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
                        dataGridView.Columns["content"]!.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                        dataGridView.Sort(dataGridView.Columns["clippingdate"]!, ListSortDirection.Descending);
                    } else {
                        DataTable filteredBooks = _clippingsDataTable.AsEnumerable().Where(row => row.Field<string>("bookname") == _selectedBook).CopyToDataTable();
                        lblBookCount.Text = Strings.Total_Clippings + Strings.Space + filteredBooks.Rows.Count + Strings.Space + Strings.X_Clippings;
                        lblBookCount.Image = Resources.open_book;
                        lblBookCount.Visible = true;

                        dataGridView.DataSource = filteredBooks;

                        dataGridView.Columns["content"]!.HeaderText = Strings.Content;
                        dataGridView.Columns["bookname"]!.HeaderText = Strings.Books;
                        dataGridView.Columns["authorname"]!.HeaderText = Strings.Author;
                        dataGridView.Columns["clippingdate"]!.HeaderText = Strings.Time;
                        dataGridView.Columns["pagenumber"]!.HeaderText = Strings.Page;
                        dataGridView.Columns["clippingdate"]!.HeaderText = Strings.Time;

                        dataGridView.Columns["key"]!.Visible = false;
                        dataGridView.Columns["content"]!.Visible = true;
                        dataGridView.Columns["bookname"]!.Visible = false;
                        dataGridView.Columns["authorname"]!.Visible = false;
                        dataGridView.Columns["brieftype"]!.Visible = false;
                        dataGridView.Columns["clippingtypelocation"]!.Visible = false;
                        dataGridView.Columns["clippingdate"]!.Visible = true;
                        dataGridView.Columns["read"]!.Visible = false;
                        dataGridView.Columns["clipping_importdate"]!.Visible = false;
                        dataGridView.Columns["tag"]!.Visible = false;
                        dataGridView.Columns["sync"]!.Visible = false;
                        dataGridView.Columns["newbookname"]!.Visible = false;
                        dataGridView.Columns["colorRGB"]!.Visible = false;
                        dataGridView.Columns["pagenumber"]!.Visible = true;

                        dataGridView.Columns["clippingdate"]!.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
                        dataGridView.Columns["pagenumber"]!.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
                        dataGridView.Columns["content"]!.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

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

                    if (string.IsNullOrWhiteSpace(_selectedWord) || _selectedWord.Equals(Strings.Select_All)) {
                        _selectedWord = Strings.Select_All;

                        dataGridView.Columns["word"]!.HeaderText = Strings.Vocabulary;
                        dataGridView.Columns["stem"]!.HeaderText = Strings.Stem;
                        dataGridView.Columns["frequency"]!.HeaderText = Strings.Frequency;
                        dataGridView.Columns["usage"]!.HeaderText = Strings.Content;
                        dataGridView.Columns["title"]!.HeaderText = Strings.Books;
                        dataGridView.Columns["authors"]!.HeaderText = Strings.Author;
                        dataGridView.Columns["timestamp"]!.HeaderText = Strings.Time;

                        dataGridView.Columns["word"]!.Visible = true;
                        dataGridView.Columns["stem"]!.Visible = true;
                        dataGridView.Columns["frequency"]!.Visible = true;
                        dataGridView.Columns["word_key"]!.Visible = false;
                        dataGridView.Columns["usage"]!.Visible = true;
                        dataGridView.Columns["title"]!.Visible = false;
                        dataGridView.Columns["authors"]!.Visible = false;
                        dataGridView.Columns["timestamp"]!.Visible = true;

                        dataGridView.Columns["word"]!.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
                        dataGridView.Columns["stem"]!.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
                        dataGridView.Columns["frequency"]!.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
                        dataGridView.Columns["usage"]!.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                        dataGridView.Columns["timestamp"]!.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
                    } else {
                        DataTable filteredWords = _lookupsDataTable.AsEnumerable().Where(row => row.Field<string>("word_key")?[3..] == _selectedWord).CopyToDataTable();
                        lblBookCount.Text = Strings.Totally_Vocabs + Strings.Space + filteredWords.Rows.Count + Strings.Space + Strings.X_Lookups;
                        lblBookCount.Image = Resources.input_latin_uppercase;
                        lblBookCount.Visible = true;

                        dataGridView.DataSource = filteredWords;

                        dataGridView.Columns["word"]!.HeaderText = Strings.Vocabulary;
                        dataGridView.Columns["stem"]!.HeaderText = Strings.Stem;
                        dataGridView.Columns["frequency"]!.HeaderText = Strings.Frequency;
                        dataGridView.Columns["usage"]!.HeaderText = Strings.Content;
                        dataGridView.Columns["title"]!.HeaderText = Strings.Books;
                        dataGridView.Columns["authors"]!.HeaderText = Strings.Author;
                        dataGridView.Columns["timestamp"]!.HeaderText = Strings.Time;

                        dataGridView.Columns["word"]!.Visible = false;
                        dataGridView.Columns["stem"]!.Visible = true;
                        dataGridView.Columns["frequency"]!.Visible = false;
                        dataGridView.Columns["word_key"]!.Visible = false;
                        dataGridView.Columns["usage"]!.Visible = true;
                        dataGridView.Columns["title"]!.Visible = false;
                        dataGridView.Columns["authors"]!.Visible = false;
                        dataGridView.Columns["timestamp"]!.Visible = true;

                        dataGridView.Columns["word"]!.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                        dataGridView.Columns["stem"]!.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                        dataGridView.Columns["frequency"]!.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
                        dataGridView.Columns["usage"]!.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                        dataGridView.Columns["timestamp"]!.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
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
                    lblCount.Text = Strings.Space + Strings.Totally + Strings.Space + booksCount + Strings.Space + Strings.X_Books + Strings.Symbol_Comma + clippingsCount + Strings.Space + Strings.X_Clippings + Strings.Symbol_Comma +
                                    Strings.Deleted_X + Strings.Space + diff + Strings.Space + Strings.X_Rows;
                    break;
                case 1:
                    var vocabCount = _vocabDataTable.Rows.Count;
                    var lookupsCount = _lookupsDataTable.Rows.Count;
                    lblCount.Text = Strings.Space + Strings.Totally + Strings.Space + vocabCount + Strings.Space + Strings.X_Vocabs + Strings.Symbol_Comma + Strings.Quried_X + Strings.Space + lookupsCount + Strings.Space + Strings.X_Times;
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

                    var entityClipping = new Clipping {
                        key = row["key"].ToString() ?? string.Empty,
                        content = row["content"].ToString() ?? string.Empty,
                        bookname = row["bookname"].ToString() ?? string.Empty,
                        authorname = row["authorname"].ToString() ?? string.Empty,
                        briefType = (BriefType)brieftype,
                        clippingtypelocation = row["clippingtypelocation"].ToString() ?? string.Empty,
                        read = read,
                        clipping_importdate = row["clipping_importdate"].ToString() ?? string.Empty,
                        tag = row["tag"].ToString() ?? string.Empty,
                        sync = sync,
                        newbookname = row["newbookname"].ToString() ?? string.Empty,
                        colorRGB = colorRgb,
                        pagenumber = pagenumber
                    };

                    if (_staticData.InsertClippings(entityClipping)) {
                        insertedCount += 1;
                    }
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

                wordsInsertedCount = (from DataRow row in vocabDataTable.Rows
                                      where !_staticData.IsExistVocab(row["word_key"].ToString() ?? string.Empty)
                                      select _staticData.InsertVocab(row["id"].ToString() ?? string.Empty, row["word_key"].ToString() ?? string.Empty, row["word"].ToString() ?? string.Empty, row["stem"].ToString() ?? string.Empty,
                                          int.Parse(row["category"].ToString() ?? string.Empty), row["timestamp"].ToString() ?? string.Empty, int.Parse(row["frequency"].ToString() ?? string.Empty))).Sum();

                _staticData.CommitTransaction();
            } catch (Exception) {
                _staticData.RollbackTransaction();
            }

            UpdateFrequency();

            var rowsCount = clippingsDataTable.Rows.Count + lookupsDataTable.Rows.Count;

            return Strings.Parsed_X + Strings.Space + rowsCount + Strings.Space + Strings.X_Records + Strings.Symbol_Comma + Strings.Imported_X + Strings.Space + insertedCount + Strings.Space + Strings.X_Clippings + Strings.Symbol_Comma +
                   wordsInsertedCount + Strings.Space + Strings.X_Vocabs;
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
                lines[i] = RemoveControlChar(lines[i]);
                if (lines[i].StartsWith("===") && lines[i - 2].Trim().Equals("") && lines[i].EndsWith("===")) {
                    delimiterIndex.Add(i);
                }
            }

            var insertedCount = 0;

            _staticData.BeginTransaction();

            try {
                for (var i = 0; i < delimiterIndex.Count; i++) {
                    var entityClipping = new Clipping();

                    var ceilDelimiter = i == 0 ? -1 : delimiterIndex[i - 1];
                    var florDelimiter = delimiterIndex[i];

                    var line1 = lines[ceilDelimiter + 1].Trim();
                    var line2 = lines[ceilDelimiter + 2].Trim();
                    var line3 = lines[ceilDelimiter + 3].Trim(); // line3 should be empty
                    var line4 = lines[ceilDelimiter + 4].Trim();
                    if (ceilDelimiter + 5 == ceilDelimiter) {
                        // line4 is the rest
                        for (var index = ceilDelimiter + 3; index < florDelimiter; index++) {
                            line4 += lines[index];
                            if (index < florDelimiter - 1) {
                                line4 += "\n";
                            }
                        }
                    }

                    var content = line4;
                    entityClipping.content = content;

                    var line5 = lines[florDelimiter].Trim(); // line 5 is "=========="

                    var brieftype = BriefType.Clipping;
                    if (line2.Contains("笔记") || line2.Contains("Note")) {
                        brieftype = BriefType.Note;
                    } else if (line2.Contains("书签") || line2.Contains("Bookmark")) {
                        //brieftype = BriefType.Bookmark;
                        continue;
                    } else if (line2.Contains("文章剪切") || line2.Contains("Cut")) {
                        brieftype = BriefType.Cut;
                    }
                    entityClipping.briefType = brieftype;

                    if (line4.Contains("您已达到本内容的剪贴上限")) {
                        continue;
                    }

                    var split_b = line2.Split('|');

                    var clippingtypelocation = string.Empty;
                    if (split_b.Length > 1) {
                        clippingtypelocation = split_b[0][1..].Trim();
                    }
                    var pagenumber = -1;
                    var pagenPattern = @"\d+(-\d+)?";
                    var isPagenIsMatch = Regex.IsMatch(clippingtypelocation, pagenPattern);
                    var romanPattern = @"^(M{0,3})(CM|CD|D?C{0,3})(XC|XL|L?X{0,3})(IX|IV|V?I{0,3})$";
                    var isRomanMatched = Regex.IsMatch(clippingtypelocation, romanPattern);
                    var isPageParsed = false;
                    if (isPagenIsMatch) {
                        var regex = new Regex(pagenPattern);
                        var strMatched = regex.Matches(clippingtypelocation)[0].Value;
                        var split = strMatched.Split("-");
                        if (split.Length > 1) {
                            strMatched = strMatched.Split("-")[1];
                        }
                        strMatched = strMatched.Replace("#", "");
                        strMatched = strMatched.Split("）")[0];
                        isPageParsed = int.TryParse(strMatched, out pagenumber);
                    } else if (isRomanMatched) {
                        var strMatched = StaticData.RomanToInteger(clippingtypelocation).ToString();
                        isPageParsed = int.TryParse(strMatched, out pagenumber);
                    }
                    if (isPageParsed == false || pagenumber == 0) {
                        continue;
                    }
                    entityClipping.clippingtypelocation = clippingtypelocation;
                    entityClipping.pagenumber = pagenumber;

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
                    entityClipping.clippingdate = clippingdate;

                    var key = clippingdate + "|" + clippingtypelocation;
                    if (_staticData.IsExistOriginalClippings(key)) {
                        continue;
                    }
                    var isOriginClippingsInserted = _staticData.InsertOriginClippings(key, line1, line2, line3, line4, line5);
                    if (!isOriginClippingsInserted) {
                        continue;
                    }

                    entityClipping.key = key;

                    string bookname;
                    string authorname;
                    var pattern = @"\(([^()]+)\)[^(]*$";
                    Match match = Regex.Match(line1, pattern);
                    if (match.Success) {
                        authorname = match.Groups[1].Value.Trim();
                        bookname = line1.Replace(match.Groups[0].Value.Trim(), "").Trim();
                    } else {
                        authorname = string.Empty;
                        bookname = line1;
                    }
                    bookname = bookname.Trim();
                    entityClipping.bookname = bookname;
                    entityClipping.authorname = authorname;

                    if (brieftype == BriefType.Note) {
                        _ = _staticData.SetClippingsBriefTypeHide(bookname, pagenumber.ToString());
                    }

                    if (_staticData.IsExistClippings(key) || _staticData.IsExistClippingsOfContent(line4)) {
                        continue;
                    }

                    var insertResult = _staticData.InsertClippings(entityClipping);
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

                if (dataGridView.SelectedRows.Count > 0) {
                    selectedRow = dataGridView.SelectedRows[0];
                    _selectedDataGridIndex = dataGridView.SelectedRows[0].Index;
                } else {
                    return;
                }

                var selectedIndex = tabControl.SelectedIndex;

                lblBook.Text = string.Empty;
                lblAuthor.Text = string.Empty;
                lblLocation.Text = string.Empty;
                lblContent.Text = string.Empty;

                switch (selectedIndex) {
                    case 0:
                        //var clippingdate = selectedRow.Cells["clippingdate"].Value.ToString() ?? string.Empty;
                        var bookname = selectedRow.Cells["bookname"].Value.ToString() ?? string.Empty;
                        var authorname = selectedRow.Cells["authorname"].Value.ToString() ?? string.Empty;
                        var pagenumber = selectedRow.Cells["pagenumber"].Value.ToString() ?? string.Empty;
                        var content = selectedRow.Cells["content"].Value.ToString()?.Replace(" 　　", "\n") ?? string.Empty;
                        var brieftype = selectedRow.Cells["brieftype"].Value.ToString() ?? string.Empty;

                        lblBook.Text = bookname;
                        if (authorname != string.Empty) {
                            lblAuthor.Text = Strings.Left_Parenthesis + authorname + Strings.Right_Parenthesis;
                        } else {
                            lblAuthor.Text = string.Empty;
                        }

                        lblLocation.Text = Strings.Page_ + Strings.Space + pagenumber + Strings.Space + Strings.X_Page;

                        lblContent.Text = string.Empty;
                        lblContent.SelectionBullet = false;
                        lblContent.AppendText(content);
                        if (brieftype.Equals("1")) {
                            label1.Text = @"[" + Strings.Note + @"]";
                            label2.Text = @"[" + Strings.Clipping + @"]";
                            label3.Text = _staticData.GetClippingsBriefTypeHide(bookname, pagenumber);
                            label1.Visible = true;
                            label2.Visible = true;
                            label3.Visible = true;
                        } else {
                            label1.Visible = false;
                            label2.Visible = false;
                            label3.Visible = false;
                        }
                        break;
                    case 1:
                        var word_key = selectedRow.Cells["word_key"].Value.ToString() ?? string.Empty;
                        var word = selectedRow.Cells["word"].Value.ToString() ?? string.Empty;
                        var stem = selectedRow.Cells["stem"].Value.ToString() ?? string.Empty;
                        var frequency = selectedRow.Cells["frequency"].Value.ToString() ?? string.Empty;

                        if (string.IsNullOrWhiteSpace(word_key) || string.IsNullOrWhiteSpace(word) || string.IsNullOrWhiteSpace(stem) || string.IsNullOrWhiteSpace(frequency)) {
                            break;
                        }

                        var listUsage = new HashSet<string>();

                        var usage_list = new List<string>();
                        foreach (DataRow row in _lookupsDataTable.Rows) {
                            if (string.Equals(row["word_key"].ToString(), word_key, StringComparison.OrdinalIgnoreCase)) {
                                var str = row["word_key"].ToString() ?? string.Empty;
                                var strContent = row["usage"].ToString() ?? string.Empty;
                                if (!string.IsNullOrWhiteSpace(str) && !string.IsNullOrWhiteSpace(strContent)) {
                                    var title = " ——《" + row["title"] + "》";
                                    listUsage.Add(title);
                                    usage_list.Add(strContent + title);
                                }
                            }
                        }

                        var usage = usage_list.Aggregate("", (current, s) => current + (s + "\n").Replace(" 　　", "\n"));
                        var usage_clippings_list = new List<DataRow>();
                        if (word.Length > 1) {
                            usage_clippings_list = _clippingsDataTable.Rows.Cast<DataRow>().Where(row => (row["content"].ToString() ?? string.Empty).Contains(word)).ToList();
                        }
                        var usage_clippings = new List<string>();
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
                                usage_clippings.Add(strContent.Replace(" 　　", "\n") + " ——《" + row["bookname"] + "》" + "\n");
                                listUsage.Add(" ——《" + row["bookname"] + "》");
                            }
                        }

                        lblBook.Text = word;
                        if (stem != string.Empty && stem != word) {
                            lblAuthor.Text = Strings.Left_Parenthesis + Strings.Stem + Strings.Symbol_Colon + stem + Strings.Space + Strings.Right_Parenthesis;
                        } else {
                            lblAuthor.Text = string.Empty;
                        }

                        lblLocation.Text = Strings.Frequency + Strings.Symbol_Colon + frequency + Strings.Space + Strings.X_Times;

                        lblContent.SelectionBullet = true;
                        var usageLines = usage.Split(['\n'], StringSplitOptions.RemoveEmptyEntries);
                        foreach (var alines in usageLines.Select(line => line.Split("\n"))) {
                            for (var i = 0; i < alines.Length; i++) {
                                lblContent.SelectionBullet = i == 0;
                                var aline = alines[i];
                                lblContent.AppendText(aline.Trim() + "\n");
                            }
                            lblContent.SelectionBullet = false;
                        }

                        lblContent.SelectionBullet = false;
                        lblContent.AppendText("\n\n");

                        foreach (var alines in usage_clippings.Select(line => line.Split("\n"))) {
                            for (var i = 0; i < alines.Length; i++) {
                                lblContent.SelectionBullet = i == 0;
                                var aline = alines[i];
                                lblContent.AppendText(aline.Trim() + "\n");
                            }
                            lblContent.SelectionBullet = false;
                        }
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
                            lblContent.SelectionFont = new Font(lblContent.Font, FontStyle.Bold | FontStyle.Underline);

                            index = wordStartIndex + word.Length;
                        }

                        index = 0;

                        foreach (var book in listUsage) {
                            while (index < lblContent.TextLength) {
                                var wordStartIndex = lblContent.Find(book, index, RichTextBoxFinds.None);
                                if (wordStartIndex == -1) {
                                    break;
                                }

                                lblContent.Select(wordStartIndex, book.Length);
                                lblContent.SelectionFont = new Font(lblContent.Font, FontStyle.Italic);

                                index = wordStartIndex + book.Length;
                            }
                        }

                        lblBookCount.Text = Strings.Totally_Vocabs + Strings.Space + usage_list.Count + Strings.Space + Strings.X_Lookups;
                        if (usage_clippings_list.Count > 0) {
                            lblBookCount.Text += Strings.Symbol_Comma + Strings.Totally_Other_Books + Strings.Space + usage_clippings_list.Count + Strings.Space + Strings.X_Other_Books;
                        }
                        lblBookCount.Image = Resources.input_latin_uppercase;
                        lblBookCount.Visible = true;

                        label1.Visible = false;
                        label2.Visible = false;
                        label3.Visible = false;

                        break;
                }
            } catch (Exception) {
                // ignored
            }
        }

        private void TreeViewBooks_Select() {
            splitContainerDetail.Panel1Collapsed = false;

            if (string.IsNullOrWhiteSpace(_selectedBook) || _selectedBook.Equals(Strings.Select_All)) {
                _selectedBook = Strings.Select_All;
                lblBookCount.Text = string.Empty;
                lblBookCount.Image = null;
                lblBookCount.Visible = false;
                dataGridView.DataSource = _clippingsDataTable;
                dataGridView.Columns["bookname"]!.Visible = true;
                dataGridView.Columns["authorname"]!.Visible = true;
                dataGridView.Sort(dataGridView.Columns["clippingdate"]!, ListSortDirection.Descending);
            } else {
                DataTable filteredBooks = _clippingsDataTable.AsEnumerable().Where(row => row.Field<string>("bookname") == _selectedBook).CopyToDataTable();
                lblBookCount.Text = Strings.Space + Strings.Total_Clippings + Strings.Space + filteredBooks.Rows.Count + Strings.Space + Strings.X_Clippings;
                lblBookCount.Image = Resources.open_book;
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

            treeViewBooks.SelectedNode = currentNode;

            if (currentNode.Text.Equals(Strings.Select_All)) {
                return;
            }

            currentNode.ContextMenuStrip = menuBooks;
            treeViewBooks.SelectedNode = currentNode;
        }

        private void LblContent_MouseDoubleClick(object sender, MouseEventArgs e) {
            ShowContentEditDialog();
        }

        private void LblContent_LostFocus(object? sender, EventArgs e) {
            HideCaret(lblContent.Handle);
            DestroyCaret();
        }

        private void LblContent_GotFocus(object? sender, EventArgs e) {
            HideCaret(lblContent.Handle);
            DestroyCaret();
        }

        private void ShowContentEditDialog() {
            if (tabControl.SelectedIndex == 0) {
                var bookName = lblBook.Text;
                var content = lblContent.Text;

                var fields = new List<KeyValue> {
                    new(Strings.Content, content, KeyValue.ValueTypes.Multiline)
                };

                Messenger.ValidateControls += [SuppressMessage("ReSharper", "AccessToModifiedClosure")] (_, e) => {
                    if (fields != null) {
                        var fValue = fields[0].Value;
                        if (string.IsNullOrWhiteSpace(fValue)) {
                            e.Cancel = true;
                        }
                    }
                };

                if (dataGridView.SelectedRows.Count <= 0) {
                    return;
                }

                var key = dataGridView.SelectedRows[0].Cells["key"].Value.ToString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(key)) {
                    return;
                }

                if (Messenger.InputBox(Strings.Edit_Clippings + Strings.Space + bookName, "", ref fields, MsgIcon.Edit, MessageBoxButtons.OKCancel, _isDarkTheme) != DialogResult.OK) {
                    return;
                }
                var dialogContent = fields[0].Value.Trim();
                if (string.IsNullOrWhiteSpace(dialogContent)) {
                    return;
                }
                if (dialogContent.Equals(content)) {
                    return;
                }
                if (!_staticData.UpdateClippings(key, dialogContent, string.Empty)) {
                    MessageBox(Strings.Clippings_Revised_Failed, Strings.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                MessageBox(Strings.Clippings_Revised, Strings.Successful, MessageBoxButtons.OK, MessageBoxIcon.Information);
                RefreshData();
            }
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
                        lblBookCount.Image = Resources.open_book;
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
                        lblBookCount.Image = Resources.open_book;
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

        private void MenuClippingDelete_Click(object sender, EventArgs e) {
            if (dataGridView.SelectedRows.Count <= 0) {
                return;
            }

            var index = tabControl.SelectedIndex;
            switch (index) {
                case 0:
                    DialogResult resultClippings = MessageBox(Strings.Confirm_Delete_Selected_Clippings, Strings.Confirm, MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                    if (resultClippings != DialogResult.Yes) {
                        return;
                    }

                    _staticData.BeginTransaction();

                    try {
                        foreach (DataGridViewRow row in dataGridView.SelectedRows) {
                            if (_staticData.DeleteClippingsByKey(row.Cells["key"].Value.ToString() ?? string.Empty)) {
                                dataGridView.Rows.Remove(row);
                            } else {
                                MessageBox(Strings.Delete_Failed, Strings.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }
                        _staticData.CommitTransaction();
                    } catch (Exception) {
                        _staticData.RollbackTransaction();
                    }

                    break;
                case 1:
                    DialogResult resultWords = MessageBox(Strings.Confirm_Delete_Lookups, Strings.Confirm, MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                    if (resultWords != DialogResult.Yes) {
                        return;
                    }

                    _staticData.BeginTransaction();

                    try {
                        foreach (DataGridViewRow row in dataGridView.SelectedRows) {
                            if (_staticData.DeleteLookupsByTimeStamp(row.Cells["timestamp"].Value.ToString() ?? string.Empty)) {
                                dataGridView.Rows.Remove(row);
                            } else {
                                MessageBox(Strings.Delete_Failed, Strings.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }
                        _staticData.CommitTransaction();
                    } catch (Exception) {
                        _staticData.RollbackTransaction();
                    }

                    break;
            }

            if (_selectedDataGridIndex >= 1) {
                _selectedDataGridIndex -= 1;
            }

            RefreshData();
        }

        private void MenuClippingCopy_Click(object sender, EventArgs e) {
            if (dataGridView.SelectedRows.Count <= 0) {
                return;
            }

            var index = tabControl.SelectedIndex;
            switch (index) {
                case 0:
                    var content = dataGridView.SelectedRows[0].Cells["content"].Value.ToString() ?? string.Empty;
                    Clipboard.SetText(content != string.Empty ? content : lblContent.Text);
                    break;
                case 1:
                    var usage = dataGridView.SelectedRows[0].Cells["usage"].Value.ToString() ?? string.Empty;
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
                MenuClippingDelete_Click(sender, e);
            }
        }

        private void DataGridView_MouseDown(object sender, MouseEventArgs e) {
            DataGridView.HitTestInfo? hitTestInfo = dataGridView.HitTest(e.X, e.Y);
            if (hitTestInfo.RowIndex >= 0) {
                dataGridView.ClearSelection();
                dataGridView.Rows[hitTestInfo.RowIndex].Selected = true;
                _selectedDataGridIndex = hitTestInfo.RowIndex;
            }
        }

        private void MenuBooksDelete_Click(object sender, EventArgs e) {
            var index = tabControl.SelectedIndex;
            switch (index) {
                case 0:
                    if (treeViewBooks.SelectedNode is null || treeViewBooks.SelectedNode.Text.Equals(Strings.Select_All)) {
                        return;
                    }

                    DialogResult resultBooks = MessageBox(Strings.Confirm_Delete_Clippings_Book, Strings.Confirm, MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                    if (resultBooks != DialogResult.Yes) {
                        return;
                    }

                    var bookname = treeViewBooks.SelectedNode.Text;
                    if (!_staticData.DeleteClippingsByBook(bookname)) {
                        MessageBox(Strings.Delete_Failed, Strings.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
                        _selectedBook = string.Empty;
                    }

                    break;
                case 1:
                    if (treeViewWords.SelectedNode is null || treeViewWords.SelectedNode.Text.Equals(Strings.Select_All)) {
                        return;
                    }

                    DialogResult resultWords = MessageBox(Strings.Confirm_Delete_Lookups_Vocabs, Strings.Confirm, MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                    if (resultWords != DialogResult.Yes) {
                        return;
                    }

                    var word = treeViewWords.SelectedNode.Text;
                    var word_key = string.Empty;
                    foreach (DataRow row in _vocabDataTable.Rows) {
                        if (row["word"].ToString() != word) {
                            continue;
                        }

                        word_key = row["word_key"].ToString() ?? string.Empty;
                        break;
                    }

                    if (!_staticData.DeleteVocab(word_key) && !_staticData.DeleteLookupsByWordKey(word_key)) {
                        MessageBox(Strings.Delete_Failed, Strings.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }

                    _selectedWord = string.Empty;

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
            bw.DoWork += (_, workEventArgs) => { workEventArgs.Result = ImportKindleClippings(fileDialog.FileName); };
            bw.RunWorkerCompleted += (_, workerCompletedEventArgs) => {
                if (workerCompletedEventArgs.Result != null && !string.IsNullOrWhiteSpace(workerCompletedEventArgs.Result.ToString())) {
                    MessageBox(workerCompletedEventArgs.Result.ToString() ?? string.Empty, Strings.Successful, MessageBoxButtons.OK, MessageBoxIcon.Information);
                } else {
                    MessageBox(Strings.Import_Failed, Strings.Failed, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                SetProgressBar(false);
                RefreshData();
            };
            bw.RunWorkerAsync();
        }

        private void SetProgressBar(bool isShow) {
            progressBar.Enabled = isShow;
            progressBar.Visible = isShow;
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
            bw.DoWork += (_, workEventArgs) => { workEventArgs.Result = ImportKMDatabase(fileDialog.FileName); };
            bw.RunWorkerCompleted += (_, workerCompletedEventArgs) => {
                if (workerCompletedEventArgs.Result != null && !string.IsNullOrWhiteSpace(workerCompletedEventArgs.Result.ToString())) {
                    MessageBox(workerCompletedEventArgs.Result.ToString() ?? string.Empty, Strings.Successful, MessageBoxButtons.OK, MessageBoxIcon.Information);
                } else {
                    MessageBox(Strings.Import_Failed, Strings.Failed, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                SetProgressBar(false);
                RefreshData();
            };
            bw.RunWorkerAsync();
        }

        private void MenuRepo_Click(object sender, EventArgs e) {
            const string repoUrl = "https://github.com/lzcapp/KindleMate2";
            OpenUrl(repoUrl);
        }

        private void OpenUrl(string url) {
            try {
                Process.Start(new ProcessStartInfo {
                    FileName = url,
                    UseShellExecute = true
                });
            } catch (Exception) {
                Clipboard.SetText(url);
                MessageBox(Strings.Repo_URL_Copied, Strings.Prompt, MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            bw.DoWork += (_, e) => { e.Result = Import(kindleClippingsPath, kindleWordsPath); };
            bw.RunWorkerCompleted += (_, e) => {
                if (e.Result != null && !string.IsNullOrWhiteSpace(e.Result.ToString())) {
                    MessageBox(e.Result.ToString() ?? string.Empty, Strings.Successful, MessageBoxButtons.OK, MessageBoxIcon.Information);
                } else {
                    MessageBox(Strings.Import_Failed, Strings.Failed, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                SetProgressBar(false);
                RefreshData();
            };
            bw.RunWorkerAsync();
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
            var index = tabControl.SelectedIndex;
            if (index == 0) {
                if (treeViewBooks.SelectedNode != null && !treeViewBooks.SelectedNode.Text.Equals(Strings.Select_All)) {
                    var bookname = treeViewBooks.SelectedNode.Text;
                    var authorname = GetAuthornameFromClippings(bookname);
                    ShowBookRenameDialog(bookname, authorname);
                }
            }
        }

        private string GetAuthornameFromClippings(string bookname) {
            var authorname = string.Empty;
            foreach (DataRow row in _clippingsDataTable.Rows) {
                if ((row["bookname"].ToString() ?? string.Empty).Equals(bookname)) {
                    authorname = row["authorname"].ToString() ?? string.Empty;
                    break;
                }
            }
            return authorname;
        }

        private void ShowBookRenameDialog(string bookname, string authorname) {
            var fields = new List<KeyValue> {
                new(Strings.Book_Title, bookname),
                new(Strings.Author, authorname)
            };

            Messenger.ValidateControls += [SuppressMessage("ReSharper", "AccessToModifiedClosure")] (_, e) => {
                if (fields != null) {
                    var dialogBook = fields[0].Value;
                    var dialogAuthor = fields[1].Value;
                    if (string.IsNullOrWhiteSpace(dialogBook) || string.IsNullOrWhiteSpace(dialogAuthor)) {
                        e.Cancel = true;
                    }
                }
            };

            if (Messenger.InputBox(Strings.Rename, "", ref fields, MsgIcon.Edit, MessageBoxButtons.OKCancel, _isDarkTheme) == DialogResult.OK) {
                var dialogBook = fields[0].Value.Trim();
                var dialogAuthor = fields[1].Value.Trim();

                if (string.IsNullOrWhiteSpace(dialogBook)) {
                    return;
                }
                if (!string.IsNullOrWhiteSpace(authorname) && string.IsNullOrWhiteSpace(dialogAuthor)) {
                    dialogAuthor = authorname;
                }
                if (bookname == dialogBook && authorname == dialogAuthor) {
                    MessageBox(Strings.Books_Title_Not_Changed, Strings.Prompt, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                if (_clippingsDataTable.AsEnumerable().Any(row => row.Field<string>("BookName") == "dialogBook")) {
                    DialogResult result = MessageBox(Strings.Confirm_Same_Title_Combine, Strings.Confirm, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (result != DialogResult.Yes) {
                        return;
                    }

                    var resultRows = _clippingsDataTable.Select($"bookname = '{bookname}'");
                    dialogAuthor = (resultRows.Length > 0 ? resultRows[0]["authorname"].ToString() : string.Empty) ?? string.Empty;
                }

                _staticData.UpdateLookups(bookname, dialogBook, dialogAuthor);

                if (!_staticData.RenameBook(bookname, dialogBook, dialogAuthor)) {
                    MessageBox(Strings.Book_Renamed_Failed, Strings.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                MessageBox(Strings.Books_Renamed, Strings.Successful, MessageBoxButtons.OK, MessageBoxIcon.Information);

                _selectedBook = dialogBook;

                RefreshData();
            }
        }

        private string GetBooknameFromContent() {
            string bookname;
            if (!string.IsNullOrWhiteSpace(lblBook.Text)) {
                bookname = lblBook.Text;
            } else {
                bookname = dataGridView.Rows[0].Cells["bookname"].Value.ToString() ?? string.Empty;
            }
            return bookname;
        }

        private string GetAuthornameFromContent() {
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

        private void SetSelection() {
            if (dataGridView.Rows.Count <= 0) {
                return;
            }

            var index = tabControl.SelectedIndex;

            if (_selectedDataGridIndex < 0 || _selectedDataGridIndex >= dataGridView.Rows.Count) {
                _selectedDataGridIndex = 0;
            }

            switch (index) {
                case 0:
                    if (_selectedTreeIndex < 0 || _selectedTreeIndex >= treeViewBooks.Nodes.Count) {
                        _selectedTreeIndex = 0;
                    }

                    treeViewBooks.SelectedNode = treeViewBooks.Nodes[_selectedTreeIndex];

                    dataGridView.FirstDisplayedScrollingRowIndex = _selectedDataGridIndex;
                    dataGridView.Rows[_selectedDataGridIndex].Selected = true;

                    DataGridViewRow selectedRow = dataGridView.SelectedRows[0];

                    var bookname = selectedRow.Cells["bookname"].Value.ToString();
                    var authorname = selectedRow.Cells["authorname"].Value.ToString();
                    //var clippinglocation = selectedRow.Cells["clippingtypelocation"].Value.ToString();
                    //var content = selectedRow.Cells["content"].Value.ToString();

                    lblBook.Text = bookname;
                    if (authorname != string.Empty) {
                        lblAuthor.Text = Strings.Left_Parenthesis + authorname + Strings.Right_Parenthesis;
                    } else {
                        lblAuthor.Text = string.Empty;
                    }
                    break;
                case 1:
                    if (_selectedTreeIndex < 0 || _selectedTreeIndex >= treeViewWords.Nodes.Count) {
                        _selectedTreeIndex = 0;
                    }
                    treeViewWords.SelectedNode = treeViewWords.Nodes[_selectedTreeIndex];

                    dataGridView.FirstDisplayedScrollingRowIndex = _selectedDataGridIndex;
                    dataGridView.Rows[_selectedDataGridIndex].Selected = true;
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
                MessageBox(Strings.No_Data_To_Backup, Strings.Prompt, MessageBoxButtons.OK, MessageBoxIcon.Information);
            } else {
                if (!BackupOriginClippings()) {
                    MessageBox(Strings.Backup_Clippings_Failed, Strings.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
                } else {
                    DialogResult result = MessageBox(Strings.Backup_Successful + Strings.Open_Folder, Strings.Successful, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
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
                MessageBox(Strings.No_Data_To_Clear, Strings.Prompt, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            DialogResult result = MessageBox(Strings.Confirm_Clear_All_Data, Strings.Confirm, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result != DialogResult.Yes) {
                return;
            }

            if (_staticData.EmptyTables()) {
                MessageBox(Strings.Data_Cleared, Strings.Successful, MessageBoxButtons.OK, MessageBoxIcon.Information);
            } else {
                MessageBox(Strings.Clear_Failed, Strings.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            Restart();
        }

        private static void Restart() {
            Process.Start(new ProcessStartInfo {
                FileName = Application.ExecutablePath,
                UseShellExecute = true
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
            bw.DoWork += (_, workEventArgs) => { workEventArgs.Result = ImportKindleWords(fileDialog.FileName); };
            bw.RunWorkerCompleted += (_, workerCompletedEventArgs) => {
                if (workerCompletedEventArgs.Result != null && !string.IsNullOrWhiteSpace(workerCompletedEventArgs.Result.ToString())) {
                    MessageBox(workerCompletedEventArgs.Result.ToString() ?? string.Empty, Strings.Successful, MessageBoxButtons.OK, MessageBoxIcon.Information);
                } else {
                    MessageBox(Strings.Import_Failed, Strings.Failed, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                SetProgressBar(false);
                RefreshData();
            };
            bw.RunWorkerAsync();
        }

        private void TabControl_SelectedIndexChanged(object sender, EventArgs e) {
            var index = tabControl.SelectedIndex;
            menuRename.Visible = index switch {
                0 => true,
                1 => false,
                _ => menuRename.Visible,
            };
            lblCount.Text = string.Empty;
            lblBookCount.Text = string.Empty;
            RefreshData(false);
        }

        private void TreeViewWords_Select() {
            if (string.IsNullOrWhiteSpace(_selectedWord) || _selectedWord.Equals(Strings.Select_All)) {
                _selectedWord = Strings.Select_All;

                splitContainerDetail.Panel1Collapsed = false;

                lblBookCount.Text = string.Empty;
                lblBookCount.Image = null;
                lblBookCount.Visible = false;
                dataGridView.DataSource = _lookupsDataTable;

                dataGridView.Columns["word"]!.Visible = true;
                dataGridView.Columns["usage"]!.Visible = true;
                dataGridView.Columns["title"]!.Visible = true;
                dataGridView.Columns["authors"]!.Visible = true;
                dataGridView.Columns["frequency"]!.Visible = true;

                dataGridView.Columns["frequency"]!.HeaderText = Strings.Frequency;
            } else {
                splitContainerDetail.Panel1Collapsed = true;

                DataTable filteredWords = _lookupsDataTable.AsEnumerable().Where(row => row.Field<string>("word_key")?[3..] == _selectedWord).CopyToDataTable();
                lblBookCount.Text = Strings.Totally_Vocabs + Strings.Space + filteredWords.Rows.Count + Strings.Space + Strings.X_Lookups;
                lblBookCount.Image = Resources.input_latin_uppercase;
                lblBookCount.Visible = true;
                dataGridView.DataSource = filteredWords;

                dataGridView.Columns["word"]!.Visible = false;
                dataGridView.Columns["usage"]!.Visible = true;
                dataGridView.Columns["title"]!.Visible = true;
                dataGridView.Columns["authors"]!.Visible = true;

                dataGridView.Columns["frequency"]!.Visible = false;
            }
            dataGridView.Columns["usage"]!.HeaderText = Strings.Content;
            dataGridView.Columns["title"]!.HeaderText = Strings.Books;
            dataGridView.Columns["authors"]!.HeaderText = Strings.Author;
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

            treeViewWords.SelectedNode = currentNode;

            if (currentNode.Text.Equals(Strings.Select_All)) {
                return;
            }

            currentNode.ContextMenuStrip = menuBooks;
            treeViewWords.SelectedNode = currentNode;
        }

        private void TreeViewBooks_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e) {
            if (e.Node.Text.Equals(Strings.Select_All)) {
                return;
            }
            if (!e.Node.Text.Equals(Strings.Select_All)) {
                var bookname = e.Node.Text;
                var authorname = GetAuthornameFromClippings(bookname);
                ShowBookRenameDialog(bookname, authorname);
            }
        }

        private void Content_Rename_MouseDoubleClick() {
            if (tabControl.SelectedIndex == 0) {
                var bookname = GetBooknameFromContent();
                var authorname = GetAuthornameFromContent();
                ShowBookRenameDialog(bookname, authorname);
            }
        }

        private void LblBook_MouseDoubleClick(object sender, MouseEventArgs e) {
            Content_Rename_MouseDoubleClick();
        }

        private void LblAuthor_MouseDoubleClick(object sender, MouseEventArgs e) {
            Content_Rename_MouseDoubleClick();
        }

        private void FlowLayoutPanel_MouseDoubleClick(object sender, MouseEventArgs e) {
            Content_Rename_MouseDoubleClick();
        }

        private void TreeViewBooks_KeyDown(object sender, KeyEventArgs e) {
            // ReSharper disable once ConvertIfStatementToSwitchStatement
            if (e.KeyCode == Keys.Delete) {
                MenuBooksDelete_Click(sender, e);
            } else if (e.KeyCode == Keys.Enter) {
                MenuRename_Click(sender, e);
            }
        }

        private void MenuRestart_Click(object sender, EventArgs e) {
            Restart();
        }

        private void MenuRebuild_Click(object sender, EventArgs e) {
            DialogResult result = MessageBox(Strings.Confirm_Rebuild_Database, Strings.Confirm, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result != DialogResult.Yes) {
                return;
            }
            SetProgressBar(true);
            var bw = new BackgroundWorker();
            bw.DoWork += (_, doWorkEventArgs) => {
                doWorkEventArgs.Result = RebuildDatabase();
            };
            bw.RunWorkerCompleted += (_, runWorkerCompletedEventArgs) => {
                if (runWorkerCompletedEventArgs.Result != null && !string.IsNullOrWhiteSpace(runWorkerCompletedEventArgs.Result.ToString())) {
                    MessageBox(runWorkerCompletedEventArgs.Result.ToString() ?? string.Empty, Strings.Rebuild_Database, MessageBoxButtons.OK, MessageBoxIcon.Information);
                } else {
                    MessageBox(Strings.Rebuild_Database + Strings.Failed, Strings.Rebuild_Database, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                SetProgressBar(false);
                RefreshData();
            };
            bw.RunWorkerAsync();

        }

        private string RebuildDatabase() {
            DataTable origin = _staticData.GetOriginClippingsDataTable();
            if (origin.Rows.Count <= 0) {
                return Strings.No_Data_To_Clear;
            }

            _staticData.EmptyTables("clippings");
            var insertedCount = 0;
            foreach (DataRow row in origin.Rows) {
                var entityClipping = new Clipping();

                var line1 = row["line1"].ToString() ?? string.Empty;
                var line2 = row["line2"].ToString() ?? string.Empty;
                //var line3 = row["line3"].ToString() ?? string.Empty;
                var line4 = row["line4"].ToString() ?? string.Empty;
                //var line5 = row["line5"].ToString() ?? string.Empty;

                entityClipping.content = line4;

                var brieftype = BriefType.Clipping;
                if (line2.Contains("笔记") || line2.Contains("Note")) {
                    brieftype = BriefType.Note;
                } else if (line2.Contains("书签") || line2.Contains("Bookmark")) {
                    //brieftype = BriefType.Bookmark;
                    continue;
                } else if (line2.Contains("文章剪切") || line2.Contains("Cut")) {
                    brieftype = BriefType.Cut;
                }
                entityClipping.briefType = brieftype;

                if (line4.Contains("您已达到本内容的剪贴上限")) {
                    continue;
                }

                var split_b = line2.Split('|');

                var clippingtypelocation = string.Empty;
                if (split_b.Length > 1) {
                    clippingtypelocation = split_b[0][1..].Trim();
                }
                var pagenumber = -1;
                var pagenPattern = @"#\d+(-\d+)?";
                var isPagenIsMatch = Regex.IsMatch(clippingtypelocation, pagenPattern);
                var romanPattern = @"^(M{0,3})(CM|CD|D?C{0,3})(XC|XL|L?X{0,3})(IX|IV|V?I{0,3})$";
                var isRomanMatched = Regex.IsMatch(clippingtypelocation, romanPattern);
                var isPageParsed = false;
                if (isPagenIsMatch) {
                    var regex = new Regex(pagenPattern);
                    var strMatched = regex.Matches(clippingtypelocation)[0].Value;
                    var split = strMatched.Split("-");
                    if (split.Length > 1) {
                        strMatched = strMatched.Split("-")[1];
                    }
                    strMatched = strMatched.Replace("#", "");
                    strMatched = strMatched.Split("）")[0];
                    isPageParsed = int.TryParse(strMatched, out pagenumber);
                } else if (isRomanMatched) {
                    var strMatched = StaticData.RomanToInteger(clippingtypelocation).ToString();
                    isPageParsed = int.TryParse(strMatched, out pagenumber);
                }
                if (isPageParsed == false || pagenumber == 0) {
                    continue;
                }
                entityClipping.clippingtypelocation = clippingtypelocation;
                entityClipping.pagenumber = pagenumber;

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
                entityClipping.clippingdate = clippingdate;

                var key = clippingdate + "|" + clippingtypelocation;

                entityClipping.key = key;
                string bookname;
                string authorname;
                var pattern = @"\(([^()]+)\)[^(]*$";
                Match match = Regex.Match(line1, pattern);
                if (match.Success) {
                    authorname = match.Groups[1].Value.Trim();
                    bookname = line1.Replace(match.Groups[0].Value.Trim(), "").Trim();
                } else {
                    authorname = string.Empty;
                    bookname = line1;
                }
                bookname = bookname.Trim();
                entityClipping.bookname = bookname;
                entityClipping.authorname = authorname;

                if (brieftype == BriefType.Note) {
                    _ = _staticData.SetClippingsBriefTypeHide(bookname, pagenumber.ToString());
                }

                if (_staticData.IsExistClippings(key) || _staticData.IsExistClippingsOfContent(line4)) {
                    continue;
                }

                var insertResult = _staticData.InsertClippings(entityClipping);
                if (insertResult) {
                    insertedCount += 1;
                }
            }
            _staticData.CommitTransaction();
            var clipping = Strings.Parsed_X + Strings.Space + origin.Rows.Count + Strings.Space + Strings.X_Clippings + Strings.Symbol_Comma + Strings.Imported_X + Strings.Space + insertedCount + Strings.Space + Strings.X_Clippings;
            return clipping;
        }

        private void MenuClean_Click(object sender, EventArgs e) {
            _clippingsDataTable = _staticData.GetClipingsDataTable();

            if (_clippingsDataTable.Rows.Count <= 0) {
                MessageBox(Strings.Database_No_Need_Clean, Strings.Prompt, MessageBoxButtons.OK, MessageBoxIcon.Information);
            } else {
                SetProgressBar(true);
                var bw = new BackgroundWorker();
                bw.DoWork += (_, doWorkEventArgs) => { doWorkEventArgs.Result = CleanDatabase(); };
                bw.RunWorkerCompleted += (_, runWorkerCompletedEventArgs) => {
                    if (runWorkerCompletedEventArgs.Result != null && !string.IsNullOrWhiteSpace(runWorkerCompletedEventArgs.Result.ToString())) {
                        MessageBox(runWorkerCompletedEventArgs.Result.ToString() ?? string.Empty, Strings.Clean_Database, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    } else {
                        MessageBox(Strings.Clear_Failed, Strings.Clean_Database, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    SetProgressBar(false);
                    RefreshData();
                };
                bw.RunWorkerAsync();
            }
        }

        private string CleanDatabase() {
            _clippingsDataTable = _staticData.GetClipingsDataTable();

            _staticData.BeginTransaction();

            try {
                var countEmpty = 0;
                var countTrimmed = 0;
                var countDuplicated = 0;

                foreach (DataRow row in _clippingsDataTable.Rows) {
                    var key = row["key"].ToString() ?? string.Empty;
                    var content = row["content"].ToString() ?? string.Empty;
                    var bookname = row["bookname"].ToString() ?? string.Empty;

                    var contentTrimmed = TrimContent(content);
                    var booknameTrimmed = TrimContent(bookname);

                    if (string.IsNullOrWhiteSpace(key)) {
                        continue;
                    }
                    if (string.IsNullOrWhiteSpace(content) || string.IsNullOrWhiteSpace(bookname) || string.IsNullOrWhiteSpace(contentTrimmed) || string.IsNullOrWhiteSpace(booknameTrimmed)) {
                        if (_staticData.DeleteClippingsByKey(key)) {
                            countEmpty++;
                        }
                        continue;
                    }

                    switch (contentTrimmed.Equals(content)) {
                        case false when !booknameTrimmed.Equals(bookname): {
                                if (_staticData.UpdateClippings(key, contentTrimmed, booknameTrimmed)) {
                                    countTrimmed++;
                                }
                                break;
                            }
                        case false: {
                                if (_staticData.UpdateClippings(key, contentTrimmed, string.Empty)) {
                                    countTrimmed++;
                                }
                                break;
                            }
                        default: {
                                if (!booknameTrimmed.Equals(bookname)) {
                                    if (_staticData.UpdateClippings(key, string.Empty, booknameTrimmed)) {
                                        countTrimmed++;
                                    }
                                }
                                break;
                            }
                    }

                    if (_staticData.IsExistClippingsContainingContent(content)) {
                        if (_staticData.DeleteClippingsByKey(key)) {
                            countDuplicated++;
                        }
                    }
                }

                var fileInfo = new FileInfo(_filePath);
                var originFileSize = fileInfo.Length;

                _staticData.CommitTransaction();

                _staticData.VacuumDatabase();

                var newFileSize = fileInfo.Length;

                var fileSizeDelta = originFileSize - newFileSize;

                if (countEmpty > 0 || countTrimmed > 0 || countDuplicated > 0 || fileSizeDelta > 0) {
                    return Strings.Cleaned + Strings.Space + Strings.Empty_Content + Strings.Space + countEmpty + Strings.Space + Strings.X_Rows + Strings.Symbol_Comma + Strings.Trimmed + Strings.Space + countTrimmed + Strings.Space +
                           Strings.X_Rows + Strings.Symbol_Comma + Strings.Duplicate_Content + Strings.Space + countDuplicated + Strings.Space + Strings.X_Rows + Strings.Symbol_Comma + Strings.Database_Cleaned + Strings.Space +
                           FormatFileSize(fileSizeDelta);
                }
                return Strings.Database_No_Need_Clean;
            } catch (Exception) {
                _staticData.RollbackTransaction();
                return string.Empty;
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

        private static string SanitizeFilename(string filename) {
            var invalidChars = Path.GetInvalidFileNameChars();
            filename = invalidChars.Aggregate(filename, (current, c) => current.Replace(c, '_'));
            filename = filename.Trim();
            return filename;
        }

        private bool ClippingsToMarkdown(string bookname = "") {
            try {
                var filename = "Clippings";

                var markdown = new StringBuilder();

                markdown.AppendLine("# \ud83d\udcda " + Strings.Books);

                markdown.AppendLine();

                if (string.IsNullOrWhiteSpace(bookname) || bookname.Equals(Strings.Select_All)) {
                    markdown.AppendLine("[TOC]");

                    markdown.AppendLine();

                    foreach (TreeNode node in treeViewBooks.Nodes) {
                        var nodeBookName = node.Text;

                        if (nodeBookName.Equals(Strings.Select_All)) {
                            continue;
                        }

                        DataTable filteredBooks = _clippingsDataTable.AsEnumerable().Where(row => row.Field<string>("bookname")!.Equals(nodeBookName)).CopyToDataTable();

                        if (filteredBooks.Rows.Count <= 0) {
                            return false;
                        }

                        markdown.AppendLine("## \ud83d\udcd6 " + nodeBookName.Trim());

                        markdown.AppendLine();

                        foreach (DataRow row in filteredBooks.Rows) {
                            var clippinglocation = row["clippingtypelocation"].ToString();
                            var content = row["content"].ToString();

                            markdown.AppendLine("**" + clippinglocation + "**");

                            markdown.AppendLine();

                            markdown.AppendLine(content);

                            markdown.AppendLine();
                        }
                    }
                } else {
                    filename = SanitizeFilename(bookname);

                    DataTable filteredBooks = _clippingsDataTable.AsEnumerable().Where(row => row.Field<string>("bookname")!.Equals(bookname)).CopyToDataTable();

                    if (filteredBooks.Rows.Count <= 0) {
                        return false;
                    }

                    markdown.AppendLine("## \ud83d\udcd6 " + bookname.Trim());

                    markdown.AppendLine();

                    foreach (DataRow row in filteredBooks.Rows) {
                        var clippinglocation = row["clippingtypelocation"].ToString();
                        var content = row["content"].ToString();

                        markdown.AppendLine("**" + clippinglocation + "**");

                        markdown.AppendLine();

                        markdown.AppendLine(content);

                        markdown.AppendLine();
                    }
                }

                File.WriteAllText(Path.Combine(_programsDirectory, "Exports", filename + ".md"), markdown.ToString(), Encoding.UTF8);

                var htmlContent = "<html><head>\r\n<link rel=\"stylesheet\" href=\"styles.css\">\r\n</head><body>\r\n";

                MarkdownPipeline pipeline = new MarkdownPipelineBuilder()
                                            .UseAdvancedExtensions()
                                            .UseTableOfContent()
                                            .Build();
                htmlContent += Markdown.ToHtml(markdown.ToString(), pipeline);

                htmlContent += "\r\n</body>\r\n</html>";

                File.WriteAllText(Path.Combine(_programsDirectory, "Exports", filename + ".html"), htmlContent, Encoding.UTF8);

                return true;
            } catch (Exception) {
                return false;
            }
        }

        private bool VocabsToMarkdown(string word = "") {
            try {
                var filename = "Vocabs";

                var markdown = new StringBuilder();

                markdown.AppendLine("# \ud83d\udcda " + Strings.Vocabulary_List);

                markdown.AppendLine();

                if (string.IsNullOrWhiteSpace(word) || word.Equals(Strings.Select_All)) {
                    markdown.AppendLine("[TOC]");

                    markdown.AppendLine();

                    foreach (TreeNode node in treeViewWords.Nodes) {
                        var nodeWordText = node.Text;

                        if (nodeWordText.Equals(Strings.Select_All)) {
                            continue;
                        }

                        DataTable filteredBooks = _lookupsDataTable.AsEnumerable().Where(row => row.Field<string>("word") == nodeWordText).CopyToDataTable();

                        if (filteredBooks.Rows.Count <= 0) {
                            return false;
                        }

                        markdown.AppendLine("## \ud83d\udd24 " + nodeWordText.Trim());

                        markdown.AppendLine();

                        foreach (DataRow row in filteredBooks.Rows) {
                            var title = row["title"].ToString();
                            var usage = row["usage"].ToString();

                            if (usage == null) {
                                continue;
                            }

                            markdown.AppendLine("**《" + title + "》**");

                            markdown.AppendLine();

                            markdown.AppendLine(usage.Replace(nodeWordText, " **`" + nodeWordText + "`** "));

                            markdown.AppendLine();
                        }
                    }
                } else {
                    filename = SanitizeFilename(word);

                    DataTable filteredBooks = _lookupsDataTable.AsEnumerable().Where(row => row.Field<string>("word")!.Equals(word)).CopyToDataTable();

                    if (filteredBooks.Rows.Count <= 0) {
                        return false;
                    }

                    markdown.AppendLine("## \ud83d\udd24 " + word.Trim());

                    markdown.AppendLine();

                    foreach (DataRow row in filteredBooks.Rows) {
                        var title = row["title"].ToString();
                        var usage = row["usage"].ToString();

                        if (usage == null) {
                            continue;
                        }

                        markdown.AppendLine("**《" + title + "》**");

                        markdown.AppendLine();

                        markdown.AppendLine(usage.Replace(word, " **`" + word + "`** "));

                        markdown.AppendLine();
                    }
                }

                File.WriteAllText(Path.Combine(_programsDirectory, "Exports", filename + ".md"), markdown.ToString(), Encoding.UTF8);

                var htmlContent = "<html><head>\r\n<link rel=\"stylesheet\" href=\"styles.css\">\r\n</head><body>\r\n";

                MarkdownPipeline pipeline = new MarkdownPipelineBuilder()
                                            .UseAdvancedExtensions()
                                            .UseTableOfContent()
                                            .Build();
                htmlContent += Markdown.ToHtml(markdown.ToString(), pipeline);

                htmlContent += "\r\n</body>\r\n</html>";

                File.WriteAllText(Path.Combine(_programsDirectory, "Exports", filename + ".html"), htmlContent, Encoding.UTF8);

                return true;
            } catch (Exception) {
                return false;
            }
        }

        private void MenuExportMd_Click(object sender, EventArgs e) {
            var path = Path.Combine(_programsDirectory, "Exports");
            if (!Directory.Exists(path)) {
                Directory.CreateDirectory(path);
            }

            const string css = "@import url(https://fonts.googleapis.com/css2?family=Noto+Color+Emoji&family=Noto+Emoji:wght@300..700&display=swap);*{font-family:-apple-system,\"Noto Sans\",\"Helvetica Neue\",Helvetica,\"Nimbus Sans L\",Arial,\"Liberation Sans\",\"PingFang SC\",\"Hiragino Sans GB\",\"Noto Sans CJK SC\",\"Source Han Sans SC\",\"Source Han Sans CN\",\"Microsoft YaHei UI\",\"Microsoft YaHei\",\"Wenquanyi Micro Hei\",\"WenQuanYi Zen Hei\",\"ST Heiti\",SimHei,\"WenQuanYi Zen Hei Sharp\",\"Noto Emoji\",sans-serif}body{font-family:Arial,sans-serif;background-color:#f9f9f9;color:#333;line-height:1.6;align-items:center;width:80vw;margin:20px auto}h1{font-size:30px;text-align:center;margin:30px auto;color:#333}h2{font-size:24px;margin:30px auto;color:#333}p{font-size:16px;margin:20px auto}code{background-color:#faebd7;border-radius:10px;padding:2px 6px}";

            File.WriteAllText(Path.Combine(_programsDirectory, "Exports", "styles.css"), css);

            if (!ClippingsToMarkdown() || !VocabsToMarkdown()) {
                return;
            }

            DialogResult result = MessageBox(Strings.Export_Successful + Strings.Open_Folder, Strings.Successful, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result != DialogResult.Yes) {
                return;
            }

            Process.Start("explorer.exe", Path.Combine(_programsDirectory, "Exports"));
        }

        private void MenuStatistic_Click(object sender, EventArgs e) {
            if (_clippingsDataTable.Rows.Count <= 0) {
                MessageBox(Strings.No_Data_To_Clear, Strings.Prompt, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
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
            _staticData.SetTheme(_isDarkTheme ? "light" : "dark");
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

        private void FrmMain_FormClosing(object sender, FormClosingEventArgs e) {
            try {
                _staticData.CommitTransaction();
            } catch {
                // ignored
            }
        }

        private void MenuContentCopy_Click(object sender, EventArgs e) {
            Clipboard.SetText(string.IsNullOrEmpty(lblContent.SelectedText) ? lblContent.Text : lblContent.SelectedText);
        }

        private void PicSearch_Click(object sender, EventArgs e) {
            var searchText = GetSearchText();
            if (!_searchText.Equals(searchText)) {
                _searchText = searchText;
                RefreshData();
            }
        }

        private string GetSearchText() {
            var strSearch = txtSearch.Text;
            if (!string.IsNullOrWhiteSpace(strSearch)) { } else {
                txtSearch.Text = string.Empty;
            }
            return txtSearch.Text;
        }

        private void CmbSearch_SelectedIndexChanged(object sender, EventArgs e) {
            var selected = cmbSearch.SelectedItem?.ToString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(selected)) {
                return;
            }
            List<string> list = [];
            if (selected.Equals(Strings.Book_Title)) {
                list.AddRange(_staticData.GetClippingsBookTitleList());
            } else if (selected.Equals(Strings.Author)) {
                list.AddRange(_staticData.GetClippingsAuthorList());
            } else if (selected.Equals(Strings.Vocabulary)) {
                list.AddRange(_staticData.GetVocabWordList());
            } else if (selected.Equals(Strings.Stem)) {
                list.AddRange(_staticData.GetVocabStemList());
            } else {
                list.AddRange(_staticData.GetClippingsBookTitleList());
                list.AddRange(_staticData.GetClippingsAuthorList());
                list.AddRange(_staticData.GetVocabWordList());
                list.AddRange(_staticData.GetVocabStemList());
            }
            var autoCompleteStringCollection = new AutoCompleteStringCollection();
            autoCompleteStringCollection.AddRange(list.ToArray());
            txtSearch.AutoCompleteSource = AutoCompleteSource.CustomSource;
            txtSearch.AutoCompleteCustomSource = autoCompleteStringCollection;
        }

        private void TxtSearch_KeyPress(object sender, KeyPressEventArgs e) {
            if (e.KeyChar != (char)Keys.Enter) {
                return;
            }
            e.Handled = true;
            PicSearch_Click(this, e);
        }

        private void TxtSearch_Leave(object sender, EventArgs e) {
            PicSearch_Click(this, e);
        }

        private void MenuBooksExport_Click(object sender, EventArgs e) {
            var index = tabControl.SelectedIndex;
            switch (index) {
                case 0:
                    if (treeViewBooks.SelectedNode is null || treeViewBooks.SelectedNode.Text.Equals(Strings.Select_All)) {
                        return;
                    }
                    if (!ClippingsToMarkdown(treeViewBooks.SelectedNode.Text.Trim())) {
                        return;
                    }
                    break;
                case 1:
                    if (treeViewWords.SelectedNode is null || treeViewWords.SelectedNode.Text.Equals(Strings.Select_All)) {
                        return;
                    }
                    if (!VocabsToMarkdown(treeViewWords.SelectedNode.Text.Trim())) {
                        return;
                    }
                    break;
            }
            DialogResult result = MessageBox(Strings.Export_Successful + Strings.Open_Folder, Strings.Successful, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result != DialogResult.Yes) {
                return;
            }
            Process.Start("explorer.exe", Path.Combine(_programsDirectory, "Exports"));
        }

        private void TreeViewBooks_AfterSelect(object sender, TreeViewEventArgs e) {
            _selectedBook = e.Node != null ? e.Node.Text : Strings.Select_All;
            _selectedTreeIndex = e.Node?.Index ?? 0;
            TreeViewBooks_Select();
        }

        private void TreeViewWords_AfterSelect(object sender, TreeViewEventArgs e) {
            _selectedWord = e.Node != null ? e.Node.Text : Strings.Select_All;
            _selectedTreeIndex = e.Node?.Index ?? 0;
            TreeViewWords_Select();
        }

        private void TreeViewWords_KeyDown(object sender, KeyEventArgs e) {
            if (e.KeyCode == Keys.Delete) {
                MenuBooksDelete_Click(sender, e);
            }
        }

        private static string RemoveControlChar(string input) {
            var output = new StringBuilder();
            foreach (var c in input.Where(c => !c.IsControl() && !c.IsNewLineOrLineFeed() && c != 65279)) {
                output.Append(c);
            }
            return output.ToString();
        }
    }
}