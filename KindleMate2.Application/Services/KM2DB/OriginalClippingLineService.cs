using System.Text;
using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Domain.Interfaces.KM2DB;
using KindleMate2.Shared.Entities;

namespace KindleMate2.Application.Services.KM2DB {
    public class OriginalClippingLineService(IOriginalClippingLineRepository repository) : IOriginalClippingLineService {
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
            if (string.IsNullOrWhiteSpace(originalClippingLine.Key)) {
                throw new ArgumentException("[Key] cannot be empty");
            }

            repository.Add(originalClippingLine);
        }

        public void UpdateOriginalClippingLine(OriginalClippingLine originalClippingLine) {
            repository.Update(originalClippingLine);
        }

        public void DeleteOriginalClippingLine(string key) {
            repository.Delete(key);
        }

        public void DeleteAllOriginalClippingLines() {
            repository.DeleteAll();
        }

        /// <summary>
        /// 把原始标注行逐条写出(用于写回设备与备份)。
        /// </summary>
        /// <remarks>
        /// <para>
        /// <paramref name="liveKeys"/> 为写回设备/备份的**过滤开关**:见接口上的说明。
        /// 之所以由调用方传入而不是在这里计算 —— 本服务只依赖
        /// <see cref="IOriginalClippingLineRepository"/>,拿不到 <c>clippings</c>,
        /// 无从判断哪些原始行属于回收站。
        /// </para>
        /// <para>
        /// 换行显式写成 <c>\r\n</c>:设备上的原文件是 CRLF,而 <see cref="StreamWriter"/>
        /// 默认用 <see cref="Environment.NewLine"/>,在 Linux / macOS 上会产出 LF-only 的文件,
        /// 与设备格式不一致。编码沿用 UTF-8 **无 BOM**(与设备原文件一致)。
        /// </para>
        /// </remarks>
        public bool Export(string filePath, string fileName, out Exception? exception,
            IReadOnlySet<string>? liveKeys = null) {
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
                using var writer = new StreamWriter(fileStream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)) {
                    NewLine = "\r\n"
                };
                foreach (OriginalClippingLine originalClippingLine in originalClippingLines) {
                    // 跳过回收站里的条目(原始行仍在、但 clippings 里已无该 key = 已删除)。
                    // 不过滤的话,写回设备会把用户删掉的标注一并写上去 —— 条目在设备上"复活"。
                    if (liveKeys is not null && !liveKeys.Contains(originalClippingLine.Key)) {
                        continue;
                    }

                    writer.WriteLine(originalClippingLine.Line1);
                    writer.WriteLine(originalClippingLine.Line2);
                    writer.WriteLine(originalClippingLine.Line3 ?? string.Empty);
                    writer.WriteLine(originalClippingLine.Line4);
                    writer.WriteLine(originalClippingLine.Line5 ?? "==========");
                }

                exception = null;
                return true;
            } catch (Exception ex) {
                exception = ex;
                return false;
            }
        }
    }
}
