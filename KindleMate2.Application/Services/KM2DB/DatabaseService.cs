using KindleMate2.Domain.Interfaces.KM2DB;

namespace KindleMate2.Application.Services.KM2DB {
    public class DatabaseService(IDatabaseRepository repository) : IDatabaseService {
        public bool IsDatabaseEmpty() {
            return repository.IsDatabaseEmpty();
        }
    }
}
