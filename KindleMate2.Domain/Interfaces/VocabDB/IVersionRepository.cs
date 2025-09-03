namespace KindleMate2.Domain.Interfaces.VocabDB {
    using Version = Entities.VocabDB.Version;

    public interface IVersionRepository {
        Version? GetById(string id);

        List<Version> GetAll();

        int GetCount();

        bool Add(Version version);

        bool Update(Version version);

        bool Delete(string id);
    }
}