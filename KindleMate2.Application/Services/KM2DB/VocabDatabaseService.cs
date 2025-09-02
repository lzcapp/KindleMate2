using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Domain.Entities.VocabDB;
using KindleMate2.Domain.Interfaces.KM2DB;
using KindleMate2.Domain.Interfaces.VocabDB;
using KindleMate2.Shared.Constants;
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

        public bool ImportKindleWords(string sourceFilePath, out Dictionary<string, string> result) {
            var words = _wordRepository.GetAll();
            var lookups = _vocabLookupRepository.GetAll();
            var bookInfos = _bookInfoRepository.GetAll();

            var lookupCount = lookups.Count;
            var insertedLookupCount = 0;
            var insertedVocabCount = 0;

            try {
                foreach (Word item in words) {
                    var id = item.Id;
                    var word = item.word;
                    var stem = item.Stem;
                    var category = item.Category;
                    var timestamp = item.Timestamp;

                    if (timestamp == null) {
                        continue;
                    }
                    DateTimeOffset dateTimeOffset = DateTimeOffset.FromUnixTimeMilliseconds((long)timestamp);
                    DateTime dateTime = dateTimeOffset.LocalDateTime;
                    var formattedDateTime = dateTime.ToString("yyyy-MM-dd HH:mm:ss");

                    if (_vocabRepository.GetById(word + timestamp) != null) {
                        continue;
                    }
                    var isAdded = word != null && _vocabRepository.Add(new Vocab {
                        Id = word + timestamp,
                        WordKey = id,
                        Word = word,
                        Stem = stem,
                        Category = category,
                        Timestamp = formattedDateTime,
                        Frequency = 0
                    });
                    if (isAdded) {
                        insertedVocabCount += 1;
                    }
                }

                foreach (Lookup item in lookups) {
                    var wordKey = item.WordKey;
                    var bookKey = item.BookKey;
                    var usage = item.Usage;
                    var timestamp = item.Timestamp;

                    var title = string.Empty;
                    var authors = string.Empty;
                    foreach (BookInfo bookInfoRow in bookInfos) {
                        var id = bookInfoRow.Id;
                        var guid = bookInfoRow.Guid;
                        if (id != bookKey && guid != bookKey) {
                            continue;
                        }
                        title = bookInfoRow.Title;
                        authors = bookInfoRow.Authors;
                    }

                    if (timestamp == null) {
                        continue;
                    }
                    DateTimeOffset dateTimeOffset = DateTimeOffset.FromUnixTimeMilliseconds((long)timestamp);
                    DateTime dateTime = dateTimeOffset.LocalDateTime;
                    var formattedDateTime = dateTime.ToString("yyyy-MM-dd HH:mm:ss");

                    if (_km2DbLookupRepository.GetByTimestamp(formattedDateTime).Count != 0) {
                        continue;
                    }
                    var isAdded = _km2DbLookupRepository.Add(new Domain.Entities.KM2DB.Lookup {
                        WordKey = wordKey,
                        Usage = usage,
                        Title = title,
                        Authors = authors,
                        Timestamp = formattedDateTime
                    });
                    if (isAdded) {
                        insertedLookupCount += 1;
                    }
                }

                if (!UpdateFrequency(out Exception exception)) {
                    throw exception;
                }

                result = new Dictionary<string, string> {
                    { AppConstants.LookupCount, lookupCount.ToString() },
                    { AppConstants.InsertedLookupCount, insertedLookupCount.ToString() },
                    { AppConstants.InsertedVocabCount, insertedVocabCount.ToString() }
                };

                return true;
            } catch (Exception exception) {
                result = new Dictionary<string, string> {
                    { AppConstants.Exception, exception.Message }
                };

                return false;
            }
        }

        private bool UpdateFrequency(out Exception exception) {
            exception = new Exception();
            try {
                var vocabs = _vocabRepository.GetAll();
                var lookups = _km2DbLookupRepository.GetAll();

                foreach (Vocab vocab in vocabs) {
                    var wordKey = vocab.WordKey;
                    var frequency = lookups.AsEnumerable().Count(lookupsRow => lookupsRow.WordKey?.Trim() == wordKey);
                    _vocabRepository.UpdateFrequencyByWordKey(new Vocab {
                        WordKey = wordKey,
                        Frequency = frequency,
                        Id = string.Empty,
                        Word = string.Empty
                    });
                }
                return true;
            } catch (Exception e) {
                exception = e;
                return false;
            }
        }
    }
}