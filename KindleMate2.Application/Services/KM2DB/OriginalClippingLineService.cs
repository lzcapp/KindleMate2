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
        /// 换行固定写成 <c>\r\n</c>。理由与「设备端偏好哪种行尾」**无关** —— 那种偏好本项目
        /// **未在真机样本上实测**,不作为依据。取舍依据是这两条:
        /// </para>
        /// <list type="number">
        /// <item><see cref="StreamWriter"/> 默认用 <see cref="Environment.NewLine"/>,会让同一份
        /// 代码在 Windows / Linux / macOS 上产出**不同字节**(Windows CRLF、类 Unix LF)。
        /// 同一输入给出不同输出本身就是缺陷,固定为 CRLF 即消除这种跨平台差异。</item>
        /// <item>对齐已退役、**只在 Windows 上运行**的原 WinForms 版行为(该版写出的即 CRLF),
        /// 见 <c>arch.md</c> 的「行为对齐原则」:功能与行为须与原 WinForms 版一致。</item>
        /// </list>
        /// <para>
        /// 编码为 UTF-8 **无 BOM** —— 这本就是 <see cref="StreamWriter"/> 的默认,未作改动。
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
