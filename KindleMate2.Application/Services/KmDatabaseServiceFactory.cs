using KindleMate2.Application.Services.KM2DB;
using KindleMate2.Domain.Interfaces.KM2DB;
using KindleMate2.Infrastructure.Helpers;
using KindleMate2.Infrastructure.Repositories.KM2DB;

namespace KindleMate2.Application.Services;

/// <inheritdoc cref="IKmDatabaseServiceFactory"/>
public class KmDatabaseServiceFactory : IKmDatabaseServiceFactory {
    private readonly IClippingRepository _clippingRepository;
    private readonly ILookupRepository _lookupRepository;
    private readonly IOriginalClippingLineRepository _originalClippingLineRepository;
    private readonly ISettingRepository _settingRepository;
    private readonly IVocabRepository _vocabRepository;

    public KmDatabaseServiceFactory(
        IClippingRepository clippingRepository,
        ILookupRepository lookupRepository,
        IOriginalClippingLineRepository originalClippingLineRepository,
        ISettingRepository settingRepository,
        IVocabRepository vocabRepository) {
        _clippingRepository = clippingRepository;
        _lookupRepository = lookupRepository;
        _originalClippingLineRepository = originalClippingLineRepository;
        _settingRepository = settingRepository;
        _vocabRepository = vocabRepository;
    }

    public KmDatabaseService Create(string kmDbPath) {
        var kmConnectionString = DatabaseHelper.GetConnectionString(kmDbPath);

        var kmClippingRepository = new ClippingRepository(kmConnectionString);
        var kmLookupRepository = new Infrastructure.Repositories.KM2DB.LookupRepository(kmConnectionString);
        var kmOriginalClippingLineRepository = new OriginalClippingLineRepository(kmConnectionString);
        var kmSettingRepository = new SettingRepository(kmConnectionString);
        var kmVocabRepository = new VocabRepository(kmConnectionString);

        return new KmDatabaseService(
            _clippingRepository,
            _lookupRepository,
            _originalClippingLineRepository,
            _settingRepository,
            _vocabRepository,
            kmClippingRepository,
            kmLookupRepository,
            kmOriginalClippingLineRepository,
            kmSettingRepository,
            kmVocabRepository);
    }
}
