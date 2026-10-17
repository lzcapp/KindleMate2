# KindleMate2 架构文档

> 最后更新：2026-10-03

## 项目概述

KindleMate2 是 Kindle 标注 / 生词本的管理与整理工具。数据来自 Kindle 设备的
`My Clippings.txt` 与 `vocab.db`，统一落在本地 SQLite 库中，供检索、编辑、
统计与导出（Markdown / HTML+CSS / CSV / JSON / 分享卡 PNG / Obsidian vault）。

> 两个源文件的**结构与字段语义**（含若干极易误解的字段，例如 `LOOKUPS.pos` 其实是
> 书中位置而非词性）单独记在 [`formats.md`](formats.md)。

当前形态：**Avalonia 是唯一的桌面 UI**，架构按分层组织，并已做到
**库层与设备层均跨平台**（Windows / macOS / Linux 均可构建，三平台设备同步均支持）。

---

## 架构设计

### 1. 分层架构

| 层 | 项目 | 职责 |
|---|---|---|
| 表现层 (UI) | `KindleMate2.Avalonia` | 界面与交互（Avalonia 12，无第三方 UI 库） |
| 应用层 (Application) | `KindleMate2.Application` | 协调领域与基础设施；导入 / 导出 / 维护 / 设备接口 |
| 领域层 (Domain) | `KindleMate2.Domain` | 实体与仓储接口 |
| 基础设施层 (Infrastructure) | `KindleMate2.Infrastructure` | SQLite 仓储实现、建库 / 迁移 / 备份、Markdown 导出 |
| 共享层 (Shared) | `KindleMate2.Shared` | 常量、实体、多语言资源（`Strings`） |
| 平台实现 | `KindleMate2.Devices.Windows` | Windows 专有设备实现（USB 盘符 + MTP(MediaDevices) + WMI） |
| 平台实现 | `KindleMate2.Devices.Posix` | macOS/Linux 共用的设备实现（libmtp P/Invoke + libusb 只读探测 + 卷扫描） |
| 平台壳 | `KindleMate2.Devices.MacOS` / `KindleMate2.Devices.Linux` | 上面 Posix 实现的薄壳（候选挂载根不同），均为中性 `net10.0` |
| 测试 | `KindleMate2.Tests` | 单元测试（`dotnet test`；真机/联网探测默认跳过，需环境变量开启） |

> 历史说明：早期的 WPF（`KindleMate2.UI`）与 Windows Forms（`KindleMate2`）两个壳
> 已于 2026-09-13 退役删除，`DarkModeForms` 子模块随之移除。
> **本仓库目前不含任何 git 子模块。**

### 2. 项目与目标框架

| 项目 | TFM | 说明 |
|---|---|---|
| `KindleMate2.Shared` | `net10.0` | 纯跨平台 |
| `KindleMate2.Domain` | `net10.0` | 纯跨平台 |
| `KindleMate2.Infrastructure` | `net10.0` | 纯跨平台 |
| `KindleMate2.Application` | `net10.0` | 纯跨平台 |
| `KindleMate2.Avalonia` | **按宿主 OS 条件化**：非 Windows 宿主上 `net10.0`；Windows 宿主上 `net10.0-windows;net10.0` | Windows 用 `WinExe`，其他平台用 `Exe`。条件化是为了让 IDE 在 macOS/Linux 上没有选中 `net10.0-windows` 的余地（那个 TFM 会走 Windows 专有实现，见 §3 末尾） |
| `KindleMate2.Devices.Windows` | `net10.0-windows` | 仅被 Avalonia 的 Windows TFM 引用；该 TFM 只在 Windows 宿主上构建 |
| `KindleMate2.Devices.Posix` | `net10.0` | 纯 BCL（libmtp P/Invoke + libusb 探测），macOS / Linux 共用 |
| `KindleMate2.Devices.MacOS` | `net10.0` | Posix 实现的 macOS 薄壳（`/Volumes` 挂载根） |
| `KindleMate2.Devices.Linux` | `net10.0` | Posix 实现的 Linux 薄壳（`/media`、`/run/media` 等挂载根） |
| `KindleMate2.Tests` | `net10.0` | 只引用 Application / Infrastructure / Domain，可跨平台运行 |

