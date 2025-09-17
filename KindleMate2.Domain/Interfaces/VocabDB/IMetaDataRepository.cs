using KindleMate2.Domain.Entities.VocabDB;

namespace KindleMate2.Domain.Interfaces.VocabDB {
    public interface IMetaDataRepository {
        MetaData? GetById(string id);

        List<MetaData> GetAll();

        int GetCount();

        bool Add(MetaData metaData);

        bool Update(MetaData metaData);

        bool Delete(string id);
    }
}