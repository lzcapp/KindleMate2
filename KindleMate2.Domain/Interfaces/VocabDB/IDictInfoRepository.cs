using KindleMate2.Domain.Entities.VocabDB;

namespace KindleMate2.Domain.Interfaces.VocabDB {
    public interface IDictInfoRepository {
        DictInfo? GetById(string id);
        List<DictInfo> GetAll();
        int GetCount();
        void Add(DictInfo dictInfo);
        void Update(DictInfo dictInfo);
        void Delete(string id);
    }
}