### 3. 项目依赖关系

```mermaid
graph TD
    UI["KindleMate2.Avalonia<br/>net10.0（Windows 宿主另加 net10.0-windows）"] --> APP["KindleMate2.Application<br/>net10.0"]
    UI --> INF["KindleMate2.Infrastructure<br/>net10.0"]
    UI --> DOM["KindleMate2.Domain<br/>net10.0"]
    UI --> SH["KindleMate2.Shared<br/>net10.0"]
    UI -. "仅 Windows TFM" .-> DEV["KindleMate2.Devices.Windows<br/>net10.0-windows"]
    UI -. "非 Windows" .-> POS["Devices.MacOS / Devices.Linux<br/>net10.0（薄壳）"]

    APP --> DOM
    APP --> INF
    APP --> SH

    DEV --> APP
    DEV --> DOM
    DEV --> INF
    DEV --> SH

    POS --> POSIX["KindleMate2.Devices.Posix<br/>net10.0"]
    POSIX --> APP
    POSIX --> DOM
    POSIX --> INF
    POSIX --> SH

    INF --> DOM
    INF --> SH

    DOM --> SH

    T["KindleMate2.Tests<br/>net10.0"] --> APP
    T --> INF
    T --> DOM
    T --> POS
    T --> POSIX
```

### 4. 平台专有代码的边界

**原则：平台专有代码一律收敛到独立程序集，不在业务层散布条件编译。**

- Windows 专有程序集是 `KindleMate2.Devices.Windows`（USB 盘符枚举、MTP(MediaDevices)、WMI 事件监听，`net10.0-windows`，依赖 `MediaDevices` + `System.Management`）。
- macOS / Linux 的设备实现是纯 BCL 的 `KindleMate2.Devices.Posix`（`net10.0`：libmtp 的 P/Invoke 绑定、libusb 只读 USB 探测、卷扫描），由两个薄壳 `Devices.MacOS` / `Devices.Linux` 注入各自的候选挂载根；因此这两个程序集可在**任意平台**编译与单测（用例把卷根指到临时目录）。
- `Application` 只定义跨平台接口 `IDeviceManager`；三者之外的平台才落到 `Application.Services.NullDeviceManager` 兜底（如实报告"未连接"，同步操作抛 `PlatformNotSupportedException` 而不是静默失败）。
- **`IDeviceManager` 的注册由各平台壳负责**，不在 `Application/DependencyInjection.cs` 里 —— 否则 Application 就得引用平台专有程序集。Avalonia 在 `Services/DatabaseSession.cs` 内按平台选择实现。
- Avalonia 壳仅在 Windows TFM 下引用 `Devices.Windows`，因此 `KindleMate2.Devices.Windows.dll` 只出现在 `net10.0-windows` 的输出目录中。**该 TFM 自 2026-10-03 起按宿主 OS 条件化**（`$([MSBuild]::IsOSPlatform('Windows'))`，只在 Windows 宿主上构建）—— 因为 `net10.0-windows` 自带 `WINDOWS` 常量，会让 `DatabaseSession.CreateDeviceManager` 选中 `Devices.Windows`，而它依赖的 `MediaDevices`（Windows COM/MTA）在 macOS/Linux 上**必然抛异常**（`Failed to set MTA apartment state`），表现为设备永远显示"未连接"。条件化后非 Windows 宿主上根本不存在这个 TFM，IDE 没有误选的余地。

---

## 关键组件与数据流

### `Services/DatabaseSession.cs`（UI 与业务之间的组装点）

按"当前打开的库路径"组装仓储 + 服务 + 各工厂 + `DeviceManager` / `ImportManager` /
`ExportManager`。这是绕开 `Application/DependencyInjection.cs` 中硬编码连接串的正解，
所有 UI 动作都经由它。

```
Avalonia View  →  ViewModel  →  DatabaseSession  →  Infrastructure 仓储  →  SQLite
```

### 操作反馈契约（`OperationResult`）

