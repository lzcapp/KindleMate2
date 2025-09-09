using KindleMate2.UI.Commands;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;

namespace KindleMate2.UI.ViewModels {
    public class MainViewModel : BaseViewModel {
        public ICommand CmdRefresh { get; }
        public ICommand CmdRestart { get; }
        public ICommand CmdExit { get; }

        public MainViewModel() {
            CmdRestart = new RelayCommand(_ => Refresh());
            CmdRestart = new RelayCommand(_ => Restart());
            CmdExit = new RelayCommand(_ => ExitApplication());
        }

        private static void Refresh() { }

        private static void Restart() {
            var caption = Application.Current.Resources["DialogRestart_Caption"] as string ?? "Exit";
            var message = Application.Current.Resources["DialogRestart_Message"] as string ?? "Are you sure?";

            MessageBoxResult result = MessageBox.Show(message,
                caption,
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
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

            MessageBoxResult result = MessageBox.Show(message,
                caption,
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes) {
                Application.Current.Shutdown();
            }
        }
    }
}
