# KindleMate2.Avalonia

Kindle Mate 2 的 **Avalonia 桌面客户端**。数据/业务层（`Shared` / `Domain` /
`Infrastructure` / `Application`）原样复用，平台专有代码收敛在独立程序集里。

> 2026-09-13：原 WinForms 壳（`KindleMate2/`）与 `DarkModeForms` 子模块已**退役**，
> 本工程即当前唯一的桌面 UI，并已加入 `KindleMate2.sln`。

## 设计规范（2026-09-12 修订）

**不再照搬 WinForms 布局，也不使用 DarkModeForms 配色。** 界面按设计系统自主构建：

- 信息架构：内容优先列表 + 右侧预览面板（邮件客户端范式），搜索为全局命令栏。
- 设计令牌：5 阶冷中性表面 + 唯一强调色 `#7B8CFF` + 4 类标注标签色；正文对比度
  15.2:1、次要 6.3:1（过 WCAG AA）。深浅双主题，默认跟随系统。
- 全部定义在 `App.axaml`（`ThemeDictionaries` + 控件样式），无第三方 UI 库。
- 图表是自研的 `Charts/ChartControl.cs`（重写 `Render(DrawingContext)` 自绘，零依赖）。

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

启动后优先打开上次的库（记录在 `%APPDATA%/KindleMate2/settings.json`），
其次尝试输出目录附近的 `KM2.db`，或从「文件 → 打开数据库…」手动选择。

## 无头自检

两套自检都不开窗口，用于 CI 与本机验证：

```bash
# 只读链路：列表 / 详情 / 统计 / 关于 / 设置 / 搜索 / 多语言
KindleMate2.Avalonia(.exe) --smoke <db> [out.txt]

# 写操作端到端：导入 → 导出 → 备份 → 重命名 → 删除 → 清理 → 重建 → 清空 → 空库重导
KindleMate2.Avalonia(.exe) --ops <db> <clippings.txt> <vocab.db> <out.txt>
```

`--ops` **全程在临时副本上执行**，不会改动传入的库。测试夹具见
`Documents\KindleMate2\fixtures\`（不在仓库内）。

## 注意

- **数据库 schema**：读取的是**当前程序生成的库**（`clippings` 表含 `key`/`content`/
  `bookname` 等列）。仓库根的 `KM2.db` 是旧版 Kindle Mate 格式
  （`source_clippings`/`book_id`），不适用。
- **跨平台现状**：`net8.0` 变体可构建、可运行，除 **Kindle 设备同步**外功能完整。
  非 Windows 的设备支持（挂载点 / libmtp）与各平台打包发布仍待补齐。
- **界面文案**一律走 `Shared.Strings`（简/繁/英三套），不要在 XAML / VM 里写死中文。
  新增文案需同步改 4 个 resx 并手工补 `Strings.Designer.cs` 的强类型属性。
- **CI**：`.github/workflows/build.yml` 在 windows 上跑全量构建 + 单测，在
  ubuntu/macOS 上跑跨平台构建 + 单测 + 启动自检。
