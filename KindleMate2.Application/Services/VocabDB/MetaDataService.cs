using KindleMate2.Domain.Entities.VocabDB;
using KindleMate2.Domain.Interfaces.VocabDB;

namespace KindleMate2.Application.Services.VocabDB {
    public class MetaDataService(IMetaDataRepository repository) {
        public MetaData? GetMetaDataByKey(string id) {
            return repository.GetById(id);
        }

        public IEnumerable<MetaData> GetAllMetaDatas() {
            return repository.GetAll();
        }

        public int GetCount() {
            return repository.GetCount();
        }

        public void AddMetaData(MetaData metaData) {
            if (string.IsNullOrWhiteSpace(metaData.Id)) {
                throw new ArgumentException("[id] cannot be empty");
            }

            repository.Add(metaData);
        }

        public void UpdateMetaData(MetaData metaData) {
            repository.Update(metaData);
        }

        public void DeleteMetaData(string id) {
            repository.Delete(id);
        }
    }
}