using KindleMate2.Domain.Entities.VocabDB;

namespace KindleMate2.Domain.Interfaces.VocabDB {
    public interface IDictInfoRepository {
        DictInfo? GetById(string id);

        List<DictInfo> GetAll();

        int GetCount();

        bool Add(DictInfo dictInfo);

        bool Update(DictInfo dictInfo);

        bool Delete(string id);
    }
}