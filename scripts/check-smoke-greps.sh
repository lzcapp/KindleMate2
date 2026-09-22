#!/usr/bin/env bash
#
# 校验 build.yml 里那些**针对 --smoke 报告**的 grep 断言,是否真的能匹配上一份真实报告。
#
# 用法:
#   scripts/check-smoke-greps.sh <smoke 报告路径> [--build-yml <路径>]
#
# 先造一份报告(库用空文件即可,自检会自己造临时库):
#   dotnet KindleMate2.Avalonia/bin/Release/net10.0/KindleMate2.Avalonia.dll \
#       --smoke /tmp/empty.dat /tmp/smoke.txt
#   scripts/check-smoke-greps.sh /tmp/smoke.txt
#
# 为什么需要它(2026-09-22 踩到):
#   给 CI 加了一条 grep 断言后,我"验证"的是 **build.yml 里有这个模式**(`grep -c` 数出 2 处),
#   而**不是这个模式能匹配真实输出** —— 两件事看着很像,实际差一次 CI 往返:
#   模式里写了个真实输出中并不存在的单空格,于是 build-and-test 与两个跨平台作业**同时红**,
#   而自检本身是过的(报告里 `-> OK` 清清楚楚),排查还得先怀疑自检坏了。
#   本脚本把"拿真实产出跑一遍模式"变成一条命令,几秒内就能发现这类失配。
#
# 判定口径:
#   · 只取 `grep -q|-qE "<模式>" "$work/smoke.txt"` / `"$out"` 这类**针对报告**的断言;
#     针对源码文件的(如 MainWindow.axaml)与 shell 变量(`$expect`)自动跳过 —— 它们不是报告断言;
#   · 含 `DeviceManager` 的模式视为**平台相关**(三个平台各断言自己的实现),只提示不判失败;
#   · 其余模式**全部必须命中**,有失配则退出码 1。
#
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
build_yml="$repo_root/.github/workflows/build.yml"
report=""

usage() {
    sed -n '3,6p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
}

while [ $# -gt 0 ]; do
    case "$1" in
        --build-yml) build_yml="${2:-}"; shift 2 ;;
        -h|--help) usage; exit 0 ;;
        -*) echo "未知参数: $1" >&2; usage >&2; exit 2 ;;
        *) report="$1"; shift ;;
    esac
done

if [ -z "$report" ]; then
    usage >&2
    exit 2
fi
if [ ! -f "$report" ]; then
    echo "找不到 smoke 报告: $report" >&2
    echo "先跑一次 --smoke 生成它(见本脚本头部注释)。" >&2
    exit 2
fi
if [ ! -f "$build_yml" ]; then
    echo "找不到工作流文件: $build_yml" >&2
    exit 2
fi

echo "报告   : $report"
echo "工作流 : $build_yml"
echo

ok=0
skipped=0
miss=0
missed=""

# 去重:同一个模式在两个作业里各出现一次,只该校验一遍(否则统计翻倍、噪音也翻倍)。
# 用临时文件 + grep -qxF 而不是关联数组:macOS 自带的是 bash 3.2,没有关联数组。
seen_file="$(mktemp)"
trap 'rm -f "$seen_file"' EXIT

# 抽取形如 `grep -q "PATTERN" "$work/smoke.txt"` / `grep -qE "PATTERN" "$out"` 的断言。
# 同样用 while-read + 数组而非 mapfile(bash 3.2 没有 mapfile)。
assertions=()
while IFS= read -r line; do
    [ -n "$line" ] && assertions+=("$line")
done < <(grep -oE 'grep -qE? "[^"]+" "\$(work/smoke\.txt|out)"' "$build_yml" || true)

if [ "${#assertions[@]}" -eq 0 ]; then
    echo "工作流里没有找到针对 smoke 报告的 grep 断言 —— 是不是路径写法变了?" >&2
    exit 2
fi

for line in "${assertions[@]}"; do
    flag=""
    case "$line" in
        "grep -qE "*) flag="-E" ;;
    esac
    pattern="${line#*\"}"
    pattern="${pattern%%\"*}"

    if grep -qxF "$pattern" "$seen_file"; then
        continue
    fi
    printf '%s\n' "$pattern" >> "$seen_file"

    case "$pattern" in
        *DeviceManager*)
            printf '  SKIP  %s\n        (平台相关:三个平台各断言自己的设备实现)\n' "$pattern"
            skipped=$((skipped + 1))
            continue
            ;;
        *'$'*)
            printf '  SKIP  %s\n        (shell 变量,运行时才展开 —— 不是报告断言)\n' "$pattern"
            skipped=$((skipped + 1))
            continue
            ;;
    esac

    if grep -q $flag "$pattern" "$report"; then
        printf '  OK    %s\n' "$pattern"
        ok=$((ok + 1))
    else
        printf '  MISS  %s\n' "$pattern"
        miss=$((miss + 1))
        missed="$missed$pattern"$'\n'
    fi
done

echo
echo "命中 $ok / 跳过 $skipped / 失配 $miss"

if [ "$miss" -gt 0 ]; then
    echo
    echo "以下模式匹配不上这份报告 —— 它们在 CI 上必然让作业红:" >&2
    printf '%s' "$missed" | sed 's/^/    /' >&2
    echo >&2
    echo "多数是模式里写了实际不存在的字符(空格 / 标点)。请对着报告里的真实行改模式," >&2
    echo "或者把探针行改成「行首 ASCII 探针名 + 行尾 -> result=OK」的形状再写模式。" >&2
    exit 1
fi

echo "全部通过。"
