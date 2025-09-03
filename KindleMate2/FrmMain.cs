using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using DarkModeForms;
using KindleMate2.Application.Services.KM2DB;
using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Infrastructure.Helpers;
using KindleMate2.Infrastructure.Repositories.KM2DB;
using KindleMate2.Infrastructure.Repositories.VocabDB;
using KindleMate2.Properties;
using KindleMate2.Shared.Constants;
using KindleMate2.Shared.Entities;
using Markdig;
using MediaDevices;
using LookupRepository = KindleMate2.Infrastructure.Repositories.KM2DB.LookupRepository;
using VocabLookupRepository = KindleMate2.Infrastructure.Repositories.VocabDB.LookupRepository;

namespace KindleMate2 {
    public partial class FrmMain : Form {
        private readonly ClippingService _clippingService;
        private readonly LookupService _lookupService;
        private readonly OriginalClippingLineService _originalClippingLineService;
        private readonly SettingService _settingService;
        private readonly VocabService _vocabService;
        private readonly ThemeService _themeService;
        private readonly DatabaseService _databaseService;
        private readonly KM2DatabaseService _km2DatabaseService;

        private List<Clipping> _clippings = [];
        private List<OriginalClippingLine> _originClippings = [];
        private List<Vocab> _vocabs = [];
        private List<Lookup> _lookups = [];

        private Device.Type _deviceType = Device.Type.Unknown;

        private string _driveLetter = string.Empty;

        private readonly string _programPath, _tempPath, _backupPath, _databaseFilePath, _versionFilePath;

        private string _selectedBook, _selectedWord;

        private string _searchText;
        private AppEntities.SearchType _searchType;

        private int _selectedTreeIndex, _selectedDataGridIndex;

        private bool _isDarkTheme;

