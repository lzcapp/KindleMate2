using System;
using System.Globalization;
using System.IO;
using KindleMate2.Application.Services;
using KindleMate2.Application.Services.KM2DB;
using KindleMate2.Domain.Interfaces.KM2DB;
using KindleMate2.Infrastructure.Helpers;
using KindleMate2.Infrastructure.Repositories.KM2DB;
using KindleMate2.Shared.Constants;
using Microsoft.Data.Sqlite;

namespace KindleMate2.Avalonia.Services;

/// <summary>
/// 按「当前打开的库路径」组装 Application 层服务。
///
/// 背景:<c>Application/DependencyInjection.cs</c> 把所有仓储硬编码到
/// <c>AppConstants.ConnectionString</c>(相对路径 <c>KM2.dat</c>,随进程工作目录变化),
/// 既不适合多库,也不适合跨平台。Avalonia 侧因此自行组装一套按路径参数化的服务,
/// 所有导入 / 导出 / 维护 / 设备操作都作用于"当前打开的库",与 UI 语义一致。
/// </summary>
public sealed class DatabaseSession : IDisposable {
    private bool _disposed;

    public string DatabasePath { get; }
    public string WorkDirectory { get; }
    public string ConnectionString { get; }

    public string ImportDirectory { get; }
    public string BackupDirectory { get; }
    public string TempDirectory { get; }
    public string ExportDirectory { get; }

    // —— 仓储 ——
    public IClippingRepository ClippingRepository { get; }
    public ILookupRepository LookupRepository { get; }
    public IOriginalClippingLineRepository OriginalClippingLineRepository { get; }
    public IVocabRepository VocabRepository { get; }
    public ISettingRepository SettingRepository { get; }
    public IDatabaseRepository DatabaseRepository { get; }

    // —— KM2DB 服务 ——
    public IClippingService ClippingService { get; }
    public ILookupService LookupService { get; }
    public IVocabService VocabService { get; }
    public ISettingService SettingService { get; }
    public IOriginalClippingLineService OriginalClippingLineService { get; }
    public IThemeService ThemeService { get; }
    public IDatabaseService DatabaseService { get; }
    public IKm2DatabaseService Km2DatabaseService { get; }

    // —— 数据库服务工厂(导入外部库用) ——
    public IVocabDatabaseServiceFactory VocabDatabaseServiceFactory { get; }
    public IKmDatabaseServiceFactory KmDatabaseServiceFactory { get; }
    public IKmateDatabaseServiceFactory KmateDatabaseServiceFactory { get; }

    // —— 应用级管理器 ——
    public IDeviceManager DeviceManager { get; }
    public IImportManager ImportManager { get; }
    public IExportManager ExportManager { get; }

    public DatabaseSession(string databasePath) {
        DatabasePath = Path.GetFullPath(databasePath);
        WorkDirectory = Path.GetDirectoryName(DatabasePath) ?? Environment.CurrentDirectory;
        ConnectionString = DatabaseHelper.GetConnectionString(DatabasePath);

        ImportDirectory = Path.Combine(WorkDirectory, AppConstants.ImportsPathName);
        BackupDirectory = Path.Combine(WorkDirectory, AppConstants.BackupsPathName);
        TempDirectory = Path.Combine(WorkDirectory, AppConstants.TempPathName);
        ExportDirectory = Path.Combine(WorkDirectory, AppConstants.ExportsPathName);

        Directory.CreateDirectory(ImportDirectory);
        Directory.CreateDirectory(BackupDirectory);
        Directory.CreateDirectory(TempDirectory);
        Directory.CreateDirectory(ExportDirectory);

        var connectionString = ConnectionString;

        ClippingRepository = new ClippingRepository(connectionString);
        LookupRepository = new LookupRepository(connectionString);
        OriginalClippingLineRepository = new OriginalClippingLineRepository(connectionString);
        VocabRepository = new VocabRepository(connectionString);
        SettingRepository = new SettingRepository(connectionString);
        DatabaseRepository = new DatabaseRepository(connectionString);

        ClippingService = new ClippingService(ClippingRepository);
        LookupService = new LookupService(LookupRepository);
        VocabService = new VocabService(VocabRepository);
        SettingService = new SettingService(SettingRepository);
        OriginalClippingLineService = new OriginalClippingLineService(OriginalClippingLineRepository);
        ThemeService = new ThemeService(SettingRepository);
        DatabaseService = new DatabaseService(DatabaseRepository);

        Km2DatabaseService = new Km2DatabaseService(
            ClippingRepository, LookupRepository, OriginalClippingLineRepository, SettingRepository, VocabRepository);

        VocabDatabaseServiceFactory = new VocabDatabaseServiceFactory(LookupRepository, VocabRepository);
        KmDatabaseServiceFactory = new KmDatabaseServiceFactory(
            ClippingRepository, LookupRepository, OriginalClippingLineRepository, SettingRepository, VocabRepository);
        KmateDatabaseServiceFactory = new KmateDatabaseServiceFactory(
            ClippingRepository, LookupRepository, OriginalClippingLineRepository, VocabRepository);

        // 设备管理器按平台选择实现(Windows 用真实 USB/MTP,其他平台空实现兜底)。
        DeviceManager = CreateDeviceManager(WorkDirectory);

        ImportManager = new ImportManager(
            Km2DatabaseService, ClippingService, VocabService, OriginalClippingLineService, LookupService,
            VocabDatabaseServiceFactory, KmDatabaseServiceFactory, KmateDatabaseServiceFactory, ImportDirectory);

        ExportManager = new ExportManager(
            ClippingService, LookupService, OriginalClippingLineService, DeviceManager,
            WorkDirectory, BackupDirectory, TempDirectory);
    }

