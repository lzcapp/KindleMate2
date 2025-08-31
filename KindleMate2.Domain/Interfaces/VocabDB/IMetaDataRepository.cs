using KindleMate2.Domain.Entities.VocabDB;

namespace KindleMate2.Domain.Interfaces.VocabDB {
    public interface IMetaDataRepository {
        MetaData? GetById(string id);
        List<MetaData> GetAll();
        int GetCount();
        void Add(MetaData metaData);
        void Update(MetaData metaData);
        void Delete(string id);
    }
}