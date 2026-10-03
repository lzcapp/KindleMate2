using KindleMate2.Domain.Entities.KM2DB;
using KindleMate2.Shared.Entities;

namespace KindleMate2.Application.Services.KM2DB;

public interface IOriginalClippingLineService {
    OriginalClippingLine? GetOriginalClippingLineByKey(string key);
    List<OriginalClippingLine> GetAllOriginalClippingLines();
    List<OriginalClippingLine> GetByFuzzySearch(string search, AppEntities.SearchType type);
    int GetCount();
    void AddOriginalClippingLine(OriginalClippingLine originalClippingLine);
    void UpdateOriginalClippingLine(OriginalClippingLine originalClippingLine);
    void DeleteOriginalClippingLine(string key);
    void DeleteAllOriginalClippingLines();

    /// <summary>
    /// 把原始标注行逐条写出(用于写回设备与备份)。条目结构见 <c>formats.md</c> §1.2
    /// (书名 + 元数据 + 空行 + 正文 + <c>==========</c> 分隔),行尾固定 CRLF ——
    /// 理由见实现上的说明(未对设备端的行尾偏好作实测断言)。
    /// </summary>
    /// <param name="filePath">目标目录,不存在时会创建。</param>
    /// <param name="fileName">文件名。</param>
    /// <param name="exception">失败时的异常;成功为 <c>null</c>。</param>
    /// <param name="liveKeys">
    /// 「**库里仍然存在**」的标注 key 集合(即 <c>clippings</c> 表里的 key)。
    /// <para>
    /// 传 <c>null</c> 表示不过滤 —— 全部原始行都写出(仅供确实需要整表快照的场合)。
    /// </para>
    /// <para>
    /// 传非空集合时,**只**写出 key 落在集合内的原始行。这是写回设备/备份的**必需**语义:
    /// <c>original_clipping_lines</c> 同时充当回收站(「原始行仍在、但 <c>clippings</c> 里已无该
    /// key」= 已删除),若不过滤,已删除的标注会被一并导出、再覆盖回设备 → 条目复活。
    /// </para>
    /// </param>
    bool Export(string filePath, string fileName, out Exception? exception,
        IReadOnlySet<string>? liveKeys = null);
}
