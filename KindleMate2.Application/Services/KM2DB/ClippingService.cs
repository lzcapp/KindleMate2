using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Domain.Interfaces.KM2DB;
using KindleMate2.Shared.Entities;
using Microsoft.Data.Sqlite;

namespace KindleMate2.Application.Services.KM2DB {
    public class ClippingService {
        private readonly IClippingRepository _repository;

        public ClippingService(IClippingRepository repository) {
            _repository = repository;
        }

        public Clipping? GetClippingByKey(string key) {
            return _repository.GetByKey(key);
        }

        public Clipping? GetClippingByKeyAndContent(string key, string content) {
            return _repository.GetByKeyAndContent(key, content);
        }

        public List<Clipping> GetByFuzzySearch(string search, AppEntities.SearchType type) {
            return _repository.GetByFuzzySearch(search, type);
        }

        public List<Clipping> GetAllClippings() {
            return _repository.GetAll();
        }

        public int GetCount() {
            return _repository.GetCount();
        }

        public void AddClipping(Clipping clipping) {
            if (string.IsNullOrWhiteSpace(clipping.key)) {
                throw new ArgumentException("[key] cannot be empty");
            }

            _repository.Add(clipping);
        }

        public void UpdateClipping(Clipping clipping) {
            _repository.Update(clipping);
        }

        public void DeleteClipping(string key) {
            _repository.Delete(key);
        }
    }
}
