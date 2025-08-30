using KindleMate2.Domain.Entities;
using KindleMate2.Domain.Interfaces;
using KindleMate2.Infrastructure.Helpers;

namespace KindleMate2.Application.Services {
    public class ThemeService {
        private readonly ISettingRepository _settingsRepository;

        public ThemeService(ISettingRepository settingsRepository) {
            _settingsRepository = settingsRepository;
        }

        public bool IsDarkTheme() {
            Setting? theme = _settingsRepository.GetByName("theme"); // read from DB
            if (theme != null && !string.IsNullOrWhiteSpace(theme.value)) {
                if (theme.value.Equals("dark", StringComparison.OrdinalIgnoreCase)) return true;
                if (theme.value.Equals("light", StringComparison.OrdinalIgnoreCase)) return false;
            }

            // fallback to OS setting
            var isWindowsDarkTheme = ThemeHelper.IsWindowsDarkTheme();
            _settingsRepository.Add(new Setting {
                name = "theme",
                value = isWindowsDarkTheme ? "dark" : "light"
            });
            return isWindowsDarkTheme;
        }
    }
}
