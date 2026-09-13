# Kindle Mate `2`

![](Screenshots/banner.png#gh-light-mode-only)
![](Screenshots/banner_dark.png#gh-dark-mode-only)

[![GitHub License](https://img.shields.io/github/license/lzcapp/KindleMate2?style=for-the-badge)](https://github.com/lzcapp/KindleMate2?tab=MIT-1-ov-file) &ensp; [![GitHub Release](https://img.shields.io/github/v/release/lzcapp/KindleMate2?style=for-the-badge)](https://github.com/lzcapp/KindleMate2/releases/latest) &ensp; [![GitHub Release](https://img.shields.io/github/v/release/lzcapp/KindleMate2?include_prereleases&style=for-the-badge)
](https://github.com/lzcapp/KindleMate2/releases)

**Kindle Mate 2** 是一款Kindle标注/笔记、Kindle生词本内容管理程序，旨在在 [Kindle Mate](https://web.archive.org/web/20231205072358/https://kmate.me/) 停止更新后提供替代的解决方案。

**Kindle Mate 2** is a program for managing Kindle's clippings/notes and Kindle's vocabulary list, aiming to provide an alternative solution after the [Kindle Mate](https://web.archive.org/web/20231205072358/https://kmate.me/) stopped updating.

<img src="https://github.com/user-attachments/assets/cfaeb54e-f237-4803-af61-23beb77a65f8" width="35px">**README** &ensp; [![中文](https://img.shields.io/badge/README-%E4%B8%AD%E6%96%87-red?style=for-the-badge
)](#) &ensp; [![English](https://img.shields.io/badge/README-English-blue?style=for-the-badge
)](README.en.md)

## 系统要求

- **Windows**：`Windows 10 1809` 或更高（.NET 8 的要求）；现有发布产物均为 Windows
- **macOS / Linux**：可由源码构建运行（跨平台），**除 Kindle 设备同步外功能完整**；暂无官方发布包
- **架构**: `x86` 或 `x64` 或 `ARM64`

依赖运行时（runtime）的版本需要安装 [.NET Desktop Runtime 8.0](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) 。

## 构建

需要 .NET SDK 9 或更高（Avalonia 12 的 XAML 生成器要求 Roslyn ≥ 4.14）。

```bash
dotnet build KindleMate2.sln -c Debug
dotnet test  KindleMate2.Tests/KindleMate2.Tests.csproj
```

> **本仓库不含任何 git 子模块**（`DarkModeForms` 子模块随旧壳一并退役，无需 `submodule update`）。

### 项目

解决方案含 7 个工程：`Shared` / `Domain` / `Infrastructure` / `Application` /
`Devices.Windows` / `Avalonia`（**唯一桌面 UI**）/ `Tests`。分层与依赖关系见 [`arch.md`](arch.md)；
壳自身的构建、运行与无头自检见 [`KindleMate2.Avalonia/README.md`](KindleMate2.Avalonia/README.md)。

> 早期基于 Windows Forms / WPF 的两个壳已退役。需要对照旧版行为时，可用只读 tag **`winforms-final`**
> （如 `git show winforms-final:KindleMate2/FrmMain.cs`）；可直接使用的旧版见 Release **`2026.09.07`**
> （WinForms，单文件免安装）。
    
    
      
    

## 特性

- [x] 导入标注（`My Clippings.txt`）
- [x] 导入生词本（`vocab.db`）
- [x] 导入 Kindle Mate 数据库（迁移旧库）
- [x] 导入 KMate 数据库（`km3.dat`）
- [x] 从已连接的 Kindle 设备导入（标注 + 生词本）
- [x] 同步到已连接的 Kindle 设备
- [x] 编辑标注
- [x] 编辑生词本
- [x] 删除（单条 / 整本书 / 某个词）与**回收站**（删除可恢复）
- [x] 重命名书籍（书名 + 作者）
- [x] 清理 / 重建 / 备份 / 清空数据库
- [x] 导出为 Markdown
- [x] 统计功能
- [x] 夜间模式（深色模式）
- [x] 语言切换（简体中文 / 繁体中文 / English）
- [x] 搜索功能（书名 / 作者 / 内容 / 笔记）
- [x] **跨平台**（Windows 功能完整；macOS / Linux 除设备同步外功能完整）

## 截图

<img src="docs/screenshots/01.png" width="100%">
<img src="docs/screenshots/02.png" width="100%">
<img src="docs/screenshots/03.png" width="100%">
<img src="docs/screenshots/04.png" width="100%">
<img src="docs/screenshots/05.png" width="100%">

## 小星星⭐历史

<a href="https://star-history.com/#lzcapp/KindleMate2&Date">
 <picture>
   <source media="(prefers-color-scheme: dark)" srcset="https://api.star-history.com/svg?repos=lzcapp/KindleMate2&type=Date&theme=dark" />
   <source media="(prefers-color-scheme: light)" srcset="https://api.star-history.com/svg?repos=lzcapp/KindleMate2&type=Date" />
   <img alt="Star History Chart" src="https://api.star-history.com/svg?repos=lzcapp/KindleMate2&type=Date" />
 </picture>
</a>
