using KindleMate2.Application;
using Microsoft.Extensions.DependencyInjection;
using static System.Windows.Forms.Application;

namespace KindleMate2 {
    internal static class Program {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        private static void Main() {
            EnableVisualStyles();
            SetCompatibleTextRenderingDefault(false);

            var services = new ServiceCollection();
            services.AddKindleMateServices();
            services.AddTransient<FrmMain>(); // Form should be transient (created per request)

            using var serviceProvider = services.BuildServiceProvider();
            var mainForm = serviceProvider.GetRequiredService<FrmMain>();
            Run(mainForm);
        }
    }
}
