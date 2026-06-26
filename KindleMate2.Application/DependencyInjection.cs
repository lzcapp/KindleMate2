using KindleMate2.Application.Services;
using KindleMate2.Application.Services.KM2DB;
using KindleMate2.Domain.Interfaces.KM2DB;
using KindleMate2.Infrastructure.Repositories.KM2DB;
using KindleMate2.Shared.Constants;
using Microsoft.Extensions.DependencyInjection;

namespace KindleMate2.Application;

/// <summary>
/// Centralized DI registration for the Application and Infrastructure layers.
/// </summary>
public static class DependencyInjection {
    /// <summary>
    /// Registers all repositories, services, and managers into the DI container.
    /// </summary>
    public static IServiceCollection AddKindleMateServices(this IServiceCollection services) {
        // ── Repositories (Singleton — connection string is fixed) ──
        services.AddSingleton<IClippingRepository>(
            _ => new ClippingRepository(AppConstants.ConnectionString));
        services.AddSingleton<ILookupRepository>(
            _ => new Infrastructure.Repositories.KM2DB.LookupRepository(AppConstants.ConnectionString));
        services.AddSingleton<IOriginalClippingLineRepository>(
            _ => new OriginalClippingLineRepository(AppConstants.ConnectionString));
        services.AddSingleton<ISettingRepository>(
            _ => new SettingRepository(AppConstants.ConnectionString));
        services.AddSingleton<IVocabRepository>(
            _ => new VocabRepository(AppConstants.ConnectionString));
        services.AddSingleton<IDatabaseRepository>(
            _ => new DatabaseRepository(AppConstants.ConnectionString));

        // ── KM2DB Services (Singleton — thin wrappers over repositories) ──
        services.AddSingleton<IClippingService, ClippingService>();
        services.AddSingleton<ILookupService, LookupService>();
        services.AddSingleton<IVocabService, VocabService>();
        services.AddSingleton<ISettingService, SettingService>();
        services.AddSingleton<IOriginalClippingLineService, OriginalClippingLineService>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<IDatabaseService, DatabaseService>();
        services.AddSingleton<IKm2DatabaseService, Km2DatabaseService>();

        // ── Application-level Managers (Singleton — stateless coordinators) ──
        services.AddSingleton<IVocabDatabaseServiceFactory, VocabDatabaseServiceFactory>();
        services.AddSingleton<IKmDatabaseServiceFactory, KmDatabaseServiceFactory>();
        services.AddSingleton<IDeviceManager>(sp => {
            var versionFilePath = Path.Combine(AppConstants.SystemPathName, AppConstants.VersionFileName);
            return new DeviceManager(versionFilePath);
        });
        services.AddSingleton<IImportManager>(sp => {
            var importPath = Path.Combine(Environment.CurrentDirectory, AppConstants.ImportsPathName);
            return new ImportManager(
                sp.GetRequiredService<IKm2DatabaseService>(),
                sp.GetRequiredService<IClippingService>(),
                sp.GetRequiredService<IVocabService>(),
                sp.GetRequiredService<IOriginalClippingLineService>(),
                sp.GetRequiredService<ILookupService>(),
                sp.GetRequiredService<IVocabDatabaseServiceFactory>(),
                sp.GetRequiredService<IKmDatabaseServiceFactory>(),
                importPath);
        });
        services.AddSingleton<IDataDisplayService, DataDisplayService>();
        services.AddSingleton<IContentDetailService, ContentDetailService>();
        services.AddSingleton<IExportManager>(sp => {
            var programPath = Environment.CurrentDirectory;
            var backupPath = Path.Combine(programPath, AppConstants.BackupsPathName);
            var tempPath = Path.Combine(programPath, AppConstants.TempPathName);
            return new ExportManager(
                sp.GetRequiredService<IClippingService>(),
                sp.GetRequiredService<ILookupService>(),
                sp.GetRequiredService<IOriginalClippingLineService>(),
                sp.GetRequiredService<IDeviceManager>(),
                programPath, backupPath, tempPath);
        });

        return services;
    }
}
