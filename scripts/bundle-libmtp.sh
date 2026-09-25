#!/usr/bin/env bash
#
# 把 libmtp + libusb 构建进 macOS 的 .app 包,并修好 install name 与 LGPL 许可文件。
#
# 用法:
#   scripts/bundle-libmtp.sh <KindleMate2.app 路径> [--arch arm64|x86_64|universal] [--build-dir <目录>]
#
# 为什么要从源码构建,而不是直接拷 Homebrew 的 dylib:
#   ① Homebrew 的 bottle 是单一架构的(Apple Silicon 上是 arm64-only),而发布要同时出
#      osx-arm64 与 osx-x64 —— `--arch universal` 一次构出通用二进制,省掉每架构各构一遍;
#   ② LGPL-2.1 要求「随包分发该库的可执行文件时,必须同时提供其源码」,而这个脚本下载的
#      就是那份源码(CI 会把它作为 release 资产一并附上),版本因此是钉死的、可复现的。
#
# 构建产物布局:
#   <app>/Contents/Frameworks/libmtp.9.dylib
#   <app>/Contents/Frameworks/libusb-1.0.0.dylib
#   <app>/Contents/Resources/licenses/libmtp-LGPL-2.1.txt
#   <app>/Contents/Resources/licenses/libusb-LGPL-2.1.txt
#
set -euo pipefail

