using KindleMate2.Application.Services.KM2DB;

namespace KindleMate2.Application.Services;

/// <summary>
/// Factory for creating <see cref="KmDatabaseService"/> instances wired with
/// both target (current) and source (imported) KM2DB repositories.
/// </summary>
public interface IKmDatabaseServiceFactory {
    KmDatabaseService Create(string kmDbPath);
}
