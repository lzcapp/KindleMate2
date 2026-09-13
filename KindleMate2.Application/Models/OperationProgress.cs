namespace KindleMate2.Application.Models;

/// <summary>长时操作所处的阶段。应用层只报阶段枚举,展示文案由 UI 层本地化。</summary>
public enum OperationStage {
    /// <summary>没有进行中的操作。</summary>
    None,

    /// <summary>读取源文件。</summary>
    ReadingFile,

    /// <summary>解析文本(如 My Clippings.txt)。</summary>
    Parsing,

    /// <summary>准备数据(判重、构建待写入集合)。</summary>
    Preparing,

    /// <summary>写入数据库。</summary>
    Writing,

    /// <summary>重新载入并刷新界面。</summary>
    Reloading
}

/// <summary>
/// 操作进度快照。应用层通过 <see cref="IProgress{T}"/> 上报,UI 层负责渲染。
///
/// <see cref="Fraction"/> 为 null 表示无法估计比例(界面应使用不确定态),
/// 否则为 0..1 的确定比例 —— 例如导入标注时按「已处理条数 / 解析出的总条数」计算。
/// </summary>
public readonly record struct OperationProgress(OperationStage Stage, int Current = 0, int Total = 0) {
    /// <summary>确定比例(0..1);<see cref="Total"/> 不为正时返回 null。</summary>
    public double? Fraction => Total > 0 ? Math.Clamp((double)Current / Total, 0d, 1d) : null;

    /// <summary>仅阶段、无数量(不确定态)。</summary>
    public static OperationProgress At(OperationStage stage) => new(stage);
}
