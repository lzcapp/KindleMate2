# KindleMate2.Avalonia(迁移中)

Avalonia UI 迁移工程:把 WinForms 主程序 UI 切换到 Avalonia,数据/业务层
(Domain / Application / Infrastructure)原样复用。

## 布局规范(2026-09-08 定)
**所有界面一律按原 WinForms UI 的布局复刻**——以 FrmMain.Designer.cs 等 Designer
文件为布局权威源(控件分区/停靠/层级照搬),不做自创布局;视觉只做等价的现代
渲染,不改信息架构。原版截图仅作观感校准。

## 迁移状态(2026-09-08)
- **阶段 1(主界面骨架)✅**:顶部数据库行 + 搜索行;左侧书/词列表(随 Tab 切换);
  右侧「标注 | 生词」双 Tab DataGrid;内存过滤(选书/选词/搜索词);状态栏计数;
  Fluent 主题随系统深浅色。编译 0 错误,smoke 通过。
- 阶段 2(行级操作:编辑/删除/复制/重命名)、阶段 3(导入/同步/维护/导出/统计)、
  阶段 4(关于/多语言/更新/发布主产物)待做。
- 此前 Spike 已证明:Infrastructure(Domain)可被非 WinForms UI 直接引用复用。

## 构建与运行
Avalonia 12.x 的 XAML 生成器要求 Roslyn ≥ 4.14(**SDK ≥ 9**;本机用 SDK 10 构建):

```bash
dotnet build KindleMate2.Avalonia/KindleMate2.Avalonia.csproj -c Debug
dotnet run --project KindleMate2.Avalonia/KindleMate2.Avalonia.csproj
```

启动后自动尝试打开 `KM2.db`(输出目录上溯四级的仓库根,或启动参数),或点「打开数据库…」。

## 无头自检(--smoke)
```bash
dotnet run --project KindleMate2.Avalonia/KindleMate2.Avalonia.csproj -c Debug -- \
  --smoke <path-to-current-schema.db> [out.txt]
```

## 注意
- **数据库 schema**:读取的是**当前程序生成的库**(clippings 表含 key/content/bookname 等列)。
  仓库根的 `KM2.db` 是旧版 Kindle Mate 格式(`source_clippings`/`book_id`),不适用。
- 设备同步(MediaDevices/MTP)与导入尚未接入壳;跨平台到 macOS 前需把 MTP 依赖抽成
  接口(计划中的 `IDeviceProvider`)。
- 未加入 `KindleMate2.sln`,独立构建,避免影响现有 WinForms 发布流水线(阶段 4 再切换)。
