#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
mkdir -p artifacts
xvfb-run -a -s '-screen 0 1280x900x24 -noreset' bash -c '
  if test -n "${MYSHARP_NATIVE_BINARY:-}"; then
    exec "$MYSHARP_NATIVE_BINARY" --page=lab
  else
    exec "$DOTNET_ROOT/dotnet" bin/Debug/net10.0/MySharp.Workspace.dll --page=lab
  fi >artifacts/popup-native.log 2>&1 &
  app_pid=$!
  trap "kill $app_pid 2>/dev/null || true" EXIT
  sleep 2
  window_id=$(xdotool search --sync --onlyvisible --pid "$app_pid" | head -n 1)
  xdotool mousemove --window "$window_id" 560 163 click 1
  xdotool mousemove --window "$window_id" 200 500
  sleep 1
  kill -0 "$app_pid"
  import -window root -trim +repage artifacts/lab-flyout.png
  xdotool key Escape
  xdotool key ctrl+l
  sleep 1
  kill -0 "$app_pid"
  import -window root -trim +repage artifacts/lab-hotkey.png
'