    public void Dispose() {
        if (_disposed) return;
        _disposed = true;
        DeviceManager.Dispose();
    }

    /// <summary>
    /// 按平台创建设备管理器。独立成静态工厂的用意有二:
    /// ① 构造函数复用;② 无头自检 / CI 无需先打开数据库,就能断言当前平台选到了哪个实现。
    /// </summary>
    public static IDeviceManager CreateDeviceManager(string workDirectory) {
#if WINDOWS
        return new KindleMate2.Devices.Windows.DeviceManager(
            Path.Combine(workDirectory, AppConstants.SystemPathName, AppConstants.VersionFileName));
#else
        return new NullDeviceManager();
#endif
    }

    /// <summary>数据库可用性探测结果。</summary>
    public enum ProbeResult {
        /// <summary>结构符合当前 schema,可以打开。</summary>
        Valid,

        /// <summary>文件根本不是 SQLite 数据库(或已损坏)。</summary>
        NotSqlite,

        /// <summary>是 SQLite 库,但缺少当前 schema 需要的表 / 列(例如旧版 Kindle Mate 格式)。</summary>
        MissingSchema,

        /// <summary>读取失败(权限、被占用等)。</summary>
        Unreadable
    }

    /// <summary>
    /// 探测目标文件能否作为 Kindle Mate 2 的库打开(只读 sqlite_master / PRAGMA,不写文件)。
    ///
    /// 背景:仓库根目录的 KM2.db 是旧版 Kindle Mate 格式,直接用会抛
    /// <c>SQLite Error 1: 'no such column: key'</c> —— 对用户完全看不懂。
    /// 先探一次 schema,就能把「选错文件」翻译成人话;同时把
    /// 「不是数据库」与「缺表」区分开,避免两种错误共用一套措辞。
    /// </summary>
    public static (ProbeResult Result, string Detail) Probe(string path) {
        try {
            if (!File.Exists(path)) return (ProbeResult.Unreadable, path);

            using var connection = new SqliteConnection(DatabaseHelper.GetConnectionString(path));
            connection.Open();

            using (var cmd = new SqliteCommand(
                       "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='clippings'", connection)) {
                if (Convert.ToInt64(cmd.ExecuteScalar() ?? 0L, CultureInfo.InvariantCulture) == 0) {
                    return (ProbeResult.MissingSchema, "clippings");
                }
            }

            using (var cmd = new SqliteCommand("PRAGMA table_info(clippings)", connection))
            using (var reader = cmd.ExecuteReader()) {
                while (reader.Read()) {
                    if (string.Equals(DatabaseHelper.GetSafeString(reader, 1), "key", StringComparison.OrdinalIgnoreCase)) {
                        return (ProbeResult.Valid, string.Empty);
                    }
                }
            }

            return (ProbeResult.MissingSchema, "clippings.key");
        } catch (SqliteException ex) when (ex.SqliteErrorCode == 26) {
            // 26 = SQLITE_NOTADB:文件头不是 SQLite 格式
            return (ProbeResult.NotSqlite, ex.Message);
        } catch (Exception ex) {
            return (ProbeResult.Unreadable, ex.Message);
        }
    }
}
