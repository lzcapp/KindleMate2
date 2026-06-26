using KindleMate2.Application.Services.KM2DB;

namespace KindleMate2.Application.Services;

/// <summary>
/// Factory for creating <see cref="VocabDatabaseService"/> instances wired with
/// the correct repositories (VocabDB repos for the Kindle vocab.db and KM2DB repos
/// from DI).
/// </summary>
public interface IVocabDatabaseServiceFactory {
    VocabDatabaseService Create(string vocabDbPath);
}
