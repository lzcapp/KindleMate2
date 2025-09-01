using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Domain.Interfaces.KM2DB;

namespace KindleMate2.Application.Services.KM2DB {
    public class OriginalClippingLineService {
        private readonly IOriginalClippingLineRepository _repository;

        public OriginalClippingLineService(IOriginalClippingLineRepository repository) {
            _repository = repository;
        }

        public OriginalClippingLine? GetOriginalClippingLineByKey(string key) {
            return _repository.GetByKey(key);
        }

        public List<OriginalClippingLine> GetAllOriginalClippingLines() {
            return _repository.GetAll();
        }

        public int GetCount() {
            return _repository.GetCount();
        }

        public void AddOriginalClippingLine(OriginalClippingLine originalClippingLine) {
            if (string.IsNullOrWhiteSpace(originalClippingLine.key)) {
                throw new ArgumentException("[key] cannot be empty");
            }

            _repository.Add(originalClippingLine);
        }

        public void UpdateOriginalClippingLine(OriginalClippingLine originalClippingLine) {
            _repository.Update(originalClippingLine);
        }

        public void DeleteOriginalClippingLine(string key) {
            _repository.Delete(key);
        }

        public bool Export(string filePath, string fileName, out Exception? error) {
            error = null;
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

                return true;
            } catch (Exception ex) {
                error = ex;
                return false;
            }
        }
    }
}