所有写操作返回 `OperationResult(Ok, Title, Message, Kind, FolderToOpen)`，
**VM 不弹窗**，对话框由视图层按文案弹出。成败判定沿用原版语义：

| 情况 | 判定 | 弹窗 |
|---|---|---|
| 操作返回**非空串** | 成功 | 标题=成功标题，正文=返回串 |
| 操作返回**空串** | 失败 | 弹"只有标题"的错误框 |
| 抛异常 | 失败 | 标题=失败标题，正文=`失败标题 + 换行 + 异常消息` |

`Kind` 决定弹窗类型：`Silent`（不弹，如删除成功）/ `Info`（单按钮）/
`OpenFolderPrompt`（"需要打开文件夹吗?"）。

---

## 数据库生命周期

**库路径固定为 `<当前目录>/KM2.dat`**（与原 WinForms 版一致，没有库选择器）：

1. 文件不存在 → `DatabaseHelper.CreateDatabase()` 自动建库；失败 → 错误框 + 退出。
2. `DatabaseHelper.MigrateSchemaIfNeeded()` + `DatabaseHelper.MigrateLookupsSchemaIfNeeded()`
   一次性幂等 schema 迁移，失败仅告警；随后 `DatabaseHelper.EnsureSoftDeleteSchemaIfNeeded()`
   补软删除两列与 `import_batches` 表（**必须排在 MigrateLookupsSchemaIfNeeded 之后** ——
   后者重建 `lookups` 用的是老的 5 列脚本，会抹掉 `deleted_at` / `origin_batch`；
   本迁移按 `PRAGMA table_info` 探测列是否存在，故对新库、老库、半迁移库都幂等）；
   最后 `DatabaseHelper.EnsureIndexesIfNeeded()` 幂等补齐查询索引（`clippings` 复合索引、
   `vocab.word_key`），失败同样仅告警。索引 SQL 同源于 `GetIndexScripts()`。
   **迁移后置条件验证**：`DatabaseHelper.NeedsSoftDeleteColumns()` 确认软删两列真的补上了 ——
   表在、列缺 = 迁移失败，按 **Fatal 拒绝打开**（错误框 + 退出；修复磁盘 / 占用后重启即恢复，
   迁移按列探测幂等，重跑安全）。原因：半迁移库「读得写不得」（读路径按 `HasColumn` 分支，
   写路径会 `no such column`），与其让用户撞含糊的 SQLite 报错，不如说清成因；
   缺表 / 读不出不归该探测管，交由既有缺表提示与「库打不开」错误框接手，
   免得把「结构损坏」误诊成「迁移失败」。
3. 进程退出（`AppDomain.ProcessExit`）→ `DatabaseHelper.BackupDatabase()` 自动备份到
   `Backups/OnExit/`，随后 `DatabaseHelper.PruneBackups()` 只保留最新 3 份。

> **退出备份为什么单独一个子目录**：它每次关闭都产生一份，而手动备份与「清洗标注文本」前的
> 保护性备份都落在 `Backups/` 根下 —— 混在一起时根目录很快被一串时间戳文件淹没，用户主动要的
> 那几份反而找不着。分开后根目录只留用户自己要的，自动产物集中一处并由保留策略统一收敛。
> 保留份数是 `AppConstants.ExitBackupKeepCount`；判定新旧用**文件名里的时间戳**而非 mtime
> （后者会被复制 / 云同步改写），这也是备份名时间戳必须走 `InvariantCulture` 的原因之一。

当前格式的 schema 由 `DatabaseHelper.CreateDatabase()` 定义（`clippings` / `lookups` /
`original_clipping_lines` / `settings` / `vocab` / `import_batches`）。

### vocab / lookups 的软删除与批次溯源（2026-10-11）

删除 `vocab` / `lookups` 是**软删除**：置 `deleted_at`（`yyyy-MM-dd HH:mm:ss`，
InvariantCulture），行留着占住唯一键。这样"删过的词再导一次"不会被当成新行插回来
（复活缺陷）—— 判重读 `GetAllIncludingDeleted()`（含 tombstone），批量插入用
`INSERT OR IGNORE`，让 `UNIQUE(word_key, timestamp)` / 主键 `id` 当最终裁判。

