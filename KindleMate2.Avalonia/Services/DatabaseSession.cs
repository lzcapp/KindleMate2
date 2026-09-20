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
/// 背景:早期那套集中注册(<c>Application/DependencyInjection.cs</c>,现已删除)把所有仓储硬编码到
/// 一条相对路径 <c>KM2.dat</c> 的连接串上 —— 实际连到哪个库取决于进程当前目录,既不适合多库,
/// 也不适合跨平台。Avalonia 侧因此自行组装一套按路径参数化的服务,
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
        WorkDirectory = Path.GetDirectoryName(DatabasePath) ?? AppPaths.DataDirectory;
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
        // 目标库连接串按「当前打开的库」传入 —— 工厂不再依赖 AppConstants 里那个
        // 写死相对路径 KM2.dat 的常量(库不在 cwd 时它会指向错误的库)。
        KmateDatabaseServiceFactory = new KmateDatabaseServiceFactory(
            ClippingRepository, LookupRepository, OriginalClippingLineRepository, VocabRepository,
            ConnectionString);

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
    ///
    /// 三个分支与三份引用一一对应(WINDOWS / MACOS 常量由 Avalonia.csproj 按平台与 TFM 定义):
    /// Windows → Devices.Windows(USB 盘符 + MTP);macOS → Devices.MacOS(/Volumes + 轮询);
    /// 其余(Linux 等)→ NullDeviceManager 兜底。
    /// </summary>
    public static IDeviceManager CreateDeviceManager(string workDirectory) {
        var versionFilePath = Path.Combine(workDirectory, AppConstants.SystemPathName, AppConstants.VersionFileName);
#if WINDOWS
        return new KindleMate2.Devices.Windows.DeviceManager(versionFilePath);
#elif MACOS
        return new KindleMate2.Devices.MacOS.DeviceManager(versionFilePath);
#else
        return new NullDeviceManager();
#endif
    }
}
