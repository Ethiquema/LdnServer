#!/bin/sh
set -e

ban_file="${IP_BAN_FILE_PATH:-/data/ryuldn/bannedips.txt}"
ban_dir="$(dirname "$ban_file")"

# When started as root (the default), fix permissions on the data
# directory — which may be a bind mount owned by some arbitrary host
# UID — then drop privileges to appuser before running the server.
if [ "$(id -u)" = "0" ]; then
    mkdir -p "$ban_dir"
    [ -e "$ban_file" ] || touch "$ban_file"
    chown -R appuser:appgroup "$ban_dir"
    exec su-exec appuser:appgroup ./LanPlayServer "$@"
fi

# Already non-root: best-effort init, ignore failures.
mkdir -p "$ban_dir" 2>/dev/null || true
[ -e "$ban_file" ] || : > "$ban_file" 2>/dev/null || true
exec ./LanPlayServer "$@"
