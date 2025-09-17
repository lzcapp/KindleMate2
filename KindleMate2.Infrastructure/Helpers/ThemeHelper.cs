using DarkModeForms;

namespace KindleMate2.Infrastructure.Helpers {
    public static class ThemeHelper {
        public static bool IsWindowsDarkTheme() {
            return DarkModeCS.GetWindowsColorMode() <= 0;
        }
    }
}
