using System.ComponentModel;
using System.Data;
using System.Data.SQLite;
using System.Diagnostics;
using System.Globalization;
using System.Management;
using System.Text.RegularExpressions;

namespace KindleMate2 {
    public partial class FrmMain : Form {
        private DataTable _dataTable = new();

        private readonly StaticData _staticData = new();

        private readonly string _programsDirectory;

        private readonly string _newFilePath;

        private readonly string _filePath;

        private string _kindleDrive;

        private string _selectedBook;

        private int _selectedIndex;

        public FrmMain() {
            InitializeComponent();

            _programsDirectory = AppDomain.CurrentDomain.BaseDirectory;
            _newFilePath = Path.Combine(_programsDirectory, "KM2.db");
            _filePath = Path.Combine(_programsDirectory, "KM2.dat");
            _kindleDrive = string.Empty;
            _selectedBook = string.Empty;
            _selectedIndex = 0;
        }

        private void FrmMain_Load(object? sender, EventArgs e) {
            if (File.Exists(_filePath)) {
                RefreshData();
            } else {
                DialogResult result = MessageBox.Show("您有Kindle Mate的数据库文件吗？", "确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                switch (result) {
                    case DialogResult.Yes:
                        ImportKMDatabase();

                        break;
                    case DialogResult.No:
                    case DialogResult.None:
                    case DialogResult.OK:
                    case DialogResult.Cancel:
                    case DialogResult.Abort:
                    case DialogResult.Retry:
                    case DialogResult.Ignore:
                    case DialogResult.TryAgain:
                    case DialogResult.Continue:
                    default:
                        if (!string.IsNullOrEmpty(_kindleDrive)) {
                            DialogResult resultKindle = MessageBox.Show("您连接了Kindle设备，需要从Kindle中导入数据吗？", "确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                            if (resultKindle == DialogResult.Yes) {
                                var kindleClippingsPath = Path.Combine(_kindleDrive, "documents", "My Clippings.txt");
                                var kindleWordsPath = Path.Combine(_kindleDrive, "system", "vocabulary", "vocab.db");
                                ImportKindleClippings(kindleClippingsPath);
                                ImportkindleWords(kindleWordsPath);
                                return;
                            }
                        }

                        File.Delete(_filePath);
                        File.Copy(_newFilePath, _filePath, true);

                        return;
                }
            }
        }

        private void ImportkindleWords(string kindleWordsPath) {
            throw new NotImplementedException();
        }

        private void RefreshData() {
            if (dataGridView.CurrentRow is not null) {
                _selectedIndex = dataGridView.CurrentRow.Index;
            }
            DisplayData();
            CountRows();
            SelectRow();
        }

