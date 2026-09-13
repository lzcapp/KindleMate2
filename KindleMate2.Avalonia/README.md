# KindleMate2.Avalonia

Kindle Mate 2 的 **Avalonia 桌面客户端**。数据/业务层（`Shared` / `Domain` /
`Infrastructure` / `Application`）原样复用，平台专有代码收敛在独立程序集里。

> 2026-09-13：原 WinForms 壳（`KindleMate2/`）与 `DarkModeForms` 子模块已**退役**，
> 本工程即当前唯一的桌面 UI，并已加入 `KindleMate2.sln`。

## 设计规范

**核心原则：UI 与跨平台可变；功能与行为须与原 WinForms 版完全一致。**
界面按原版布局复刻（内容优先列表 + 右侧预览面板、搜索命令栏），并按设计系统自主打磨：
- 设计令牌：5 阶冷中性表面 + 唯一强调色 `#7B8CFF` + 4 类标注标签色；正文对比度
  15.2:1、次要 6.3:1（过 WCAG AA）。深浅双主题，默认跟随系统。
- 全部定义在 `App.axaml`（`ThemeDictionaries` + 控件样式），无第三方 UI 库。
- 图表是自研的 `Charts/ChartControl.cs`（重写 `Render(DrawingContext)` 自绘，零依赖）。
- 若用到 `DataGrid`，其主题须在 `App.axaml` 显式包含
  （`avares://Avalonia.Controls.DataGrid/Themes/Fluent.xaml`）—— 缺它会**整个渲染不出来**。

> **有意偏离原版之处**（均在代码注释与提交说明中标注）：导出失败改为弹窗（原版静默）、
> 清理数据库增加确认框（原版无）、统计截图成功后 Yes/No 追问（原版无条件打开）、
> **删除进回收站且可恢复**（原版直接连原始行一起删）。

## 平台与目标框架

| 工程 | TFM | 说明 |
|---|---|---|
| `KindleMate2.Avalonia` | `net8.0-windows;net8.0` | 双 TFM。Windows 用 `WinExe`，其他平台用 `Exe` |
| `KindleMate2.Devices.Windows` | `net8.0-windows` | Kindle 设备 USB/MTP 实现，仅 Windows TFM 引用 |
| 其余类库 | `net8.0` | 纯跨平台 |

设备层通过 `IDeviceManager` 抽象：Windows 用真实实现，其他平台由
`Application.Services.NullDeviceManager` 兜底（如实报告「未连接」）。

## 构建与运行

Avalonia 12.x 的 XAML 生成器要求 Roslyn ≥ 4.14（**SDK ≥ 9**；本机用 SDK 10 构建）。

```bash
# Windows（含设备支持）
dotnet build KindleMate2.Avalonia/KindleMate2.Avalonia.csproj -f net8.0-windows -c Debug
dotnet run   --project KindleMate2.Avalonia/KindleMate2.Avalonia.csproj -f net8.0-windows

# 跨平台
dotnet build KindleMate2.Avalonia/KindleMate2.Avalonia.csproj -f net8.0 -c Debug
```

启动后**自动使用程序当前目录下的 `KM2.dat`**：文件不存在会按当前 schema 自动建库；
库损坏时不做预校验，直接报错。**没有"选择数据库"入口** —— 与原版行为一致。
主题与语言偏好记录在 `%APPDATA%/KindleMate2/settings.json`。

## 无头自检

两套自检都不开窗口，用于 CI 与本机验证：

```bash
# 只读链路：列表 / 详情 / 统计 / 关于 / 设置 / 搜索 / 多语言
KindleMate2.Avalonia(.exe) --smoke <db> [out.txt]

# 写操作端到端：导入 → 导出 → 备份 → 重命名 → 编辑标注 → 删除 → 清理 → 重建
#              → 清空 → 空库重导 → 删整本/删整词 → 回收站(删除→可见→恢复→回主表)
#              → 重复 key 导入 → 批量插入降级 → 日志出口
KindleMate2.Avalonia(.exe) --ops <db> <clippings.txt> <vocab.db> <out.txt> [旧格式库样本]
```

`--ops` **全程在临时副本上执行**，不会改动传入的库。测试夹具见
`Documents\KindleMate2\fixtures\`（不在仓库内）。

## 注意

- **数据库 schema**：只读写**本程序生成的库**（扁平 `clippings(key, content, bookname, …)`）。
  仓库根原有的 `KM2.db` 属于另一套关系型 schema（`books` + `clippings.book_id` +
  `source_clippings`），**任何现有导入器都不支持**，已从仓库移除（样本保留在仓库外的
  测试夹具目录，仅供回归测试用）。旧数据的迁入通道是
  「管理 → 导入 Kindle Mate 数据库 / 导入 KMate 数据库」。
- **跨平台现状**：`net8.0` 变体可构建、可运行，除 **Kindle 设备同步**外功能完整。
  **各平台发布包已由 `release.yml` 覆盖**（Windows 6 个 zip + Linux 4 个 tar.gz + macOS 2 个 dmg）。
  仍待补齐的是**非 Windows 的设备支持本身**（挂载点 / libmtp）：那边由 `NullDeviceManager` 兜底，
  所以即便有这个平台的包，设备同步也不可用。
- **界面文案**一律走 `Shared.Strings`（简/繁/英三套），不要在 XAML / VM 里写死中文。
  新增文案需同步改 4 个 resx 并手工补 `Strings.Designer.cs` 的强类型属性。
- **日志**：库层（`Application` / `Infrastructure` / `Devices.Windows`）一律用
  `Shared.Diagnostics.AppLog`，**不要写 `Console.WriteLine`** —— Windows 上是 `WinExe`（无控制台），
  写 Console 的消息无人接收。App 层用 `Services.FileLogSink` 注入 sink，落到**程序目录**的
  `error.log`（已被 `.gitignore` 的 `*.log` 覆盖）。
  **自检路径（`--smoke` / `--ops`）不注入文件 sink** —— 它们的日志打在 stderr，避免污染 `error.log`。
- **CI**：`.github/workflows/build.yml` 在 windows 上跑全量构建 + 单测，在
  ubuntu/macOS 上跑跨平台构建 + 单测 + 启动自检。
  **`release.yml`** 负责发布：`version`（版本号唯一来源）→ `publish`（Windows 6 变体 zip）→
  `publish-linux`（ubuntu 上发布 linux-x64 / linux-arm64 × 框架依赖/自包含，tar.gz）→
  `publish-macos`（macOS 上发布自包含 .app，打包成 .dmg）→ `release`。
  **Linux 产物必须在 Unix runner 上打包** —— 从 NTFS 打出的归档记录不出可执行位，
  解压出来是 644、直接运行会 permission denied；打包前必须 `chmod +x`。
  **macOS 必须在 macOS runner 上做**：arm64 要求可执行文件签名有效（原生路径才会签），
  且 iconutil / codesign / hdiutil 只有 macOS 有。`.app` 里的 `launch` 脚本负责先 `cd` 到
  `~/Library/Application Support/KindleMate2/` 再 exec 真程序 —— Finder 启动时工作目录是 `/`，
  而库路径按「当前目录」解析（与原版一致）；**不能切进 .app 内部**（会丢数据且破坏 bundle 签名）。
