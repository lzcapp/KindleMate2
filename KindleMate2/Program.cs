using static System.Windows.Forms.Application;

namespace KindleMate2 {
    internal static class Program {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        private static void Main() {
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            EnableVisualStyles();
            SetCompatibleTextRenderingDefault(false);
            //ApplicationConfiguration.Initialize();
            Run(new FrmMain());
        }
    }
}