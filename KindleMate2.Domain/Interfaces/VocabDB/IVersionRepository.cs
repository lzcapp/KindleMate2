namespace KindleMate2.Domain.Interfaces.VocabDB {
    using Version = Entities.VocabDB.Version;

    public interface IVersionRepository {
        Version? GetById(string id);
        List<Version> GetAll();
        int GetCount();
        void Add(Version version);
        void Update(Version version);
        void Delete(string id);
    }
}