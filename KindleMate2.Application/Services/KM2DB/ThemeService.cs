using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Domain.Interfaces.KM2DB;
using KindleMate2.Infrastructure.Helpers;

namespace KindleMate2.Application.Services.KM2DB {
    public class ThemeService(ISettingRepository settingsRepository) {
        public bool IsDarkTheme() {
            Setting? theme = settingsRepository.GetByName("theme"); // read from DB
            if (theme != null && !string.IsNullOrWhiteSpace(theme.Value)) {
                if (theme.Value.Equals("dark", StringComparison.OrdinalIgnoreCase)) return true;
                if (theme.Value.Equals("light", StringComparison.OrdinalIgnoreCase)) return false;
            }

            // fallback to OS setting
            var isWindowsDarkTheme = ThemeHelper.IsWindowsDarkTheme();
            settingsRepository.Add(new Setting {
                Name = "theme",
                Value = isWindowsDarkTheme ? "dark" : "light"
            });
            return isWindowsDarkTheme;
        }
    }
}