- `GetAll()` 只返回活跃行（UI / 导出 / 词频 / 状态栏都按"用户看得见的条目"算）；
  `GetAllIncludingDeleted()` 只给导入判重用。
- `origin_batch` 指向 `import_batches.id`：每次导入开一行批次记录（来源类型 + 路径 +
  时间 + 计数），事后可对账"这批词是哪次导进来的"，也是将来「撤销本次导入」的抓手。
  批次只记账、**不参与判重**；批次行本身写不进去（表缺失 / 库被占 / 磁盘满）时
  `BeginBatch` / `CompleteBatch` 兜底吞异常，降级为「无批次」（溯源列留 NULL），
  不会反过来弄挂主导入。
- `DeleteAll()`（清空全部数据）保持**硬删**：那条路径先整库备份再清空，语义是"我要全新
  的库"，留 tombstone 反而会挡住随后的重新导入。
- `SoftDeleteAll()`（左栏「全部生词」节点，经 `VocabService.DeleteAllVocabs`）是**软删**：
  与单删同语义，批量删后重导入不整批复活 —— 否则「删全部 → 重导」就是复活缺陷原样复现，
  只是规模是全部。想整库重来的出口是「清空全部数据」（硬删 + 先备份）。
  两者刻意分开：批量删除 ≠ 我要全新的库。
- `MergeWordKey` 搬行连 tombstone 一起搬（软删除状态是行的事实，跟着词走）。
- **旧格式源库兼容**：扁平 KM 导入用同一套仓储读改造前的 `.dat`（没有这两列），
  `Query()` 按 `DatabaseHelper.HasColumn()` 探测后分支列清单与 WHERE，
  `MapVocab` / `MapLookup` 按 `FieldCount` 给缺失列补 null —— 一条 `no such column`
  都不能有。只有 `GetAll` / `GetAllIncludingDeleted` 会碰到旧源库；其余方法
  （`GetCount` / 模糊搜索等）只在当前库上用，当前库启动时已迁移、**必有新列**
  （启动时 `NeedsSoftDeleteColumns` 验证，缺列直接拒打开）。
  模糊搜索拼 `WHERE` 时谓词必须**包一层括号**：`SearchType.All` 是 `A OR B`，
  不括起来就接 `AND deleted_at IS NULL` 会被 SQL 优先级短路成 `A OR (B AND …)`，
  命中 word 分支就绕过软删过滤（vocab / lookup 两处写法都要遵守）。

> **`user_version` 保持 1 不升版**：纯加列迁移，旧版读端用显式列名不受影响，
> 因此降级回旧版仍能打开库（逃生门）。旧版对新列一无所知，只是不读不写。
> 但**降级可用 ≠ 行为一致**：旧版不认识 `deleted_at`，会把已软删的行重新显示在列表里，
> `GetCount()` 也把 tombstone 计进去，旧版的删除仍是硬删 —— 逃生门是「打得开」，
> 不是「行为无感」。

> **跨库合并时源库的 tombstone 不传染**：导入判重读的是**源侧** `GetAll()`（活跃行），
> 所以「在 A 库删了某词 → 把 A 库合并进 B 库」，该词在 B 库里仍是活的。
> 这是有意的：删除表达「我不要它了」，不该随数据搬运传播；防复活的 tombstone 过滤
> 发生在**目标库**一侧（目标侧 `GetAllIncludingDeleted()` 种判重集合 + `INSERT OR IGNORE`
> 撞键跳过）。

> **旧格式库不能直接打开。** 那套关系型旧库（样本在仓库外 fixtures）虽有 `clippings` 表但**没有 `key` 列**，
> 直接打开会报 `no such column: key`。迁入旧数据的通道是
> **菜单「管理 → 导入 Kindle Mate 数据库 / 导入 Kindle Mate 2 数据库 / 导入 KMate 数据库」**（迁移，而非原地读取）。

---

## 多语言

