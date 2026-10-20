#!/usr/bin/env bash
set -uo pipefail

# Launch WEB8 from an interactive Wine/Xfce session with an isolated,
# writable WebView2 user-data folder. This can help with stale/contended
# profile folders; it cannot add missing WebView2 support to Wine.

usage() {
  cat <<'EOF'
Usage:
  WEB8_EXE=/path/to/Web8NextAttendanceClient.exe ./web8-webview2-launcher.sh [WEB8 arguments...]
  ./web8-webview2-launcher.sh /path/to/Web8NextAttendanceClient.exe [WEB8 arguments...]

Optional environment:
  WINEPREFIX                 Wine prefix containing WEB8 and WebView2 Runtime
  WINE_BIN                   Wine executable (default: wine)
  WEB8_WEBVIEW2_PROFILE      Override the WebView2 profile directory
  XDG_STATE_HOME             Base directory for launcher logs
EOF
}

APP=${WEB8_EXE:-}
if [[ -z "$APP" && $# -gt 0 ]]; then
  APP=$1
  shift
fi
if [[ -z "$APP" ]]; then
  usage >&2
  exit 2
fi

WINE_BIN=${WINE_BIN:-wine}
WINEPREFIX=${WINEPREFIX:-"$HOME/.wine"}
export WINEPREFIX

if ! command -v "$WINE_BIN" >/dev/null 2>&1; then
  echo "Wine executable not found: $WINE_BIN" >&2
  exit 2
fi
if ! command -v winepath >/dev/null 2>&1; then
  echo "winepath not found. Install Wine tools and try again." >&2
  exit 2
fi
if [[ ! -f "$APP" ]]; then
  echo "WEB8 executable not found: $APP" >&2
  exit 2
fi
if [[ ! -d "$WINEPREFIX" ]]; then
  echo "Wine prefix not found: $WINEPREFIX" >&2
  exit 2
fi

APP=$(readlink -f -- "$APP")
APP_NAME=$(basename -- "$APP")
PROFILE_NAME=${APP_NAME%.*}
PROFILE_DIR=${WEB8_WEBVIEW2_PROFILE:-"$HOME/.local/share/web8-webview2/$PROFILE_NAME"}
STATE_HOME=${XDG_STATE_HOME:-"$HOME/.local/state"}
LOG_DIR="$STATE_HOME/web8-webview2"
mkdir -p -- "$PROFILE_DIR" "$LOG_DIR"

# Serialize launches made with this helper. flock releases automatically
# when WEB8 exits, including when the launcher is interrupted.
LOCK_FILE="$LOG_DIR/$PROFILE_NAME.lock"
exec 9>"$LOCK_FILE"
if ! flock -n 9; then
  echo "A WEB8 launch using profile '$PROFILE_NAME' is already running." >&2
  exit 3
fi

PROFILE_WIN=$(winepath -w "$PROFILE_DIR") || {
  echo "Could not convert the WebView2 profile path for Wine." >&2
  exit 2
}
export WEBVIEW2_USER_DATA_FOLDER="$PROFILE_WIN"

LOG_FILE="$LOG_DIR/$PROFILE_NAME-$(date +%Y%m%d-%H%M%S).log"
{
  echo "WEB8 WebView2 launcher"
  echo "Started: $(date --iso-8601=seconds)"
  echo "Executable: $APP"
  echo "Wine prefix: $WINEPREFIX"
  echo "WebView2 profile: $PROFILE_WIN"
  echo "Wine: $($WINE_BIN --version 2>&1 | head -n 1)"
  echo "Note: launch from the logged-in Xfce desktop session."
  echo "--- WEB8 output ---"
} | tee -a "$LOG_FILE"

"$WINE_BIN" "$APP" "$@" 2>&1 | tee -a "$LOG_FILE"
APP_STATUS=${PIPESTATUS[0]}
echo "--- WEB8 exited with status $APP_STATUS at $(date --iso-8601=seconds) ---" | tee -a "$LOG_FILE"
echo "Log saved to: $LOG_FILE"
exit "$APP_STATUS"
