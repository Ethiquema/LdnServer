#!/bin/sh
set -e

ban_file="${IP_BAN_FILE_PATH:-/data/ryuldn/bannedips.txt}"
ban_dir="$(dirname "$ban_file")"

mkdir -p "$ban_dir" 2>/dev/null || true
[ -e "$ban_file" ] || : > "$ban_file" 2>/dev/null || true

exec ./LanPlayServer "$@"
