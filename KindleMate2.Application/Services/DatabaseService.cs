using KindleMate2.Infrastructure.Repositories;

namespace KindleMate2.Application.Services {
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