- 全部用户可见文案走 `KindleMate2.Shared` 的 `Strings` 资源，**禁止在 XAML / VM 里写死中文**。
- 4 份资源需同步维护：`Strings.resx`（中性=简中）、`Strings.zh-hans.resx`、
  `Strings.zh-hant.resx`、`Strings.en.resx`。
- `Strings.Designer.cs` 由 `ResXFileCodeGenerator` 生成，新增键需**手工补**强类型属性；
  若在 VS 中重新生成该文件，手工增删会被覆盖 —— `.resx` 才是权威来源。
- 清理死键：UI 重构后容易残留"定义了但没人引用"的键。**不要靠肉眼扫** —— 键有两种引用
  写法（`Strings.Ui_X` 与字面量 `"Ui_X"`），漏掉后者会误删在用的键。建议写脚本审计：
  从 `Strings.Designer.cs` 取键清单，扫描 Shared 目录**之外**的全部源码判定引用，
  零引用者才删。注意 `Designer.cs` 与 `.resx` 自身包含所有键名，**必须排除出扫描范围**，
  否则审计恒为空。

---

## 技术栈

- **框架**：.NET 10（构建 SDK 与目标框架自 2026-09-14 起统一为 10.x）
- **UI**：Avalonia 12（自研 `Charts/ChartControl` 自绘图表，零第三方 UI 库；
  设计令牌 + `ThemeDictionaries` 深浅双主题）
- **数据库**：SQLite（`Microsoft.Data.Sqlite`）
- **Markdown**：`Markdig` + `Leisn.MarkdigToc`
- **设备**：`MediaDevices` + `System.Management`（仅 `Devices.Windows`）

---

## 验证手段

| 手段 | 命令 | 覆盖 |
|---|---|---|
| 只读自检 | `KindleMate2 --smoke <db> [out]` | 列表 / 详情 / 统计 / 关于 / 设置 / 搜索 / 多语言 |
| 写操作端到端 | `--ops <db> <clippings.txt> <vocab.db> <out>` | 导入 → 导出 → 备份 → 退出备份落点 → 重命名 → 删除 → 清理 → 重建 → 清空 → 空库重导 |
| 单元测试 | `dotnet test KindleMate2.Tests` | 跨平台 TFM；真机 / 联网探测默认跳过，需 `KM2_MANUAL_DEVICE_TESTS=1` / `KM2_MANUAL_NETWORK_TESTS=1` 手动开启 |
| CI（验证） | `.github/workflows/build.yml` | windows 全量构建 + 单测；ubuntu/macOS 跨平台构建 + 单测 + 启动自检 |
| CI（发布） | `.github/workflows/release.yml` | 12 个资产：Windows 6 变体 zip + Linux 4 变体 tar.gz（内含 `kindlemate2` 启动器）+ macOS 2 个 dmg（内含 .app）；发布前对 `linux-x64_runtime` 与 macOS 产物各做一次「解压/挂载即跑」自检 |

两套自检**全程在临时副本上执行**，不会改动传入的真实数据库。

---

## 已知边界与待办

- **设备支持**：Windows 走 USB 盘符 + MTP(MediaDevices)；macOS / Linux 走 USB 大容量存储 + MTP(libmtp，`Devices.Posix`)。
  macOS 发布包内嵌 libmtp/libusb；Linux 依赖系统自带的 libmtp/libusb（缺失时仅 MTP 机型不可用，USB 大容量存储照常）。
  三者之外的平台才落到 `NullDeviceManager`，设备同步不可用，其余功能完整。
