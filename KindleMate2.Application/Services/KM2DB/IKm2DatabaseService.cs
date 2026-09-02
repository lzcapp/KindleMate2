namespace KindleMate2.Application.Services.KM2DB;

public interface IKm2DatabaseService {
    bool ImportKindleClippings(string clippingsPath, out Dictionary<string, string> result);
    bool RebuildDatabase(out Dictionary<string, string> result);
    bool UpdateFrequency();
    bool CleanDatabase(string databaseFilePath, out Dictionary<string, string> result);
    bool IsDatabaseEmpty();
    bool DeleteAllData();
}
