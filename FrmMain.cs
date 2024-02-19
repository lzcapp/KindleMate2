using System.ComponentModel;
using System.Data;
using System.Globalization;
using static System.Windows.Forms.DataFormats;

namespace KindleMate2 {
    public partial class FrmMain : Form {
        private DataTable _dataTable = new();

        private readonly StaticData _staticData = new();

        private readonly string _programsDirectory;

        private readonly string _filePath;

        public FrmMain() {
            InitializeComponent();

            _programsDirectory = AppDomain.CurrentDomain.BaseDirectory;
            _filePath = Path.Combine(_programsDirectory, "KM2.dat");
        }

        private void FrmMain_Load(object? sender, EventArgs e) {
            if (FileHandler()) {
                DisplayData();

                CountRows();

                var bookNames = _dataTable.AsEnumerable().Select(row => row.Field<string>("bookname")).Distinct();

                var rootNode = new TreeNode("全部") {
                    ImageIndex = 2,
                    SelectedImageIndex = 2
                };

                treeView.Nodes.Add(rootNode);

                foreach (var bookName in bookNames) {
                    rootNode.Nodes.Add(bookName);
                }

                treeView.ExpandAll();
            }
        }

        private void DisplayData() {
            _dataTable = _staticData.GetClipingsDataTable();

            dataGridView.DataSource = _dataTable;

            dataGridView.Columns["key"]!.Visible = false;
            dataGridView.Columns["content"]!.HeaderText = "Content";
            dataGridView.Columns["bookname"]!.HeaderText = "Book";
            dataGridView.Columns["authorname"]!.HeaderText = "Author";
            dataGridView.Columns["brieftype"]!.Visible = false;
            dataGridView.Columns["clippingtypelocation"]!.HeaderText = "Location";
            dataGridView.Columns["clippingdate"]!.HeaderText = "Date";
            dataGridView.Columns["read"]!.Visible = false;
            dataGridView.Columns["clipping_importdate"]!.Visible = false;
            dataGridView.Columns["tag"]!.Visible = false;
            dataGridView.Columns["sync"]!.Visible = false;
            dataGridView.Columns["newbookname"]!.Visible = false;
            dataGridView.Columns["colorRGB"]!.Visible = false;
            dataGridView.Columns["pagenumber"]!.HeaderText = "Page";

            foreach (DataGridViewColumn column in dataGridView.Columns) {
                column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            }

            dataGridView.Sort(dataGridView.Columns["clippingdate"]!, ListSortDirection.Descending);
        }

        private void CountRows() {
            var clippingsCount = _staticData.GetClippingsCount();
            var originClippingsCount = _staticData.GetOriginClippingsCount();
            var diff = Math.Abs(originClippingsCount - clippingsCount);
            lblCount.Text = "共 " + clippingsCount + " 条记录，删除 " + diff + " 条";
        }

        private bool FileHandler() {
            return File.Exists(_filePath) || ImportKMDatabase();
        }

        private bool ImportKMDatabase() {
            var fileDialog = new OpenFileDialog {
                InitialDirectory = _programsDirectory,
                Title = "Import Kindle Mate Data File (KM2.dat)",
                CheckFileExists = true,
                CheckPathExists = true,
                DefaultExt = "dat",
                Filter = @"Kindle Mate Data File (*.dat)|*.dat",
                FilterIndex = 2,
                RestoreDirectory = true,
                ReadOnlyChecked = true,
                ShowReadOnly = true
            };

            if (fileDialog.ShowDialog() != DialogResult.OK) {
                return false;
            }

            var selectedFilePath = fileDialog.FileName;
            try {
                File.Copy(selectedFilePath, _filePath, true);
            } catch (Exception) {
                return false;
            }

            return true;
        }