- **各平台打包发布已完成**：`release.yml` 共 12 个资产 —— Windows 六变体 zip（x64/x86/arm64 ×
  框架依赖/自包含）+ Linux 四变体 tar.gz（linux-x64 / linux-arm64 × 两种）+ macOS 两个 dmg
  （macos-arm64 / macos-x64，均自包含，内含 `KindleMate2.app`）。
  ⚠️ **Linux 产物必须在 Unix runner 上发布并打包** —— 从 NTFS 打出的归档记录不出可执行位，
  解压出来是 644、运行即 permission denied；故用 `tar.gz` 且打包前 `chmod +x`。
  包内另带 `kindlemate2` 启动器：程序按「当前工作目录」定位库（与原版一致），而双击或从别处调用时
  工作目录并不在解压目录 —— 启动器先切到 XDG 数据目录（`$KINDLEMATE2_HOME`，否则
  `${XDG_DATA_HOME:-~/.local/share}/KindleMate2`）再 exec 真程序。自检也走启动器，这段逻辑同样被验到。
  **macOS 产物必须在 macOS runner 上做**（arm64 要求可执行文件签名有效，只有原生路径会签；
  iconutil / codesign / hdiutil 也只有 macOS 有）：bundle 内放一个 `launch` 脚本当 `CFBundleExecutable`，
  先 `cd` 到 `~/Library/Application Support/KindleMate2/` 再 exec 真程序 —— Finder 启动时工作目录是 `/`，
  而库路径按「当前目录」解析（与原版一致）；**不能切进 .app 内部**（更新应用会丢数据，且改动 bundle
  内容会破坏代码签名）。**签名默认只到 ad-hoc**（无 Apple 开发者账号、未公证），用户首次启动需清 quarantine；
  `release.yml` 已预留可选的 Developer ID 签名 + 公证（配置 `APPLE_CERTIFICATE_BASE64` 等 secrets 即启用，
  含 `scripts/macos-entitlements.plist`；未配置时自动回退 ad-hoc）。该正式签名路径尚未实机验证。
  另：`checksums` job 用维护者的 GPG 密钥（`GPG_PRIVATE_KEY` / `GPG_PASSPHRASE`）对 `SHA256SUMS` 做分离签名；
  `release-prep` 打的发布 tag 也用同一把密钥做 **GPG 签名**（`git tag -s`，可在本地 `git tag --verify` 验证）。
  **三平台数据目录约定**：Windows = 程序所在目录（同旧版，双击 exe 的默认工作目录）；
  Linux = `~/.local/share/KindleMate2/`；macOS = `~/Library/Application Support/KindleMate2/`。
  库文件与 `Backups`/`Imports`/`Temp`/`Exports` 都在这之下（由启动器切换工作目录实现，程序本身不改）。
  单文件打包（`PublishSingleFile`）仍未启用（未实机验证）。
- **版本号来自 git tag**：`KindleMate2.Avalonia.csproj` 不再维护 `<Version>`；构建时由 MSBuild 目标
  `Km2SetVersionFromGitTag` 取**最新的日期式 tag**（去前导零）作为程序集 / 文件 / 信息版本，发布时
  `release.yml` 用 `-p:Version` 覆盖。因此**发版无需在 main 提交任何版本号**（main 有必需状态检查，直推会被拒）。
- **构建 SDK 必须 ≥ 10**：Avalonia 12 的 XAML 源生成器引用 `Microsoft.CodeAnalysis 4.14`，
  在 SDK 8/9 上会被 Roslyn **静默跳过**（只发 CS9057 警告），表现为每个 `.axaml.cs` 满屏
  `CS0103: InitializeComponent 不存在`。
- **自动更新**：Avalonia 壳内置更新检查与安装（`Application/Services/UpdateChecker` + `UpdateInstaller`，数据源为本仓库的 GitHub Releases API，下载后按发布页的 `SHA256SUMS` 做 SHA-256 完整性校验）。下载前与生成替换脚本前各有一道**可执行文件名守卫**：`Environment.ProcessPath` 的基名必须是本应用（`KindleMate2`），否则拒绝自动替换 —— 以 `dotnet …dll` 宿主时其目录是 .NET 运行时目录，误覆盖后果远超本应用（口径是"可执行文件名"，不校验安装位置）。解压 tar.gz/zip 走 BCL（`TarFile`/`ZipFile`），其自带对**符号链接与 `..` 路径穿越条目**的拦截（实测抛 `IOException`、目标目录外不落文件）。**不使用 AppCast**（旧的 `docs/update_*.xml` 是 WinForms 壳 AutoUpdater 时代的产物，已随壳退役删除）。
- 行为对齐原则：**UI 与跨平台可变，功能与行为须与已退役的原 WinForms 版完全一致**。
  少数经用户确认的例外已在代码注释与提交说明中标注。
