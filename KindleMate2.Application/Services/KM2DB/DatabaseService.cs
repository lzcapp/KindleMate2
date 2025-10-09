using KindleMate2.Infrastructure.Repositories.KM2DB;

namespace KindleMate2.Application.Services.KM2DB {
    public class DatabaseService(DatabaseRepository repository) {
        public bool IsDatabaseEmpty() {
            return repository.IsDatabaseEmpty();
        }
    }
}
