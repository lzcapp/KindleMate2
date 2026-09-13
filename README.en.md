# Kindle Mate `2`

![](Screenshots/banner.png#gh-light-mode-only)
![](Screenshots/banner_dark.png#gh-dark-mode-only)

[![GitHub License](https://img.shields.io/github/license/lzcapp/KindleMate2?style=for-the-badge)](https://github.com/lzcapp/KindleMate2?tab=MIT-1-ov-file) &ensp; [![GitHub Release](https://img.shields.io/github/v/release/lzcapp/KindleMate2?style=for-the-badge)](https://github.com/lzcapp/KindleMate2/releases/latest) &ensp; [![GitHub Release](https://img.shields.io/github/v/release/lzcapp/KindleMate2?include_prereleases&style=for-the-badge)
](https://github.com/lzcapp/KindleMate2/releases)

**Kindle Mate 2** 是一款Kindle标注/笔记、Kindle生词本内容管理程序，旨在在 [Kindle Mate](https://web.archive.org/web/20231205072358/https://kmate.me/) 停止更新后提供替代的解决方案。

**Kindle Mate 2** is a program for managing Kindle's clippings/notes and Kindle's vocabulary list, aiming to provide an alternative solution after the [Kindle Mate](https://web.archive.org/web/20231205072358/https://kmate.me/) stopped updating.

<img src="https://github.com/user-attachments/assets/cfaeb54e-f237-4803-af61-23beb77a65f8" width="35px">**README** &ensp; [![中文](https://img.shields.io/badge/README-%E4%B8%AD%E6%96%87-red?style=for-the-badge
)](README.md) &ensp; [![English](https://img.shields.io/badge/README-English-blue?style=for-the-badge
)](#)

## System Requirements

- **Windows**: `Windows 10 1809` or later (required by .NET 8); all published builds are Windows
- **macOS / Linux**: buildable and runnable from source (cross-platform) — **feature complete except Kindle device sync**; no official packages yet
- **Architecture**: `x86` or `x64` or `ARM64`

[.NET Desktop Runtime 8.0](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) required for runtime dependent version.

## Building

Requires .NET SDK 9 or later (Avalonia 12's XAML compiler needs Roslyn ≥ 4.14).

```bash
dotnet build KindleMate2.sln -c Debug
dotnet test  KindleMate2.Tests/KindleMate2.Tests.csproj
```

> **This repository contains no git submodules** — the `DarkModeForms` submodule was retired along
> with the old shell, so no `submodule update` is needed.

### Projects

The solution contains 7 projects: `Shared` / `Domain` / `Infrastructure` / `Application` /
`Devices.Windows` / `Avalonia` (**the only desktop UI**) / `Tests`. See [`arch.md`](arch.md) for the
layering, and [`KindleMate2.Avalonia/README.md`](KindleMate2.Avalonia/README.md) for building, running
and the headless self-checks.

> The earlier Windows Forms / WPF shells have been retired. To compare against the old behaviour use the
> read-only tag **`winforms-final`**; a ready-to-use old build is Release **`2026.09.07`**
> (WinForms, single file, no install).

## Features

- [x] Import Highlights (`My Clippings.txt`)
- [x] Import Vocabulary List (`vocab.db`)
- [x] Import Kindle Mate Database (migrate an old library)
- [x] Import KMate Database (`km3.dat`)
- [x] Import from a connected Kindle device (highlights + vocabulary)
- [x] Sync to a connected Kindle device
- [x] Edit Highlights
- [x] Edit Vocabulary List
- [x] Delete (single / whole book / single word) and **recycle bin** (deletions are restorable)
- [x] Rename books (title + author)
- [x] Clean / Rebuild / Backup / Clear database
- [x] Export Function (Markdown)
- [x] Statistics Function
- [x] Night Mode (Dark Mode)
- [x] Language Switch (简体中文 / 繁體中文 / English)
- [x] Search Function (book / author / content / note)
- [x] **Cross-platform** (full feature set on Windows; everything but device sync on macOS / Linux)

## Screenshots

<img src="docs/screenshots/01.png" width="100%">
<img src="docs/screenshots/02.png" width="100%">
<img src="docs/screenshots/03.png" width="100%">
<img src="docs/screenshots/04.png" width="100%">
<img src="docs/screenshots/05.png" width="100%">

## Star ⭐ History

<a href="https://star-history.com/#lzcapp/KindleMate2&Date">
 <picture>
   <source media="(prefers-color-scheme: dark)" srcset="https://api.star-history.com/svg?repos=lzcapp/KindleMate2&type=Date&theme=dark" />
   <source media="(prefers-color-scheme: light)" srcset="https://api.star-history.com/svg?repos=lzcapp/KindleMate2&type=Date" />
   <img alt="Star History Chart" src="https://api.star-history.com/svg?repos=lzcapp/KindleMate2&type=Date" />
 </picture>
</a>
