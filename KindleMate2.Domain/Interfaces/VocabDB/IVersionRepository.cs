using Version = KindleMate2.Domain.Entities.VocabDB.Version;

namespace KindleMate2.Domain.Interfaces.VocabDB {
    using Version = Version;

    public interface IVersionRepository {
        Version? GetById(string id);

        List<Version> GetAll();

        int GetCount();

        bool Add(Version version);

        bool Update(Version version);

        bool Delete(string id);
    }
}