using KindleMate2.Application.Services.KM2DB;
using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.UI.Commands;
using KindleMate2.UI.ViewModels.TreeViews;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using KindleMate2.Shared.Constants;

namespace KindleMate2.UI.ViewModels {
    using Application = System.Windows.Application;

    public class MainViewModel : BaseViewModel {
        private readonly ClippingService _clippingService;
        private readonly LookupService _lookupService;

        // Books tree
        public ObservableCollection<BooksTreeItemViewModel> BooksTree { get; set; } = [];

        // Vocabulary tree
        public ObservableCollection<VocabularyTreeItemViewModel> VocabularyTree { get; set; } = [];

        private int _totalCount;

        public int TotalCount {
            get => _totalCount;
            set {
                if (_totalCount != value) {
                    _totalCount = value;
                    OnPropertyChanged(nameof(TotalCount));
                }
            }
        }

        public ICommand CmdRefresh { get; }
        public ICommand CmdRestart { get; }
        public ICommand CmdExit { get; }
        public ICommand CmdGitHubRepo { get; }

        public MainViewModel(ClippingService clippingService, LookupService lookupService) {
            _clippingService = clippingService;
            _lookupService = lookupService;

            CmdRestart = new RelayCommand(_ => Refresh());
            CmdRestart = new RelayCommand(_ => Restart());
            CmdExit = new RelayCommand(_ => ExitApplication());

            LoadSampleBookTree();
            CmdGitHubRepo = new RelayCommand(_ => OpenGitHubRepo());
        }

        private static void Refresh() { }

        private static void Restart() {
            var caption = Application.Current.Resources["DialogRestart_Caption"] as string ?? "Exit";
            var message = Application.Current.Resources["DialogRestart_Message"] as string ?? "Are you sure?";

            MessageBoxResult result = MessageBox.Show(message, caption, MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes) {
                var exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
                if (exePath != null) {
                    Process.Start(exePath);
                    Application.Current.Shutdown();
                }
            }
        }

        private static void ExitApplication() {
            var caption = Application.Current.Resources["DialogExit_Caption"] as string ?? "Exit";
            var message = Application.Current.Resources["DialogExit_Message"] as string ?? "Are you sure?";

            MessageBoxResult result = MessageBox.Show(message, caption, MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes) {
                Application.Current.Shutdown();
            }
        }

        private void LoadSampleBookTree() {
            var booksRootNode = new BooksTreeItemViewModel {
                Name = "Book A",
                Key = "1"
            };
            BooksTree.Add(booksRootNode);

            var listClippings = _clippingService.GetAllClippings();
            var listBooks = listClippings.Where(c => !string.IsNullOrWhiteSpace(c.BookName)).GroupBy(c => c.BookName).Select(group => group.OrderByDescending(c => c.PageNumber).First()).OrderBy(c => c.BookName).ToList();


            foreach (Clipping book in listBooks) {
                var name = book.BookName;
                if (!string.IsNullOrWhiteSpace(name)) {
                    var bookNode = new BooksTreeItemViewModel {
                        Name = name,
                        Key = book.Key
                    };
                    BooksTree.Add(bookNode);
                }
            }
        }

        private static void OpenGitHubRepo() {
            OpenUrl(AppConstants.RepoUrl);
        }

        private static void OpenUrl(string url) {
            try {
                Process.Start(new ProcessStartInfo {
                    FileName = url,
                    UseShellExecute = true
                });
            } catch (Exception) {
                Clipboard.SetText(url);
                //MessageBox(Strings.Repo_URL_Copied, Strings.Prompt, MessageBoxButtons.OK, MessageBoxIcon.Information);
                //Messenger.Send(new ShowDialogMessage("Delete Item", "Are you sure?"));
            }
        }
    }
}