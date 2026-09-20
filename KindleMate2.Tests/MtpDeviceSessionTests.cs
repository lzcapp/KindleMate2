using Xunit;
using KindleMate2.Devices.MacOS;

namespace KindleMate2.Tests;

/// <summary>
/// MTP 层级查找(<c>MtpDeviceSession.FindChild</c>)的用例 —— 不需要真机。
///
/// 回归背景:第一版用 <c>level.FirstOrDefault(...)</c> 配 <c>is not { } x</c> 判空。
/// <c>MtpEntry</c> 是值类型,没命中时 <c>FirstOrDefault</c> 返回的是 <c>default</c>(全零的假条目),
/// 而 <c>is not { } x</c> 对非空值类型**恒为真**,于是"设备上找不到文件"被报成了
/// "找到 id=0 的对象"(真机探测时当场现形)。值类型集合里判"有没有",必须用可空局部变量。
///
/// 另一处真机才暴露的细节也钉在这里:查询根一层要传 <c>0xFFFFFFFF</c>,但设备**回报**的顶层
/// 条目 parent_id 是 <c>0</c> —— 两者不是一回事,匹配时用的是后者。
/// </summary>
public sealed class MtpDeviceSessionTests {
    private static MtpEntry Folder(uint id, uint parent, string name) =>
        new(id, parent, 65537, name, 0, MtpInterop.FileType.Folder);

    private static MtpEntry File(uint id, uint parent, string name, ulong size = 123) =>
        new(id, parent, 65537, name, size, MtpInterop.FileType.Text);

    [Fact]
    public void FindChild_OnEmptyLevel_ReturnsNull() {
        // 这一条就是那个 bug:空集合必须给出 null,而不是"命中 id=0"
        Assert.Null(MtpDeviceSession.FindChild([], 0, "documents"));
    }

    [Fact]
    public void FindChild_ReturnsNull_WhenNameMissing() {
        MtpEntry[] level = [Folder(13291, 0, "documents"), Folder(12978, 0, "system")];

        Assert.Null(MtpDeviceSession.FindChild(level, 0, "clippings"));
    }

    [Fact]
    public void FindChild_FindsDirectChildAtRootLevel() {
        // 根一层的条目 parent_id 是 0(设备回报值)
        MtpEntry[] level = [Folder(12978, 0, "system"), Folder(13291, 0, "documents")];

        var found = MtpDeviceSession.FindChild(level, MtpInterop.TopLevelReportedParentId, "documents");

        Assert.NotNull(found);
        Assert.Equal(13291u, found!.Value.ItemId);
    }

    [Fact]
    public void FindChild_MatchesNamesCaseInsensitively() {
        // Kindle 的卷是 FAT/exFAT,大小写不敏感;设备回报的名字大小写未必与常量一致
        MtpEntry[] level = [Folder(1, 0, "DOCUMENTS"), File(2, 1, "MY CLIPPINGS.TXT", 2700768)];

        Assert.NotNull(MtpDeviceSession.FindChild(level, 0, "documents"));
        Assert.NotNull(MtpDeviceSession.FindChild(level, 1, "My Clippings.txt"));
    }

    [Fact]
    public void FindChild_DoesNotUseSameNameFromAnotherParent() {
        // 别的目录下也有个同名文件 —— 不能命中
        MtpEntry[] level = [File(2, 13291, "My Clippings.txt"), File(4, 555, "My Clippings.txt")];

        var found = MtpDeviceSession.FindChild(level, 13291, "My Clippings.txt");

        Assert.NotNull(found);
        Assert.Equal(2u, found!.Value.ItemId);
    }

    [Fact]
    public void FindChild_ReadsSizeFromTheMatchingEntry() {
        MtpEntry[] level = [Folder(13291, 0, "documents"), File(13385, 13291, "My Clippings.txt", 2700768)];

        var found = MtpDeviceSession.FindChild(level, 13291, "My Clippings.txt");

        Assert.NotNull(found);
        Assert.Equal(2700768ul, found!.Value.Size);
        Assert.False(found.Value.IsFolder);
    }

    [Fact]
    public void FindChild_DistinguishesFoldersFromFiles() {
        MtpEntry[] level = [Folder(13417, 13291, "My Clippings.sdr")];

        var found = MtpDeviceSession.FindChild(level, 13291, "My Clippings.sdr");

        Assert.NotNull(found);
        Assert.True(found!.Value.IsFolder);
    }
}
