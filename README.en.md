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

- **Windows**: `Windows 10 1809` or later (required by .NET 10) — `KindleMate2_{arm64,x64,x86}[_runtime].zip`
- **macOS**: `macOS 11` (Apple Silicon) / `macOS 10.15` (Intel) or later — `KindleMate2_macos-{arm64,x64}.dmg`
- **Linux**: `KindleMate2_{linux-x64,linux-arm64}[_runtime].tar.gz`
- **Architecture**: `x86` or `x64` or `ARM64`
- **Kindle device sync is supported on all three platforms** (USB mass storage + MTP): Windows natively, macOS with a bundled libmtp (see "Third-party components"), Linux with the system's libmtp (see below)

The runtime-dependent builds require the platform's [.NET 10 runtime](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) (Desktop Runtime on Windows); builds with the `_runtime` suffix are **self-contained** and need no runtime installation. macOS ships self-contained only.

### Download & Run

Windows and Linux each ship two flavours: `_runtime` = **self-contained** (no .NET runtime needed); without that suffix = requires the runtime above.

- **Windows**: unzip the `.zip` and run `KindleMate2.Avalonia.exe` (use `KindleMate2_x64[_runtime].zip` on 64-bit); the library lives next to the program (same as older versions)
- **Linux**: extract, then run `./kindlemate2` — the launcher switches to the data directory for you, no manual `cd`
  ```bash
  mkdir -p ~/KindleMate2 && tar -xzf KindleMate2_linux-x64_runtime.tar.gz -C ~/KindleMate2
  ~/KindleMate2/kindlemate2
  ```
  The library lives in `~/.local/share/KindleMate2/` (XDG; override with the `KINDLEMATE2_HOME` environment variable).
  The archives preserve the executable bit; if a GUI extractor drops it, run `chmod +x kindlemate2 KindleMate2.Avalonia`.
  > **Upgrading from an older tar.gz**: your library is still in the old extracted folder — move it once:
  > `mkdir -p ~/.local/share/KindleMate2 && cp <old-dir>/KM2.dat ~/.local/share/KindleMate2/`
- **macOS**: open the `.dmg` and drag `KindleMate2.app` into Applications. The build is **not notarized**
  (no Apple Developer account), so clear the quarantine attribute first, or right-click the app and choose Open:
  ```bash
  xattr -dr com.apple.quarantine "/Applications/KindleMate2.app"
  ```
  The library lives in `~/Library/Application Support/KindleMate2/`.

## Building

Requires .NET SDK 10 or later (Avalonia 12's XAML source generator needs Roslyn ≥ 4.14;
on SDK 8/9 Roslyn **silently skips** it, so every generated member disappears behind
`CS0103: The name 'InitializeComponent' does not exist`).

```bash
dotnet build KindleMate2.sln -c Debug
dotnet test  KindleMate2.Tests/KindleMate2.Tests.csproj
```

> **This repository contains no git submodules** — the `DarkModeForms` submodule was retired along
> with the old shell, so no `submodule update` is needed.

### Projects

The solution contains 8 projects: `Shared` / `Domain` / `Infrastructure` / `Application` /
`Devices.Windows` / `Devices.MacOS` / `Avalonia` (**the only desktop UI**) / `Tests`. See [`arch.md`](arch.md) for the
layering, and [`KindleMate2.Avalonia/README.md`](KindleMate2.Avalonia/README.md) for building, running
and the headless self-checks.

> The earlier Windows Forms / WPF shells have been retired. To compare against the old behaviour use the
> read-only tag **`winforms-final`**; a ready-to-use old build is Release **`2026.09.07`**
> (WinForms, single file, no install).

## Features

- [x] Import Highlights (`My Clippings.txt`)
- [x] Import Vocabulary List (`vocab.db`)
- [x] Import Kindle Mate Database (migrate an old library)
- [x] Import Kindle Mate 2 Database (`KM2.dat` — merge another library into the current one)
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
- [x] **Cross-platform** (official packages for Windows / Linux / macOS; device sync supports USB mass storage + MTP on all three)

## Network access

There are **only two things that need the network**, and neither sends your books, your highlights, or any local data:

| Action | When | What is sent | How to turn it off |
|---|---|---|---|
| Update check | **Silently once at startup**, or manually via Help → Check for Updates | One HTTPS request to GitHub Releases; only public release metadata is read | No switch |
| Online definitions | When you select a word in the **vocabulary list** (right-hand detail panel) | **The word itself**, to Youdao Dictionary's public endpoint | No switch (it only happens when you browse the vocabulary list and select a word) |

When either one fails, **nothing is shown** in the UI (no error dialog, no error message). Definitions are cached
for the current session only and are **never written to the database**.

Everything else works fully offline: your data is a local SQLite file (`KM2.dat`) and device access goes over USB.

Neither of the two can be turned off. If you need a **fully offline** build, compile it yourself with those two calls
removed — the project is open source (MIT).

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

## Third-party components

The macOS package bundles the following libraries (**unmodified**, dynamically linked) to talk to Kindles
in MTP mode (Kindles released in 2024 and later — 12th-gen Paperwhite, Colorsoft, Scribe — do not mount as
a disk on macOS):

| Library | Version | License | Purpose |
| --- | --- | --- | --- |
| [libmtp](https://libmtp.sourceforge.io/) | 1.1.23 | LGPL-2.1-or-later | MTP implementation |
| [libusb](https://libusb.info/) | 1.0.30 | LGPL-2.1-or-later | USB transport used by libmtp |

As required by LGPL-2.1: the full license texts ship inside the bundle at
`KindleMate2.app/Contents/Resources/licenses/`, and the **corresponding sources are published on the same
release page** (`libmtp-1.1.23.tar.gz`, `libusb-1.0.30.tar.bz2`). Under LGPL-2.1 clause 6b you may replace
the dylibs in `Contents/Frameworks` with your own compatible builds (re-sign afterwards with an ad-hoc
signature: `codesign --force --deep --sign - "/Applications/Kindle Mate 2.app"`).

Building and bundling is done by [`scripts/bundle-libmtp.sh`](scripts/bundle-libmtp.sh) (builds universal
binaries from the upstream sources above, covering both osx-arm64 and osx-x64) and can be run locally
outside CI.

**Linux does not bundle these libraries**: libmtp / libusb are available as distribution packages, and
shipping another copy would both bloat the package and duplicate the LGPL obligations. Install them from
your distribution instead:

```bash
sudo apt install libmtp9 libusb-1.0-0        # Debian / Ubuntu
sudo dnf install libmtp libusb1              # Fedora / RHEL
sudo pacman -S libmtp libusb                 # Arch
```

Without them, USB mass storage (pre-2024 Kindles) still works; only MTP devices will be unavailable, and
the reason is written to the log.
