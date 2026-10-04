namespace KindleMate2.Application.Services;

public interface IExportManager {
    bool ExportClippingsToMarkdown(string bookName = "");
    bool ExportVocabsToMarkdown(string word = "");
    bool ExportClippingsToCsv();
    /// <summary>导出生词 CSV。<paramref name="includeDefinitions"/> 为 true 时会**联网**查释义(把生词发给有道)。</summary>
    Task<bool> ExportVocabsToCsvAsync(bool includeDefinitions, CancellationToken cancellationToken = default);
    /// <summary>导出标注为 JSON(按书名分组),离线。</summary>
    bool ExportClippingsToJson();
    /// <summary>导出生词为 JSON,离线(不查释义)。</summary>
    bool ExportVocabsToJson();
    /// <summary>
    /// 备份标注为可直接放回 Kindle 的 <c>MyClippings_&lt;时间戳&gt;.txt</c>。
    /// 内容 = **库里仍然存在的标注**,不含回收站里已删除的条目。
    /// </summary>
    bool BackupClippings(out Exception? exception);
    /// <summary>
    /// 把标注写回已连接的 Kindle 设备(覆盖设备上的 <c>My Clippings.txt</c>)。
    /// 写回的只有**库里仍然存在的标注** —— 已删除的条目不会被写回,否则会在设备上"复活"。
    /// </summary>
    void SyncToKindle();
    void BackupDatabase();
}