        private void ImportKindleClippings() {
            var fileDialog = new OpenFileDialog {
                InitialDirectory = _programsDirectory,
                Title = "Import Kindle Clippings File (My Clippings.txt)",
                CheckFileExists = true,
                CheckPathExists = true,
                DefaultExt = "txt",
                Filter = @"Kindle Clippings File (*.txt)|*.txt",
                FilterIndex = 2,
                RestoreDirectory = true,
                ReadOnlyChecked = true,
                ShowReadOnly = true
            };

            if (fileDialog.ShowDialog() != DialogResult.OK) {
                return;
            }

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

            List<string> lines = [.. File.ReadAllLines(fileDialog.FileName)];

            for (var i = 0; i <= lines.Count - 5; i += 5) {
                var line1 = lines[i].Trim();
                var book = line1.Split(['(', '（']);
                var bookname = book[0].Trim();
                var authorname = book[^1].Replace(")", "").Replace("）", "").Trim();

                var line2 = lines[i + 1].Trim();
                var loctime = line2.Split('|');
                var location = loctime[0].Replace("-", "").Trim();
                var pagenumber = 0;
                var dashIndex = location.IndexOf('-');
                if (dashIndex != -1 && dashIndex < location.Length - 1) {
                    _ = int.TryParse(location[(dashIndex + 1)..].Replace("的标注", ""), out pagenumber);
                }
                var time = "";
                var readOnlySpan = loctime[1].Replace("添加于", "").Trim();
                var dayOfWeekIndex = readOnlySpan.IndexOf("星期", StringComparison.Ordinal);
                if (dayOfWeekIndex != -1) {
                    readOnlySpan = readOnlySpan.Remove(dayOfWeekIndex, 3);
                }
                if (DateTime.TryParseExact(readOnlySpan, "yyyy年M月d日 tth:m:s", CultureInfo.GetCultureInfo("zh-CN"), DateTimeStyles.None, out DateTime parsedDate)) {
                    time = parsedDate.ToString("yyyy-MM-dd HH:mm:ss");
                }

                var key = time + "|" + location;

                var line3 = lines[i + 2].Trim();

                var line4 = lines[i + 3].Trim();

                var line5 = lines[i + 4].Trim();

                originClippingsTable.Rows.Add(key, line1, line2, line3, line4, line5);
                clippingsTable.Rows.Add(key, line3, bookname, authorname, 0, location, time, 0, null, null, 0, null, -1, pagenumber);
            }

            var insertedCount = _staticData.InsertOriginClippingsDataTable(originClippingsTable);

            if (insertedCount <= 0) {
                return;
            }

            _staticData.InsertClippingsDataTable(clippingsTable);
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
            lblAuthor.Text = "（" + authorname + "）";
            lblLocation.Text = clippinglocation + " （第" + pagenumber + "页）";
            lblContent.Text = content;
        }

        private void TreeView_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e) {
            if (e.Node.Text is "Select All" or "全部") {
                dataGridView.DataSource = _dataTable;
                dataGridView.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
                dataGridView.Sort(dataGridView.Columns["clippingdate"]!, ListSortDirection.Descending);
            } else {
                var selectedBookName = e.Node.Text;
                DataTable filteredBooks = _dataTable.AsEnumerable().Where(row => row.Field<string>("bookname") == selectedBookName).CopyToDataTable();
                dataGridView.DataSource = filteredBooks;
                dataGridView.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
                dataGridView.Sort(dataGridView.Columns["clippingtypelocation"]!, ListSortDirection.Ascending);
            }
        }

        private void TreeView_MouseDown(object sender, MouseEventArgs e) {
            if (e.Button != MouseButtons.Right) {
                return;
            }

            var clickPoint = new Point(e.X, e.Y);
            TreeNode currentNode = treeView.GetNodeAt(clickPoint);
            if (currentNode == null) {
                return;
            }

            currentNode.ContextMenuStrip = menuBooks;
            treeView.SelectedNode = currentNode;
        }

        private void LblContent_MouseDoubleClick(object sender, MouseEventArgs e) {
            ShowContentEditDialog();
        }

        private void ShowContentEditDialog() {
            using var dialog = new FrmEdit();
            dialog.LblBook = lblBook.Text;
            dialog.TxtContent = lblContent.Text;
            if (dialog.ShowDialog() == DialogResult.OK) {
                lblContent.Text = dialog.TxtContent;
            }
        }

        private void DataGridView_CellDoubleClick(object sender, DataGridViewCellEventArgs e) {
            ShowContentEditDialog();
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

            DialogResult result = MessageBox.Show("Are you sure you want to delete the selected row(s)?", "Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (result != DialogResult.Yes) {
                return;
            }

            foreach (DataGridViewRow row in dataGridView.SelectedRows) {
                if (_staticData.DeleteClippingsByKey(row.Cells["key"].Value.ToString() ?? string.Empty)) {
                    dataGridView.Rows.Remove(row);
                }
            }

            CountRows();
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
            if (treeView.SelectedNode is null) {
                return;
            }

            DialogResult result = MessageBox.Show("Are you sure you want to delete the selected book?", "Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (result != DialogResult.Yes) {
                return;
            }

            var bookname = treeView.SelectedNode.Text;
            if (_staticData.DeleteClippingsByBook(bookname)) {
                CountRows();
            }
        }

        private void MenuExit_Click(object sender, EventArgs e) {
            Close();
        }

        private void ToolStripMenuAbout_Click(object sender, EventArgs e) {
            using var dialog = new FrmAboutBox();
            dialog.ShowDialog();
        }

        private void MenuImportKindle_Click(object sender, EventArgs e) {
            ImportKindleClippings();
        }
    }
}