        [DllImport("User32.dll")]
        private static extern bool HideCaret(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool DestroyCaret();

        public FrmMain() {
            InitializeComponent();

            var clippingRepository = new ClippingRepository(AppConstants.ConnectionString);
            _clippingService = new ClippingService(clippingRepository);

            var lookupRepository = new LookupRepository(AppConstants.ConnectionString);
            _lookupService = new LookupService(lookupRepository);

            var originalClippingLineRepository = new OriginalClippingLineRepository(AppConstants.ConnectionString);
            _originalClippingLineService = new OriginalClippingLineService(originalClippingLineRepository);

            var settingRepository = new SettingRepository(AppConstants.ConnectionString);
            _settingService = new SettingService(settingRepository);
            _themeService = new ThemeService(settingRepository);

            var vocabRepository = new VocabRepository(AppConstants.ConnectionString);
            _vocabService = new VocabService(vocabRepository);

            var databaseRepository = new DatabaseRepository(AppConstants.ConnectionString);
            _databaseService = new DatabaseService(databaseRepository);

            _km2DatabaseService = new KM2DatabaseService(clippingRepository, lookupRepository, originalClippingLineRepository, settingRepository, vocabRepository);

            _programPath = Environment.CurrentDirectory;
            _databaseFilePath = Path.Combine(_programPath, AppConstants.DatabaseFileName);
            _versionFilePath = Path.Combine(AppConstants.SystemPathName, AppConstants.VersionFileName);
            _tempPath = Path.Combine(_programPath, AppConstants.TempPathName);
            _backupPath = Path.Combine(_programPath, AppConstants.BackupsPathName);

            _selectedBook = Strings.Select_All;
            _selectedWord = Strings.Select_All;
            _searchText = string.Empty;

            dataGridView.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;

            lblContent.GotFocus += LblContent_GotFocus;
            lblContent.LostFocus += LblContent_LostFocus;

            if (!File.Exists(_databaseFilePath)) {
                if (DatabaseHelper.CreateDatabase(_databaseFilePath, out Exception exception)) {
                    var message = MessageHelper.BuildMessage(Strings.Create_Database_Failed, exception);
                    _ = MessageBox(message, Strings.Error, MessageBoxButtons.OK, MsgIcon.Error);
                    Environment.Exit(0);
                }
            }

            AppDomain.CurrentDomain.ProcessExit += (_, _) => { DatabaseHelper.BackupDatabase(_programPath, _backupPath, AppConstants.DatabaseFileName); };

            SetTheme();

            SetLang();

            SetText();
        }

        private void SetTheme() {
            _isDarkTheme = _themeService.IsDarkTheme();
            try {
                if (_isDarkTheme) {
                    _ = new DarkModeCS(this, false);
                }
            } catch (Exception e) {
                Console.WriteLine(e);
            } finally {
                menuTheme.Image = _isDarkTheme ? Resources.sun : Resources.new_moon;
            }
        }

        private void SetLang() {
            Setting? cultureSetting = _settingService.GetSettingByName("lang");
            if (cultureSetting == null || string.IsNullOrWhiteSpace(cultureSetting.value)) {
                return;
            }
            var cultureName = cultureSetting.value;
            if (!string.IsNullOrWhiteSpace(cultureName)) {
                var culture = new CultureInfo(cultureName);
                Thread.CurrentThread.CurrentUICulture = culture;
                Thread.CurrentThread.CurrentCulture = culture;

                switch (cultureName.ToLowerInvariant()) {
                    case Culture.English:
                        menuLangEN.Visible = false;
                        break;
                    case Culture.ChineseSimplified:
                        menuLangSC.Visible = false;
                        break;
                    case Culture.ChineseTraditional:
                        menuLangTC.Visible = false;
                        break;
                }
            } else {
                menuLangAuto.Visible = false;
                CultureInfo currentCulture = CultureInfo.CurrentCulture;
                if (currentCulture.EnglishName.Contains("English", StringComparison.InvariantCultureIgnoreCase) || currentCulture.TwoLetterISOLanguageName.Equals(Culture.English, StringComparison.InvariantCultureIgnoreCase)) {
                    menuLangEN.Visible = false;
                } else if (string.Equals(currentCulture.Name, "zh-CN", StringComparison.InvariantCultureIgnoreCase) || string.Equals(currentCulture.Name, "zh-SG", StringComparison.InvariantCultureIgnoreCase) ||
                           string.Equals(currentCulture.Name, Culture.ChineseSimplified, StringComparison.InvariantCultureIgnoreCase)) {
                    menuLangSC.Visible = false;
                } else if (string.Equals(currentCulture.Name, "zh-TW", StringComparison.InvariantCultureIgnoreCase) || string.Equals(currentCulture.Name, "zh-HK", StringComparison.InvariantCultureIgnoreCase) ||
                           string.Equals(currentCulture.Name, "zh-MO", StringComparison.InvariantCultureIgnoreCase) || string.Equals(currentCulture.Name, Culture.ChineseTraditional, StringComparison.InvariantCultureIgnoreCase)) {
                    menuLangTC.Visible = false;
                }
            }
        }

        private void SetText() {
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
            menuSyncToKindle.Text = Strings.Sync_To_Kindle;
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
        }

        private void FrmMain_Load(object? sender, EventArgs e) {
            if (!File.Exists(_databaseFilePath)) {
                return;
            }

            if (_databaseService.IsDatabaseEmpty()) {
                if (Directory.Exists(_backupPath)) {
                    var backupFilePath = Path.Combine(_backupPath, AppConstants.DatabaseFileName);

                    if (File.Exists(backupFilePath)) {
                        var fileSize = new FileInfo(backupFilePath).Length / 1024;
                        if (fileSize >= 20) {
                            DialogResult resultRestore = MessageBox(Strings.Confirm_Restore_Database, Strings.Confirm, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                            if (resultRestore == DialogResult.Yes) {
                                File.Copy(backupFilePath, _databaseFilePath, true);
                            }
                            DialogResult resultDeleteBackup = MessageBox(Strings.Confirm_Delete_Backup, Strings.Confirm, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                            if (resultDeleteBackup == DialogResult.Yes) {
                                File.Delete(backupFilePath);
                            }
                        }
                    }
                }
            }

            _selectedBook = Strings.Select_All;
            _selectedWord = Strings.Select_All;
            _selectedTreeIndex = 0;
            _selectedDataGridIndex = 0;
            cmbSearch.SelectedIndex = 0;

            GetSearchText();

            RefreshData();

            treeViewBooks.Focus();

            //StaticData.CheckUpdate();

            AddDeviceWatcher();
        }

        private void AddDeviceWatcher() {
            IsKindleConnected();

            const string deviceChangeQuery = "SELECT * FROM Win32_DeviceChangeEvent WHERE EventType = 2 OR EventType = 3";
            using var watcher = new ManagementEventWatcher(deviceChangeQuery);
            watcher.EventArrived += UsbDeviceEventHandler;
            watcher.Start();

            const string mtpCreationQuery = "SELECT * FROM __InstanceCreationEvent WITHIN 2 WHERE TargetInstance ISA 'Win32_PnPEntity'";
            using var deviceArrivalWatcher = new ManagementEventWatcher(mtpCreationQuery);
            deviceArrivalWatcher.EventArrived += MtpDeviceEventHandler;
            deviceArrivalWatcher.Start();

            const string mtpDeletionQuery = "SELECT * FROM __InstanceDeletionEvent WITHIN 2 WHERE TargetInstance ISA 'Win32_PnPEntity'";
            using var deviceRemovalWatcher = new ManagementEventWatcher(mtpDeletionQuery);
            deviceRemovalWatcher.EventArrived += MtpDeviceEventHandler;
            deviceRemovalWatcher.Start();
        }

        private void UsbDeviceEventHandler(object sender, EventArrivedEventArgs e) {
            _deviceType = Device.Type.USB;
            DeviceEventHandler(sender);
        }

        private void MtpDeviceEventHandler(object sender, EventArrivedEventArgs e) {
            _deviceType = Device.Type.MTP;
            DeviceEventHandler(sender);
        }

        private void DeviceEventHandler(object sender) {
            if (sender is not ManagementEventWatcher watcher) {
                return;
            }
            watcher.Stop();
            IsKindleConnected();
            watcher.Start();
        }

        private string Import(string kindleClippingsPath, string kindleWordsPath) {
            try {
                var clippingsResult = ImportKindleClippings(kindleClippingsPath);
                var wordResult = ImportKindleWords(kindleWordsPath);
                if (string.IsNullOrWhiteSpace(clippingsResult) && string.IsNullOrWhiteSpace(wordResult)) {
                    return string.Empty;
                }
                if (string.IsNullOrWhiteSpace(clippingsResult)) {
                    return wordResult;
                }
                if (string.IsNullOrWhiteSpace(wordResult)) {
                    return clippingsResult;
                }
                return clippingsResult + Environment.NewLine + wordResult;
            } catch (Exception e) {
                Console.WriteLine(e);
                return string.Empty;
            }
        }

        private string ImportKindleClippings(string clippingsPath) {
            try {
                var message = string.Empty;
                if (_km2DatabaseService.ImportKindleClippings(clippingsPath, out var result)) {
                    var parsedCount = result[AppConstants.ParsedCount];
                    var insertedCount = result[AppConstants.InsertedCount];
                    message = Strings.Parsed_X + Strings.Space + parsedCount + Strings.Space + Strings.X_Clippings + Strings.Symbol_Comma + Strings.Imported_X + Strings.Space + insertedCount + Strings.Space + Strings.X_Clippings;
                } else {
                    var exception = result[AppConstants.Exception];
                    Console.WriteLine(exception);
                }
                return message;
            } catch (Exception e) {
                Console.WriteLine(e);
                throw;
            }
        }

        private string ImportKindleWords(string kindleWordsPath) {
            try {
                if (!File.Exists(kindleWordsPath)) {
                    MessageBox(Strings.Kindle_Vocab_Not_Exist, Strings.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return string.Empty;
                }

                var connectionString = "Data Source=" + kindleWordsPath + ";Cache=Shared;Mode=ReadWrite;";

                var bookInfoRepository = new BookInfoRepository(connectionString);
                var vocabLookupRepository = new VocabLookupRepository(connectionString);
                var wordRepository = new WordRepository(connectionString);
                var lookupRepository = new LookupRepository(AppConstants.ConnectionString);
                var vocabRepository = new VocabRepository(AppConstants.ConnectionString);
                var vocabDatabaseService = new VocabDatabaseService(bookInfoRepository, vocabLookupRepository, wordRepository, lookupRepository, vocabRepository);

                if (vocabDatabaseService.ImportKindleWords(kindleWordsPath, out var result)) {
                    var lookupCount = result[AppConstants.LookupCount];
                    var insertedLookupCount = result[AppConstants.InsertedLookupCount];
                    var insertedVocabCount = result[AppConstants.InsertedVocabCount];
                    return Strings.Parsed_X + Strings.Space + lookupCount + Strings.Space + Strings.X_Vocabs + Strings.Space + Strings.Symbol_Comma + Strings.Imported_X + Strings.Space + insertedLookupCount + Strings.Space +
                           Strings.X_Lookups + Strings.Space + Strings.Symbol_Comma + insertedVocabCount + Strings.Space + Strings.X_Vocabs;
                }
                var exception = result["Exception"];
                return exception;
            } catch (Exception e) {
                Console.WriteLine(e);
                throw;
            }
        }

        private void UpdateFrequency() {
            _km2DatabaseService.UpdateFrequency();
        }

        private void RefreshData(bool isReQuery = true) {
            try {
                label1.Text = string.Empty;
                label2.Text = string.Empty;
                label3.Text = string.Empty;
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
                _clippings = _clippingService.GetAllClippings();
                _originClippings = _originalClippingLineService.GetAllOriginalClippingLines();
                _vocabs = _vocabService.GetAllVocabs();
                _lookups = _lookupService.GetAllLookups();
            } else {
                _clippings = _clippingService.GetByFuzzySearch(_searchText, _searchType);
                _originClippings = _originalClippingLineService.GetByFuzzySearch(_searchText, _searchType);
                _vocabs = _vocabService.GetByFuzzySearch(_searchText, _searchType);
                _lookups = _lookupService.GetByFuzzySearch(_searchText, _searchType);
            }

            foreach (Lookup row in _lookups) {
                var wordKey = row.WordKey;
                var word = string.Empty;
                var stem = string.Empty;
                var frequency = string.Empty;
                foreach (Vocab vocabRow in _vocabs) {
                    if (vocabRow.WordKey != wordKey) {
                        continue;
                    }

                    word = vocabRow.Word;
                    stem = vocabRow.Stem;
                    frequency = vocabRow.Frequency.ToString();
                    break;
                }

                row.Word = word;
                row.Stem = stem ?? string.Empty;
                row.Frequency = frequency ?? string.Empty;
            }

            var books = _clippings.AsEnumerable().Select(row => new {
                row.BookName
            }).Distinct().OrderBy(book => book.BookName).ToList();

            var rootNodeBooks = new TreeNode(Strings.Select_All) {
                ImageIndex = 2,
                SelectedImageIndex = 2
            };

            treeViewBooks.Nodes.Clear();

            treeViewBooks.Nodes.Add(rootNodeBooks);

            if (books.Count != 0) {
                foreach (TreeNode bookNode in books.Select(book => new TreeNode(book.BookName) {
                             ToolTipText = book.BookName
                         })) {
                    treeViewBooks.Nodes.Add(bookNode);
                }
            }

            treeViewBooks.ExpandAll();

            var words = _vocabs.AsEnumerable().Select(row => new {
                row.Word
            }).Distinct().OrderBy(word => word.Word).ToList();

            var rootNodeWords = new TreeNode(Strings.Select_All) {
                ImageIndex = 2,
                SelectedImageIndex = 2
            };

            treeViewWords.Nodes.Clear();

            if (words.Count == 0) {
                return;
            }
            treeViewWords.Nodes.Add(rootNodeWords);

            foreach (TreeNode wordNode in words.Select(word => new TreeNode(word.Word) {
                         ToolTipText = word.Word
                     })) {
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
                    if (_clippings.Count <= 0) {
                        return;
                    }

                    if (string.IsNullOrWhiteSpace(_selectedBook) || _selectedBook.Equals(Strings.Select_All)) {
                        _selectedBook = Strings.Select_All;

                        dataGridView.DataSource = DataTableHelper.ToDataTable(_clippings);

                        dataGridView.Columns[Columns.Content]!.HeaderText = Strings.Content;
                        dataGridView.Columns[Columns.BookName]!.HeaderText = Strings.Books;
                        dataGridView.Columns[Columns.AuthorName]!.HeaderText = Strings.Author;
                        dataGridView.Columns[Columns.ClippingDate]!.HeaderText = Strings.Time;
                        dataGridView.Columns[Columns.PageNumber]!.HeaderText = Strings.Page;
                        dataGridView.Columns[Columns.ClippingDate]!.HeaderText = Strings.Time;

                        dataGridView.Columns[Columns.Key]!.Visible = false;
                        dataGridView.Columns[Columns.Content]!.Visible = true;
                        dataGridView.Columns[Columns.BookName]!.Visible = true;
                        dataGridView.Columns[Columns.AuthorName]!.Visible = true;
                        dataGridView.Columns[Columns.BriefType]!.Visible = false;
                        dataGridView.Columns[Columns.ClippingTypeLocation]!.Visible = false;
                        dataGridView.Columns[Columns.ClippingDate]!.Visible = true;
                        dataGridView.Columns[Columns.Read]!.Visible = false;
                        dataGridView.Columns[Columns.ClippingImportDate]!.Visible = false;
                        dataGridView.Columns[Columns.Tag]!.Visible = false;
                        dataGridView.Columns[Columns.Sync]!.Visible = false;
                        dataGridView.Columns[Columns.NewBookName]!.Visible = false;
                        dataGridView.Columns[Columns.ColorRgb]!.Visible = false;
                        dataGridView.Columns[Columns.PageNumber]!.Visible = true;

                        dataGridView.Columns[Columns.BookName]!.Width = 100;
                        dataGridView.Columns[Columns.AuthorName]!.Width = 100;
                        dataGridView.Columns[Columns.ClippingDate]!.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
                        dataGridView.Columns[Columns.PageNumber]!.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
                        dataGridView.Columns[Columns.Content]!.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                        dataGridView.Sort(dataGridView.Columns[Columns.ClippingDate]!, ListSortDirection.Descending);
                    } else {
                        var clippings = _clippings.AsEnumerable().Where(row => row.BookName == _selectedBook).ToList();
                        var filteredBooks = DataTableHelper.ToDataTable(clippings);
                        lblBookCount.Text = Strings.Total_Clippings + Strings.Space + filteredBooks.Rows.Count + Strings.Space + Strings.X_Clippings;
                        lblBookCount.Image = Resources.open_book;
                        lblBookCount.Visible = true;

                        dataGridView.DataSource = filteredBooks;

                        dataGridView.Columns[Columns.Content]!.HeaderText = Strings.Content;
                        dataGridView.Columns[Columns.BookName]!.HeaderText = Strings.Books;
                        dataGridView.Columns[Columns.AuthorName]!.HeaderText = Strings.Author;
                        dataGridView.Columns[Columns.ClippingDate]!.HeaderText = Strings.Time;
                        dataGridView.Columns[Columns.PageNumber]!.HeaderText = Strings.Page;
                        dataGridView.Columns[Columns.ClippingDate]!.HeaderText = Strings.Time;

                        dataGridView.Columns[Columns.Key]!.Visible = false;
                        dataGridView.Columns[Columns.Content]!.Visible = true;
                        dataGridView.Columns[Columns.BookName]!.Visible = false;
                        dataGridView.Columns[Columns.AuthorName]!.Visible = false;
                        dataGridView.Columns[Columns.BriefType]!.Visible = false;
                        dataGridView.Columns[Columns.ClippingTypeLocation]!.Visible = false;
                        dataGridView.Columns[Columns.ClippingDate]!.Visible = true;
                        dataGridView.Columns[Columns.Read]!.Visible = false;
                        dataGridView.Columns[Columns.ClippingImportDate]!.Visible = false;
                        dataGridView.Columns[Columns.Tag]!.Visible = false;
                        dataGridView.Columns[Columns.Sync]!.Visible = false;
                        dataGridView.Columns[Columns.NewBookName]!.Visible = false;
                        dataGridView.Columns[Columns.ColorRgb]!.Visible = false;
                        dataGridView.Columns[Columns.PageNumber]!.Visible = true;

                        dataGridView.Columns[Columns.ClippingDate]!.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
                        dataGridView.Columns[Columns.PageNumber]!.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
                        dataGridView.Columns[Columns.Content]!.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                        dataGridView.Sort(dataGridView.Columns[Columns.PageNumber]!, ListSortDirection.Ascending);
                    }

                    dataGridView.Columns[Columns.PageNumber]!.DisplayIndex = 4;

                    break;
                case 1:
                    if (_vocabs.Count <= 0) {
                        return;
                    }

                    dataGridView.DataSource = DataTableHelper.ToDataTable(_lookups);

                    dataGridView.Columns[Columns.Word]!.DisplayIndex = 0;
                    dataGridView.Columns[Columns.Stem]!.DisplayIndex = 1;

                    if (string.IsNullOrWhiteSpace(_selectedWord) || _selectedWord.Equals(Strings.Select_All)) {
                        _selectedWord = Strings.Select_All;

                        dataGridView.Columns[Columns.Word]!.HeaderText = Strings.Vocabulary;
                        dataGridView.Columns[Columns.Stem]!.HeaderText = Strings.Stem;
                        dataGridView.Columns[Columns.Frequency]!.HeaderText = Strings.Frequency;
                        dataGridView.Columns[Columns.Usage]!.HeaderText = Strings.Content;
                        dataGridView.Columns[Columns.Title]!.HeaderText = Strings.Books;
                        dataGridView.Columns[Columns.Authors]!.HeaderText = Strings.Author;
                        dataGridView.Columns[Columns.Timestamp]!.HeaderText = Strings.Time;

                        dataGridView.Columns[Columns.Word]!.Visible = true;
                        dataGridView.Columns[Columns.Stem]!.Visible = true;
                        dataGridView.Columns[Columns.Frequency]!.Visible = true;
                        dataGridView.Columns[Columns.WordKey]!.Visible = false;
                        dataGridView.Columns[Columns.Usage]!.Visible = true;
                        dataGridView.Columns[Columns.Title]!.Visible = false;
                        dataGridView.Columns[Columns.Authors]!.Visible = false;
                        dataGridView.Columns[Columns.Timestamp]!.Visible = true;

                        dataGridView.Columns[Columns.Word]!.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
                        dataGridView.Columns[Columns.Stem]!.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
                        dataGridView.Columns[Columns.Frequency]!.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
                        dataGridView.Columns[Columns.Usage]!.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                        dataGridView.Columns[Columns.Timestamp]!.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
                    } else {
                        var lookups = _lookups.AsEnumerable().Where(row => row.WordKey?[3..] == _selectedWord).ToList();
                        var filteredWords = DataTableHelper.ToDataTable(lookups);
                        lblBookCount.Text = Strings.Totally_Vocabs + Strings.Space + filteredWords.Rows.Count + Strings.Space + Strings.X_Lookups;
                        lblBookCount.Image = Resources.input_latin_uppercase;
                        lblBookCount.Visible = true;

                        dataGridView.DataSource = filteredWords;

                        dataGridView.Columns[Columns.Word]!.HeaderText = Strings.Vocabulary;
                        dataGridView.Columns[Columns.Stem]!.HeaderText = Strings.Stem;
                        dataGridView.Columns[Columns.Frequency]!.HeaderText = Strings.Frequency;
                        dataGridView.Columns[Columns.Usage]!.HeaderText = Strings.Content;
                        dataGridView.Columns[Columns.Title]!.HeaderText = Strings.Books;
                        dataGridView.Columns[Columns.Authors]!.HeaderText = Strings.Author;
                        dataGridView.Columns[Columns.Timestamp]!.HeaderText = Strings.Time;

                        dataGridView.Columns[Columns.Word]!.Visible = false;
                        dataGridView.Columns[Columns.Stem]!.Visible = true;
                        dataGridView.Columns[Columns.Frequency]!.Visible = false;
                        dataGridView.Columns[Columns.WordKey]!.Visible = false;
                        dataGridView.Columns[Columns.Usage]!.Visible = true;
                        dataGridView.Columns[Columns.Title]!.Visible = false;
                        dataGridView.Columns[Columns.Authors]!.Visible = false;
                        dataGridView.Columns[Columns.Timestamp]!.Visible = true;

                        dataGridView.Columns[Columns.Word]!.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                        dataGridView.Columns[Columns.Stem]!.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                        dataGridView.Columns[Columns.Frequency]!.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
                        dataGridView.Columns[Columns.Usage]!.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                        dataGridView.Columns[Columns.Timestamp]!.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
                    }

                    dataGridView.Sort(dataGridView.Columns[Columns.Timestamp]!, ListSortDirection.Descending);

                    break;
            }
        }

        private void CountRows() {
            var index = tabControl.SelectedIndex;
            switch (index) {
                case 0:
                    var booksCount = treeViewBooks.Nodes.Count - 1;
                    var clippingsCount = _clippings.Count;
                    var originClippingsCount = _originClippings.Count;
                    var diff = Math.Abs(originClippingsCount - clippingsCount);
                    lblCount.Text = Strings.Space + Strings.Totally + Strings.Space + booksCount + Strings.Space + Strings.X_Books + Strings.Symbol_Comma + clippingsCount + Strings.Space + Strings.X_Clippings + Strings.Symbol_Comma +
                                    Strings.Deleted_X + Strings.Space + diff + Strings.Space + Strings.X_Rows;
                    break;
                case 1:
                    var vocabCount = _vocabs.Count;
                    var lookupsCount = _lookups.Count;
                    lblCount.Text = Strings.Space + Strings.Totally + Strings.Space + vocabCount + Strings.Space + Strings.X_Vocabs + Strings.Symbol_Comma + Strings.Quried_X + Strings.Space + lookupsCount + Strings.Space + Strings.X_Times;
                    break;
            }
        }

        private string ImportKmDatabase(string filePath) {
            var kmConnectionString = DatabaseHelper.GetConnectionString(filePath);
            
            var clippingRepository = new ClippingRepository(AppConstants.ConnectionString);
            var lookupRepository = new LookupRepository(AppConstants.ConnectionString);
            var originalClippingLineRepository = new OriginalClippingLineRepository(AppConstants.ConnectionString);
            var settingRepository = new SettingRepository(AppConstants.ConnectionString);
            var vocabRepository = new VocabRepository(AppConstants.ConnectionString);
            
            var kmClippingRepository = new ClippingRepository(kmConnectionString);
            var kmLookupRepository = new LookupRepository(kmConnectionString);
            var kmOriginalClippingLineRepository = new OriginalClippingLineRepository(kmConnectionString);
            var kmSettingRepository = new SettingRepository(kmConnectionString);
            var kmVocabRepository = new VocabRepository(kmConnectionString);
            
            var kmDatabaseService = new KMDatabaseService(clippingRepository, lookupRepository, originalClippingLineRepository, settingRepository, vocabRepository, kmClippingRepository, kmLookupRepository, kmOriginalClippingLineRepository, kmSettingRepository, kmVocabRepository);

            var clippingsCount = _clippingService.GetCount();
            var vocabCount = _vocabService.GetCount();

            kmDatabaseService.ImportFromKmDatabase();

            CleanDatabase();

            UpdateFrequency();

            clippingsCount = _clippingService.GetCount() - clippingsCount;
            vocabCount = _vocabService.GetCount() - vocabCount;

            return Strings.Parsed_X + Strings.Space + clippingsCount + vocabCount + Strings.Space + Strings.X_Records + Strings.Symbol_Comma + Strings.Imported_X + Strings.Space + clippingsCount + Strings.Space + Strings.X_Clippings +
                   Strings.Symbol_Comma + vocabCount + Strings.Space + Strings.X_Vocabs;
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
                        //var clippingdate = selectedRow.Cells[Columns.ClippingDate].Value.ToString() ?? string.Empty;
                        var bookName = selectedRow.Cells[Columns.BookName].Value.ToString() ?? string.Empty;
                        var authorName = selectedRow.Cells[Columns.AuthorName].Value.ToString() ?? string.Empty;
                        _ = int.TryParse(selectedRow.Cells[Columns.PageNumber].Value.ToString() ?? string.Empty, out var pageNumber);
                        var content = selectedRow.Cells[Columns.Content].Value.ToString()?.Replace(" 　　", "\n") ?? string.Empty;
                        var briefType = selectedRow.Cells[Columns.BriefType].Value.ToString() ?? string.Empty;

                        lblBook.Text = bookName;
                        if (authorName != string.Empty) {
                            lblAuthor.Text = Strings.Left_Parenthesis + authorName + Strings.Right_Parenthesis;
                        } else {
                            lblAuthor.Text = string.Empty;
                        }

                        lblLocation.Text = Strings.Page_ + Strings.Space + pageNumber + Strings.Space + Strings.X_Page;

                        lblContent.Text = string.Empty;
                        lblContent.SelectionBullet = false;
                        lblContent.AppendText(content);
                        if (briefType.Equals("1")) {
                            label1.Text = @"[" + Strings.Note + @"]";
                            label2.Text = @"[" + Strings.Clipping + @"]";
                            label3.Text = _clippingService.GetClippingByBookNameAndPageNumberAndBriefType(bookName, pageNumber, BriefType.Note)[0].Content;
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
                        var word_key = selectedRow.Cells[Columns.WordKey].Value.ToString() ?? string.Empty;
                        var word = selectedRow.Cells[Columns.Word].Value.ToString() ?? string.Empty;
                        var stem = selectedRow.Cells[Columns.Stem].Value.ToString() ?? string.Empty;
                        var frequency = selectedRow.Cells[Columns.Frequency].Value.ToString() ?? string.Empty;

                        if (string.IsNullOrWhiteSpace(word_key) || string.IsNullOrWhiteSpace(word) || string.IsNullOrWhiteSpace(stem) || string.IsNullOrWhiteSpace(frequency)) {
                            break;
                        }

                        var listUsage = new HashSet<string>();

                        var usage_list = new List<string>();
                        foreach (Lookup row in _lookups) {
                            if (!string.Equals(row.WordKey, word_key, StringComparison.InvariantCultureIgnoreCase)) {
                                continue;
                            }
                            var str = row.WordKey;
                            var strContent = row.Usage;
                            if (string.IsNullOrWhiteSpace(str) || string.IsNullOrWhiteSpace(strContent)) {
                                continue;
                            }
                            var title = " ——《" + row.Title + "》";
                            listUsage.Add(title);
                            usage_list.Add(strContent + title);
                        }

                        var isChinese = false;
                        var length = Encoding.GetEncoding("UTF-8").GetBytes(word).Length;
                        if (length > word.Length) {
                            isChinese = true;
                        }

                        var usage = usage_list.Aggregate("", (current, s) => current + (s + "\n").Replace(" 　　", "\n"));
                        var usage_clippings = new List<string>();
                        if (word.Length > 1) {
                            if (!isChinese) {
                                foreach (Clipping row in _clippings) {
                                    var strContent = row.Content;
                                    if (string.IsNullOrWhiteSpace(strContent)) {
                                        continue;
                                    }
                                    if (!Regex.IsMatch(strContent, $"\\b{word}\\b", RegexOptions.IgnoreCase)) {
                                        continue;
                                    }
                                    usage_clippings.Add(strContent.Replace(" 　　", "\n") + " ——《" + row.BookName + "》" + "\n");
                                    listUsage.Add(" ——《" + row.BookName + "》");
                                }
                            } else {
                                foreach (Clipping row in _clippings) {
                                    var strContent = row.Content;
                                    if (string.IsNullOrWhiteSpace(strContent)) {
                                        continue;
                                    }
                                    if (!strContent.Contains(word)) {
                                        continue;
                                    }
                                    usage_clippings.Add(strContent.Replace(" 　　", "\n") + " ——《" + row.BookName + "》" + "\n");
                                    listUsage.Add(" ——《" + row.BookName + "》");
                                }
                            }
                        }

                        lblBook.Text = word;
                        if (stem != string.Empty && stem != word) {
                            lblAuthor.Text = Strings.Left_Parenthesis + Strings.Stem + Strings.Symbol_Colon + stem + Strings.Space + Strings.Right_Parenthesis;
                        } else {
                            lblAuthor.Text = string.Empty;
                        }

                        lblContent.SelectionBullet = true;
                        var usageLines = usage.Split(['\n'], StringSplitOptions.RemoveEmptyEntries);
                        foreach (var lines in usageLines.Select(line => line.Split("\n"))) {
                            for (var i = 0; i < lines.Length; i++) {
                                lblContent.SelectionBullet = i == 0;
                                var aline = lines[i];
                                lblContent.AppendText(aline.Trim() + "\n");
                            }
                            lblContent.SelectionBullet = false;
                        }

                        lblContent.SelectionBullet = false;
                        lblContent.AppendText("\n\n");

                        foreach (var lines in usage_clippings.Select(line => line.Split("\n"))) {
                            for (var i = 0; i < lines.Length; i++) {
                                lblContent.SelectionBullet = i == 0;
                                var aline = lines[i];
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

                        lblLocation.Text = Strings.Frequency + Strings.Symbol_Colon + frequency + Strings.Space + Strings.X_Times;
                        lblBookCount.Text = Strings.Totally_Vocabs + Strings.Space + usage_list.Count + Strings.Space + Strings.X_Lookups;
                        if (usage_clippings.Count > 0) {
                            lblLocation.Text += Strings.Symbol_Comma + usage_clippings.Count + Strings.Space + Strings.X_Clippings;
                            lblBookCount.Text += Strings.Symbol_Comma + Strings.Totally_Other_Books + Strings.Space + usage_clippings.Count + Strings.Space + Strings.X_Other_Books;
                        }
                        lblBookCount.Image = Resources.input_latin_uppercase;
                        lblBookCount.Visible = true;

                        label1.Visible = false;
                        label2.Visible = false;
                        label3.Visible = false;

                        break;
                }
            } catch (Exception ex) {
                Console.WriteLine(ex.Message);
            }
        }

        private void TreeViewBooks_Select() {
            splitContainerDetail.Panel1Collapsed = false;

            if (string.IsNullOrWhiteSpace(_selectedBook) || _selectedBook.Equals(Strings.Select_All)) {
                _selectedBook = Strings.Select_All;
                lblBookCount.Text = string.Empty;
                lblBookCount.Image = null;
                lblBookCount.Visible = false;
                dataGridView.DataSource = DataTableHelper.ToDataTable(_clippings);
                dataGridView.Columns[Columns.BookName]!.Visible = true;
                dataGridView.Columns[Columns.AuthorName]!.Visible = true;
                dataGridView.Sort(dataGridView.Columns[Columns.ClippingDate]!, ListSortDirection.Descending);
            } else {
                var clippings = _clippings.AsEnumerable().Where(row => row.BookName == _selectedBook).ToList();
                var filteredBooks = DataTableHelper.ToDataTable(clippings);
                lblBookCount.Text = Strings.Space + Strings.Total_Clippings + Strings.Space + filteredBooks.Rows.Count + Strings.Space + Strings.X_Clippings;
                lblBookCount.Image = Resources.open_book;
                lblBookCount.Visible = true;
                dataGridView.DataSource = filteredBooks;
                dataGridView.Columns[Columns.BookName]!.Visible = false;
                dataGridView.Columns[Columns.AuthorName]!.Visible = false;
                dataGridView.Columns[Columns.BookName]!.HeaderText = Strings.Books;
                dataGridView.Columns[Columns.AuthorName]!.HeaderText = Strings.Author;
                dataGridView.Sort(dataGridView.Columns[Columns.PageNumber]!, ListSortDirection.Ascending);
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

        private static void LblContent_LostFocus(object? sender, EventArgs e) {
            HideCaret(sender);
        }

        private static void LblContent_GotFocus(object? sender, EventArgs e) {
            HideCaret(sender);
        }

        private void ShowContentEditDialog() {
            if (tabControl.SelectedIndex != 0) {
                return;
            }
            var bookName = lblBook.Text;
            var content = lblContent.Text;

            var fields = new List<KeyValue> {
                new(Strings.Content, content, KeyValue.ValueTypes.Multiline)
            };

            Messenger.ValidateControls += [SuppressMessage("ReSharper", "AccessToModifiedClosure")](_, e) => {
                if (fields == null) {
                    return;
                }
                var fValue = fields[0].Value;
                if (string.IsNullOrWhiteSpace(fValue)) {
                    e.Cancel = true;
                }
            };

            if (dataGridView.SelectedRows.Count <= 0) {
                return;
            }

            var key = dataGridView.SelectedRows[0].Cells[Columns.Key].Value.ToString() ?? string.Empty;
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
            Clipping? clipping = _clippingService.GetClippingByKey(key);
            if (clipping == null) {
                return;
            }
            clipping.Content = dialogContent;
            if (_clippingService.UpdateClipping(clipping)) {
                MessageBox(Strings.Clippings_Revised, Strings.Successful, MessageBoxButtons.OK, MessageBoxIcon.Information);
            } else {
                MessageBox(Strings.Clippings_Revised_Failed, Strings.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            RefreshData();
        }

        private void DataGridView_CellDoubleClick(object sender, DataGridViewCellEventArgs e) {
            if (e.RowIndex <= -1 || e.ColumnIndex <= -1) {
                return;
            }

            var index = tabControl.SelectedIndex;
            var columnName = dataGridView.Columns[e.ColumnIndex].HeaderText;

            switch (index) {
                case 0:
                    if (columnName.Equals(Strings.Books)) {
                        _selectedBook = dataGridView.Rows[e.RowIndex].Cells[Columns.BookName].Value.ToString()!;
                        var clippings = _clippings.AsEnumerable().Where(row => row.BookName == _selectedBook).ToList();
                        var filteredBooks = DataTableHelper.ToDataTable(clippings);
                        lblBookCount.Text = Strings.Total_Clippings + Strings.Space + filteredBooks.Rows.Count + Strings.Space + Strings.X_Clippings;
                        lblBookCount.Image = Resources.open_book;
                        lblBookCount.Visible = true;
                        dataGridView.DataSource = filteredBooks;
                        dataGridView.Columns[Columns.BookName]!.Visible = false;
                        dataGridView.Columns[Columns.AuthorName]!.Visible = false;
                        dataGridView.Sort(dataGridView.Columns[Columns.ClippingTypeLocation]!, ListSortDirection.Ascending);
                        RefreshData();
                    } else {
                        ShowContentEditDialog();
                    }
                    break;
                case 1:
                    if (columnName.Equals(Strings.Vocabulary) && treeViewWords.SelectedNode.Index == 0) {
                        _selectedWord = dataGridView.Rows[e.RowIndex].Cells[Columns.Word].Value.ToString()!;
                        var lookups = _lookups.AsEnumerable().Where(row => row.Word == _selectedWord).ToList();
                        var filteredWord = DataTableHelper.ToDataTable(lookups);
                        lblBookCount.Text = Strings.Total_Clippings + Strings.Space + filteredWord.Rows.Count + Strings.Space + Strings.X_Clippings;
                        lblBookCount.Image = Resources.open_book;
                        lblBookCount.Visible = true;
                        dataGridView.Columns[Columns.Usage]!.Visible = true;
                        dataGridView.Columns[Columns.Title]!.Visible = true;
                        dataGridView.Columns[Columns.Authors]!.Visible = true;
                        dataGridView.Columns[Columns.Frequency]!.Visible = false;
                        dataGridView.Columns[Columns.Usage]!.HeaderText = Strings.Content;
                        dataGridView.Columns[Columns.Title]!.HeaderText = Strings.Books;
                        dataGridView.Columns[Columns.Authors]!.HeaderText = Strings.Author;
                        dataGridView.Sort(dataGridView.Columns[Columns.Timestamp]!, ListSortDirection.Descending);
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
            DeleteRow();
        }

        private void DeleteRow() {
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

                    try {
                        foreach (DataGridViewRow row in dataGridView.SelectedRows) {
                            var key = row.Cells[Columns.Key].Value.ToString() ?? string.Empty;
                            if (string.IsNullOrWhiteSpace(key)) {
                                return;
                            }
                            if (_clippingService.DeleteClipping(key)) {
                                dataGridView.Rows.Remove(row);
                            } else {
                                MessageBox(Strings.Delete_Failed, Strings.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }
                    } catch (Exception ex) {
                        Console.WriteLine(ex);
                    }

                    break;
                case 1:
                    DialogResult resultWords = MessageBox(Strings.Confirm_Delete_Lookups, Strings.Confirm, MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                    if (resultWords != DialogResult.Yes) {
                        return;
                    }

                    try {
                        foreach (DataGridViewRow row in dataGridView.SelectedRows) {
                            var timestamp = row.Cells[Columns.Timestamp].Value.ToString() ?? string.Empty;
                            if (string.IsNullOrWhiteSpace(timestamp)) {
                                continue;
                            }
                            var lookups = _lookupService.GetLookupsByTimestamp(timestamp);
                            foreach (Lookup lookup in lookups) {
                                if (lookup.WordKey != null && _lookupService.DeleteLookup(lookup.WordKey)) {
                                    dataGridView.Rows.Remove(row);
                                } else {
                                    MessageBox(Strings.Delete_Failed, Strings.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
                                }
                            }
                        }
                    } catch (Exception ex) {
                        Console.WriteLine(ex);
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

            try {
                var index = tabControl.SelectedIndex;
                switch (index) {
                    case 0:
                        var content = dataGridView.SelectedRows[0].Cells[Columns.Content].Value.ToString() ?? string.Empty;
                        Clipboard.SetText(content != string.Empty ? content : lblContent.Text);
                        break;
                    case 1:
                        var usage = dataGridView.SelectedRows[0].Cells[Columns.Usage].Value.ToString() ?? string.Empty;
                        Clipboard.SetText(usage != string.Empty ? usage : lblBook.Text);
                        break;
                }
            } catch (Exception exception) {
                Console.WriteLine(exception);
            }
        }

        private void DataGridView_KeyDown(object sender, KeyEventArgs e) {
            switch (e.KeyCode) {
                case Keys.Enter:
                    ShowContentEditDialog();
                    e.Handled = true;
                    break;
                case Keys.Delete:
                    DeleteRow();
                    break;
            }
        }

        private void DataGridView_MouseDown(object sender, MouseEventArgs e) {
            try {
                DataGridView.HitTestInfo? hitTestInfo = dataGridView.HitTest(e.X, e.Y);
                if (hitTestInfo.RowIndex < 0) {
                    return;
                }
                dataGridView.ClearSelection();
                dataGridView.Rows[hitTestInfo.RowIndex].Selected = true;
                _selectedDataGridIndex = hitTestInfo.RowIndex;
            } catch (Exception ex) {
                Console.WriteLine(ex);
            }
        }

        private void MenuBooksDelete_Click(object sender, EventArgs e) {
            DeleteNodes();
        }

        private void DeleteNodes() {
            var index = tabControl.SelectedIndex;
            DialogResult result;
            switch (index) {
                case 0:
                    if (treeViewBooks.SelectedNode is null) {
                        return;
                    }
                    if (treeViewBooks.SelectedNode.Text.Equals(Strings.Select_All)) {
                        result = MessageBox(Strings.Confirm_Clear_Clippings, Strings.Confirm, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                        if (result == DialogResult.Yes) {
                            _clippingService.DeleteAllClippings();
                        }
                    } else {
                        result = MessageBox(Strings.Confirm_Delete_Clippings_Book, Strings.Confirm, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                        if (result == DialogResult.Yes) {
                            var bookname = treeViewBooks.SelectedNode.Text;
                            var clippings = _clippingService.GetClippingByBookName(bookname);
                            var deletedCount = 0;
                            foreach (Clipping clipping in clippings) {
                                if (_clippingService.DeleteClipping(clipping.Key)) {
                                    deletedCount++;
                                }
                            }
                            if (deletedCount == 0) {
                                MessageBox(Strings.Delete_Failed, Strings.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                            _selectedBook = Strings.Select_All;
                        }
                    }
                    break;
                case 1:
                    if (treeViewWords.SelectedNode is null) {
                        return;
                    }
                    if (treeViewWords.SelectedNode.Text.Equals(Strings.Select_All)) {
                        result = MessageBox(Strings.Confirm_Clear_Vocabulary, Strings.Confirm, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                        if (result == DialogResult.Yes) {
                            _vocabService.DeleteAllVocabs();
                        }
                    } else {
                        result = MessageBox(Strings.Confirm_Delete_Lookups_Vocabs, Strings.Confirm, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                        if (result == DialogResult.Yes) {
                            var word = treeViewWords.SelectedNode.Text;
                            var wordKey = string.Empty;
                            foreach (Vocab vocab in _vocabs) {
                                if (vocab.Word != word) {
                                    continue;
                                }
                                wordKey = vocab.WordKey;
                                break;
                            }

                            if (wordKey != null && !_vocabService.DeleteVocabByWordKey(wordKey) && !_lookupService.DeleteLookup(wordKey)) {
                                MessageBox(Strings.Delete_Failed, Strings.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }

                            _selectedWord = Strings.Select_All;
                        }
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
                Title = Strings.Import_Kindle_Mate_Database_File + Strings.Space + @"(" + AppConstants.KindleMateDatabaseFileName + @")",
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
            bw.DoWork += (_, workEventArgs) => { workEventArgs.Result = ImportKmDatabase(fileDialog.FileName); };
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
            OpenUrl(AppConstants.RepoUrl);
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

        private void IsKindleConnected() {
            var isConnected = false;

            try {
                isConnected = _deviceType switch {
                    Device.Type.USB => HandleUsbDevice(),
                    Device.Type.MTP => HandleMtpDevice(),
                    _ => isConnected
                };

                if (!isConnected) {
                    isConnected = HandleUsbDevice() || HandleMtpDevice();
                }

                if (!isConnected) {
                    _driveLetter = string.Empty;
                    menuSyncFromKindle.Visible = false;
                    menuKindle.Visible = false;
                } else {
                    menuSyncFromKindle.Visible = true;
                    menuKindle.Visible = true;
                }

                menuKindle.Enabled = isConnected;
            } catch (Exception e) {
                Console.WriteLine(e);
            }
        }

        private bool HandleUsbDevice() {
            try {
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

                    _driveLetter = drive.Name;

                    var versionText = string.Empty;
                    var kindleVersionPath = Path.Combine(_driveLetter, _versionFilePath);
                    if (File.Exists(kindleVersionPath)) {
                        using var reader = new StreamReader(kindleVersionPath);
                        versionText = reader.ReadToEnd();
                    }
                    SetKindleVersionText(versionText);
                    _deviceType = Device.Type.USB;
                    return true;
                }
                return false;
            } catch (Exception e) {
                Console.WriteLine(e);
                return false;
            }
        }

        private bool HandleMtpDevice() {
            try {
                var devices = MediaDevice.GetDevices();
                foreach (MediaDevice? device in devices) {
                    try {
                        device.Connect();
                        if (!device.FriendlyName.Contains(AppConstants.Kindle, StringComparison.InvariantCultureIgnoreCase) && !device.Model.Contains(AppConstants.Kindle, StringComparison.InvariantCultureIgnoreCase)) {
                            device.Disconnect();
                            continue;
                        }
                        _driveLetter = @"\Internal Storage\";
                        MediaDirectoryInfo? systemDir = device.GetDirectoryInfo(Path.Combine(_driveLetter, AppConstants.SystemPathName));
                        var files = systemDir.EnumerateFiles(AppConstants.VersionFileName);
                        var mediaFileInfos = files as MediaFileInfo[] ?? files.ToArray();
                        if (mediaFileInfos.Length == 0) {
                            return false;
                        }
                        MediaFileInfo? file = mediaFileInfos[0];
                        var memoryStream = new MemoryStream();
                        device.DownloadFile(file.FullName, memoryStream);
                        memoryStream.Position = 0;
                        using var reader = new StreamReader(memoryStream);
                        var versionText = reader.ReadToEnd();
                        SetKindleVersionText(versionText);
                        device.Disconnect();
                        _deviceType = Device.Type.MTP;
                        return true;
                    } catch (Exception e) {
                        Console.WriteLine(e);
                    }
                }
                return false;
            } catch (Exception e) {
                Console.WriteLine(e);
                return false;
            }
        }

        private void SetKindleVersionText(string versionText) {
            menuKindle.Text = Strings.Space + Strings.Kindle_Device_Connected;
            if (string.IsNullOrWhiteSpace(versionText)) {
                return;
            }
            var kindleVersion = StringHelper.ParseKindleVersion(versionText);
            if (!string.IsNullOrEmpty(kindleVersion)) {
                menuKindle.Text += Strings.Left_Parenthesis + kindleVersion + Strings.Right_Parenthesis;
            }
        }

        private void MenuSyncFromKindle_Click(object sender, EventArgs e) {
            ImportFromKindle();
        }

        private void ImportFromKindle() {
            try {
                var backupClippingsPath = Path.Combine(_backupPath, AppConstants.ImportsPathName);
                var backupWordsPath = Path.Combine(_backupPath, AppConstants.ImportsPathName);

                if (!Directory.Exists(backupClippingsPath)) {
                    Directory.CreateDirectory(backupClippingsPath);
                }
                if (!Directory.Exists(backupWordsPath)) {
                    Directory.CreateDirectory(backupWordsPath);
                }

                var backupClippingsFilePath = Path.Combine(backupClippingsPath, "MyClippings_" + DateTimeHelper.GetCurrentTimestamp() + ".txt");
                var backupWordsFilePath = Path.Combine(backupWordsPath, "vocab_" + DateTimeHelper.GetCurrentTimestamp() + ".db");

                if (!ImportFilesFromDevice(backupClippingsFilePath, backupWordsFilePath, out Exception exception)) {
                    throw exception;
                }

                SetProgressBar(true);
                menuKindle.Enabled = false;
                menuSyncFromKindle.Enabled = false;
                Cursor = Cursors.Default;

                var bw = new BackgroundWorker();
                bw.DoWork += (_, e) => { e.Result = Import(backupClippingsFilePath, backupWordsFilePath); };
                bw.RunWorkerCompleted += (_, e) => {
                    if (e.Result != null && !string.IsNullOrWhiteSpace(e.Result.ToString())) {
                        MessageBox(e.Result.ToString() ?? Strings.Import_Successful, Strings.Successful, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    } else {
                        MessageBox(Strings.Import_Failed, Strings.Failed, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    SetProgressBar(false);
                    menuKindle.Enabled = true;
                    menuSyncFromKindle.Enabled = true;
                    RefreshData();
                };
                bw.RunWorkerAsync();
            } catch (Exception ex) {
                MessageBox(MessageHelper.BuildMessage(Strings.Import_Failed, ex), Strings.Failed, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private bool ImportFilesFromDevice(string backupClippingsPath, string backupWordsPath, out Exception exception) {
            exception = new Exception();
            try {
                var documentPath = Path.Combine(_driveLetter, AppConstants.DocumentsPathName);
                var vocabularyPath = Path.Combine(_driveLetter, AppConstants.SystemPathName, AppConstants.VocabularyPathName);
                switch (_deviceType) {
                    case Device.Type.USB: {
                        File.Copy(Path.Combine(documentPath, AppConstants.ClippingsFileName), backupClippingsPath);
                        File.Copy(Path.Combine(vocabularyPath, AppConstants.VocabFileName), backupWordsPath);
                        return true;
                    }
                    case Device.Type.MTP: {
                        var devices = MediaDevice.GetDevices();
                        using MediaDevice? device = devices.First(d =>
                            d.FriendlyName.Contains(AppConstants.Kindle, StringComparison.InvariantCultureIgnoreCase) || d.Model.Contains(AppConstants.Kindle, StringComparison.InvariantCultureIgnoreCase));
                        device.Connect();
                        ReadMtpFile(device, documentPath, AppConstants.ClippingsFileName, backupClippingsPath);
                        ReadMtpFile(device, vocabularyPath, AppConstants.VocabFileName, backupWordsPath);
                        device.Disconnect();
                        return true;
                    }
                    case Device.Type.Unknown:
                    default: {
                        throw new Exception(Strings.Kindle_Connect_Failed);
                    }
                }
            } catch (Exception e) {
                exception = e;
                return false;
            }
        }

        private static void ReadMtpFile(MediaDevice device, string path, string fileName, string filePath) {
            MediaDirectoryInfo? systemDir = device.GetDirectoryInfo(path);
            IEnumerable<MediaFileInfo> files = systemDir.EnumerateFiles(fileName);
            var fileInfos = files as MediaFileInfo[] ?? files.ToArray();
            if (fileInfos.Length == 0) {
                return;
            }
            MediaFileInfo file = fileInfos[0];
            var memoryStream = new MemoryStream();
            device.DownloadFile(file.FullName, memoryStream);
            memoryStream.Position = 0;
            try {
                File.WriteAllBytes(filePath, memoryStream.ToArray());
            } catch (Exception ex) {
                Console.WriteLine($"Error saving MemoryStream to file: {ex.Message}");
            }
        }

        private static void WriteMtpFile(MediaDevice device, string path, string fileName, string filePath) {
            using var sr = new StreamReader(filePath);
            device.DeleteFile(Path.Combine(path, fileName));
            device.UploadFile(sr.BaseStream, Path.Combine(path, fileName));
        }

        private void MenuKindle_Click(object sender, EventArgs e) {
            ImportFromKindle();
        }

        private void MenuRename_Click(object sender, EventArgs e) {
            var index = tabControl.SelectedIndex;
            if (index == 0) {
                if (treeViewBooks.SelectedNode == null || treeViewBooks.SelectedNode.Text.Equals(Strings.Select_All)) {
                    return;
                }
                var bookName = treeViewBooks.SelectedNode.Text;
                var authorName = GetAuthorNameFromClippings(bookName);
                ShowBookRenameDialog(bookName, authorName);
            }
        }

        private string GetAuthorNameFromClippings(string bookName) {
            var authorName = string.Empty;
            foreach (Clipping row in _clippings) {
                if (row.BookName != null && !row.BookName.Equals(bookName)) {
                    continue;
                }
                authorName = row.AuthorName;
                break;
            }
            return authorName ?? string.Empty;
        }

        private void ShowBookRenameDialog(string bookName, string authorName) {
            var fields = new List<KeyValue> {
                new(Strings.Book_Title, bookName),
                new(Strings.Author, authorName)
            };

            Messenger.ValidateControls += [SuppressMessage("ReSharper", "AccessToModifiedClosure")](_, e) => {
                if (fields != null) {
                    var dialogBook = fields[0].Value;
                    var dialogAuthor = fields[1].Value;
                    if (string.IsNullOrWhiteSpace(dialogBook) || string.IsNullOrWhiteSpace(dialogAuthor)) {
                        e.Cancel = true;
                    }
                }
            };

            if (Messenger.InputBox(Strings.Rename, "", ref fields, MsgIcon.Edit, MessageBoxButtons.OKCancel, _isDarkTheme) != DialogResult.OK) {
                return;
            }
            var dialogBook = fields[0].Value.Trim();
            var dialogAuthor = fields[1].Value.Trim();

            if (string.IsNullOrWhiteSpace(dialogBook)) {
                return;
            }
            if (!string.IsNullOrWhiteSpace(authorName) && string.IsNullOrWhiteSpace(dialogAuthor)) {
                dialogAuthor = authorName;
            }
            if (bookName == dialogBook && authorName == dialogAuthor) {
                MessageBox(Strings.Books_Title_Not_Changed, Strings.Prompt, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (_clippings.AsEnumerable().Any(row => row.BookName == "dialogBook")) {
                DialogResult result = MessageBox(Strings.Confirm_Same_Title_Combine, Strings.Confirm, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result != DialogResult.Yes) {
                    return;
                }

                var resultRows = _clippings.Where(row => row.BookName == bookName).ToList();
                dialogAuthor = (resultRows.Count > 0 ? resultRows[0].AuthorName : string.Empty);
            }

            if (dialogAuthor != null) {
                _lookupService.RenameBook(bookName, dialogBook, dialogAuthor);

                if (!_clippingService.RenameBook(bookName, dialogBook, dialogAuthor)) {
                    MessageBox(Strings.Book_Renamed_Failed, Strings.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            MessageBox(Strings.Books_Renamed, Strings.Successful, MessageBoxButtons.OK, MessageBoxIcon.Information);

            _selectedBook = dialogBook;

            RefreshData();
        }

        private string GetBooknameFromContent() {
            string bookname;
            if (!string.IsNullOrWhiteSpace(lblBook.Text)) {
                bookname = lblBook.Text;
            } else {
                bookname = dataGridView.Rows[0].Cells[Columns.BookName].Value.ToString() ?? string.Empty;
            }
            return bookname;
        }

        private string GetAuthorNameFromContent() {
            string authorName;
            if (!string.IsNullOrWhiteSpace(lblAuthor.Text)) {
                authorName = lblAuthor.Text;
                var startIndex = authorName.IndexOf(Strings.Left_Parenthesis, StringComparison.Ordinal) + 1;
                var endIndex = authorName.LastIndexOf(Strings.Right_Parenthesis, StringComparison.Ordinal) - 1;
                authorName = authorName.Substring(startIndex, endIndex - startIndex + 1);
            } else {
                authorName = dataGridView.Rows[0].Cells[Columns.AuthorName].Value.ToString() ?? string.Empty;
            }
            return authorName;
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

                    var bookName = selectedRow.Cells[Columns.BookName].Value.ToString();
                    var authorName = selectedRow.Cells[Columns.AuthorName].Value.ToString();
                    //var clippinglocation = selectedRow.Cells[Columns.ClippingTypeLocation].Value.ToString();
                    //var content = selectedRow.Cells[Columns.Content].Value.ToString();

                    lblBook.Text = bookName;
                    if (authorName != string.Empty) {
                        lblAuthor.Text = Strings.Left_Parenthesis + authorName + Strings.Right_Parenthesis;
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

            SetCmbSearchSelection();
        }

        private void MenuBookRefresh_Click(object sender, EventArgs e) {
            RefreshData();
        }

        private void MenuBackup_Click(object sender, EventArgs e) {
            DatabaseHelper.BackupDatabase(_programPath, _backupPath, AppConstants.DatabaseFileName);

            if (_clippings.Count <= 0) {
                MessageBox(Strings.No_Data_To_Backup, Strings.Prompt, MessageBoxButtons.OK, MessageBoxIcon.Information);
            } else {
                if (_originalClippingLineService.Export(_backupPath, AppConstants.DatabaseFileName, out Exception exception)) {
                    DialogResult result = MessageBox(Strings.Backup_Successful + Strings.Open_Folder, Strings.Successful, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (result != DialogResult.Yes) {
                        return;
                    }

                    Process.Start("explorer.exe", Path.Combine(_programPath, "Backups"));
                } else {
                    var message = MessageHelper.BuildMessage(Strings.Backup_Clippings_Failed, exception);
                    MessageBox(message, Strings.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void MenuRefresh_Click(object sender, EventArgs e) {
            RefreshData();
        }

        private void MenuClear_Click(object sender, EventArgs e) {
            if (_km2DatabaseService.IsDatabaseEmpty()) {
                MessageBox(Strings.Database_Empty, Strings.Prompt, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            DialogResult result = MessageBox(Strings.Confirm_Clear_All_Data, Strings.Confirm, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result != DialogResult.Yes) {
                return;
            }

            if (_km2DatabaseService.DeleteAllData()) {
                MessageBox(Strings.Data_Cleared, Strings.Successful, MessageBoxButtons.OK, MessageBoxIcon.Information);
            } else {
                MessageBox(Strings.Clear_Failed, Strings.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            Restart();
        }

        private static void Restart() {
            Process.Start(new ProcessStartInfo {
                FileName = System.Windows.Forms.Application.ExecutablePath,
                UseShellExecute = true
            });
            Environment.Exit(0);
        }

        private void MenuImportKindleWords_Click(object sender, EventArgs e) {
            var fileDialog = new OpenFileDialog {
                Title = Strings.Import_Kindle_Vocab_File + Strings.Space + @"(" + AppConstants.VocabFileName + @")",
                CheckFileExists = true,
                CheckPathExists = true,
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
                dataGridView.DataSource = DataTableHelper.ToDataTable(_lookups);

                dataGridView.Columns[Columns.Word]!.Visible = true;
                dataGridView.Columns[Columns.Usage]!.Visible = true;
                dataGridView.Columns[Columns.Title]!.Visible = true;
                dataGridView.Columns[Columns.Authors]!.Visible = true;
                dataGridView.Columns[Columns.Frequency]!.Visible = true;

                dataGridView.Columns[Columns.Frequency]!.HeaderText = Strings.Frequency;
            } else {
                splitContainerDetail.Panel1Collapsed = true;

                var lookups = _lookups.AsEnumerable().Where(row => row.WordKey?[3..] == _selectedWord).ToList();
                var filteredWords = DataTableHelper.ToDataTable(lookups);
                lblBookCount.Text = Strings.Totally_Vocabs + Strings.Space + filteredWords.Rows.Count + Strings.Space + Strings.X_Lookups;
                lblBookCount.Image = Resources.input_latin_uppercase;
                lblBookCount.Visible = true;
                dataGridView.DataSource = filteredWords;

                dataGridView.Columns[Columns.Word]!.Visible = false;
                dataGridView.Columns[Columns.Usage]!.Visible = true;
                dataGridView.Columns[Columns.Title]!.Visible = true;
                dataGridView.Columns[Columns.Authors]!.Visible = true;

                dataGridView.Columns[Columns.Frequency]!.Visible = false;
            }
            dataGridView.Columns[Columns.Usage]!.HeaderText = Strings.Content;
            dataGridView.Columns[Columns.Title]!.HeaderText = Strings.Books;
            dataGridView.Columns[Columns.Authors]!.HeaderText = Strings.Author;
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
                var bookName = e.Node.Text;
                var authorName = GetAuthorNameFromClippings(bookName);
                ShowBookRenameDialog(bookName, authorName);
            }
        }

        private void Content_Rename_MouseDoubleClick() {
            if (tabControl.SelectedIndex == 0) {
                var bookName = GetBooknameFromContent();
                var authorName = GetAuthorNameFromContent();
                ShowBookRenameDialog(bookName, authorName);
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
            if (e.KeyCode == Keys.Delete) {
                DeleteNodes();
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
            bw.DoWork += (_, doWorkEventArgs) => { doWorkEventArgs.Result = RebuildDatabase(); };
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
            if (_km2DatabaseService.RebuildDatabase(out var result)) {
                var parsedCount = result[AppConstants.ParsedCount];
                var insertedCount = result[AppConstants.InsertedCount];
                var clipping = Strings.Parsed_X + Strings.Space + parsedCount + Strings.Space + Strings.X_Clippings + Strings.Symbol_Comma + Strings.Imported_X + Strings.Space + insertedCount + Strings.Space + Strings.X_Clippings;
                return clipping;
            }
            return string.Empty;
        }

        private void MenuClean_Click(object sender, EventArgs e) {
            _clippings = _clippingService.GetAllClippings();

            if (_clippings.Count <= 0) {
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
            if (_km2DatabaseService.CleanDatabase(_databaseFilePath, out var result)) {
                var countEmpty = result[AppConstants.EmptyCount];
                var countTrimmed = result[AppConstants.TrimmedCount];
                var countDuplicated = result[AppConstants.DuplicatedCount];
                var fileSizeDelta = result[AppConstants.FileSizeDelta];
                return Strings.Cleaned + Strings.Space + Strings.Empty_Content + Strings.Space + countEmpty + Strings.Space + Strings.X_Rows + Strings.Symbol_Comma + Strings.Trimmed + Strings.Space + countTrimmed + Strings.Space +
                       Strings.X_Rows + Strings.Symbol_Comma + Strings.Duplicate_Content + Strings.Space + countDuplicated + Strings.Space + Strings.X_Rows + Strings.Symbol_Comma + Strings.Database_Cleaned + Strings.Space + fileSizeDelta;
            }
            var exception = result[AppConstants.Exception];
            Console.WriteLine(exception);
            return exception.Equals(AppConstants.DatabaseNoNeedCleaning) ? Strings.Database_No_Need_Clean : string.Empty;
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

                        var clippings = _clippings.AsEnumerable().Where(row => row.BookName != null && row.BookName.Equals(nodeBookName)).ToList();
                        var filteredBooks = DataTableHelper.ToDataTable(clippings);

                        if (filteredBooks.Rows.Count <= 0) {
                            return false;
                        }

                        markdown.AppendLine("## \ud83d\udcd6 " + nodeBookName.Trim());

                        markdown.AppendLine();

                        foreach (DataRow row in filteredBooks.Rows) {
                            var clippingLocation = row[Columns.ClippingTypeLocation].ToString();
                            var content = row[Columns.Content].ToString();

                            markdown.AppendLine("**" + clippingLocation + "**");

                            markdown.AppendLine();

                            markdown.AppendLine(content);

                            markdown.AppendLine();
                        }
                    }
                } else {
                    filename = StringHelper.SanitizeFilename(bookname);

                    var clippings = _clippings.AsEnumerable().Where(row => row.BookName != null && row.BookName.Equals(bookname)).ToList();
                    var filteredBooks = DataTableHelper.ToDataTable(clippings);

                    if (filteredBooks.Rows.Count <= 0) {
                        return false;
                    }

                    markdown.AppendLine("## \ud83d\udcd6 " + bookname.Trim());

                    markdown.AppendLine();

                    foreach (DataRow row in filteredBooks.Rows) {
                        var clippingLocation = row[Columns.ClippingTypeLocation].ToString();
                        var content = row[Columns.Content].ToString();

                        markdown.AppendLine("**" + clippingLocation + "**");

                        markdown.AppendLine();

                        markdown.AppendLine(content);

                        markdown.AppendLine();
                    }
                }

                File.WriteAllText(Path.Combine(_programPath, AppConstants.ExportsPathName, filename + ".md"), markdown.ToString(), Encoding.UTF8);

                var htmlContent = "<html><head>\r\n<link rel=\"stylesheet\" href=\"styles.css\">\r\n</head><body>\r\n";

                MarkdownPipeline pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().UseTableOfContent().Build();
                htmlContent += Markdown.ToHtml(markdown.ToString(), pipeline);

                htmlContent += "\r\n</body>\r\n</html>";

                File.WriteAllText(Path.Combine(_programPath, AppConstants.ExportsPathName, filename + ".html"), htmlContent, Encoding.UTF8);

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

                        var lookups = _lookups.AsEnumerable().Where(row => row.Word == nodeWordText).ToList();
                        var filteredBooks = DataTableHelper.ToDataTable(lookups);

                        if (filteredBooks.Rows.Count <= 0) {
                            return false;
                        }

                        markdown.AppendLine("## \ud83d\udd24 " + nodeWordText.Trim());

                        markdown.AppendLine();

                        foreach (DataRow row in filteredBooks.Rows) {
                            var title = row[Columns.Title].ToString();
                            var usage = row[Columns.Usage].ToString();

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
                    filename = StringHelper.SanitizeFilename(word);

                    var lookups = _lookups.AsEnumerable().Where(row => row.Word != null && row.Word.Equals(word)).ToList();
                    var filteredBooks = DataTableHelper.ToDataTable(lookups);

                    if (filteredBooks.Rows.Count <= 0) {
                        return false;
                    }

                    markdown.AppendLine("## \ud83d\udd24 " + word.Trim());

                    markdown.AppendLine();

                    foreach (DataRow row in filteredBooks.Rows) {
                        var title = row[Columns.Title].ToString();
                        var usage = row[Columns.Usage].ToString();

                        if (usage == null) {
                            continue;
                        }

                        markdown.AppendLine("**《" + title + "》**");

                        markdown.AppendLine();

                        markdown.AppendLine(usage.Replace(word, " **`" + word + "`** "));

                        markdown.AppendLine();
                    }
                }

                File.WriteAllText(Path.Combine(_programPath, AppConstants.ExportsPathName, filename + ".md"), markdown.ToString(), Encoding.UTF8);

                var htmlContent = "<html><head>\r\n<link rel=\"stylesheet\" href=\"styles.css\">\r\n</head><body>\r\n";

                MarkdownPipeline pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().UseTableOfContent().Build();
                htmlContent += Markdown.ToHtml(markdown.ToString(), pipeline);

                htmlContent += "\r\n</body>\r\n</html>";

                File.WriteAllText(Path.Combine(_programPath, AppConstants.ExportsPathName, filename + ".html"), htmlContent, Encoding.UTF8);

                return true;
            } catch (Exception) {
                return false;
            }
        }

        private void MenuExportMd_Click(object sender, EventArgs e) {
            var path = Path.Combine(_programPath, AppConstants.ExportsPathName);
            if (!Directory.Exists(path)) {
                Directory.CreateDirectory(path);
            }

            File.WriteAllText(Path.Combine(_programPath, AppConstants.ExportsPathName, "styles.css"), AppConstants.Css);

            if (!ClippingsToMarkdown() || !VocabsToMarkdown()) {
                return;
            }

            DialogResult result = MessageBox(Strings.Export_Successful + Strings.Open_Folder, Strings.Successful, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result != DialogResult.Yes) {
                return;
            }

            Process.Start("explorer.exe", Path.Combine(_programPath, AppConstants.ExportsPathName));
        }

        private void MenuStatistic_Click(object sender, EventArgs e) {
            if (_clippings.Count <= 0) {
                MessageBox(Strings.Database_Empty, Strings.Prompt, MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            _settingService.UpdateSetting(new Setting {
                Name = AppConstants.SettingTheme,
                value = _isDarkTheme ? Theme.Light : Theme.Dark
            });
            Restart();
        }

        private void MenuTheme_MouseEnter(object sender, EventArgs e) {
            Cursor = Cursors.Hand;
        }

        private void MenuTheme_MouseLeave(object sender, EventArgs e) {
            Cursor = Cursors.Default;
        }

        private void MenuLangEN_Click(object sender, EventArgs e) {
            UpdateSettingLanguage(Culture.English);
            Restart();
        }

        private void MenuLangSC_Click(object sender, EventArgs e) {
            UpdateSettingLanguage(Culture.ChineseSimplified);
            Restart();
        }

        private void MenuLangTC_Click(object sender, EventArgs e) {
            UpdateSettingLanguage(Culture.ChineseTraditional);
            Restart();
        }

        private void MenuLangAuto_Click(object sender, EventArgs e) {
            UpdateSettingLanguage();
            Restart();
        }

        private void MenuContentCopy_Click(object sender, EventArgs e) {
            Clipboard.SetText(string.IsNullOrEmpty(lblContent.SelectedText) ? lblContent.Text : lblContent.SelectedText);
        }

        private void PicSearch_Click(object sender, EventArgs e) {
            GetSearchText();
            RefreshData();
        }

        private void GetSearchText() {
            var strSearch = txtSearch.Text;
            if (string.IsNullOrWhiteSpace(strSearch)) {
                txtSearch.Text = string.Empty;
            }
            _searchText = txtSearch.Text;

            var searchType = cmbSearch.SelectedItem?.ToString() ?? string.Empty;
            if (searchType == Strings.Select_All) {
                _searchType = AppEntities.SearchType.All;
            } else if (searchType == Strings.Book_Title) {
                _searchType = AppEntities.SearchType.BookTitle;
            } else if (searchType == Strings.Author) {
                _searchType = AppEntities.SearchType.Author;
            } else if (searchType == Strings.Content) {
                _searchType = AppEntities.SearchType.Content;
            }
        }

        private void CmbSearch_SelectedIndexChanged(object sender, EventArgs e) {
            SetCmbSearchSelection();
        }

        private void SetCmbSearchSelection() {
            var selected = cmbSearch.SelectedItem?.ToString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(selected)) {
                return;
            }
            List<string> list = [];
            if (selected.Equals(Strings.Book_Title)) {
                list.AddRange(_clippingService.GetClippingsBookTitleList());
            } else if (selected.Equals(Strings.Author)) {
                list.AddRange(_clippingService.GetClippingsAuthorList());
            } else if (selected.Equals(Strings.Vocabulary)) {
                list.AddRange(_vocabService.GetVocabWordList());
            } else if (selected.Equals(Strings.Stem)) {
                list.AddRange(_vocabService.GetVocabStemList());
            } else {
                list.AddRange(_clippingService.GetClippingsBookTitleList());
                list.AddRange(_clippingService.GetClippingsAuthorList());
                list.AddRange(_vocabService.GetVocabWordList());
                list.AddRange(_vocabService.GetVocabStemList());
            }
            var autoCompleteStringCollection = new AutoCompleteStringCollection();
            autoCompleteStringCollection.AddRange([.. list]);
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
            Process.Start(AppConstants.ExplorerFileName, Path.Combine(_programPath, AppConstants.ExportsPathName));
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
                DeleteNodes();
            }
        }

        private void lblContent_MouseDown(object sender, MouseEventArgs e) {
            HideCaret(sender);
        }

        private static void HideCaret(object? sender) {
            if (sender == null) {
                return;
            }
            Control? control = sender as Control ?? null;
            if (control == null) {
                return;
            }
            HideCaret(control.Handle);
            DestroyCaret();
        }

        private void menuSyncToKindle_Click(object sender, EventArgs e) {
            SyncToKindle();
        }

        private void SyncToKindle() {
            DialogResult dialogResult = MessageBox(Strings.Confirm_Sync_To_Kindle, Strings.Confirm, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (dialogResult != DialogResult.Yes) {
                return;
            }
            try {
                var backupClippingsPath = Path.Combine(_backupPath, "MyClippings_" + DateTimeHelper.GetCurrentTimestamp() + ".txt");
                var backupWordsPath = Path.Combine(_backupPath, "vocab_" + DateTimeHelper.GetCurrentTimestamp() + ".db");
                if (!ImportFilesFromDevice(backupClippingsPath, backupWordsPath, out Exception exception) || !_originalClippingLineService.Export(backupClippingsPath, AppConstants.ClippingsFileName, out exception)) {
                    throw exception;
                }
                var exportedClippingsPath = Path.Combine(_tempPath, AppConstants.ClippingsFileName);
                var documentPath = Path.Combine(_driveLetter, AppConstants.DocumentsPathName);
                switch (_deviceType) {
                    case Device.Type.USB:
                        File.Copy(exportedClippingsPath, Path.Combine(documentPath, AppConstants.ClippingsFileName), true);
                        MessageBox(Strings.Sync_Successful, Strings.Successful, MessageBoxButtons.OK, MessageBoxIcon.Information);
                        break;
                    case Device.Type.MTP: {
                        var devices = MediaDevice.GetDevices();
                        using MediaDevice? device = devices.First(d =>
                            d.FriendlyName.Contains(AppConstants.Kindle, StringComparison.InvariantCultureIgnoreCase) || d.Model.Contains(AppConstants.Kindle, StringComparison.InvariantCultureIgnoreCase));
                        device.Connect();
                        WriteMtpFile(device, documentPath, AppConstants.ClippingsFileName, exportedClippingsPath);
                        device.Disconnect();
                        MessageBox(Strings.Sync_Successful, Strings.Successful, MessageBoxButtons.OK, MessageBoxIcon.Information);
                        break;
                    }
                    case Device.Type.Unknown:
                    default:
                        break;
                }
            } catch (Exception ex) {
                Console.WriteLine(ex);
                _ = MessageBox(MessageHelper.BuildMessage(Strings.Sync_Failed, ex), Strings.Error, MessageBoxButtons.OK, MsgIcon.Error);
            }
        }

        private void UpdateSettingLanguage(string lang = "") {
            _settingService.UpdateSetting(new Setting {
                Name = AppConstants.SettingLanguage,
                value = lang
            });
        }

        private DialogResult MessageBox(string message, string title, MessageBoxButtons buttons, MessageBoxIcon icon) {
            return Messenger.MessageBox(message, title, buttons, icon, _isDarkTheme);
        }

        private DialogResult MessageBox(string message, string title, MessageBoxButtons buttons, MsgIcon icon) {
            return Messenger.MessageBox(message, title, buttons, icon, _isDarkTheme);
        }
    }
}