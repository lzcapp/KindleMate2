using System;
using System.IO;
using KindleMate2.Application.Services;
using KindleMate2.Application.Services.KM2DB;
using KindleMate2.Domain.Interfaces.KM2DB;
using KindleMate2.Infrastructure.Helpers;
using KindleMate2.Infrastructure.Repositories.KM2DB;
using KindleMate2.Shared.Constants;

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

        DeviceManager = new DeviceManager(Path.Combine(WorkDirectory, AppConstants.SystemPathName, AppConstants.VersionFileName));

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
}
