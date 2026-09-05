using KindleMate2.Application.Services.KM2DB;

namespace KindleMate2.Application.Services;

/// <summary>
/// Factory for creating <see cref="KmateDatabaseService"/> instances wired with
/// the target KM2 repositories and a read-only path to the KMate km3.dat source.
/// </summary>
public interface IKmateDatabaseServiceFactory {
    KmateDatabaseService Create(string km3DbPath);
}
