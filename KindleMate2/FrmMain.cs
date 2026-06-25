using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using AutoUpdaterDotNET;
using DarkModeForms;
using KindleMate2.Application.Services;
using KindleMate2.Application.Services.KM2DB;
using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Infrastructure.Helpers;
using KindleMate2.Properties;
using KindleMate2.Shared;
using KindleMate2.Shared.Constants;
using KindleMate2.Shared.Entities;

namespace KindleMate2 {
    public partial class FrmMain : Form {
        private readonly IClippingService _clippingService;
        private readonly ILookupService _lookupService;
        private readonly IOriginalClippingLineService _originalClippingLineService;
        private readonly ISettingService _settingService;
        private readonly IVocabService _vocabService;
        private readonly IThemeService _themeService;
        private readonly IDatabaseService _databaseService;
        private readonly IKm2DatabaseService _km2DatabaseService;

        private readonly IDeviceManager _deviceManager;
        private readonly IImportManager _importManager;
        private readonly IDataDisplayService _dataDisplayService;
        private readonly IContentDetailService _contentDetailService;
        private readonly IExportManager _exportManager;

        private readonly string _programPath, _tempPath, _backupPath, _importPath, _databaseFilePath, _versionFilePath;

        private string _selectedBook, _selectedWord;
        private string _searchText;
        private AppEntities.SearchType _searchType;
        private int _selectedTreeIndex, _selectedDataGridIndex;
        private bool _isDarkTheme;

