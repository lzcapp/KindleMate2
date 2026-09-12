using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using KindleMate2.Avalonia.Services;
using KindleMate2.Avalonia.ViewModels;
using KindleMate2.Avalonia.Views;

namespace KindleMate2.Avalonia;

public partial class App : global::Avalonia.Application {
    /// <summary>应用级设置(主题 / 语言 / 上次打开的库),在 Initialize 时装载。</summary>
    public AppSettings Settings { get; private set; } = new();

    public override void Initialize() {
        Settings = AppSettings.Load();
        Settings.ApplyCulture();
        AvaloniaXamlLoader.Load(this);
        ApplySavedTheme();
    }

    private void ApplySavedTheme() {
        RequestedThemeVariant = Settings.Theme switch {
            "dark" => ThemeVariant.Dark,
            "light" => ThemeVariant.Light,
            _ => ThemeVariant.Default
        };
    }

    public override void OnFrameworkInitializationCompleted() {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
            desktop.MainWindow = new MainWindow {
                DataContext = new MainWindowViewModel { Settings = Settings }
            };
        }
        base.OnFrameworkInitializationCompleted();
    }
}
