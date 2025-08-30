using KindleMate2.Domain.Entities;
using KindleMate2.Domain.Interfaces;

namespace KindleMate2.Application.Services {
    public class ClippingService {
        private readonly IClippingRepository _repository;

        public ClippingService(IClippingRepository repository) {
            _repository = repository;
        }

        public IEnumerable<Clipping> GetAllClippings() {
            return _repository.GetAll();
        }

        public Clipping? GetClippingByKey(string key) {
            return _repository.GetByKey(key);
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