        [LibraryImport("User32.dll", EntryPoint = "HideCaret")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial void HideCaret(IntPtr hWnd);

        [LibraryImport("user32.dll", EntryPoint = "DestroyCaretA")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial void DestroyCaret();

        public FrmMain(
            IClippingService clippingService,
            ILookupService lookupService,
            IOriginalClippingLineService originalClippingLineService,
            ISettingService settingService,
            IVocabService vocabService,
            IThemeService themeService,
            IDatabaseService databaseService,
            IKm2DatabaseService km2DatabaseService,
            IDeviceManager deviceManager,
            IImportManager importManager,
            IDataDisplayService dataDisplayService,
            IContentDetailService contentDetailService,
            IExportManager exportManager) {
            InitializeComponent();

            _clippingService = clippingService;
            _lookupService = lookupService;
            _originalClippingLineService = originalClippingLineService;
            _settingService = settingService;
            _vocabService = vocabService;
            _themeService = themeService;
            _databaseService = databaseService;
            _km2DatabaseService = km2DatabaseService;

            _deviceManager = deviceManager;
            _importManager = importManager;
            _dataDisplayService = dataDisplayService;
            _contentDetailService = contentDetailService;
            _exportManager = exportManager;

            _programPath = Environment.CurrentDirectory;
            _databaseFilePath = Path.Combine(_programPath, AppConstants.DatabaseFileName);
            _versionFilePath = Path.Combine(AppConstants.SystemPathName, AppConstants.VersionFileName);
            _tempPath = Path.Combine(_programPath, AppConstants.TempPathName);
            _backupPath = Path.Combine(_programPath, AppConstants.BackupsPathName);
            _importPath = Path.Combine(_programPath, AppConstants.ImportsPathName);

            _selectedBook = Strings.Select_All;
            _selectedWord = Strings.Select_All;
            _searchText = string.Empty;

            dataGridView.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;

            lblContent.GotFocus += LblContent_GotFocus;
            lblContent.LostFocus += LblContent_LostFocus;

            if (!File.Exists(_databaseFilePath)) {
                if (!DatabaseHelper.CreateDatabase(_databaseFilePath, out Exception exception)) {
                    var message = MessageHelper.BuildMessage(Strings.Create_Database_Failed, exception);
                    _ = MessageBox(message, Strings.Error, MessageBoxButtons.OK, MsgIcon.Error);
                    Environment.Exit(0);
                }
            }

            AppDomain.CurrentDomain.ProcessExit += (_, _) => {
                DatabaseHelper.BackupDatabase(_programPath, _backupPath, AppConstants.DatabaseFileName);
            };

            _deviceManager.ConnectionChanged += OnDeviceConnectionChanged;

            SetTheme();
            SetLang();
            SetText();
            SetAutoUpdater();
        }

        private void OnDeviceConnectionChanged(bool isConnected) {
            if (InvokeRequired) {
                Invoke(() => OnDeviceConnectionChanged(isConnected));
                return;
            }
            menuSyncFromKindle.Visible = isConnected;
            menuKindle.Visible = isConnected;
            menuKindle.Enabled = isConnected;
        }

        #region Initialization

        private void SetTheme() {
            _isDarkTheme = _themeService.IsDarkTheme();
            try {
                if (_isDarkTheme) {
                    _ = new DarkModeCS(this, false);
                }
            } catch (Exception e) {
                MessageBox($"Failed to apply dark theme: {e.Message}", Strings.Error, MessageBoxButtons.OK, MsgIcon.Warning);
            } finally {
                menuTheme.Image = _isDarkTheme ? Resources.sun : Resources.new_moon;
            }
        }

        private void SetLang() {
            Setting? cultureSetting = _settingService.GetSettingByName("lang");
            if (cultureSetting == null || string.IsNullOrWhiteSpace(cultureSetting.Value)) {
                return;
            }
            var cultureName = cultureSetting.Value;
            if (!string.IsNullOrWhiteSpace(cultureName)) {
                var culture = new CultureInfo(cultureName);
                Thread.CurrentThread.CurrentUICulture = culture;
                Thread.CurrentThread.CurrentCulture = culture;

                switch (cultureName.ToLowerInvariant()) {
                    case Cultures.English:
                        menuLangEN.Visible = false;
                        break;
                    case Cultures.ChineseSimplified:
                        menuLangSC.Visible = false;
                        break;
                    case Cultures.ChineseTraditional:
                        menuLangTC.Visible = false;
                        break;
                }
            } else {
                menuLangAuto.Visible = false;
                CultureInfo currentCulture = CultureInfo.CurrentCulture;
                if (currentCulture.EnglishName.Contains(nameof(Cultures.English), StringComparison.InvariantCultureIgnoreCase) ||
                    currentCulture.TwoLetterISOLanguageName.Equals(Cultures.English, StringComparison.InvariantCultureIgnoreCase)) {
                    menuLangEN.Visible = false;
                } else if (string.Equals(currentCulture.Name, Cultures.ChineseCN, StringComparison.InvariantCultureIgnoreCase) ||
                           string.Equals(currentCulture.Name, Cultures.ChineseSG, StringComparison.InvariantCultureIgnoreCase) ||
                           string.Equals(currentCulture.Name, Cultures.ChineseMY, StringComparison.InvariantCultureIgnoreCase) ||
                           string.Equals(currentCulture.Name, Cultures.ChineseSimplified, StringComparison.InvariantCultureIgnoreCase)) {
                    menuLangSC.Visible = false;
                } else if (string.Equals(currentCulture.Name, Cultures.ChineseTW, StringComparison.InvariantCultureIgnoreCase) ||
                           string.Equals(currentCulture.Name, Cultures.ChineseHK, StringComparison.InvariantCultureIgnoreCase) ||
                           string.Equals(currentCulture.Name, Cultures.ChineseMO, StringComparison.InvariantCultureIgnoreCase) ||
                           string.Equals(currentCulture.Name, Cultures.ChineseTraditional, StringComparison.InvariantCultureIgnoreCase)) {
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
                            DialogResult resultRestore = MessageBox(Strings.Confirm_Restore_Database, Strings.Confirm,
                                MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                            if (resultRestore == DialogResult.Yes) {
                                File.Copy(backupFilePath, _databaseFilePath, true);
                            }
                            DialogResult resultDeleteBackup = MessageBox(Strings.Confirm_Delete_Backup, Strings.Confirm,
                                MessageBoxButtons.YesNo, MessageBoxIcon.Question);
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

            _deviceManager.StartWatching();
            OnDeviceConnectionChanged(_deviceManager.IsConnected);
        }

        #endregion

        #region Data Refresh & Display

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
                    GetSearchText();
                    _dataDisplayService.QueryData(_searchText, _searchType);
                }

                PopulateTreeViews();
                SetDataGridView();
                SetSelection();
                CountRows();
            } catch (Exception ex) {
                Console.WriteLine($"[RefreshData] {ex}");
                MessageBox(MessageHelper.BuildMessage(Strings.Failed, ex), Strings.Error, MessageBoxButtons.OK, MsgIcon.Error);
            }
        }

        private void PopulateTreeViews() {
            // Books tree
            var books = _dataDisplayService.GetDistinctBookNames();
            treeViewBooks.Nodes.Clear();
            treeViewBooks.Nodes.Add(new TreeNode(Strings.Select_All) { ImageIndex = 2, SelectedImageIndex = 2 });
            if (books.Count != 0) {
                foreach (TreeNode bookNode in books.Select(book => new TreeNode(book) { ToolTipText = book })) {
                    treeViewBooks.Nodes.Add(bookNode);
                }
            }
            treeViewBooks.ExpandAll();

            // Words tree
            var words = _dataDisplayService.GetDistinctWordNames();
            treeViewWords.Nodes.Clear();
            if (words.Count == 0) return;
            treeViewWords.Nodes.Add(new TreeNode(Strings.Select_All) { ImageIndex = 2, SelectedImageIndex = 2 });
            foreach (TreeNode wordNode in words.Select(word => new TreeNode(word) { ToolTipText = word })) {
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
                    SetClippingsGridView();
                    break;
                case 1:
                    SetVocabsGridView();
                    break;
            }
        }

        private void SetClippingsGridView() {
            var clippings = _dataDisplayService.Clippings;
            if (clippings.Count <= 0) return;

            if (string.IsNullOrWhiteSpace(_selectedBook) || _selectedBook.Equals(Strings.Select_All)) {
                _selectedBook = Strings.Select_All;
                dataGridView.DataSource = _dataDisplayService.ClippingsToDataTable();
                ConfigureClippingColumns(showBook: true, showAuthor: true);
                if (dataGridView.Columns.Contains(Columns.ClippingDate))
                    dataGridView.Sort(dataGridView.Columns[Columns.ClippingDate]!, ListSortDirection.Descending);
            } else {
                var filtered = _dataDisplayService.GetClippingsByBook(_selectedBook);
                if (filtered.Count == 0) {
                    ShowBookCountLabel(Strings.Total_Clippings, 0, Strings.X_Clippings, Resources.open_book);
                    return;
                }
                dataGridView.DataSource = _dataDisplayService.ClippingsToDataTable(filtered);
                ShowBookCountLabel(Strings.Total_Clippings, filtered.Count, Strings.X_Clippings, Resources.open_book);
                ConfigureClippingColumns(showBook: false, showAuthor: false);
                if (dataGridView.Columns.Contains(Columns.PageNumber))
                    dataGridView.Sort(dataGridView.Columns[Columns.PageNumber]!, ListSortDirection.Ascending);
            }
        }

        private void ConfigureClippingColumns(bool showBook, bool showAuthor) {
            if (dataGridView.Columns.Contains(Columns.Content)) {
                dataGridView.Columns[Columns.Content]!.HeaderText = Strings.Content;
                dataGridView.Columns[Columns.Content]!.Visible = true;
                dataGridView.Columns[Columns.Content]!.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            }
            if (dataGridView.Columns.Contains(Columns.BookName)) {
                dataGridView.Columns[Columns.BookName]!.HeaderText = Strings.Books;
                dataGridView.Columns[Columns.BookName]!.Visible = showBook;
                if (showBook) dataGridView.Columns[Columns.BookName]!.Width = 100;
            }
            if (dataGridView.Columns.Contains(Columns.AuthorName)) {
                dataGridView.Columns[Columns.AuthorName]!.HeaderText = Strings.Author;
                dataGridView.Columns[Columns.AuthorName]!.Visible = showAuthor;
                if (showAuthor) dataGridView.Columns[Columns.AuthorName]!.Width = 100;
            }
            if (dataGridView.Columns.Contains(Columns.ClippingDate)) {
                dataGridView.Columns[Columns.ClippingDate]!.HeaderText = Strings.Time;
                dataGridView.Columns[Columns.ClippingDate]!.Visible = true;
                dataGridView.Columns[Columns.ClippingDate]!.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            }
            if (dataGridView.Columns.Contains(Columns.PageNumber)) {
                dataGridView.Columns[Columns.PageNumber]!.HeaderText = Strings.Page;
                dataGridView.Columns[Columns.PageNumber]!.Visible = true;
                dataGridView.Columns[Columns.PageNumber]!.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
                dataGridView.Columns[Columns.PageNumber]!.DisplayIndex = 4;
            }
            // Hide internal columns
            foreach (var col in new[] { Columns.Key, Columns.BriefType, Columns.ClippingTypeLocation,
                         Columns.Read, Columns.ClippingImportDate, Columns.Tag, Columns.Sync,
                         Columns.NewBookName, Columns.ColorRgb }) {
                if (dataGridView.Columns.Contains(col)) dataGridView.Columns[col]!.Visible = false;
            }
        }

        private void SetVocabsGridView() {
            var lookups = _dataDisplayService.Lookups;
            if (lookups.Count <= 0) return;

            dataGridView.DataSource = _dataDisplayService.LookupsToDataTable();
            if (dataGridView.Columns.Contains(Columns.Word))
                dataGridView.Columns[Columns.Word]!.DisplayIndex = 0;
            if (dataGridView.Columns.Contains(Columns.Stem))
                dataGridView.Columns[Columns.Stem]!.DisplayIndex = 1;

            if (string.IsNullOrWhiteSpace(_selectedWord) || _selectedWord.Equals(Strings.Select_All)) {
                _selectedWord = Strings.Select_All;
                ConfigureVocabColumns(showWord: true);
            } else {
                var filtered = _dataDisplayService.GetLookupsByWord(_selectedWord);
                if (filtered.Count == 0) {
                    ShowBookCountLabel(Strings.Totally_Vocabs, 0, Strings.X_Lookups, Resources.input_latin_uppercase);
                    return;
                }
                dataGridView.DataSource = _dataDisplayService.LookupsToDataTable(filtered);
                ShowBookCountLabel(Strings.Totally_Vocabs, filtered.Count, Strings.X_Lookups, Resources.input_latin_uppercase);
                ConfigureVocabColumns(showWord: false);
            }

            if (dataGridView.Columns.Contains(Columns.Timestamp))
                dataGridView.Sort(dataGridView.Columns[Columns.Timestamp]!, ListSortDirection.Descending);
        }

        private void ConfigureVocabColumns(bool showWord) {
            if (dataGridView.Columns.Contains(Columns.Word)) {
                dataGridView.Columns[Columns.Word]!.HeaderText = Strings.Vocabulary;
                dataGridView.Columns[Columns.Word]!.Visible = showWord;
                dataGridView.Columns[Columns.Word]!.AutoSizeMode = showWord
                    ? DataGridViewAutoSizeColumnMode.AllCells
                    : DataGridViewAutoSizeColumnMode.Fill;
            }
            if (dataGridView.Columns.Contains(Columns.Stem)) {
                dataGridView.Columns[Columns.Stem]!.HeaderText = Strings.Stem;
                dataGridView.Columns[Columns.Stem]!.Visible = true;
                dataGridView.Columns[Columns.Stem]!.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            }
            if (dataGridView.Columns.Contains(Columns.Frequency)) {
                dataGridView.Columns[Columns.Frequency]!.HeaderText = Strings.Frequency;
                dataGridView.Columns[Columns.Frequency]!.Visible = showWord;
                dataGridView.Columns[Columns.Frequency]!.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            }
            if (dataGridView.Columns.Contains(Columns.Usage)) {
                dataGridView.Columns[Columns.Usage]!.HeaderText = Strings.Content;
                dataGridView.Columns[Columns.Usage]!.Visible = true;
                dataGridView.Columns[Columns.Usage]!.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            }
            if (dataGridView.Columns.Contains(Columns.Title)) {
                dataGridView.Columns[Columns.Title]!.HeaderText = Strings.Books;
                dataGridView.Columns[Columns.Title]!.Visible = false;
            }
            if (dataGridView.Columns.Contains(Columns.Authors)) {
                dataGridView.Columns[Columns.Authors]!.HeaderText = Strings.Author;
                dataGridView.Columns[Columns.Authors]!.Visible = false;
            }
            if (dataGridView.Columns.Contains(Columns.Timestamp)) {
                dataGridView.Columns[Columns.Timestamp]!.HeaderText = Strings.Time;
                dataGridView.Columns[Columns.Timestamp]!.Visible = true;
                dataGridView.Columns[Columns.Timestamp]!.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            }
            if (dataGridView.Columns.Contains(Columns.WordKey))
                dataGridView.Columns[Columns.WordKey]!.Visible = false;
        }

        private void ShowBookCountLabel(string prefix, int count, string suffix, Image? icon) {
            lblBookCount.Text = prefix + Strings.Space + count + Strings.Space + suffix;
            lblBookCount.Image = icon;
            lblBookCount.Visible = true;
        }

        private void HideBookCountLabel() {
            lblBookCount.Text = string.Empty;
            lblBookCount.Image = null;
            lblBookCount.Visible = false;
        }

        private void CountRows() {
            lblCount.Text = _dataDisplayService.GetStatusText(tabControl.SelectedIndex);
        }

        private void SetSelection() {
            if (dataGridView.Rows.Count <= 0) return;

            var index = tabControl.SelectedIndex;

            if (_selectedDataGridIndex < 0 || _selectedDataGridIndex >= dataGridView.Rows.Count)
                _selectedDataGridIndex = 0;

            switch (index) {
                case 0:
                    if (_selectedTreeIndex < 0 || _selectedTreeIndex >= treeViewBooks.Nodes.Count)
                        _selectedTreeIndex = 0;
                    treeViewBooks.SelectedNode = treeViewBooks.Nodes[_selectedTreeIndex];
                    dataGridView.FirstDisplayedScrollingRowIndex = _selectedDataGridIndex;
                    dataGridView.Rows[_selectedDataGridIndex].Selected = true;
                    var selectedRow = dataGridView.SelectedRows[0];
                    lblBook.Text = selectedRow.Cells[Columns.BookName].Value.ToString();
                    var authorName = selectedRow.Cells[Columns.AuthorName].Value.ToString();
                    lblAuthor.Text = authorName != string.Empty ? Strings.Left_Parenthesis + authorName + Strings.Right_Parenthesis : string.Empty;
                    break;
                case 1:
                    if (_selectedTreeIndex < 0 || _selectedTreeIndex >= treeViewWords.Nodes.Count)
                        _selectedTreeIndex = 0;
                    treeViewWords.SelectedNode = treeViewWords.Nodes[_selectedTreeIndex];
                    dataGridView.FirstDisplayedScrollingRowIndex = _selectedDataGridIndex;
                    dataGridView.Rows[_selectedDataGridIndex].Selected = true;
                    break;
            }

            SetCmbSearchSelection();
        }

        #endregion

        #region TreeView Selection Handlers

        private void TreeViewBooks_Select() {
            splitContainerDetail.Panel1Collapsed = false;

            if (string.IsNullOrWhiteSpace(_selectedBook) || _selectedBook.Equals(Strings.Select_All)) {
                _selectedBook = Strings.Select_All;
                HideBookCountLabel();

                var clippings = _dataDisplayService.Clippings;
                if (clippings.Count == 0) {
                    dataGridView.DataSource = null;
                    return;
                }
                dataGridView.DataSource = _dataDisplayService.ClippingsToDataTable();
                dataGridView.Columns[Columns.BookName]!.Visible = true;
                dataGridView.Columns[Columns.AuthorName]!.Visible = true;
                if (dataGridView.Columns.Contains(Columns.ClippingDate))
                    dataGridView.Sort(dataGridView.Columns[Columns.ClippingDate]!, ListSortDirection.Descending);
            } else {
                var filtered = _dataDisplayService.GetClippingsByBook(_selectedBook);
                if (filtered.Count == 0) {
                    ShowBookCountLabel(Strings.Total_Clippings, 0, Strings.X_Clippings, Resources.open_book);
                    dataGridView.DataSource = null;
                    return;
                }
                dataGridView.DataSource = _dataDisplayService.ClippingsToDataTable(filtered);
                ShowBookCountLabel(Strings.Total_Clippings, filtered.Count, Strings.X_Clippings, Resources.open_book);
                dataGridView.Columns[Columns.BookName]!.Visible = false;
                dataGridView.Columns[Columns.BookName]!.HeaderText = Strings.Books;
                dataGridView.Columns[Columns.AuthorName]!.Visible = false;
                dataGridView.Columns[Columns.AuthorName]!.HeaderText = Strings.Author;
                dataGridView.Sort(dataGridView.Columns[Columns.PageNumber]!, ListSortDirection.Ascending);
            }
        }

        private void TreeViewWords_Select() {
            if (string.IsNullOrWhiteSpace(_selectedWord) || _selectedWord.Equals(Strings.Select_All)) {
                _selectedWord = Strings.Select_All;
                splitContainerDetail.Panel1Collapsed = false;
                HideBookCountLabel();

                var lookups = _dataDisplayService.Lookups;
                if (lookups.Count == 0) {
                    dataGridView.DataSource = null;
                    return;
                }
                dataGridView.DataSource = _dataDisplayService.LookupsToDataTable();
                dataGridView.Columns[Columns.Word]!.Visible = true;
                dataGridView.Columns[Columns.Usage]!.Visible = true;
                dataGridView.Columns[Columns.Usage]!.HeaderText = Strings.Content;
                dataGridView.Columns[Columns.Title]!.Visible = true;
                dataGridView.Columns[Columns.Title]!.HeaderText = Strings.Books;
                dataGridView.Columns[Columns.Authors]!.Visible = true;
                dataGridView.Columns[Columns.Authors]!.HeaderText = Strings.Author;
                dataGridView.Columns[Columns.Frequency]!.Visible = true;
                dataGridView.Columns[Columns.Frequency]!.HeaderText = Strings.Frequency;
            } else {
                splitContainerDetail.Panel1Collapsed = true;

                var filtered = _dataDisplayService.GetLookupsByWord(_selectedWord);
                if (filtered.Count == 0) {
                    ShowBookCountLabel(Strings.Totally_Vocabs, 0, Strings.X_Lookups, Resources.input_latin_uppercase);
                    dataGridView.DataSource = null;
                    return;
                }
                dataGridView.DataSource = _dataDisplayService.LookupsToDataTable(filtered);
                ShowBookCountLabel(Strings.Totally_Vocabs, filtered.Count, Strings.X_Lookups, Resources.input_latin_uppercase);
                dataGridView.Columns[Columns.Word]!.Visible = false;
                dataGridView.Columns[Columns.Usage]!.Visible = true;
                dataGridView.Columns[Columns.Usage]!.HeaderText = Strings.Content;
                dataGridView.Columns[Columns.Title]!.Visible = true;
                dataGridView.Columns[Columns.Title]!.HeaderText = Strings.Books;
                dataGridView.Columns[Columns.Authors]!.Visible = true;
                dataGridView.Columns[Columns.Authors]!.HeaderText = Strings.Author;
                dataGridView.Columns[Columns.Frequency]!.Visible = false;
            }
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

        #endregion

        #region DataGridView Event Handlers

        private void DataGridView_SelectionChanged(object sender, EventArgs e) {
            try {
                if (dataGridView.SelectedRows.Count <= 0) return;

                var selectedRow = dataGridView.SelectedRows[0];
                _selectedDataGridIndex = selectedRow.Index;
                var selectedIndex = tabControl.SelectedIndex;

                lblBook.Text = string.Empty;
                lblAuthor.Text = string.Empty;
                lblLocation.Text = string.Empty;
                lblContent.Text = string.Empty;

                switch (selectedIndex) {
                    case 0:
                        DisplayClippingDetail(selectedRow);
                        break;
                    case 1:
                        DisplayVocabDetail(selectedRow);
                        break;
                }
            } catch (Exception ex) {
                Console.WriteLine(ex.Message);
            }
        }

        private void DisplayClippingDetail(DataGridViewRow selectedRow) {
            var bookName = selectedRow.Cells[Columns.BookName].Value.ToString() ?? string.Empty;
            var authorName = selectedRow.Cells[Columns.AuthorName].Value.ToString() ?? string.Empty;
            _ = int.TryParse(selectedRow.Cells[Columns.PageNumber].Value.ToString() ?? string.Empty, out var pageNumber);
            var content = selectedRow.Cells[Columns.Content].Value.ToString()
                ?.Replace(AppConstants.SpaceForNewLine, Environment.NewLine) ?? string.Empty;
            var briefType = selectedRow.Cells[Columns.BriefType].Value.ToString() ?? string.Empty;
            var key = selectedRow.Cells[Columns.Key].Value.ToString() ?? string.Empty;

            var detail = _contentDetailService.BuildClippingDetail(bookName, authorName, pageNumber, content, briefType, key);

            lblBook.Text = detail.BookName;
            lblAuthor.Text = detail.AuthorName != string.Empty
                ? Strings.Left_Parenthesis + detail.AuthorName + Strings.Right_Parenthesis
                : string.Empty;
            lblLocation.Text = Strings.Page_ + Strings.Space + detail.PageNumber + Strings.Space + Strings.X_Page;

            lblContent.Text = string.Empty;
            lblContent.SelectionBullet = false;
            lblContent.AppendText(detail.Content);

            if (detail.IsNote && detail.NoteClippingContent != null) {
                label1.Text = @"[" + Strings.Note + @"]";
                label2.Text = @"[" + Strings.Clipping + @"]";
                label3.Text = detail.NoteClippingContent;
                label1.Visible = true;
                label2.Visible = true;
                label3.Visible = true;
            } else {
                label1.Visible = false;
                label2.Visible = false;
                label3.Visible = false;
            }
        }

        private void DisplayVocabDetail(DataGridViewRow selectedRow) {
            var wordKey = selectedRow.Cells[Columns.WordKey].Value.ToString() ?? string.Empty;
            var word = selectedRow.Cells[Columns.Word].Value.ToString() ?? string.Empty;
            var stem = selectedRow.Cells[Columns.Stem].Value.ToString() ?? string.Empty;
            var frequency = selectedRow.Cells[Columns.Frequency].Value.ToString() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(wordKey) || string.IsNullOrWhiteSpace(word) ||
                string.IsNullOrWhiteSpace(stem) || string.IsNullOrWhiteSpace(frequency)) return;

            var detail = _contentDetailService.BuildVocabDetail(wordKey, word, stem, frequency,
                _dataDisplayService.Lookups, _dataDisplayService.Clippings);

            lblBook.Text = detail.Word;
            lblAuthor.Text = detail.Stem != string.Empty && detail.Stem != detail.Word
                ? Strings.Left_Parenthesis + Strings.Stem + Strings.Symbol_Colon + detail.Stem + Strings.Space + Strings.Right_Parenthesis
                : string.Empty;

            // Append lookup entries
            foreach (var lines in detail.LookupEntries.Select(line => line.Split(Environment.NewLine))) {
                for (var i = 0; i < lines.Length; i++) {
                    lblContent.SelectionBullet = i == 0;
                    lblContent.AppendText(lines[i].Trim() + Environment.NewLine);
                }
                lblContent.SelectionBullet = false;
            }
            lblContent.SelectionBullet = false;
            lblContent.AppendText(Environment.NewLine);

            // Append clipping entries
            foreach (var lines in detail.ClippingEntries.Select(line => line.Split(Environment.NewLine))) {
                for (var i = 0; i < lines.Length; i++) {
                    lblContent.SelectionBullet = i == 0;
                    lblContent.AppendText(lines[i].Trim() + Environment.NewLine);
                }
                lblContent.SelectionBullet = false;
            }
            lblContent.SelectionBullet = false;

            // Highlight the word
            if (word != null) {
                var index = 0;
                while (index < lblContent.TextLength) {
                    var wordStartIndex = lblContent.Find(word, index, RichTextBoxFinds.None);
                    if (wordStartIndex == -1) break;
                    lblContent.Select(wordStartIndex, word.Length);
                    lblContent.SelectionFont = new Font(lblContent.Font, FontStyle.Bold | FontStyle.Underline);
                    index = wordStartIndex + word.Length;
                }
            }

            // Italicize book titles
            foreach (var book in detail.BookTitles) {
                var index = 0;
                while (index < lblContent.TextLength) {
                    var wordStartIndex = lblContent.Find(book, index, RichTextBoxFinds.None);
                    if (wordStartIndex == -1) break;
                    lblContent.Select(wordStartIndex, book.Length);
                    lblContent.SelectionFont = new Font(lblContent.Font, FontStyle.Italic);
                    index = wordStartIndex + book.Length;
                }
            }

            lblLocation.Text = Strings.Frequency + Strings.Symbol_Colon + detail.Frequency + Strings.Space + Strings.X_Times;
            lblLocation.Text += Strings.Symbol_Comma + detail.LookupEntries.Count + Strings.Space + Strings.X_Lookups;
            ShowBookCountLabel(Strings.Totally_Vocabs, detail.LookupEntries.Count, Strings.X_Lookups, Resources.input_latin_uppercase);
            if (detail.ClippingEntries.Count > 0) {
                lblLocation.Text += Strings.Symbol_Comma + detail.ClippingEntries.Count + Strings.Space + Strings.X_Clippings;
                lblBookCount.Text += Strings.Symbol_Comma + Strings.Totally_Other_Books + Strings.Space + detail.ClippingEntries.Count +
                                     Strings.Space + Strings.X_Other_Books;
            }

            label1.Visible = false;
            label2.Visible = false;
            label3.Visible = false;
        }

        private void DataGridView_CellDoubleClick(object sender, DataGridViewCellEventArgs e) {
            if (e.RowIndex <= -1 || e.ColumnIndex <= -1 || e.ColumnIndex >= dataGridView.Columns.Count) return;

            var index = tabControl.SelectedIndex;
            var columnName = dataGridView.Columns[e.ColumnIndex].HeaderText;

            switch (index) {
                case 0:
                    if (columnName.Equals(Strings.Books)) {
                        _selectedBook = dataGridView.Rows[e.RowIndex].Cells[Columns.BookName].Value.ToString()!;
                        var clippings = _dataDisplayService.GetClippingsByBook(_selectedBook);
                        if (clippings.Count == 0) {
                            ShowBookCountLabel(Strings.Total_Clippings, 0, Strings.X_Clippings, Resources.open_book);
                            dataGridView.DataSource = null;
                            RefreshData();
                            return;
                        }
                        dataGridView.DataSource = _dataDisplayService.ClippingsToDataTable(clippings);
                        ShowBookCountLabel(Strings.Total_Clippings, clippings.Count, Strings.X_Clippings, Resources.open_book);
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
                        var lookups = _dataDisplayService.GetLookupsByWord(_selectedWord);
                        if (lookups.Count == 0) {
                            ShowBookCountLabel(Strings.Total_Clippings, 0, Strings.X_Clippings, Resources.open_book);
                            RefreshData();
                            return;
                        }
                        dataGridView.DataSource = _dataDisplayService.LookupsToDataTable(lookups);
                        ShowBookCountLabel(Strings.Total_Clippings, lookups.Count, Strings.X_Clippings, Resources.open_book);
                        dataGridView.Columns[Columns.Usage]!.Visible = true;
                        dataGridView.Columns[Columns.Usage]!.HeaderText = Strings.Content;
                        dataGridView.Columns[Columns.Title]!.Visible = true;
                        dataGridView.Columns[Columns.Title]!.HeaderText = Strings.Books;
                        dataGridView.Columns[Columns.Authors]!.Visible = true;
                        dataGridView.Columns[Columns.Authors]!.HeaderText = Strings.Author;
                        dataGridView.Columns[Columns.Frequency]!.Visible = false;
                        dataGridView.Sort(dataGridView.Columns[Columns.Timestamp]!, ListSortDirection.Descending);
                        RefreshData();
                    }
                    break;
            }
        }

        private void DataGridView_CellMouseDown(object sender, DataGridViewCellMouseEventArgs e) {
            if (e.Button != MouseButtons.Right) return;
            if (e.RowIndex <= -1 || e.ColumnIndex <= -1) return;
            dataGridView.CurrentCell = dataGridView.Rows[e.RowIndex].Cells[e.ColumnIndex];
            Point location = dataGridView.PointToClient(Cursor.Position);
            menuClippings.Show(dataGridView, location);
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
                if (e.Button == MouseButtons.Right) {
                    DataGridView.HitTestInfo hitTestInfo = dataGridView.HitTest(e.X, e.Y);
                    if (hitTestInfo.RowIndex >= 0 && !dataGridView.Rows[hitTestInfo.RowIndex].Selected) {
                        dataGridView.ClearSelection();
                        dataGridView.Rows[hitTestInfo.RowIndex].Selected = true;
                    }
                }
            } catch (Exception ex) {
                Console.WriteLine(ex);
            }
        }

        #endregion

        #region Content Edit Dialog

        private void ShowContentEditDialog() {
            if (tabControl.SelectedIndex != 0) return;
            if (dataGridView.SelectedRows.Count <= 0) return;

            var bookName = lblBook.Text;
            var content = lblContent.Text;

            var fields = new List<KeyValue> {
                new(Strings.Content, content, KeyValue.ValueTypes.Multiline)
            };

            Messenger.ValidateControls += [SuppressMessage("ReSharper", "AccessToModifiedClosure")](_, e) => {
                if (fields == null) return;
                var fValue = fields[0].Value;
                if (string.IsNullOrWhiteSpace(fValue)) e.Cancel = true;
            };

            var key = dataGridView.SelectedRows[0].Cells[Columns.Key].Value.ToString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(key)) return;

            if (Messenger.InputBox(Strings.Edit_Clippings + Strings.Space + bookName, string.Empty, ref fields,
                    MsgIcon.Edit, MessageBoxButtons.OKCancel, _isDarkTheme) != DialogResult.OK) return;

            var dialogContent = fields[0].Value.Trim();
            if (string.IsNullOrWhiteSpace(dialogContent)) return;
            if (dialogContent.Equals(content)) return;

            Clipping? clipping = _clippingService.GetClippingByKey(key);
            if (clipping == null) return;

            clipping.Content = dialogContent;
            if (_clippingService.UpdateClipping(clipping)) {
                MessageBox(Strings.Clippings_Revised, Strings.Successful, MessageBoxButtons.OK, MessageBoxIcon.Information);
            } else {
                MessageBox(Strings.Clippings_Revised_Failed, Strings.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            RefreshData();
        }

        #endregion

        #region Delete Operations

        private void DeleteRow() {
            if (dataGridView.SelectedRows.Count <= 0) return;

            var index = tabControl.SelectedIndex;
            switch (index) {
                case 0:
                    DeleteClippingRows();
                    break;
                case 1:
                    DeleteLookupRows();
                    break;
            }

            if (_selectedDataGridIndex >= 1) _selectedDataGridIndex -= 1;
            RefreshData();
        }

        private void DeleteClippingRows() {
            DialogResult result = MessageBox(Strings.Confirm_Delete_Selected_Clippings, Strings.Confirm,
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result != DialogResult.Yes) return;

            try {
                foreach (DataGridViewRow row in dataGridView.SelectedRows) {
                    var key = row.Cells[Columns.Key].Value.ToString() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(key)) return;
                    if (_clippingService.DeleteClipping(key)) {
                        dataGridView.Rows.Remove(row);
                    } else {
                        MessageBox(Strings.Delete_Failed, Strings.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            } catch (Exception ex) {
                Console.WriteLine(ex);
            }
        }

        private void DeleteLookupRows() {
            DialogResult result = MessageBox(Strings.Confirm_Delete_Lookups, Strings.Confirm,
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result != DialogResult.Yes) return;

            try {
                foreach (DataGridViewRow row in dataGridView.SelectedRows) {
                    var timestamp = row.Cells[Columns.Timestamp].Value.ToString() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(timestamp)) continue;
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
        }

        private void DeleteNodes() {
            var index = tabControl.SelectedIndex;
            switch (index) {
                case 0:
                    DeleteBookNodes();
                    break;
                case 1:
                    DeleteWordNodes();
                    break;
            }
            RefreshData();
        }

        private void DeleteBookNodes() {
            if (treeViewBooks.SelectedNode is null) return;

            if (treeViewBooks.SelectedNode.Text.Equals(Strings.Select_All)) {
                var result = MessageBox(Strings.Confirm_Clear_Clippings, Strings.Confirm,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result == DialogResult.Yes) _clippingService.DeleteAllClippings();
            } else {
                var result = MessageBox(Strings.Confirm_Delete_Clippings_Book, Strings.Confirm,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result == DialogResult.Yes) {
                    var bookname = treeViewBooks.SelectedNode.Text;
                    var clippings = _clippingService.GetClippingsByBookName(bookname);
                    var deletedCount = clippings.Count(clipping => _clippingService.DeleteClipping(clipping.Key));
                    if (deletedCount == 0)
                        MessageBox(Strings.Delete_Failed, Strings.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    _selectedBook = Strings.Select_All;
                }
            }
        }

        private void DeleteWordNodes() {
            if (treeViewWords.SelectedNode is null) return;

            if (treeViewWords.SelectedNode.Text.Equals(Strings.Select_All)) {
                var result = MessageBox(Strings.Confirm_Clear_Vocabulary, Strings.Confirm,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result == DialogResult.Yes) _vocabService.DeleteAllVocabs();
            } else {
                var result = MessageBox(Strings.Confirm_Delete_Lookups_Vocabs, Strings.Confirm,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result == DialogResult.Yes) {
                    var word = treeViewWords.SelectedNode.Text;
                    var wordKey = string.Empty;
                    foreach (Vocab vocab in _dataDisplayService.Vocabs) {
                        if (vocab.Word != word) continue;
                        wordKey = vocab.WordKey;
                        break;
                    }
                    if (wordKey != null && !_vocabService.DeleteVocabByWordKey(wordKey) && !_lookupService.DeleteLookup(wordKey)) {
                        MessageBox(Strings.Delete_Failed, Strings.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    _selectedWord = Strings.Select_All;
                }
            }
        }

        #endregion

        #region Copy Operations

        private void MenuClippingCopy_Click(object sender, EventArgs e) {
            if (dataGridView.SelectedRows.Count <= 0) return;

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

        private void MenuContentCopy_Click(object sender, EventArgs e) {
            Clipboard.SetText(string.IsNullOrEmpty(lblContent.SelectedText) ? lblContent.Text : lblContent.SelectedText);
        }

        #endregion

        #region Rename Operations

        private void MenuRename_Click(object sender, EventArgs e) {
            if (tabControl.SelectedIndex != 0) return;
            if (treeViewBooks.SelectedNode == null || treeViewBooks.SelectedNode.Text.Equals(Strings.Select_All)) return;

            var bookName = treeViewBooks.SelectedNode.Text;
            var authorName = GetAuthorNameFromClippings(bookName);
            ShowBookRenameDialog(bookName, authorName);
        }

        private string GetAuthorNameFromClippings(string bookName) {
            foreach (Clipping row in _dataDisplayService.Clippings) {
                if (row.BookName != null && !row.BookName.Equals(bookName)) continue;
                return row.AuthorName ?? string.Empty;
            }
            return string.Empty;
        }

        private string GetBooknameFromContent() {
            return !string.IsNullOrWhiteSpace(lblBook.Text) ? lblBook.Text :
                dataGridView.Rows[0].Cells[Columns.BookName].Value.ToString() ?? string.Empty;
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

        private void ShowBookRenameDialog(string bookName, string authorName) {
            var fields = new List<KeyValue> {
                new(Strings.Book_Title, bookName),
                new(Strings.Author, authorName)
            };

            Messenger.ValidateControls += [SuppressMessage("ReSharper", "AccessToModifiedClosure")](_, e) => {
                if (fields == null) return;
                var dialogBook = fields[0].Value;
                var dialogAuthor = fields[1].Value;
                if (string.IsNullOrWhiteSpace(dialogBook) || string.IsNullOrWhiteSpace(dialogAuthor)) e.Cancel = true;
            };

            if (Messenger.InputBox(Strings.Rename, string.Empty, ref fields, MsgIcon.Edit,
                    MessageBoxButtons.OKCancel, _isDarkTheme) != DialogResult.OK) return;

            var dialogBook = fields[0].Value.Trim();
            var dialogAuthor = fields[1].Value.Trim();

            if (string.IsNullOrWhiteSpace(dialogBook)) return;
            if (!string.IsNullOrWhiteSpace(authorName) && string.IsNullOrWhiteSpace(dialogAuthor))
                dialogAuthor = authorName;
            if (bookName == dialogBook && authorName == dialogAuthor) {
                MessageBox(Strings.Books_Title_Not_Changed, Strings.Prompt, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (_dataDisplayService.Clippings.Any(row => row.BookName == dialogBook)) {
                DialogResult result = MessageBox(Strings.Confirm_Same_Title_Combine, Strings.Confirm,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result != DialogResult.Yes) return;
                var resultRows = _dataDisplayService.Clippings.Where(row => row.BookName == bookName).ToList();
                dialogAuthor = resultRows.Count > 0 ? resultRows[0].AuthorName : string.Empty;
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

        #endregion

        #region Import Menu Handlers

        private void MenuImportKindle_Click(object sender, EventArgs e) {
            var fileDialog = new OpenFileDialog {
                Title = Strings.Import_Kindle_Clipping_File + Strings.Space + @"(" + AppConstants.ClippingsFileName + @")",
                CheckFileExists = true, CheckPathExists = true, DefaultExt = "txt",
                Filter = Strings.Kindle_Clipping_File + Strings.Space + @"(*.txt)|*.txt",
                FilterIndex = 2, RestoreDirectory = true, ReadOnlyChecked = true, ShowReadOnly = true
            };
            if (fileDialog.ShowDialog() != DialogResult.OK) return;

            RunBackgroundTask(
                () => _importManager.ImportKindleClippings(fileDialog.FileName),
                Strings.Successful, Strings.Import_Failed);
        }

        private void MenuImportKindleWords_Click(object sender, EventArgs e) {
            var fileDialog = new OpenFileDialog {
                Title = Strings.Import_Kindle_Vocab_File + Strings.Space + @"(" + AppConstants.VocabFileName + @")",
                CheckFileExists = true, CheckPathExists = true,
                Filter = Strings.Kindle_Vocab_File + Strings.Space + @"(*.db)|*.db",
                FilterIndex = 2, RestoreDirectory = true, ReadOnlyChecked = true, ShowReadOnly = true
            };
            if (fileDialog.ShowDialog() != DialogResult.OK) return;

            RunBackgroundTask(
                () => _importManager.ImportKindleWords(fileDialog.FileName),
                Strings.Successful, Strings.Import_Failed);
        }

        private void MenuImportKindleMate_Click(object sender, EventArgs e) {
            var fileDialog = new OpenFileDialog {
                Title = Strings.Import_Kindle_Mate_Database_File + Strings.Space + @"(" + AppConstants.DatabaseFileName + @")",
                CheckFileExists = true, CheckPathExists = true, DefaultExt = "dat",
                Filter = Strings.Kindle_Mate_Database_File + Strings.Space + @"(*.dat)|*.dat",
                FilterIndex = 2, RestoreDirectory = true, ReadOnlyChecked = true, ShowReadOnly = true
            };
            if (fileDialog.ShowDialog() != DialogResult.OK) return;

            RunBackgroundTask(
                () => _importManager.ImportKmDatabase(fileDialog.FileName),
                Strings.Successful, Strings.Import_Failed);
        }

        private void MenuSyncFromKindle_Click(object sender, EventArgs e) {
            ImportFromKindle();
        }

        private void ImportFromKindle() {
            try {
                var backupClippingsPath = Path.Combine(_backupPath, AppConstants.ImportsPathName);
                var backupWordsPath = Path.Combine(_backupPath, AppConstants.ImportsPathName);

                if (!Directory.Exists(_backupPath)) Directory.CreateDirectory(_backupPath);
                if (!Directory.Exists(backupClippingsPath)) Directory.CreateDirectory(backupClippingsPath);

                var backupClippingsFilePath = Path.Combine(backupClippingsPath,
                    "MyClippings_" + DateTimeHelper.GetCurrentTimestamp() + FileExtension.TXT);
                var backupWordsFilePath = Path.Combine(backupWordsPath,
                    "vocab_" + DateTimeHelper.GetCurrentTimestamp() + FileExtension.DB);

                if (!_deviceManager.ImportFilesFromDevice(backupClippingsFilePath, backupWordsFilePath, out Exception exception))
                    throw exception;

                SetProgressBar(true);
                menuKindle.Enabled = false;
                menuSyncFromKindle.Enabled = false;
                Cursor = Cursors.Default;

                RunBackgroundTask(
                    () => _importManager.Import(backupClippingsFilePath, backupWordsFilePath),
                    Strings.Successful, Strings.Import_Failed,
                    onCompleted: () => {
                        menuKindle.Enabled = true;
                        menuSyncFromKindle.Enabled = true;
                    });
            } catch (Exception ex) {
                MessageBox(MessageHelper.BuildMessage(Strings.Import_Failed, ex), Strings.Failed,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #endregion

        #region Export & Sync Menu Handlers

        private void MenuExportMd_Click(object sender, EventArgs e) {
            var path = Path.Combine(_programPath, AppConstants.ExportsPathName);
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);

            if (!_exportManager.ExportClippingsToMarkdown() || !_exportManager.ExportVocabsToMarkdown()) return;

            ShowExportSuccessDialog();
        }

        private void MenuBooksExport_Click(object sender, EventArgs e) {
            var index = tabControl.SelectedIndex;
            switch (index) {
                case 0:
                    if (treeViewBooks.SelectedNode is null) return;
                    if (!_exportManager.ExportClippingsToMarkdown(treeViewBooks.SelectedNode.Text.Trim())) return;
                    break;
                case 1:
                    if (treeViewWords.SelectedNode is null) return;
                    if (!_exportManager.ExportVocabsToMarkdown(treeViewWords.SelectedNode.Text.Trim())) return;
                    break;
            }
            ShowExportSuccessDialog();
        }

        private void ShowExportSuccessDialog() {
            DialogResult result = MessageBox(Strings.Export_Successful + Strings.Open_Folder, Strings.Successful,
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result == DialogResult.Yes)
                Process.Start(AppConstants.ExplorerFileName, Path.Combine(_programPath, AppConstants.ExportsPathName));
        }

        private void menuSyncToKindle_Click(object sender, EventArgs e) {
            SyncToKindle();
        }

        private void SyncToKindle() {
            DialogResult dialogResult = MessageBox(Strings.Confirm_Sync_To_Kindle, Strings.Confirm,
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (dialogResult != DialogResult.Yes) return;

            try {
                _exportManager.SyncToKindle();
                MessageBox(Strings.Sync_Successful, Strings.Successful, MessageBoxButtons.OK, MessageBoxIcon.Information);
            } catch (Exception ex) {
                Console.WriteLine(ex);
                _ = MessageBox(MessageHelper.BuildMessage(Strings.Sync_Failed, ex), Strings.Error,
                    MessageBoxButtons.OK, MsgIcon.Error);
            }
        }

        #endregion

        #region Database Operations Menu Handlers

        private void MenuBackup_Click(object sender, EventArgs e) {
            _exportManager.BackupDatabase();

            if (_dataDisplayService.Clippings.Count <= 0) {
                MessageBox(Strings.No_Data_To_Backup, Strings.Prompt, MessageBoxButtons.OK, MessageBoxIcon.Information);
            } else {
                if (_exportManager.BackupClippings(out Exception exception)) {
                    DialogResult result = MessageBox(Strings.Backup_Successful + Strings.Open_Folder, Strings.Successful,
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (result == DialogResult.Yes)
                        Process.Start(AppConstants.ExplorerFileName, Path.Combine(_programPath, AppConstants.BackupsPathName));
                } else {
                    MessageBox(MessageHelper.BuildMessage(Strings.Backup_Clippings_Failed, exception),
                        Strings.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void MenuRebuild_Click(object sender, EventArgs e) {
            DialogResult result = MessageBox(Strings.Confirm_Rebuild_Database, Strings.Confirm,
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result != DialogResult.Yes) return;

            RunBackgroundTask(RebuildDatabase, Strings.Rebuild_Database, Strings.Rebuild_Database + Strings.Failed);
        }

        private string RebuildDatabase() {
            if (!_km2DatabaseService.RebuildDatabase(out var result)) return string.Empty;
            var parsedCount = result[AppConstants.ParsedCount];
            var insertedCount = result[AppConstants.InsertedCount];
            return Strings.Parsed_X + Strings.Space + parsedCount + Strings.Space + Strings.X_Clippings +
                   Strings.Symbol_Comma + Strings.Imported_X + Strings.Space + insertedCount + Strings.Space + Strings.X_Clippings;
        }

        private void MenuClean_Click(object sender, EventArgs e) {
            if (_dataDisplayService.Clippings.Count <= 0) {
                MessageBox(Strings.Database_No_Need_Clean, Strings.Prompt, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            RunBackgroundTask(CleanDatabase, Strings.Clean_Database, Strings.Clear_Failed);
        }

        private string CleanDatabase() {
            if (_km2DatabaseService.CleanDatabase(_databaseFilePath, out var result)) {
                var countEmpty = result[AppConstants.EmptyCount];
                var countDuplicated = result[AppConstants.DuplicatedCount];
                var fileSizeDelta = result[AppConstants.FileSizeDelta];
                return Strings.Cleaned + Strings.Space + Strings.Empty_Content + Strings.Space + countEmpty +
                       Strings.Space + Strings.X_Rows + Strings.Symbol_Comma + Strings.Duplicate_Content + Strings.Space +
                       countDuplicated + Strings.Space + Strings.X_Rows + Strings.Symbol_Comma +
                       Strings.Database_Cleaned + Strings.Space + fileSizeDelta;
            }
            var exception = result[AppConstants.Exception];
            Console.WriteLine(exception);
            return exception.Equals(AppConstants.DatabaseNoNeedCleaning) ? Strings.Database_No_Need_Clean : string.Empty;
        }

        private void MenuClear_Click(object sender, EventArgs e) {
            if (_km2DatabaseService.IsDatabaseEmpty()) {
                MessageBox(Strings.Database_Empty, Strings.Prompt, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            DialogResult result = MessageBox(Strings.Confirm_Clear_All_Data, Strings.Confirm,
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result != DialogResult.Yes) return;

            if (_km2DatabaseService.DeleteAllData()) {
                MessageBox(Strings.Data_Cleared, Strings.Successful, MessageBoxButtons.OK, MessageBoxIcon.Information);
            } else {
                MessageBox(Strings.Clear_Failed, Strings.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            Restart();
        }

        #endregion

        #region Search

        private void GetSearchText() {
            var strSearch = txtSearch.Text;
            if (string.IsNullOrWhiteSpace(strSearch)) txtSearch.Text = string.Empty;
            _searchText = txtSearch.Text;

            var searchType = cmbSearch.SelectedItem?.ToString() ?? string.Empty;
            _searchType = searchType switch {
                var s when s == Strings.Book_Title => AppEntities.SearchType.BookTitle,
                var s when s == Strings.Author => AppEntities.SearchType.Author,
                var s when s == Strings.Content => AppEntities.SearchType.Content,
                _ => AppEntities.SearchType.All
            };
        }

        private void PicSearch_Click(object sender, EventArgs e) {
            GetSearchText();
            RefreshData();
        }

        private void TxtSearch_KeyPress(object sender, KeyPressEventArgs e) {
            if (e.KeyChar != (char)Keys.Enter) return;
            e.Handled = true;
            PicSearch_Click(this, e);
        }

        private void TxtSearch_Leave(object sender, EventArgs e) {
            PicSearch_Click(this, e);
        }

        private void CmbSearch_SelectedIndexChanged(object sender, EventArgs e) {
            SetCmbSearchSelection();
        }

        private void SetCmbSearchSelection() {
            var selected = cmbSearch.SelectedItem?.ToString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(selected)) return;

            _searchType = selected switch {
                var s when s == Strings.Book_Title => AppEntities.SearchType.BookTitle,
                var s when s == Strings.Author => AppEntities.SearchType.Author,
                var s when s == Strings.Vocabulary => AppEntities.SearchType.Vocabulary,
                var s when s == Strings.Stem => AppEntities.SearchType.Stem,
                _ => AppEntities.SearchType.All
            };

            var list = _dataDisplayService.GetSearchSuggestions(_searchType);
            var autoCompleteStringCollection = new AutoCompleteStringCollection();
            autoCompleteStringCollection.AddRange([.. list]);
            txtSearch.AutoCompleteSource = AutoCompleteSource.CustomSource;
            txtSearch.AutoCompleteCustomSource = autoCompleteStringCollection;
        }

        #endregion

        #region Theme & Language

        private void MenuTheme_Click(object sender, EventArgs e) {
            _settingService.UpdateSetting(new Setting {
                Name = AppConstants.SettingTheme,
                Value = _isDarkTheme ? Theme.Light : Theme.Dark
            });
            Restart();
        }

        private void MenuLangEN_Click(object sender, EventArgs e) {
            UpdateSettingLanguage(Cultures.English);
            Restart();
        }

        private void MenuLangSC_Click(object sender, EventArgs e) {
            UpdateSettingLanguage(Cultures.ChineseSimplified);
            Restart();
        }

        private void MenuLangTC_Click(object sender, EventArgs e) {
            UpdateSettingLanguage(Cultures.ChineseTraditional);
            Restart();
        }

        private void MenuLangAuto_Click(object sender, EventArgs e) {
            UpdateSettingLanguage();
            Restart();
        }

        private void UpdateSettingLanguage(string lang = "") {
            _settingService.UpdateSetting(new Setting {
                Name = AppConstants.SettingLanguage,
                Value = lang
            });
        }

        #endregion

        #region Simple Menu Handlers & Navigation

        private void MenuRefresh_Click(object sender, EventArgs e) => RefreshData();
        private void MenuBookRefresh_Click(object sender, EventArgs e) => RefreshData();
        private void MenuClippingsRefresh_Click(object sender, EventArgs e) => RefreshData();
        private void MenuClippingDelete_Click(object sender, EventArgs e) => DeleteRow();
        private void MenuBooksDelete_Click(object sender, EventArgs e) => DeleteNodes();
        private void MenuExit_Click(object sender, EventArgs e) => Close();

        private void MenuAbout_Click(object sender, EventArgs e) {
            using var dialog = new FrmAboutBox();
            dialog.ShowDialog();
        }

        private void MenuStatistic_Click(object sender, EventArgs e) {
            if (_dataDisplayService.Clippings.Count <= 0) {
                MessageBox(Strings.Database_Empty, Strings.Prompt, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using var dialog = new FrmStatistics();
            dialog.ShowDialog();
        }

        private void MenuKindle_Click(object sender, EventArgs e) => ImportFromKindle();

        private void MenuRepo_Click(object sender, EventArgs e) => OpenUrl(AppConstants.RepoUrl);

        private void OpenUrl(string url) {
            try {
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            } catch (Exception) {
                Clipboard.SetText(url);
                MessageBox(Strings.Repo_URL_Copied, Strings.Prompt, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void MenuRestart_Click(object sender, EventArgs e) => Restart();

        private static void Restart() {
            Process.Start(new ProcessStartInfo {
                FileName = System.Windows.Forms.Application.ExecutablePath,
                UseShellExecute = true
            });
            Environment.Exit(0);
        }

        private void TabControl_SelectedIndexChanged(object sender, EventArgs e) {
            menuRename.Visible = tabControl.SelectedIndex == 0;
            lblCount.Text = string.Empty;
            lblBookCount.Text = string.Empty;
            RefreshData(false);
        }

        #endregion

        #region Mouse Event Handlers

        private void TreeViewBooks_MouseDown(object sender, MouseEventArgs e) {
            if (e.Button != MouseButtons.Right) return;
            var clickPoint = new Point(e.X, e.Y);
            TreeNode currentNode = treeViewBooks.GetNodeAt(clickPoint);
            if (currentNode == null || currentNode.Text.Equals(Strings.Select_All)) return;
            currentNode.ContextMenuStrip = menuBooks;
            treeViewBooks.SelectedNode = currentNode;
        }

        private void TreeViewWords_MouseDown(object sender, MouseEventArgs e) {
            if (e.Button != MouseButtons.Right) return;
            var clickPoint = new Point(e.X, e.Y);
            TreeNode currentNode = treeViewWords.GetNodeAt(clickPoint);
            if (currentNode == null || currentNode.Text.Equals(Strings.Select_All)) return;
            currentNode.ContextMenuStrip = menuBooks;
            treeViewWords.SelectedNode = currentNode;
        }

        private void TreeViewBooks_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e) {
            if (e.Node.Text.Equals(Strings.Select_All)) return;
            var bookName = e.Node.Text;
            var authorName = GetAuthorNameFromClippings(bookName);
            ShowBookRenameDialog(bookName, authorName);
        }

        private void TreeViewBooks_KeyDown(object sender, KeyEventArgs e) {
            switch (e.KeyCode) {
                case Keys.Delete: DeleteNodes(); break;
                case Keys.Enter: MenuRename_Click(sender, e); break;
            }
        }

        private void TreeViewWords_KeyDown(object sender, KeyEventArgs e) {
            if (e.KeyCode == Keys.Delete) DeleteNodes();
        }

        private void Content_Rename_MouseDoubleClick() {
            if (tabControl.SelectedIndex != 0) return;
            var bookName = GetBooknameFromContent();
            var authorName = GetAuthorNameFromContent();
            ShowBookRenameDialog(bookName, authorName);
        }

        private void LblBook_MouseDoubleClick(object sender, MouseEventArgs e) => Content_Rename_MouseDoubleClick();
        private void LblAuthor_MouseDoubleClick(object sender, MouseEventArgs e) => Content_Rename_MouseDoubleClick();
        private void FlowLayoutPanel_MouseDoubleClick(object sender, MouseEventArgs e) => Content_Rename_MouseDoubleClick();
        private void LblContent_MouseDoubleClick(object sender, MouseEventArgs e) => ShowContentEditDialog();
        private void lblContent_MouseDown(object sender, MouseEventArgs e) => HideCaret(sender);

        private static void LblContent_LostFocus(object? sender, EventArgs e) => HideCaret(sender);
        private static void LblContent_GotFocus(object? sender, EventArgs e) => HideCaret(sender);

        private static void HideCaret(object? sender) {
            if (sender == null) return;
            Control? control = sender as Control ?? null;
            if (control == null) return;
            HideCaret(control.Handle);
            DestroyCaret();
        }

        private void MenuKindle_MouseEnter(object sender, EventArgs e) => Cursor = Cursors.Hand;
        private void MenuKindle_MouseLeave(object sender, EventArgs e) => Cursor = Cursors.Default;
        private void MenuTheme_MouseEnter(object sender, EventArgs e) => Cursor = Cursors.Hand;
        private void MenuTheme_MouseLeave(object sender, EventArgs e) => Cursor = Cursors.Default;

        #endregion

        #region Helpers

        private void RunBackgroundTask(Func<string> work, string successTitle, string failureTitle, Action? onCompleted = null) {
            SetProgressBar(true);
            var bw = new BackgroundWorker();
            bw.DoWork += (_, e) => { e.Result = work(); };
            bw.RunWorkerCompleted += (_, e) => {
                if (e.Result != null && !string.IsNullOrWhiteSpace(e.Result.ToString())) {
                    MessageBox(e.Result.ToString() ?? string.Empty, successTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
                } else {
                    MessageBox(failureTitle, failureTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                SetProgressBar(false);
                onCompleted?.Invoke();
                RefreshData();
            };
            bw.RunWorkerAsync();
        }

        private void SetProgressBar(bool isShow) {
            progressBar.Enabled = isShow;
            progressBar.Visible = isShow;
        }

        private DialogResult MessageBox(string message, string title, MessageBoxButtons buttons, MessageBoxIcon icon) {
            return Messenger.MessageBox(message, title, buttons, icon, _isDarkTheme);
        }

        private DialogResult MessageBox(string message, string title, MessageBoxButtons buttons, MsgIcon icon) {
            return Messenger.MessageBox(message, title, buttons, icon, _isDarkTheme);
        }

        private bool SetAutoUpdater() {
            try {
                var arch = StringHelper.GetRuntimeArchitecture();
                Console.WriteLine($@"Detected architecture: {arch}");

                var updateUrl = arch switch {
                    "x64" => "https://github.lzc.app/KindleMate2/update_x64.xml",
                    "x86" => "https://github.lzc.app/KindleMate2/update_x86.xml",
                    "x64_runtime" => "https://github.lzc.app/KindleMate2/update_x64_runtime.xml",
                    "x86_runtime" => "https://github.lzc.app/KindleMate2/update_x86_runtime.xml",
                    _ => throw new NotSupportedException("Unsupported architecture detected")
                };

                AutoUpdater.Start(updateUrl);
                return true;
            } catch (Exception e) {
                Console.WriteLine(StringHelper.GetExceptionMessage(nameof(SetAutoUpdater), e));
                return false;
            }
        }

        #endregion
    }
}
