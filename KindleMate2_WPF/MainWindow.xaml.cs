using System;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;

namespace KindleMate2_WPF {
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow {
        private readonly StaticData _staticData;

        private DataTable _clippingsTable;

        private DataTable _originClippingsDataTable = new();

        private DataTable _vocabulariesDataTable;

        private DataTable _lookupsDataTable = new();

        private readonly string _programsDirectory;

        private readonly string _filePath;

        private string _kindleDrive;

        private readonly string _kindleClippingsPath;

        private readonly string _kindleWordsPath;

        private readonly string _kindleVersionPath;

        private string _selectedBook;

        private string _selectedWord;

        private int _selectedIndex;

        public MainWindow() {
            _staticData = new StaticData();
            
            AppDomain.CurrentDomain.ProcessExit += (_, _) => {
                //BackupDatabase();
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

            _clippingsTable = _staticData.GetClipingsDataTable();
            _vocabulariesDataTable = _staticData.GetVocabDataTable();
            _lookupsDataTable = _staticData.GetLookupsDataTable();

            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) {
            if (File.Exists(_filePath)) {
                BtnClippings.IsChecked = true;
                RadioButton_Click(BtnClippings, null);
            } else {
            }
        }

        private void RefreshData() {
            try {
                
            } catch (Exception) {
                // ignored
            }
        }

        private void RadioButton_Click(object sender, RoutedEventArgs e) {
            if (sender is not RadioButton radioButton) {
                return;
            }
            if (radioButton.IsChecked != true) {
                return;
            }
            var selectedOption = radioButton.Content.ToString();
            switch (selectedOption) {
                case "Clippings":
                    DisplayClippings();
                    break;
                case "Vocabularies":
                    DisplayVocabularies();
                    break;
            }
        }

        private void DisplayClippings() {
            var bookNames = _clippingsTable.AsEnumerable()
                                           .Select(row => row.Field<string>("bookname"))
                                           .Distinct();

            TreeView.Items.Clear();
            var rootNode = new TreeViewItem() {
                Header = "Select All"
            };
            TreeView.Items.Add(rootNode);
            foreach (var bookName in bookNames) {
                TreeViewItem bookNode = new() {
                    Header = bookName
                };
                TreeView.Items.Add(bookNode);
            }

            DataGrid.Columns.Clear();
            foreach (DataColumn column in _clippingsTable.Columns) {
                DataGridTextColumn dataGridColumn = new() {
                    Header = column.ColumnName,
                    Binding = new Binding(column.ColumnName),
                    Width = new DataGridLength(1, DataGridLengthUnitType.Star)
                };
                DataGrid.Columns.Add(dataGridColumn);
                DataGrid.ItemsSource = _clippingsTable.DefaultView;
            }
        }

        private void DisplayVocabularies() {
            var words = _vocabulariesDataTable.AsEnumerable()
                                           .Select(row => row.Field<string>("word"))
                                           .Distinct();

            TreeView.Items.Clear();
            foreach (var word in words) {
                TreeViewItem wordNode = new() {
                    Header = word
                };
                TreeView.Items.Add(wordNode);
            }

            DataGrid.Columns.Clear();
            foreach (DataColumn column in _vocabulariesDataTable.Columns) {
                DataGridTextColumn dataGridColumn = new() {
                    Header = column.ColumnName,
                    Binding = new Binding(column.ColumnName),
                    Width = new DataGridLength(1, DataGridLengthUnitType.Star)
                };
                DataGrid.Columns.Add(dataGridColumn);
                DataGrid.ItemsSource = _vocabulariesDataTable.DefaultView;
            }
        }

        private void DataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            TextContent.Document.Blocks.Clear();
            var selectedRow = (DataRowView)DataGrid.SelectedItem;
            if (selectedRow is null) {
                if (DataGrid.Items.Count > 0) {
                    DataGrid.SelectedItem = DataGrid.Items[0];
                    selectedRow = (DataRowView)DataGrid.SelectedItem;
                } else {
                    return;
                }
            }
            if (IsClippingsTab()) {
                var content = selectedRow["content"].ToString();
                TextContent.AppendText(content);
            } else {
                var word_key = selectedRow["word_key"].ToString();
                var word = selectedRow["word"].ToString();
                var usages = _lookupsDataTable.AsEnumerable().Where(row => row.Field<string>("word_key") == word_key).Select(row => row.Field<string>("usage")).Distinct().ToList();

                var flowDocument = new FlowDocument();
                foreach (Paragraph para in usages.Select(usage => new Paragraph(new Run(usage)))) {
                    flowDocument.Blocks.Add(para);
                }

                TextContent.Document = flowDocument;

                var textRange = new TextRange(TextContent.Document.ContentStart, TextContent.Document.ContentEnd);
                var text = textRange.Text;

                var index = text.IndexOf(word, StringComparison.InvariantCulture);
                if (index == -1) {
                    return;
                }
                TextPointer start = textRange.Start.GetPositionAtOffset(index);
                if (start == null) {
                    return;
                }
                TextPointer end = start.GetPositionAtOffset(word.Length);
                var range = new TextRange(start, end);
                range.ApplyPropertyValue(Inline.TextDecorationsProperty, TextDecorations.Underline);
            }
        }

        private bool IsClippingsTab() {
            return BtnClippings.IsChecked == true;
        }

        private void MenuExit_Click(object sender, RoutedEventArgs e) {
            Environment.Exit(0);
        }

        private void MenuRestart_Click(object sender, RoutedEventArgs e) {
            ProcessModule processModule = Process.GetCurrentProcess().MainModule;
            if (processModule != null) {
                var currentProcess = processModule.FileName;
                Process.Start(currentProcess);
            }
            Environment.Exit(0);
        }

        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e) {
            if (TreeView.SelectedItem is TreeViewItem selectedItem) {
                var selectedNode = selectedItem.Header.ToString();
                var selectedRows = _clippingsTable.AsEnumerable()
                                                  .Where(row => row.Field<string>("bookname") == selectedNode);
            }
        }
    }
}
