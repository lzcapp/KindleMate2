using KindleMate2.Application.Services.KM2DB;
using KindleMate2.Infrastructure.Repositories.KM2DB;
using KindleMate2.Shared.Constants;
using KindleMate2.UI.ViewModels;
using System.Windows;

namespace KindleMate2.UI.Views {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow {
        public MainWindow() {
            InitializeComponent();

            var clippingRepository = new ClippingRepository(AppConstants.ConnectionString);
            var clippingService = new ClippingService(clippingRepository);

            var lookupRepository = new LookupRepository(AppConstants.ConnectionString);
            var lookupService = new LookupService(lookupRepository);

            var originalClippingLineRepository = new OriginalClippingLineRepository(AppConstants.ConnectionString);
            var originalClippingLineService = new OriginalClippingLineService(originalClippingLineRepository);

            var settingRepository = new SettingRepository(AppConstants.ConnectionString);
            var settingService = new SettingService(settingRepository);
            var themeService = new ThemeService(settingRepository);

            var vocabRepository = new VocabRepository(AppConstants.ConnectionString);
            var vocabService = new VocabService(vocabRepository);

            var databaseRepository = new DatabaseRepository(AppConstants.ConnectionString);
            var databaseService = new DatabaseService(databaseRepository);

            DataContext = new MainViewModel(clippingService, lookupService);
        }
    }
}