using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Domain.Entities.MyClippings;
using KindleMate2.Infrastructure.Helpers;
using KindleMate2.Infrastructure.Repositories.KM2DB;
using KindleMate2.Shared.Constants;
using System.Globalization;
using System.Text.RegularExpressions;

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
