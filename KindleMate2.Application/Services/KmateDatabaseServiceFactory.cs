using KindleMate2.Application.Services.KM2DB;
using KindleMate2.Domain.Interfaces.KM2DB;

namespace KindleMate2.Application.Services;

/// <inheritdoc cref="IKmateDatabaseServiceFactory"/>
public class KmateDatabaseServiceFactory : IKmateDatabaseServiceFactory {
    private readonly IClippingRepository _clippingRepository;
    private readonly ILookupRepository _lookupRepository;
    private readonly IOriginalClippingLineRepository _originalClippingLineRepository;
    private readonly IVocabRepository _vocabRepository;

    public KmateDatabaseServiceFactory(
        IClippingRepository clippingRepository,
        ILookupRepository lookupRepository,
        IOriginalClippingLineRepository originalClippingLineRepository,
        IVocabRepository vocabRepository) {
        _clippingRepository = clippingRepository;
        _lookupRepository = lookupRepository;
        _originalClippingLineRepository = originalClippingLineRepository;
        _vocabRepository = vocabRepository;
    }

    public KmateDatabaseService Create(string km3DbPath) {
        return new KmateDatabaseService(
            _clippingRepository,
            _lookupRepository,
            _originalClippingLineRepository,
            _vocabRepository,
            km3DbPath);
    }
}