# 把相对路径锚定成绝对路径(不要求目标已存在)。
#
# 为什么非锚不可:本脚本中途会 `cd "$BUILD_DIR"` 进构建目录,而 cd 之后再出现同一个
# 相对路径字符串,shell 会拿它**相对新 cwd 再解析一次** —— 等于被套用了两遍。
# release.yml 传进来的恰好是相对路径(`build/KindleMate2.app`、`build/libmtp`),
# 于是 `cd "$BUILD_DIR/$LIBUSB_SRC"` 会去找 <repo>/build/libmtp/build/libmtp/libusb-…,
# 必然 `No such file or directory`;同理 `cp … "$FRAMEWORKS/…"` 也会炸。
# 这类缺陷在本机人工演练时看不出来(手打命令惯用绝对路径),只在 CI 上才现形。
abspath() {
  case "$1" in
    /*) printf '%s' "$1" ;;
    *)  printf '%s/%s' "$PWD" "${1#./}" ;;
  esac
}

LIBMTP_VERSION="1.1.23"
LIBUSB_VERSION="1.0.30"
LIBMTP_TARBALL="libmtp-${LIBMTP_VERSION}.tar.gz"
LIBUSB_TARBALL="libusb-${LIBUSB_VERSION}.tar.bz2"
LIBMTP_URL="https://downloads.sourceforge.net/project/libmtp/libmtp/${LIBMTP_VERSION}/${LIBMTP_TARBALL}"
LIBUSB_URL="https://downloads.sourceforge.net/project/libusb/libusb-1.0/libusb-${LIBUSB_VERSION}/${LIBUSB_TARBALL}"

APP=""
ARCH="universal"
BUILD_DIR=""

while [ $# -gt 0 ]; do
  case "$1" in
    --arch)      ARCH="$2"; shift 2 ;;
    --build-dir) BUILD_DIR="$2"; shift 2 ;;
    -*)          echo "未知参数:$1" >&2; exit 2 ;;
    *)           APP="$1"; shift ;;
  esac
done

if [ -z "$APP" ]; then
  echo "用法: $0 <KindleMate2.app 路径> [--arch arm64|x86_64|universal] [--build-dir <目录>]" >&2
  exit 2
fi

if [ "$(uname -s)" != "Darwin" ]; then
  echo "本脚本只能在 macOS 上运行(需要 clang 的 -arch 与 install_name_tool)" >&2
  exit 1
fi

case "$ARCH" in
  arm64)     ARCH_FLAGS=(-arch arm64) ;;
  x86_64)    ARCH_FLAGS=(-arch x86_64) ;;
  universal) ARCH_FLAGS=(-arch arm64 -arch x86_64) ;;
  *)         echo "不支持的架构:$ARCH(可选 arm64 / x86_64 / universal)" >&2; exit 2 ;;
esac

APP="$(abspath "$APP")"
if [ ! -d "$APP/Contents" ]; then
  echo "$APP 看起来不是 .app 包(找不到 Contents/)" >&2
  exit 1
fi

# ————————————— 路径锚定(必须在任何 cd 之前) —————————————
[ -n "$BUILD_DIR" ] || BUILD_DIR="$(mktemp -d)"
BUILD_DIR="$(abspath "$BUILD_DIR")"

# 自检模式:不下载、不编译,只断言「进入 cd 之前所有路径都已锚定为绝对路径」。
# 存在的理由:本脚本那个"相对路径被 cd 二次套用"的缺陷,只在调用方传相对路径时
# 才现形(CI 正是如此),本地人工演练惯用绝对路径、永远发现不了 —— 需要一条能在
# CI 上以近零成本复现的检查,否则同类问题还会再犯一次。
# 刻意放在 pkg-config 检查之**前**:自检只关心路径,不该被"CI 镜像里有没有
# pkgconf"这类无关因素掩盖成另一种失败。
if [ "${BUNDLE_LIBMTP_CHECK_PATHS:-0}" = "1" ]; then
  for p in "$APP" "$BUILD_DIR"; do
    case "$p" in
      /*) ;;
      *)  echo "路径未被绝对化:$p" >&2; exit 1 ;;
    esac
  done
  echo "路径自检通过:APP=$APP BUILD_DIR=$BUILD_DIR"
  echo "  cd 之后 libusb 源码目录将解析为:$BUILD_DIR/libusb-${LIBUSB_VERSION}"
  echo "  cd 之后 .app 库目录将解析为:$APP/Contents/Frameworks"
  exit 0
fi

# libmtp 的 configure 用 pkg-config 的 PKG_CHECK_MODULES 定位 libusb,缺了会直接
# `configure: error: pkg-config not found`。Homebrew 装的 pkgconf 提供 pkg-config,
# 但 /opt/homebrew/bin 未必在 PATH 里(本机与不少 CI 环境都是如此),所以显式补上。
for dir in /opt/homebrew/bin /usr/local/bin; do
  if [ -x "$dir/pkg-config" ]; then
    case ":$PATH:" in *":$dir:"*) ;; *) export PATH="$dir:$PATH" ;; esac
    break
  fi
done
if ! command -v pkg-config >/dev/null 2>&1; then
  echo "缺少 pkg-config —— libmtp 的 configure 用它定位 libusb。macOS 上执行:brew install pkgconf" >&2
  exit 1
fi
echo "pkg-config:$(command -v pkg-config)(版本 $(pkg-config --version))"

mkdir -p "$BUILD_DIR"
echo "构建目录:$BUILD_DIR(架构:$ARCH)"

FRAMEWORKS="$APP/Contents/Frameworks"
LICENSES="$APP/Contents/Resources/licenses"
mkdir -p "$FRAMEWORKS" "$LICENSES"

# ————————————————————— 下载 —————————————————————
cd "$BUILD_DIR"
[ -f "$LIBUSB_TARBALL" ] || curl -sSL --retry 3 --retry-delay 2 -o "$LIBUSB_TARBALL" "$LIBUSB_URL"
[ -f "$LIBMTP_TARBALL" ] || curl -sSL --retry 3 --retry-delay 2 -o "$LIBMTP_TARBALL" "$LIBMTP_URL"

# SHA-256 钉死(与 release.yml 的 checksums job 同一对):上游/镜像被换包即失败;
# 缓存里已有的旧下载同样会重验 —— 顺带挡住"被污染的构建缓存"。
# 两处校验保证:打进 .app 的二进制与 Release 页签名清单里的源码包出自同一份字节。
LIBMTP_SHA256="74a2b6e8cb4a0304e95b995496ea3ac644c29371649b892b856e22f12a0bdeed"
LIBUSB_SHA256="fea36f34f9156400209595e300840767ab1a385ede1dc7ee893015aea9c6dbaf"
echo "$LIBMTP_SHA256  $LIBMTP_TARBALL" | shasum -a 256 -c -
echo "$LIBUSB_SHA256  $LIBUSB_TARBALL" | shasum -a 256 -c -

tar xf "$LIBUSB_TARBALL"
tar xf "$LIBMTP_TARBALL"

LIBUSB_SRC="libusb-${LIBUSB_VERSION}"
LIBMTP_SRC="libmtp-${LIBMTP_VERSION}"
LIBUSB_PREFIX="$BUILD_DIR/libusb-dist"

# ————————————————————— libusb —————————————————————
# 注意:-arch 必须同时进 CFLAGS 与 LDFLAGS,只给一边会得到"编译成 arm64、链接成 x86_64"的报错。
echo "== 构建 libusb $LIBUSB_VERSION"
cd "$BUILD_DIR/$LIBUSB_SRC"
./configure --prefix="$LIBUSB_PREFIX" --disable-static --enable-shared \
  CFLAGS="-O2 ${ARCH_FLAGS[*]}" LDFLAGS="${ARCH_FLAGS[*]}" >/dev/null
make -j"$(sysctl -n hw.ncpu)" >/dev/null
make install >/dev/null

# ————————————————————— libmtp —————————————————————
# libmtp 用 pkg-config 找 libusb,所以要把我们刚装的那份暴露给它 ——
# 否则它可能链到系统/Homebrew 里的另一份 libusb,随包时就漏带依赖。
echo "== 构建 libmtp $LIBMTP_VERSION"
cd "$BUILD_DIR/$LIBMTP_SRC"
PKG_CONFIG_PATH="$LIBUSB_PREFIX/lib/pkgconfig" ./configure \
  --prefix="$BUILD_DIR/libmtp-dist" --disable-static --enable-shared \
  CFLAGS="-O2 ${ARCH_FLAGS[*]}" LDFLAGS="${ARCH_FLAGS[*]}" >/dev/null
make -j"$(sysctl -n hw.ncpu)" >/dev/null

# ————————————————————— 拷进 .app 并修 install name —————————————————————
echo "== 放进 .app"
cp "src/.libs/libmtp.9.dylib" "$FRAMEWORKS/libmtp.9.dylib"
cp "$LIBUSB_PREFIX/lib/libusb-1.0.0.dylib" "$FRAMEWORKS/libusb-1.0.0.dylib"
chmod u+w "$FRAMEWORKS"/*.dylib

# 可执行文件通过 @rpath 找库;rpath 指向 .app 内的 Frameworks(由 release.yml 加到主程序上)
install_name_tool -id "@rpath/libmtp.9.dylib" "$FRAMEWORKS/libmtp.9.dylib"
install_name_tool -id "@rpath/libusb-1.0.0.dylib" "$FRAMEWORKS/libusb-1.0.0.dylib"

# libmtp 依赖 libusb:构建时它记的是绝对路径(我们的临时前缀),必须改写成 @rpath,
# 否则用户机上会去找一个不存在的路径 —— 这类错误在本机构建时不会暴露,只在别人的机器上炸。
install_name_tool -change "$LIBUSB_PREFIX/lib/libusb-1.0.0.dylib" \
  "@rpath/libusb-1.0.0.dylib" "$FRAMEWORKS/libmtp.9.dylib"

# 光把依赖改成 @rpath 还不够:**得有地方告诉 dyld 这个 @rpath 是什么**。
# 给 dylib 自己加一条 @loader_path(= 它所在目录),这样无论谁、以何种方式加载它,
# 都能在同一个目录里找到 libusb —— 不依赖宿主程序的 rpath,自洽且可复制。
# (macOS 27 的工具链上 -add_rpath 会写入两条相同条目,dyld 忽略重复项,无影响)
install_name_tool -add_rpath "@loader_path" "$FRAMEWORKS/libmtp.9.dylib"
install_name_tool -add_rpath "@loader_path" "$FRAMEWORKS/libusb-1.0.0.dylib"

# ————————————————————— LGPL 许可文件 —————————————————————
# LGPL-2.1 第 6 条:随包分发库的可执行文件时,必须显著声明并随附许可证与源码获取方式。
cp "$BUILD_DIR/$LIBUSB_SRC/COPYING" "$LICENSES/libusb-LGPL-2.1.txt"
cp "$BUILD_DIR/$LIBMTP_SRC/COPYING" "$LICENSES/libmtp-LGPL-2.1.txt"

cat > "$LICENSES/README.txt" <<'NOTICE'
本应用内嵌了以下两个 GNU LGPL-2.1-or-later 库(以动态链接方式使用):

  libmtp 1.1.23   https://libmtp.sourceforge.io/     (用于访问 MTP 模式的 Kindle)
  libusb 1.0.30   https://libusb.info/               (libmtp 的 USB 传输后端)

上述库的完整对应源码随本应用的发布包一并提供(见同一发布页面的源码压缩包),
你也可以从上面的官网获取。许可证全文见本目录下的两个 .txt 文件。

这两个库均未被修改。按 LGPL-2.1 第 6b 条,你可以用自行编译的兼容版本替换
Contents/Frameworks 下的同名动态库 —— 注意替换后需要重新签名(ad-hoc 即可):
  codesign --force --deep --sign - "/Applications/Kindle Mate 2.app"
NOTICE

echo "== 结果"
otool -L "$FRAMEWORKS/libmtp.9.dylib"
# lipo -archs 一次只接受一个输入文件,所以逐个来(写成两个参数会报
# "lipo: -archs requires exactly one input file")
for lib in "$FRAMEWORKS/libmtp.9.dylib" "$FRAMEWORKS/libusb-1.0.0.dylib"; do
  echo "  $(basename "$lib"): $(lipo -archs "$lib")"
done
ls -la "$FRAMEWORKS" "$LICENSES"
