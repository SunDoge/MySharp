#!/usr/bin/env python3
"""Run under xvfb-run with -noreset to keep the display alive between launches."""
import json
import os
from pathlib import Path
import subprocess
import sys
import time

root = Path(__file__).resolve().parents[1]
os.chdir(root)
results = []
for mode in sys.argv[1:] or ['framework-dependent', 'self-contained', 'native-aot']:
    for page in (['overview', 'tasks', 'settings', 'lab', 'custom-chrome'] if mode.startswith('current-') else ['overview', 'tasks', 'settings']):
        binary = root / 'artifacts/publish' / mode / 'MySharp.Workspace'
        env = os.environ.copy()
        if mode == 'framework-dependent':
            env['DOTNET_ROOT'] = str(Path.home() / '.dotnet')
        else:
            env.pop('DOTNET_ROOT', None)
        with open(f'artifacts/publish-smoke-{mode}-{page}.log', 'w') as log:
            arguments = [str(binary), f'--page={"lab" if page == "custom-chrome" else page}']
            if page == 'custom-chrome':
                arguments.append('--custom-chrome')
            app = subprocess.Popen(arguments, env=env, stdout=log, stderr=log)
            try:
                shown = False
                for attempt in range(100):
                    if app.poll() is not None:
                        break
                    windows = subprocess.check_output(['xwininfo', '-root', '-tree'], text=True)
                    if 'MySharp' in windows:
                        shown = True
                        break
                    time.sleep(0.1)
                time.sleep(0.3)
                if not shown or app.poll() is not None:
                    raise RuntimeError(f'No live window for {mode}/{page}; exit={app.poll()}')
                if mode.startswith('current-') and page in ('lab', 'custom-chrome'):
                    subprocess.run(['import', '-window', 'root', '-trim', '+repage', f'artifacts/{mode}-{page}.png'], check=True)
                results.append(dict(mode=mode, page=page, window_created=True))
                print(mode, page, 'OK', flush=True)
            finally:
                if app.poll() is None:
                    app.terminate()
                app.wait(timeout=10)
                time.sleep(0.1)
Path('artifacts/publish-smoke-current.json' if sys.argv[1:] else 'artifacts/publish-smoke.json').write_text(json.dumps(results, indent=2) + '\n')
