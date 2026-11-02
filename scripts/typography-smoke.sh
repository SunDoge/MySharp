#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
mkdir -p artifacts
dotnet build tests/MySharp.NativeSmoke --no-restore >artifacts/typography-smoke-build.log
xvfb-run -a -s '-screen 0 1800x1200x24 -noreset' bash -c '
  set -euo pipefail
  for scale in 1 1.5; do
    AVALONIA_GLOBAL_SCALE_FACTOR="$scale" timeout 20s dotnet tests/MySharp.NativeSmoke/bin/Debug/net10.0/MySharp.NativeSmoke.dll \
      --typography "artifacts/typography-$scale" >"artifacts/typography-$scale.log" 2>&1
  done
'
python3 - <<'PY'
import json
from pathlib import Path
for scale in (1, 1.5):
    report = json.loads(Path(f'artifacts/typography-{scale}/typography.json').read_text())
    assert report['render_scale'] == scale, report
    assert all(not run['missing'] for sample in report['samples'] for run in sample['runs']), report
    print(f"Typography at {scale}x: 12/14/16px mixed-script runs, no missing glyphs")
PY
