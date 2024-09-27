# Kindle Mate `2`

[![GitHub License](https://img.shields.io/github/license/lzcapp/KindleMate2?style=for-the-badge)](https://github.com/lzcapp/KindleMate2?tab=MIT-1-ov-file) &ensp; [![GitHub Release](https://img.shields.io/github/v/release/lzcapp/KindleMate2?style=for-the-badge)](https://github.com/lzcapp/KindleMate2/releases/latest)

**Kindle Mate 2** 是一款Kindle标注/笔记、Kindle生词本内容管理程序，旨在在 [Kindle Mate](https://web.archive.org/web/20231205072358/https://kmate.me/) 停止更新后提供替代的解决方案。

**Kindle Mate 2** is a program for managing Kindle's clippings/notes and Kindle's vocabulary list, aiming to provide an alternative solution after the [Kindle Mate](https://web.archive.org/web/20231205072358/https://kmate.me/) stopped updating.

<img src="https://github.com/user-attachments/assets/cfaeb54e-f237-4803-af61-23beb77a65f8" width="30px">README &ensp; [![中文](https://img.shields.io/badge/README-%E4%B8%AD%E6%96%87-red?style=for-the-badge
)](README.md) &ensp; [![English](https://img.shields.io/badge/README-English-blue?style=for-the-badge
)](README.en.md)

## System Requirements

- **Minimum**: `Windows 7` or later
- **Recommended**: `Windows 11`
- **Architecture**: `x86` or `x64`.

[.NET Desktop Runtime 8.0](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) required for runtime dependent version.

## Building

`Visual Studio` & `C#`

### Submodules

1. Do `git submodule update --init --recursive` first after cloning this repo.
2. Change project `Dark-Mode-Forms`'s output type to `Class Library`.

- [`lzcapp/Dark-Mode-Forms`](https://github.com/lzcapp/Dark-Mode-Forms): forked from [`BlueMystical/Dark-Mode-Forms`](https://github.com/BlueMystical/Dark-Mode-Forms)

### Projects

- `KindleMate2` (WinForm): Current version.
- `KindleMate2_WPF` (WPF): Not yet done, much work to do.

## Features

- [x] Import Highlights (`My Clippings.txt`)
- [x] Import Vocabulary List (`vocab.db`)
- [x] Sync Connected Kindle Devices
- [x] Edit Highlights
- [x] Edit Vocabulary List
- [x] Cleaning Function
- [x] Export Function
- [x] Statistics Function
- [x] Night Mode (Dark Mode)
- [x] Language Switch
- [x] Search Function
- [ ] Share Function (?)

## Screenshots

<img src="Screenshots/01.png" width="100%">
<img src="Screenshots/02.png" width="100%">
<img src="Screenshots/03.png" width="100%">
<img src="Screenshots/04.png" width="100%">

## Star ⭐ History

<a href="https://star-history.com/#lzcapp/KindleMate2&Date">
 <picture>
   <source media="(prefers-color-scheme: dark)" srcset="https://api.star-history.com/svg?repos=lzcapp/KindleMate2&type=Date&theme=dark" />
   <source media="(prefers-color-scheme: light)" srcset="https://api.star-history.com/svg?repos=lzcapp/KindleMate2&type=Date" />
   <img alt="Star History Chart" src="https://api.star-history.com/svg?repos=lzcapp/KindleMate2&type=Date" />
 </picture>
</a>
