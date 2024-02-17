using System.Data.SQLite;

namespace KindleMate2;

public partial class FrmMain : Form {
    public FrmMain() {
        InitializeComponent();
    }

    private void FrmMain_Load(object? sender, EventArgs e) {
        FileHandler();

        var conn = new SQLiteConnection("Data Source=KM2.dat;") {
            Site = null,
            DefaultTimeout = 0,
            DefaultMaximumSleepTime = 0,
            BusyTimeout = 0,
            WaitTimeout = 0,
            PrepareRetries = 0,
            StepRetries = 0,
            ProgressOps = 0,
            ParseViaFramework = false,
            Flags = SQLiteConnectionFlags.None,
            DefaultDbType = null,
            DefaultTypeName = null,
            VfsName = null,
            TraceFlags = SQLiteTraceFlags.SQLITE_TRACE_NONE
        };

        conn.Open();
        const string query = "SELECT COUNT(*) FROM clippings";
        var cmd = new SQLiteCommand(query, conn);
        var a = cmd.ExecuteReader();
        conn.Close();
    }

    private void FileHandler() {
        var programsDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var filePath = Path.Combine(programsDirectory, "KM2.dat");

        if (File.Exists(filePath)) {

        }

        var fileDialog = new OpenFileDialog {
            InitialDirectory = programsDirectory,
            Title = "FrmMain_FileHandler_Import_KM2_Data",

            CheckFileExists = true,
            CheckPathExists = true,

            DefaultExt = "dat",
            Filter = @"KM2 Data Files (*.dat)|*.dat",
            FilterIndex = 2,
            RestoreDirectory = true,

            ReadOnlyChecked = true,
            ShowReadOnly = true
        };

        if (fileDialog.ShowDialog() != DialogResult.OK) return;
        
        var selectedFilePath = fileDialog.FileName;
        try {
            File.Copy(selectedFilePath, filePath, true);
        } catch (Exception ex) {
            Console.WriteLine("Error copying file: " + ex.Message);
        }
    }
}