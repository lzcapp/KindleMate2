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
        var connectionString = BuildSourceConnectionString(vocabDbPath);

        // 源库的表/列由设备决定,先探测一次实表/实列:仓储据此逐表降级,
        // 服务据此把"跳过了哪些表"计入导入结果(不阻断其它表)。
        var schema = VocabSourceSchema.Probe(connectionString);

        var bookInfoRepository = new BookInfoRepository(connectionString, schema);
        var vocabLookupRepository = new Infrastructure.Repositories.VocabDB.LookupRepository(connectionString, schema);
        var wordRepository = new WordRepository(connectionString, schema);

        return new VocabDatabaseService(
            bookInfoRepository,
            vocabLookupRepository,
            wordRepository,
            _km2DbLookupRepository,
            _vocabRepository,
            schema);
    }

    /// <summary>
    /// 源库(设备 <c>vocab.db</c>)的连接串。**只读**打开:
    /// 它是设备写就的数据源,不归本程序所有 —— 可写打开在只读挂载 / 被占用时会失败,
    /// 还可能顺手生成 <c>-journal</c> / <c>-wal</c> 附属文件污染设备盘。
    /// (与 <c>KmateSourceReader</c> 读 km3.dat 同一口径。)
    /// </summary>
    internal static string BuildSourceConnectionString(string vocabDbPath) {
        return new SqliteConnectionStringBuilder {
            DataSource = vocabDbPath,
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Shared
        }.ToString();
    }
}
