using KindleMate2.Domain.Entities.VocabDB;
using KindleMate2.Domain.Interfaces.VocabDB;

namespace KindleMate2.Application.Services.VocabDB {
    public class MetaDataService {
        private readonly IMetaDataRepository _repository;

        public MetaDataService(IMetaDataRepository repository) {
            _repository = repository;
        }

        public MetaData? GetMetaDataByKey(string id) {
            return _repository.GetById(id);
        }

        public IEnumerable<MetaData> GetAllMetaDatas() {
            return _repository.GetAll();
        }

        public int GetCount() {
            return _repository.GetCount();
        }

        public void AddMetaData(MetaData metaData) {
            if (string.IsNullOrWhiteSpace(metaData.Id)) {
                throw new ArgumentException("[id] cannot be empty");
            }

            _repository.Add(metaData);
        }

        public void UpdateMetaData(MetaData metaData) {
            _repository.Update(metaData);
        }

        public void DeleteMetaData(string id) {
            _repository.Delete(id);
        }
    }
}