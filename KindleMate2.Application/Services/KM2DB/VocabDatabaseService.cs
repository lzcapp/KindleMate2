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
            try {
                var words = _wordRepository.GetAll();
                var lookups = _vocabLookupRepository.GetAll();
                var bookInfos = _bookInfoRepository.GetAll();

                var lookupCount = lookups.Count;
                var insertedVocabCount = 0;
                var insertedLookupCount = 0;

                var bookInfoMap = bookInfos.ToDictionary(b => b.Id ?? string.Empty, StringComparer.OrdinalIgnoreCase);

                // Dedup against one in-memory snapshot of existing vocab ids instead of a
                // GetById round-trip (new connection + query) per candidate row.
                var existingVocabIds = _vocabRepository.GetAll()
                    .Where(v => v.Id != null)
                    .Select(v => v.Id!)
                    .ToHashSet(StringComparer.Ordinal);

                var newVocabs = new List<Vocab>();
                foreach (Word item in words) {
                    var id = item.Id;
                    var word = item.WordText;
                    var stem = item.Stem;
                    var category = item.Category;
                    var timestamp = item.Timestamp;

                    if (timestamp == null || word == null) {
                        continue;
                    }
                    DateTimeOffset dateTimeOffset = DateTimeOffset.FromUnixTimeMilliseconds((long)timestamp);
                    DateTime dateTime = dateTimeOffset.LocalDateTime;
                    var formattedDateTime = dateTime.ToString("yyyy-MM-dd HH:mm:ss");

                    if (!existingVocabIds.Add(word + timestamp)) {
                        continue;
                    }
                    newVocabs.Add(new Vocab {
                        Id = word + timestamp,
                        WordKey = id,
                        Word = word,
                        Stem = stem,
                        Category = category,
                        Timestamp = formattedDateTime,
                        Frequency = 0
                    });
                }
                if (newVocabs.Count > 0) {
                    insertedVocabCount = _vocabRepository.Add(newVocabs);
                }

                var newLookups = new List<Domain.Entities.KM2DB.Lookup>();
                // KM2DB.lookups enforces UNIQUE(word_key, timestamp): several words looked
                // up within the same second are legitimate, but the same word looked up at
                // the exact same formatted timestamp must not be inserted twice. Dedup
                // in-memory on that composite key so a single import pass never trips the
                // constraint and rolls back the whole batch. The set is seeded from the
                // existing destination rows so the previous per-row
                // ExistsByWordKeyAndTimestamp query (one connection each) disappears too.
                var seenLookupKeys = _km2DbLookupRepository.GetAll()
                    .Where(l => l.WordKey != null && l.Timestamp != null)
                    .Select(l => l.WordKey + "\u0000" + l.Timestamp)
                    .ToHashSet(StringComparer.Ordinal);
                foreach (Lookup item in lookups) {
                    var wordKey = item.WordKey;
                    var bookKey = item.BookKey;
                    var usage = item.Usage;
                    var timestamp = item.Timestamp;

                    var title = string.Empty;
                    var authors = string.Empty;
                    if (bookKey != null && bookInfoMap.TryGetValue(bookKey, out var bookInfo)) {
                        title = bookInfo.Title;
                        authors = bookInfo.Authors;
                    }

                    if (timestamp == null) {
                        continue;
                    }
                    DateTimeOffset dateTimeOffset = DateTimeOffset.FromUnixTimeMilliseconds((long)timestamp);
                    DateTime dateTime = dateTimeOffset.LocalDateTime;
                    var formattedDateTime = dateTime.ToString("yyyy-MM-dd HH:mm:ss");

                    if (string.IsNullOrWhiteSpace(wordKey)) {
                        // Rows without a word key cannot be linked to vocabulary — skip.
                        continue;
                    }

                    var lookupKey = wordKey + "\u0000" + formattedDateTime;
                    if (!seenLookupKeys.Add(lookupKey)) {
                        continue;
                    }

                    newLookups.Add(new Domain.Entities.KM2DB.Lookup {
                        WordKey = wordKey,
                        Usage = usage,
                        Title = title,
                        Authors = authors,
                        Timestamp = formattedDateTime
                    });
                }
                if (newLookups.Count > 0) {
                    insertedLookupCount = _km2DbLookupRepository.Add(newLookups);
                }

                UpdateFrequency();

                result = new Dictionary<string, string> {
                    { AppConstants.LookupCount, lookupCount.ToString() },
                    { AppConstants.InsertedLookupCount, insertedLookupCount.ToString() },
                    { AppConstants.InsertedVocabCount, insertedVocabCount.ToString() }
                };

                return true;
            } catch (Exception e) {
                Console.WriteLine(e.Message);
                result = new Dictionary<string, string> {
                    { AppConstants.Exception, e.Message }
                };

                return false;
            }
        }

        private void UpdateFrequency() {
            var vocabs = _vocabRepository.GetAll();
            var lookups = _km2DbLookupRepository.GetAll();
            var frequencyMap = lookups
                .Where(l => !string.IsNullOrWhiteSpace(l.WordKey))
                .GroupBy(l => l.WordKey!.Trim())
                .ToDictionary(g => g.Key, g => g.Count());

            // Collect every row first, then push frequencies in ONE batched,
            // transaction-wrapped call instead of one connection + UPDATE per vocab.
            var updates = new List<Vocab>(vocabs.Count);
            foreach (Vocab vocab in vocabs) {
                var wordKey = vocab.WordKey;
                if (wordKey == null) {
                    continue;
                }
                frequencyMap.TryGetValue(wordKey, out var frequency);
                updates.Add(new Vocab {
                    WordKey = wordKey,
                    Frequency = frequency,
                    Id = vocab.Id,
                    Word = vocab.Word
                });
            }
            if (updates.Count > 0) {
                _vocabRepository.UpdateFrequencyByWordKey(updates);
            }
        }
    }
}
