#!/usr/bin/env python3
"""Run under xvfb-run. Real Release desktop process; sample CPU and /proc memory."""
import json
import os
from pathlib import Path
import subprocess
import time

root = Path(__file__).resolve().parents[1]
os.chdir(root)
dotnet = os.environ.get('DOTNET', str(Path.home() / '.dotnet/dotnet'))
ticks = os.sysconf('SC_CLK_TCK')

def cpu(pid):
    fields = Path(f'/proc/{pid}/stat').read_text().rsplit(')', 1)[1].split()
    return (int(fields[11]) + int(fields[12])) / ticks

def memory(pid):
    entries = {}
    for line in Path(f'/proc/{pid}/smaps_rollup').read_text().splitlines():
        parts = line.split()
        if parts[0] in ('Rss:', 'Pss:', 'Private_Clean:', 'Private_Dirty:'):
            entries[parts[0].rstrip(':')] = int(parts[1])
    return entries

results = []
for page in ['overview', 'tasks', 'settings']:
    with open(f'artifacts/idle-{page}.log', 'w') as log:
        process = subprocess.Popen([dotnet, 'bin/Release/net10.0/MySharp.Workspace.dll', f'--page={page}'], stdout=log, stderr=log)
        try:
            time.sleep(5)
            if process.poll() is not None:
                raise RuntimeError(f'{page} exited early; see artifacts/idle-{page}.log')
            start_cpu, start_wall = cpu(process.pid), time.perf_counter()
            time.sleep(8)
            elapsed = time.perf_counter() - start_wall
            used = cpu(process.pid) - start_cpu
            result = dict(page=page, observation_seconds=round(elapsed, 3), cpu_seconds=round(used, 3),
                          cpu_percent_one_core=round(used / elapsed * 100, 3), memory_kib=memory(process.pid))
            results.append(result)
            print(json.dumps(result), flush=True)
        finally:
            process.terminate()
            process.wait(timeout=10)
output = dict(environment='Release; real Avalonia desktop window on Linux X11/Xvfb; 5s settle + 8s idle sample per page',
              limitation='Application process only; excludes Xvfb and GPU/driver memory outside the process. CPU 100% means one core. One idle sample per page; not a long-duration leak or animation benchmark.',
              results=results)
Path('artifacts/runtime-idle.json').write_text(json.dumps(output, indent=2) + '\n')