        private void DisplayData() {
            _dataTable = _staticData.GetClipingsDataTable();

            dataGridView.DataSource = _dataTable;

            dataGridView.Columns["key"]!.Visible = false;
            dataGridView.Columns["content"]!.HeaderText = "内容";
            dataGridView.Columns["bookname"]!.HeaderText = "书籍";
            dataGridView.Columns["authorname"]!.HeaderText = "作者";
            dataGridView.Columns["brieftype"]!.Visible = false;
            dataGridView.Columns["clippingtypelocation"]!.HeaderText = "位置";
            dataGridView.Columns["clippingdate"]!.HeaderText = "时间";
            dataGridView.Columns["read"]!.Visible = false;
            dataGridView.Columns["clipping_importdate"]!.Visible = false;
            dataGridView.Columns["tag"]!.Visible = false;
            dataGridView.Columns["sync"]!.Visible = false;
            dataGridView.Columns["newbookname"]!.Visible = false;
            dataGridView.Columns["colorRGB"]!.Visible = false;
            dataGridView.Columns["pagenumber"]!.Visible = false;

            dataGridView.Sort(dataGridView.Columns["clippingdate"]!, ListSortDirection.Descending);

            dataGridView.Columns["content"]!.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            dataGridView.Columns["bookname"]!.Width = 150;
            dataGridView.Columns["authorname"]!.Width = 100;
            dataGridView.Columns["clippingdate"]!.Width = 150;
            dataGridView.Columns["clippingtypelocation"]!.Width = 100;

            var books = _dataTable.AsEnumerable().Select(row => new {
                BookName = row.Field<string>("bookname")
            }).Distinct();

            var rootNodeBooks = new TreeNode("全部") {
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

            var rootNodeWords = new TreeNode("全部") {
                ImageIndex = 2,
                SelectedImageIndex = 2
            };

            treeViewWords.Nodes.Clear();

            treeViewWords.Nodes.Add(rootNodeWords);

            if (string.IsNullOrWhiteSpace(_selectedBook)) {
                treeViewBooks.SelectedNode = rootNodeBooks;
            } else {
                foreach (TreeNode node in treeViewBooks.Nodes) {
                    if (node.Text.Trim().Equals(_selectedBook.Trim())) {
                        treeViewBooks.SelectedNode = node;
                        DataTable filteredBooks = _dataTable.AsEnumerable().Where(row => row.Field<string>("bookname") == _selectedBook).CopyToDataTable();
                        lblBookCount.Text = "|  本书中有 " + filteredBooks.Rows.Count + " 条标注";
                        dataGridView.DataSource = filteredBooks;
                        dataGridView.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
                        dataGridView.Sort(dataGridView.Columns["clippingtypelocation"]!, ListSortDirection.Ascending);
                        break;
                    }
                    treeViewBooks.SelectedNode = rootNodeBooks;
                }
            }
        }

        private void CountRows() {
            var clippingsCount = _staticData.GetClippingsCount();
            var originClippingsCount = _staticData.GetOriginClippingsCount();
            var diff = Math.Abs(originClippingsCount - clippingsCount);
            lblCount.Text = "共 " + clippingsCount + " 条标注，已删除 " + diff + " 条";
        }

        private void ImportKMDatabase() {
            var fileDialog = new OpenFileDialog {
                InitialDirectory = _programsDirectory,
                Title = "导入Kindle Mate数据库文件 (KM2.dat)",
                CheckFileExists = true,
                CheckPathExists = true,
                DefaultExt = "dat",
                Filter = @"Kindle Mate数据库文件 (*.dat)|*.dat",
                FilterIndex = 2,
                RestoreDirectory = true,
                ReadOnlyChecked = true,
                ShowReadOnly = true
            };

            if (fileDialog.ShowDialog() != DialogResult.OK) {
                return;
            }

            var selectedFilePath = fileDialog.FileName;

            var clippingsCount = _staticData.GetClippingsCount();
            var originClippingsCount = _staticData.GetOriginClippingsCount();
            if (clippingsCount <= 0 && originClippingsCount <= 0) {
                File.Copy(selectedFilePath, _filePath, true);
            } else {
                SQLiteConnection connection = new("Data Source=" + selectedFilePath + ";") {
                    Site = null,
                    DefaultTimeout = 0,
                    DefaultMaximumSleepTime = 0,
                    BusyTimeout = 0,
                    WaitTimeout = 0,
                    PrepareRetries = 0,
                    StepRetries = 0,
                    ProgressOps = 0,
                    ParseViaFramework = false,
                    Flags = SQLiteConnectionFlags.None,
                    DefaultDbType = null,
                    DefaultTypeName = null,
                    VfsName = null,
                    TraceFlags = SQLiteTraceFlags.SQLITE_TRACE_NONE
                };

                var clippingsDataTable = new DataTable();
                var originClippingsDataTable = new DataTable();

                connection.Open();

                const string queryClippings = "SELECT * FROM clippings;";
                using var clippingsCommand = new SQLiteCommand(queryClippings, connection);
                using var clippingAdapter = new SQLiteDataAdapter(clippingsCommand);
                clippingAdapter.Fill(clippingsDataTable);

                const string queryOriginClippings = "SELECT * FROM original_clipping_lines;";
                using var originCommand = new SQLiteCommand(queryOriginClippings, connection);
                using var originAdapter = new SQLiteDataAdapter(originCommand);
                originAdapter.Fill(originClippingsDataTable);

                connection.Close();

                var insertedCount = 0;

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

                DisplayData();
                CountRows();

                MessageBox.Show("共解析 " + clippingsDataTable.Rows.Count + " 条记录，导入 " + insertedCount + " 条记录", "解析完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void ImportKindleClippings(string clippingsPath) {
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

            List<string> lines = [.. File.ReadAllLines(clippingsPath)];

            for (var i = 0; i <= lines.Count - 5; i += 5) {
                var line1 = lines[i].Trim();
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

                var line2 = lines[i + 1].Trim();
                var brieftype = 0;
                if (line2.Contains("笔记") || line2.Contains("Note")) {
                    brieftype = 1;
                }
                var loctime = line2.Split('|');
                var location = "";
                var pagenumber = 0;
                var dashIndex = loctime[0].IndexOf('-');
                if (dashIndex != -1 && dashIndex < loctime[0].Length - 1) {
                    location = loctime[0][(dashIndex + 1)..].Trim();
                    var pagePattern = @"第\s+\d+\s+页";
                    Match pageMatch = Regex.Match(location, pagePattern);
                    if (pageMatch.Success) {
                        _ = int.TryParse(pageMatch.Value.Replace("第 ", "").Replace(" 页", "").Trim(), out pagenumber);
                    } else {
                        var lastIndexOfDash = location.LastIndexOf('-');
                        if (lastIndexOfDash != -1) {
                            _ = int.TryParse(location[(lastIndexOfDash + 1)..].Replace("的标注", "").Replace("的笔记", "").Trim(), out pagenumber);
                        }
                        var lastIndexOfDot = location.LastIndexOf('.');
                        if (lastIndexOfDot != -1) {
                            _ = int.TryParse(location[(lastIndexOfDot + 2)..].Trim(), out pagenumber);
                        }
                    }
                }

                var time = "";
                var datetime = loctime[1].Replace("Added on", "").Replace("添加于", "").Trim();
                var lastCommaIndex = datetime.LastIndexOf(',');
                if (lastCommaIndex != -1 && lastCommaIndex < datetime.Length - 1) {
                    datetime = datetime.Substring(lastCommaIndex + 1).Trim();
                    if (DateTime.TryParseExact(datetime, "MMMM dd, yyyy, hh:mm tt", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedDate)) {
                        time = parsedDate.ToString("yyyy-MM-dd HH:mm:ss");
                    }
                } else {
                    var dayOfWeekIndex = datetime.IndexOf("星期", StringComparison.Ordinal);
                    if (dayOfWeekIndex != -1) {
                        datetime = datetime.Remove(dayOfWeekIndex, 3);
                    }

                    if (DateTime.TryParseExact(datetime, "yyyy年M月d日 tth:m:s", CultureInfo.GetCultureInfo("zh-CN"), DateTimeStyles.None, out DateTime parsedDate)) {
                        time = parsedDate.ToString("yyyy-MM-dd HH:mm:ss");
                    }
                }

                var key = time + "|" + location;

                var line3 = lines[i + 2].Trim();

                var line4 = lines[i + 3].Trim();

                var line5 = lines[i + 4].Trim();

                if (string.IsNullOrWhiteSpace(line4)) {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(line4)) {
                    continue;
                }

                originClippingsTable.Rows.Add(key, line1, line2, line3, line4, line5);
                clippingsTable.Rows.Add(key, line4, bookname, authorname, brieftype, location, time, 0, null, null, 0, null, -1, pagenumber);
            }

            if (originClippingsTable.Rows.Count <= 0 || clippingsTable.Rows.Count <= 0) {
                return;
            }

            var insertedOriginCount = (from DataRow row in originClippingsTable.Rows where !_staticData.IsExistOriginalClippings(row["key"].ToString()) select _staticData.InsertOriginClippings(row["key"].ToString() ?? string.Empty, row["line1"].ToString() ?? string.Empty, row["line2"].ToString() ?? string.Empty, row["line3"].ToString() ?? string.Empty, row["line4"].ToString() ?? string.Empty, row["line5"].ToString() ?? string.Empty)).Sum();

            var insertedCount = 0;

            if (insertedOriginCount > 0) {
                insertedCount += (from DataRow row in clippingsTable.Rows where !_staticData.IsExistClippings(row["key"].ToString()) select _staticData.InsertClippings(row["key"].ToString() ?? string.Empty, row["content"].ToString() ?? string.Empty, row["bookname"].ToString() ?? string.Empty, row["authorname"].ToString() ?? string.Empty, (int)row["brieftype"], row["clippingtypelocation"].ToString() ?? string.Empty, row["clippingdate"].ToString() ?? string.Empty, (int)row["read"], row["clipping_importdate"].ToString() ?? string.Empty, row["tag"].ToString() ?? string.Empty, (int)row["sync"], row["newbookname"].ToString() ?? string.Empty, (int)row["colorRGB"], (int)row["pagenumber"])).Sum();

                if (insertedCount > 0) {
                    DisplayData();
                    CountRows();
                }
            }

            MessageBox.Show("共解析 " + originClippingsTable.Rows.Count + " 条记录，导入 " + insertedCount + " 条记录", "解析完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void DataGridView_SelectionChanged(object sender, EventArgs e) {
            DataGridViewRow selectedRow;

            if (dataGridView.CurrentRow is not null) {
                selectedRow = dataGridView.CurrentRow;
            } else {
                return;
            }

            var bookname = selectedRow.Cells["bookname"].Value.ToString();
            var authorname = selectedRow.Cells["authorname"].Value.ToString();
            var clippinglocation = selectedRow.Cells["clippingtypelocation"].Value.ToString();
            var pagenumber = selectedRow.Cells["pagenumber"].Value.ToString();
            var content = selectedRow.Cells["content"].Value.ToString();

            lblBook.Text = bookname;
            if (authorname != string.Empty) {
                lblAuthor.Text = "（" + authorname + "）";
            } else {
                lblAuthor.Text = "";
            }

            lblLocation.Text = clippinglocation + " （第 " + pagenumber + " 页）";
            lblContent.Text = content;
        }

        private void TreeViewBooks_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e) {
            if (e.Node.Text is "Select All" or "全部") {
                _selectedBook = string.Empty;
                lblBookCount.Text = string.Empty;
                dataGridView.DataSource = _dataTable;
                dataGridView.Columns["bookname"]!.Visible = true;
                dataGridView.Columns["authorname"]!.Visible = true;
                dataGridView.Sort(dataGridView.Columns["clippingdate"]!, ListSortDirection.Descending);
            } else {
                var selectedBookName = e.Node.Text;
                _selectedBook = selectedBookName;
                DataTable filteredBooks = _dataTable.AsEnumerable().Where(row => row.Field<string>("bookname") == selectedBookName).CopyToDataTable();
                lblBookCount.Text = "|  本书中有 " + filteredBooks.Rows.Count + " 条标注";
                dataGridView.DataSource = filteredBooks;
                dataGridView.Columns["bookname"]!.Visible = false;
                dataGridView.Columns["authorname"]!.Visible = false;
                dataGridView.Sort(dataGridView.Columns["clippingtypelocation"]!, ListSortDirection.Ascending);
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
                MessageBox.Show("标注修改失败", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            MessageBox.Show("标注已修改", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            RefreshData();
        }

        private void DataGridView_CellDoubleClick(object sender, DataGridViewCellEventArgs e) {
            if (e is not { RowIndex: >= 0, ColumnIndex: >= 0 }) {
                return;
            }
            var columnName = dataGridView.Columns[e.ColumnIndex].HeaderText;
            if (columnName == "书籍") {
                _selectedBook = dataGridView.Rows[e.RowIndex].Cells["bookname"].Value.ToString()!;
                DataTable filteredBooks = _dataTable.AsEnumerable().Where(row => row.Field<string>("bookname") == _selectedBook).CopyToDataTable();
                lblBookCount.Text = "|  本书中有 " + filteredBooks.Rows.Count + " 条标注";
                dataGridView.DataSource = filteredBooks;
                dataGridView.Columns["bookname"]!.Visible = false;
                dataGridView.Columns["authorname"]!.Visible = false;
                dataGridView.Sort(dataGridView.Columns["clippingtypelocation"]!, ListSortDirection.Ascending);
            } else {
                ShowContentEditDialog();
            }
        }

        private void DataGridView_CellMouseDown(object sender, DataGridViewCellMouseEventArgs e) {
            if (e.Button != MouseButtons.Right) {
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

            DialogResult result = MessageBox.Show("确认要删除选中的标注吗？", "确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (result != DialogResult.Yes) {
                return;
            }

            foreach (DataGridViewRow row in dataGridView.SelectedRows) {
                if (_staticData.DeleteClippingsByKey(row.Cells["key"].Value.ToString() ?? string.Empty)) {
                    dataGridView.Rows.Remove(row);
                } else {
                    MessageBox.Show("删除失败", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            RefreshData();
        }

        private void ClippingMenuCopy_Click(object sender, EventArgs e) {
            if (dataGridView.CurrentRow is null) {
                return;
            }

            var content = dataGridView.CurrentRow.Cells["content"].Value.ToString() ?? string.Empty;
            Clipboard.SetText(content != string.Empty ? content : lblContent.Text);
        }

        private void DataGridView_KeyDown(object sender, KeyEventArgs e) {
            if (e.KeyCode != Keys.Enter) {
                return;
            }

            ShowContentEditDialog();
            e.Handled = true;
        }

        private void BooksMenuDelete_Click(object sender, EventArgs e) {
            if (treeViewBooks.SelectedNode is null) {
                return;
            }

            DialogResult result = MessageBox.Show("确认要删除这本书中所有的标注吗？", "确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (result != DialogResult.Yes) {
                return;
            }

            var bookname = treeViewBooks.SelectedNode.Text;
            if (!_staticData.DeleteClippingsByBook(bookname)) {
                MessageBox.Show("删除失败", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            RefreshData();
        }

        private void MenuExit_Click(object sender, EventArgs e) {
            Close();
        }

        private void ToolStripMenuAbout_Click(object sender, EventArgs e) {
            using var dialog = new FrmAboutBox();
            dialog.ShowDialog();
        }

        private void MenuImportKindle_Click(object sender, EventArgs e) {
            var fileDialog = new OpenFileDialog {
                InitialDirectory = _programsDirectory,
                Title = "导入Kindle标注文件 (My Clippings.txt)",
                CheckFileExists = true,
                CheckPathExists = true,
                DefaultExt = "txt",
                Filter = @"Kindle标注文件 (*.txt)|*.txt",
                FilterIndex = 2,
                RestoreDirectory = true,
                ReadOnlyChecked = true,
                ShowReadOnly = true
            };

            if (fileDialog.ShowDialog() != DialogResult.OK) {
                return;
            }

            ImportKindleClippings(fileDialog.FileName);
        }

        private void MenuImportKindleMate_Click(object sender, EventArgs e) {
            ImportKMDatabase();
        }

        private void MenuRepo_Click(object sender, EventArgs e) {
            const string repoUrl = "https://github.com/lzcapp/KindleMate2";
            Process.Start(new ProcessStartInfo {
                FileName = repoUrl,
                UseShellExecute = true
            });
        }

        private bool IsKindleDeviceConnected() {
            ManagementObjectCollection collection;
            using (var searcher = new ManagementObjectSearcher(@"SELECT * FROM Win32_PnPEntity WHERE Caption LIKE '%Kindle%'")) {
                collection = searcher.Get();
            }

            if (collection.Count <= 0) {
                _kindleDrive = string.Empty;
                return false;
            }

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
            ImportKindleClippings();
        }

        private void Timer_Tick(object sender, EventArgs e) {
            if (IsKindleDeviceConnected()) {
                menuSyncFromKindle.Visible = true;
                menuKindle.Visible = true;
            } else {
                menuSyncFromKindle.Visible = false;
                menuKindle.Visible = false;
            }
        }

        private void MenuKindle_Click(object sender, EventArgs e) {
            ImportKindleClippings();
        }

        private void ImportKindleClippings() {
            var kindleClippingsPath = Path.Combine(_kindleDrive, "documents", "My Clippings.txt");
            var kindleWordsPath = Path.Combine(_kindleDrive, "system", "vocabulary", "vocab.db");
            ImportKindleClippings(kindleClippingsPath);
            ImportkindleWords(kindleWordsPath);
        }

        private void MenuRename_Click(object sender, EventArgs e) {
            ShowBookRenameDialog();
        }

        private void ShowBookRenameDialog() {
            using var dialog = new FrmBookRename();
            var bookname = dataGridView.Rows[0].Cells["bookname"].Value.ToString();
            dialog.TxtBook = bookname ?? string.Empty;
            var authorname = dataGridView.Rows[0].Cells["authorname"].Value.ToString();
            dialog.TxtAuthor = authorname ?? string.Empty;
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
                MessageBox.Show("书籍名称未改变", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (_dataTable.AsEnumerable().Any(row => row.Field<string>("BookName") == "dialogBook")) {
                DialogResult result = MessageBox.Show("同名书籍已存在，确认要合并吗？", "确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result != DialogResult.Yes) {
                    return;
                }
                var resultRows = _dataTable.Select($"bookname = '{bookname}'");
                dialogAuthor = resultRows.Length > 0 ? resultRows[0]["authorname"].ToString() : string.Empty;
            }

            if (!_staticData.UpdateClippings(bookname ?? string.Empty, dialogBook, dialogAuthor ?? string.Empty)) {
                MessageBox.Show("书籍重命名失败", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            MessageBox.Show("书籍重命名成功", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            _selectedBook = dialogBook;
            RefreshData();
        }

        private void MenuClippingsRefresh_Click(object sender, EventArgs e) {
            RefreshData();
        }

        private void SelectRow() {
            if (_selectedIndex >= dataGridView.Rows.Count) {
                _selectedIndex = dataGridView.Rows.Count - 1;
            }
            dataGridView.CurrentCell = dataGridView.Rows[_selectedIndex].Cells[1];
            dataGridView.FirstDisplayedScrollingRowIndex = _selectedIndex;
            dataGridView.Rows[_selectedIndex].Selected = true;

            DataGridViewRow selectedRow = dataGridView.SelectedRows[0];

            var bookname = selectedRow.Cells["bookname"].Value.ToString();
            var authorname = selectedRow.Cells["authorname"].Value.ToString();
            var clippinglocation = selectedRow.Cells["clippingtypelocation"].Value.ToString();
            var content = selectedRow.Cells["content"].Value.ToString();

            lblBook.Text = bookname;
            if (authorname != string.Empty) {
                lblAuthor.Text = "（" + authorname + "）";
            } else {
                lblAuthor.Text = "";
            }

            lblLocation.Text = clippinglocation;
            lblContent.Text = content;
        }

        private void MenuBookRefresh_Click(object sender, EventArgs e) {
            RefreshData();
        }

        private void menuCombine_Click(object sender, EventArgs e) {
            ShowCombineDialog();
        }

        private void ShowCombineDialog() {
            var booksList = new List<string>();

            var set = new HashSet<string>();
            var list = _dataTable.AsEnumerable().Select(row => row.Field<string>("bookname")).OfType<string>().Where(set.Add).ToList();
            booksList.AddRange(list);

            var dialog = new FrmCombine {
                BookNames = booksList
            };

            if (dialog.ShowDialog() != DialogResult.OK) {
                return;
            }

            var bookname = dialog.SelectedBookName;

            if (string.IsNullOrWhiteSpace(bookname)) {
                return;
            }

            if (bookname == _selectedBook) {
                MessageBox.Show("不能合并到原书籍", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var resultRows = _dataTable.Select($"bookname = '{bookname}'");
            var authorName = resultRows.Length > 0 ? resultRows[0]["authorname"].ToString() : string.Empty;

            if (!_staticData.UpdateClippings(_selectedBook, bookname, authorName ?? string.Empty)) {
                MessageBox.Show("书籍重命名失败", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            MessageBox.Show("书籍重命名成功", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            _selectedBook = bookname;
            RefreshData();
        }

        private void BackupOriginClippings() {
            DataTable dataTable = _staticData.GetOriginClippingsDataTable();
            dataTable.DefaultView.Sort = "key ASC";
            DataTable sortedDataTable = dataTable.DefaultView.ToTable();

            var filePath = Path.Combine(_programsDirectory, "My Clippings.txt");

            if (File.Exists(filePath)) {
                File.Delete(filePath);
            }

            using StreamWriter writer = new(filePath);
            foreach (DataRow row in sortedDataTable.Rows) {
                writer.WriteLine(row["line1"]);
                writer.WriteLine(row["line2"]);
                writer.WriteLine(row["line3"]);
                writer.WriteLine(row["line4"]);
                writer.WriteLine(row["line5"]);
            }
            writer.Close();

            MessageBox.Show("备份完成", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void MenuBackup_Click(object sender, EventArgs e) {
            BackupOriginClippings();
        }

        private void MenuRefresh_Click(object sender, EventArgs e) {
            RefreshData();
        }

        private void MenuClear_Click(object sender, EventArgs e) {
            DialogResult result = MessageBox.Show("确认要清空全部数据吗？", "确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result != DialogResult.Yes) {
                return;
            }
            File.Delete(_filePath);
            File.Copy(_newFilePath, _filePath, true);
            MessageBox.Show("数据已清空", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}