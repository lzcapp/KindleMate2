using KindleMate2.Application.Services.KM2DB;
using KindleMate2.Domain.Interfaces.KM2DB;

namespace KindleMate2.Application.Services;

/// <inheritdoc cref="IKmateDatabaseServiceFactory"/>
public class KmateDatabaseServiceFactory : IKmateDatabaseServiceFactory {
    private readonly IClippingRepository _clippingRepository;
    private readonly ILookupRepository _lookupRepository;
    private readonly IOriginalClippingLineRepository _originalClippingLineRepository;
    private readonly IVocabRepository _vocabRepository;
    private readonly string _targetConnectionString;

    public KmateDatabaseServiceFactory(
        IClippingRepository clippingRepository,
        ILookupRepository lookupRepository,
        IOriginalClippingLineRepository originalClippingLineRepository,
        IVocabRepository vocabRepository,
        string targetConnectionString) {
        _clippingRepository = clippingRepository;
        _lookupRepository = lookupRepository;
        _originalClippingLineRepository = originalClippingLineRepository;
        _vocabRepository = vocabRepository;
        _targetConnectionString = targetConnectionString;
    }

    /// <summary>
    /// 目标库连接串由壳按「当前打开的库」传入,不再取 <c>AppConstants.ConnectionString</c>
    /// ——那个常量写死了相对路径 <c>KM2.dat</c>,只在"cwd 恰好等于库目录"时才正确;
    /// 一旦支持多库或库选择器,<c>KmateAtomicWriter</c> 就会往错误的库写入。
    /// 同族的 <see cref="KmDatabaseServiceFactory"/> 一直是按路径构造的,这里补齐一致性。
    /// </summary>
    public KmateDatabaseService Create(string km3DbPath) {
        return new KmateDatabaseService(
            _clippingRepository,
            _lookupRepository,
            _originalClippingLineRepository,
            _vocabRepository,
            km3DbPath,
            _targetConnectionString);
    }
}
