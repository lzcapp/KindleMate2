using KindleMate2.Domain.Entities.VocabDB;
using KindleMate2.Domain.Interfaces.VocabDB;

namespace KindleMate2.Application.Services.VocabDB {
    public class DictInfoService(IDictInfoRepository repository) {
        public DictInfo? GetDictInfoByWordKey(string id) {
            return repository.GetById(id);
        }

        public IEnumerable<DictInfo> GetAllDictInfos() {
            return repository.GetAll();
        }

        public int GetCount() {
            return repository.GetCount();
        }

        public void AddDictInfo(DictInfo dictInfo) {
            if (string.IsNullOrWhiteSpace(dictInfo.Id)) {
                throw new ArgumentException("[id] cannot be empty");
            }

            repository.Add(dictInfo);
        }

        public void UpdateDictInfo(DictInfo dictInfo) {
            repository.Update(dictInfo);
        }

        public void DeleteDictInfo(string id) {
            repository.Delete(id);
        }
    }
}
