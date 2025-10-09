using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Domain.Interfaces.KM2DB;
using KindleMate2.Shared.Entities;

namespace KindleMate2.Application.Services.KM2DB {
    public class OriginalClippingLineService(IOriginalClippingLineRepository repository) {
        public OriginalClippingLine? GetOriginalClippingLineByKey(string key) {
            return repository.GetByKey(key);
        }

        public List<OriginalClippingLine> GetAllOriginalClippingLines() {
            return repository.GetAll();
        }

        public List<OriginalClippingLine> GetByFuzzySearch(string search, AppEntities.SearchType type) {
            return repository.GetByFuzzySearch(search, type);
        }

        public int GetCount() {
            return repository.GetCount();
        }

        public void AddOriginalClippingLine(OriginalClippingLine originalClippingLine) {
            if (string.IsNullOrWhiteSpace(originalClippingLine.key)) {
                throw new ArgumentException("[key] cannot be empty");
            }

            repository.Add(originalClippingLine);
        }

        public void UpdateOriginalClippingLine(OriginalClippingLine originalClippingLine) {
            repository.Update(originalClippingLine);
        }

        public void DeleteOriginalClippingLine(string key) {
            repository.Delete(key);
        }

        public bool Export(string filePath, string fileName, out Exception exception) {
            try {
                var originalClippingLines = GetAllOriginalClippingLines();

                var exportFilePath = Path.Combine(filePath, fileName);

                if (!Directory.Exists(filePath)) {
                    Directory.CreateDirectory(filePath);
                }

                if (File.Exists(exportFilePath)) {
                    File.Delete(exportFilePath);
                }

                using var fileStream = new FileStream(exportFilePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
                using var writer = new StreamWriter(fileStream);
                foreach (OriginalClippingLine originalClippingLine in originalClippingLines) {
                    writer.WriteLine(originalClippingLine.line1);
                    writer.WriteLine(originalClippingLine.line2);
                    writer.WriteLine(originalClippingLine.line3);
                    writer.WriteLine(originalClippingLine.line4);
                    writer.WriteLine(originalClippingLine.line5);
                }

                writer.Close();
                fileStream.Close();

                exception = new Exception();
                return true;
            } catch (Exception ex) {
                exception = ex;
                return false;
            }
        }
    }
}
