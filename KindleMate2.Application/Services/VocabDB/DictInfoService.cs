using KindleMate2.Domain.Entities.VocabDB;
using KindleMate2.Domain.Interfaces.VocabDB;

namespace KindleMate2.Application.Services.VocabDB {
    public class DictInfoService {
        private readonly IDictInfoRepository _repository;

        public DictInfoService(IDictInfoRepository repository) {
            _repository = repository;
        }

        public DictInfo? GetDictInfoByWordKey(string id) {
            return _repository.GetById(id);
        }

        public IEnumerable<DictInfo> GetAllDictInfos() {
            return _repository.GetAll();
        }

        public int GetCount() {
            return _repository.GetCount();
        }

        public void AddDictInfo(DictInfo dictInfo) {
            if (string.IsNullOrWhiteSpace(dictInfo.id)) {
                throw new ArgumentException("[id] cannot be empty");
            }

            _repository.Add(dictInfo);
        }

        public void UpdateDictInfo(DictInfo dictInfo) {
            _repository.Update(dictInfo);
        }

        public void DeleteDictInfo(string id) {
            _repository.Delete(id);
        }
    }
}
