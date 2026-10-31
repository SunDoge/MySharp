#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
mkdir -p artifacts
export XDG_CONFIG_HOME="$PWD/artifacts/native-smoke-config"
export XDG_DATA_HOME="$PWD/artifacts/native-smoke-data"
export XDG_CACHE_HOME="$PWD/artifacts/native-smoke-cache"
mkdir -p "$XDG_CONFIG_HOME" "$XDG_DATA_HOME" "$XDG_CACHE_HOME"
python3 -c 'from pathlib import Path; Path("artifacts/native-smoke-path.txt").unlink(missing_ok=True); Path("artifacts/native-services-smoke.json").unlink(missing_ok=True)'
xvfb-run -a -s '-screen 0 1280x900x24 -noreset' bash -c '
  set -euo pipefail
  timeout 30s "$DOTNET_ROOT/dotnet" tests/MySharp.NativeSmoke/bin/Debug/net10.0/MySharp.NativeSmoke.dll --picker >artifacts/native-services.log 2>&1 &
  app_pid=$!
  trap "kill $app_pid 2>/dev/null || true" EXIT
  picker_id=""
  for attempt in $(seq 1 100); do
    kill -0 "$app_pid" 2>/dev/null || { wait "$app_pid"; exit 1; }
    picker_id=$(xdotool search --onlyvisible --name "MySharp native smoke picker" 2>/dev/null | head -n 1 || true)
    if test -n "$picker_id" && test -f artifacts/native-smoke-path.txt; then break; fi
    sleep 0.1
  done
  test -n "$picker_id"
  xwininfo -root -tree >artifacts/native-picker-windows.txt
  xdotool getwindowname "$picker_id" >artifacts/native-picker-title.txt
  xdotool windowfocus "$picker_id"
  xdotool key ctrl+l
  sleep 0.2
  xdotool type --clearmodifiers --file artifacts/native-smoke-path.txt
  import -window root -trim +repage artifacts/native-picker.png
  xdotool key Return
  sleep 0.5
  if kill -0 "$app_pid" 2>/dev/null; then xdotool key Return; fi
  wait "$app_pid"
  trap - EXIT
'

python3 -c 'import json; r=json.load(open("artifacts/native-services-smoke.json")); assert r.get("native_picker_selected_fixture") and "error" not in r, r'
