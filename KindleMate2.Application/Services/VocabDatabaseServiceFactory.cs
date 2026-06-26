using KindleMate2.Application.Services.KM2DB;
using KindleMate2.Domain.Interfaces.KM2DB;
using KindleMate2.Infrastructure.Repositories.VocabDB;
using Microsoft.Data.Sqlite;
using IKm2DbLookupRepository = KindleMate2.Domain.Interfaces.KM2DB.ILookupRepository;

namespace KindleMate2.Application.Services;

/// <inheritdoc cref="IVocabDatabaseServiceFactory"/>
public class VocabDatabaseServiceFactory : IVocabDatabaseServiceFactory {
    private readonly IKm2DbLookupRepository _km2DbLookupRepository;
    private readonly IVocabRepository _vocabRepository;

    public VocabDatabaseServiceFactory(IKm2DbLookupRepository km2DbLookupRepository, IVocabRepository vocabRepository) {
        _km2DbLookupRepository = km2DbLookupRepository;
        _vocabRepository = vocabRepository;
    }

    public VocabDatabaseService Create(string vocabDbPath) {
        var connectionString = new SqliteConnectionStringBuilder {
            DataSource = vocabDbPath,
            Mode = SqliteOpenMode.ReadWrite,
            Cache = SqliteCacheMode.Shared
        }.ToString();

        var bookInfoRepository = new BookInfoRepository(connectionString);
        var vocabLookupRepository = new Infrastructure.Repositories.VocabDB.LookupRepository(connectionString);
        var wordRepository = new WordRepository(connectionString);

        return new VocabDatabaseService(
            bookInfoRepository,
            vocabLookupRepository,
            wordRepository,
            _km2DbLookupRepository,
            _vocabRepository);
    }
}
