using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Domain.Interfaces.KM2DB;
using KindleMate2.Infrastructure.Helpers;

namespace KindleMate2.Application.Services.KM2DB {
    public class KmDatabaseService {
        private readonly IClippingRepository _clippingRepository;
        private readonly ILookupRepository _lookupRepository;
        private readonly IOriginalClippingLineRepository _originalClippingLineRepository;
        private readonly ISettingRepository _settingRepository;
        private readonly IVocabRepository _vocabRepository;
        
        private readonly IClippingRepository _kmClippingRepository;
        private readonly ILookupRepository _kmLookupRepository;
        private readonly IOriginalClippingLineRepository _kmOriginalClippingLineRepository;
        private readonly ISettingRepository _kmSettingRepository;
        private readonly IVocabRepository _kmVocabRepository;

        public KmDatabaseService(IClippingRepository clippingRepository, ILookupRepository lookupRepository, IOriginalClippingLineRepository originalClippingLineRepository, ISettingRepository settingRepository, IVocabRepository vocabRepository, IClippingRepository kmClippingRepository, ILookupRepository kmLookupRepository, IOriginalClippingLineRepository kmOriginalClippingLineRepository, ISettingRepository kmSettingRepository, IVocabRepository kmVocabRepository) {
            _clippingRepository = clippingRepository;
            _lookupRepository = lookupRepository;
            _originalClippingLineRepository = originalClippingLineRepository;
            _settingRepository = settingRepository;
            _vocabRepository = vocabRepository;
            
            _kmClippingRepository = kmClippingRepository;
            _kmLookupRepository = kmLookupRepository;
            _kmOriginalClippingLineRepository = kmOriginalClippingLineRepository;
            _kmSettingRepository = kmSettingRepository;
            _kmVocabRepository = kmVocabRepository;
        }

        public bool ImportFromKmDatabase() {
            try {
                var kmClippings = _kmClippingRepository.GetAll();
                var kmOriginalClippingLines = _kmOriginalClippingLineRepository.GetAll();
                if (kmClippings.Count > 0 || kmOriginalClippingLines.Count > 0) {
                    foreach (Clipping kmClipping in kmClippings.Where(kmClipping => !string.IsNullOrEmpty(kmClipping.Content) && (_clippingRepository.GetByKey(kmClipping.Key) == null || _clippingRepository.GetByContent(kmClipping.Content).Count > 0))) {
                        _clippingRepository.Add(kmClipping);
                    }
                    foreach (OriginalClippingLine kmOriginalClippingLine in kmOriginalClippingLines.Where(kmOriginalClippingLine => _originalClippingLineRepository.GetByKey(kmOriginalClippingLine.key) == null)) {
                        _originalClippingLineRepository.Add(kmOriginalClippingLine);
                    }
                }
            
                var kmLookups = _kmLookupRepository.GetAll();
                var kmVocabs = _kmVocabRepository.GetAll();
                if (kmLookups.Count > 0 || kmVocabs.Count > 0) {
                    foreach (Lookup kmLookup in kmLookups.Where(kmLookup => !string.IsNullOrWhiteSpace(kmLookup.WordKey) && _lookupRepository.GetByWordKey(kmLookup.WordKey) == null)) {
                        _lookupRepository.Add(kmLookup);
                    }
                    foreach (Vocab kmVocab in kmVocabs.Where(kmVocab => _vocabRepository.GetById(kmVocab.Id) == null)) {
                        _vocabRepository.Add(kmVocab);
                    }
                }
            
                var kmSettings = _kmSettingRepository.GetAll();
                if (kmSettings.Count <= 0) {
                    return true;
                }
                foreach (Setting kmSetting in kmSettings.Where(kmSetting => _settingRepository.GetByName(kmSetting.Name) == null)) {
                    _settingRepository.Add(kmSetting);
                }

                return true;
            } catch (Exception e) {
                Console.WriteLine(StringHelper.GetExceptionMessage(nameof(ImportFromKmDatabase), e));
                return false;
            }
        }
    }
}