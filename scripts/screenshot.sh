#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$PATH"
mkdir -p artifacts
xvfb-run -a -s '-screen 0 1280x900x24 -noreset' bash -c '
  for page in overview tasks settings lab controls; do
    dotnet bin/Debug/net10.0/MySharp.Workspace.dll --page="$page" >"artifacts/$page.log" 2>&1 &
    app_pid=$!
    trap "kill $app_pid 2>/dev/null || true" EXIT
    sleep 3
    kill -0 "$app_pid"
    import -window root -trim +repage "artifacts/$page.png"
    kill "$app_pid"
    wait "$app_pid" 2>/dev/null || true
    trap - EXIT
  done
  dotnet bin/Debug/net10.0/MySharp.Workspace.dll --page=lab --custom-chrome >artifacts/custom-chrome.log 2>&1 &
  app_pid=$!
  trap "kill $app_pid 2>/dev/null || true" EXIT
  sleep 3
  kill -0 "$app_pid"
  import -window root -trim +repage artifacts/custom-chrome.png
  kill "$app_pid"
  wait "$app_pid" 2>/dev/null || true
  trap - EXIT
  dotnet bin/Debug/net10.0/MySharp.Workspace.dll --page=controls --dark >artifacts/controls-dark.log 2>&1 &
  app_pid=$!
  trap "kill $app_pid 2>/dev/null || true" EXIT
  sleep 2
  kill -0 "$app_pid"
  import -window root -trim +repage artifacts/controls-dark.png
  kill "$app_pid"
  wait "$app_pid" 2>/dev/null || true
  trap - EXIT
  dotnet bin/Debug/net10.0/MySharp.Workspace.dll --page=lab --dark >artifacts/theme-dark.log 2>&1 &
  app_pid=$!
  trap "kill $app_pid 2>/dev/null || true" EXIT
  sleep 2
  kill -0 "$app_pid"
  import -window root -trim +repage artifacts/theme-dark.png
  kill "$app_pid"
  wait "$app_pid" 2>/dev/null || true
  trap - EXIT
'
cp artifacts/overview.png artifacts/gui.png
