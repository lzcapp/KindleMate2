# KindleMate2.Avalonia(Spike)

跨平台壳**概念验证**:用 Avalonia UI 复用现有分层(Domain / Infrastructure / SQLite 仓储),
验证"业务层可移植性"假设。当前是**只读浏览壳**,非产品。

## 验证结论(2026-09-08)
- 现有 `KindleMate2.Infrastructure`(ClippingRepository / DatabaseHelper)+ Domain 实体可被
  Avalonia 壳**直接引用并工作**,零业务逻辑改动 → 分层复用假设成立。
- 前置清理:Application/Infrastructure 不再引用 WinForms 子模块 DarkModeForms
  (`ThemeHelper` 改为直读注册表,行为等价)——这是非 WinForms UI 复用的必要解耦。

## 构建与运行
Avalonia 12.x 的 XAML 生成器要求 Roslyn ≥ 4.14(**SDK ≥ 9**;本机用 SDK 10 构建):

```bash
dotnet build KindleMate2.Avalonia/KindleMate2.Avalonia.csproj -c Debug
dotnet run --project KindleMate2.Avalonia/KindleMate2.Avalonia.csproj
```

启动后:
- 自动尝试打开 `KM2.db`(输出目录上溯四级的仓库根,或作为启动参数传入)
- 或点「打开数据库…」选择文件

## 无头自检(--smoke)
不开窗口验证数据链路,便于开发/CI:

```bash
dotnet run --project KindleMate2.Avalonia/KindleMate2.Avalonia.csproj -c Debug -- \
  --smoke <path-to-current-schema.db> [out.txt]
```

## 注意
- **数据库 schema**:壳读取的是**当前程序生成的库**(clippings 表含 key/content/bookname 等列)。
  仓库根的 `KM2.db` 是旧版 Kindle Mate 格式(`source_clippings`/`book_id`),不适用。
- 设备同步(MediaDevices/MTP)与导入(Application 层)尚未接入壳;
  跨平台到 macOS 前还需把 MTP 依赖抽成接口(计划中的 `IDeviceProvider`)。
- 未加入 `KindleMate2.sln`,独立构建,避免影响现有 WinForms 发布流水线。
