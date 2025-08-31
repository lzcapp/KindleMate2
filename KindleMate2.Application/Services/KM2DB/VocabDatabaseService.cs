using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Domain.Entities.VocabDB;
using KindleMate2.Domain.Interfaces.KM2DB;
using KindleMate2.Domain.Interfaces.VocabDB;
using IVocabLookupRepository = KindleMate2.Domain.Interfaces.VocabDB.ILookupRepository;
using IKm2DbLookupRepository = KindleMate2.Domain.Interfaces.KM2DB.ILookupRepository;
using Lookup = KindleMate2.Domain.Entities.VocabDB.Lookup;

namespace KindleMate2.Application.Services.KM2DB {
    public class VocabDatabaseService {
        private readonly IBookInfoRepository _bookInfoRepository;
        private readonly IVocabLookupRepository _vocabLookupRepository;
        private readonly IWordRepository _wordRepository;

        private readonly IKm2DbLookupRepository _km2DbLookupRepository;
        private readonly IVocabRepository _vocabRepository;
        
        public VocabDatabaseService(IBookInfoRepository bookInfoRepository, IVocabLookupRepository vocabLookupRepository, IWordRepository wordRepository, IKm2DbLookupRepository km2DbLookupRepository, IVocabRepository vocabRepository) {
            _bookInfoRepository = bookInfoRepository;
            _vocabLookupRepository = vocabLookupRepository;
            _wordRepository = wordRepository;
            _km2DbLookupRepository = km2DbLookupRepository;
            _vocabRepository = vocabRepository;
        }
        
        public bool ImportKindleWords(string sourceFilePath, out Dictionary<string, object> result) {
            var words = _wordRepository.GetAll();
            var lookups = _vocabLookupRepository.GetAll();
            var bookInfos = _bookInfoRepository.GetAll();

            var lookupCount = lookups.Count;
            var insertedLookupCount = 0;
            var insertedVocabCount = 0;
            
            try {
                foreach (Word item in words) {
                    var id = item.id;
                    var word = item.word;
                    var stem = item.stem;
                    var category = item.category;
                    var timestamp = item.timestamp;

                    DateTimeOffset dateTimeOffset = DateTimeOffset.FromUnixTimeMilliseconds(timestamp);
                    DateTime dateTime = dateTimeOffset.LocalDateTime;
                    var formattedDateTime = dateTime.ToString("yyyy-MM-dd HH:mm:ss");

                    if (_vocabRepository.GetById(word + timestamp) != null) {
                        continue;
                    }
                    _vocabRepository.Add(new Vocab {
                        id = word + timestamp, 
                        word_key = id, word = word, stem = stem, category = category, timestamp = formattedDateTime, frequency = 0
                    });
                    insertedVocabCount += 1;
                }

                foreach (Lookup item in lookups) {
                    var wordKey = item.word_key;
                    var bookKey = item.book_key;
                    var usage = item.usage;
                    var timestamp = item.timestamp;

                    var title = string.Empty;
                    var authors = string.Empty;
                    foreach (BookInfo bookInfoRow in bookInfos) {
                        var id = bookInfoRow.id;
                        var guid = bookInfoRow.guid;
                        if (id != bookKey && guid != bookKey) {
                            continue;
                        }
                        title = bookInfoRow.title;
                        authors = bookInfoRow.authors;
                    }

                    DateTimeOffset dateTimeOffset = DateTimeOffset.FromUnixTimeMilliseconds(timestamp);
                    DateTime dateTime = dateTimeOffset.LocalDateTime;
                    var formattedDateTime = dateTime.ToString("yyyy-MM-dd HH:mm:ss");

                    if (_km2DbLookupRepository.GetByTimestamp(formattedDateTime) != null) {
                        continue;
                    }
                    _km2DbLookupRepository.Add(new Domain.Entities.KM2DB.Lookup {
                        WordKey = wordKey,
                        Usage = usage,
                        Title = title,
                        Authors = authors,
                        Timestamp = formattedDateTime
                    });
                    insertedLookupCount += 1;
                }

                UpdateFrequency();

                result = new Dictionary<string, object> {
                    { "LookupCount", lookupCount },
                    { "InsertedLookupCount", insertedLookupCount },
                    { "InsertedVocabCount", insertedVocabCount }
                };

                return true;
            } catch (Exception exception) {
                result = new Dictionary<string, object> {
                    { "Exception", exception.Message }
                };
                
                return false;
            }
        }

        private void UpdateFrequency() {
            var vocabs = _vocabRepository.GetAll();
            var lookups = _km2DbLookupRepository.GetAll();

            try {
                foreach (Vocab vocab in vocabs) {
                    var wordKey = vocab.word_key;
                    var frequency = lookups.AsEnumerable().Count(lookupsRow => lookupsRow.WordKey.Trim() == wordKey);
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