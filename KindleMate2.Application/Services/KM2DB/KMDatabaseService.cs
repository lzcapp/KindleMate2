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
                    // Pre-fetch target data for O(1) in-memory dedup checks
                    var targetClippings = _clippingRepository.GetAll();
                    var targetClippingKeys = targetClippings.Select(c => c.Key).ToHashSet();
                    var targetClippingContents = targetClippings
                        .Where(c => c.Content != null)
                        .Select(c => c.Content!)
                        .ToHashSet();

                    foreach (Clipping kmClipping in kmClippings) {
                        if (string.IsNullOrEmpty(kmClipping.Content)) {
                            continue;
                        }
                        if (!targetClippingKeys.Contains(kmClipping.Key) &&
                            !targetClippingContents.Contains(kmClipping.Content)) {
                            _clippingRepository.Add(kmClipping);
                        }
                    }
                    var targetOriginalKeys = _originalClippingLineRepository.GetAllKeys();
                    foreach (OriginalClippingLine kmOriginalClippingLine in kmOriginalClippingLines) {
                        if (!targetOriginalKeys.Contains(kmOriginalClippingLine.Key)) {
                            _originalClippingLineRepository.Add(kmOriginalClippingLine);
                        }
                    }
                }
            
                var kmLookups = _kmLookupRepository.GetAll();
                var kmVocabs = _kmVocabRepository.GetAll();
                if (kmLookups.Count > 0 || kmVocabs.Count > 0) {
                    var targetLookupWordKeys = _lookupRepository.GetWordKeysList().ToHashSet();
                    var targetVocabIds = _vocabRepository.GetAll().Select(v => v.Id).ToHashSet();

                    foreach (Lookup kmLookup in kmLookups) {
                        if (!string.IsNullOrWhiteSpace(kmLookup.WordKey) &&
                            !targetLookupWordKeys.Contains(kmLookup.WordKey)) {
                            _lookupRepository.Add(kmLookup);
                        }
                    }
                    foreach (Vocab kmVocab in kmVocabs) {
                        if (!targetVocabIds.Contains(kmVocab.Id)) {
                            _vocabRepository.Add(kmVocab);
                        }
                    }
                }
            
                var kmSettings = _kmSettingRepository.GetAll();
                if (kmSettings.Count > 0) {
                    var targetSettingNames = _settingRepository.GetAll()
                        .Where(s => s.Name != null)
                        .Select(s => s.Name!)
                        .ToHashSet();

                    foreach (Setting kmSetting in kmSettings) {
                        if (!targetSettingNames.Contains(kmSetting.Name)) {
                            _settingRepository.Add(kmSetting);
                        }
                    }
                }

                return true;
            } catch (Exception e) {
                Console.WriteLine(StringHelper.GetExceptionMessage(nameof(ImportFromKmDatabase), e));
                return false;
            }
        }
    }
}