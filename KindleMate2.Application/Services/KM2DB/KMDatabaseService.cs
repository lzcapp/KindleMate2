using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Domain.Interfaces.KM2DB;
using KindleMate2.Infrastructure.Helpers;

namespace KindleMate2.Application.Services.KM2DB {
    public class KMDatabaseService {
        private readonly ILookupRepository _lookupRepository;
        private readonly IVocabRepository _vocabRepository;
        
        public KMDatabaseService(ILookupRepository lookupRepository, IVocabRepository vocabRepository) {
            _lookupRepository = lookupRepository;
            _vocabRepository = vocabRepository;
        }

        public void ImportFromKmDatabase(string sourceDbPath, string targetDbPath) {
            DatabaseHelper.ImportKMDatabase(sourceDbPath, targetDbPath);
        }

        public void ImportFromVocabDatabase(string sourceDbPath, string targetDbPath) {
            DatabaseHelper.ImportVocabDatabase(sourceDbPath, targetDbPath);
        }

        public void UpdateFrequency() {
            var vocabs = _vocabRepository.GetAll();
            var lookups = _lookupRepository.GetAll();

            try {
                foreach (Vocab vocab in vocabs) {
                    var wordKey = vocab.word_key;
                    var frequency = lookups.AsEnumerable().Count(lookupsRow => lookupsRow.word_key.Trim() == wordKey);
                    _vocabRepository.UpdateFrequencyByWordKey(new Vocab {
                        word_key = wordKey, 
                        frequency = frequency
                    });
                }
            } catch (Exception) {
            }
        }
    }
}