using KindleMate2.Infrastructure.Repositories.KM2DB;

namespace KindleMate2.Application.Services.KM2DB {
    public class DatabaseService {
        private readonly DatabaseRepository _repository;

        public DatabaseService(DatabaseRepository repository) {
            _repository = repository;
        }

        public bool IsDatabaseEmpty() {
            return _repository.IsDatabaseEmpty();
        }
    }
}
