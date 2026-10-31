#!/usr/bin/env python3
"""Compare normal compilation vs AOT publishing; restore first, warm caches, no downloads in timing."""
import json
import os
from pathlib import Path
import statistics
import subprocess
import time

root = Path(__file__).resolve().parents[1]
os.chdir(root)
dotnet = os.environ.get('DOTNET', str(Path.home() / '.dotnet/dotnet'))
source = root / 'examples/Workspace/Overview.cs'
original = source.read_text()
phrase = 'Your projects, priorities and recent work in one place.'
assert phrase in original
common = ['MySharp.csproj', '-c', 'Release', '-r', 'linux-x64', '-v:q', '--nologo', '-p:DebugType=None', '-p:DebugSymbols=false']

def run(command):
    started = time.perf_counter()
    result = subprocess.run(command, capture_output=True, text=True)
    elapsed = time.perf_counter() - started
    if result.returncode:
        raise RuntimeError(result.stdout + result.stderr)
    return round(elapsed, 4)

report = {'configuration': 'Release linux-x64; 3 rounds per case; warm package/filesystem/compiler caches; restore excluded; symbols disabled',
          'limitation': 'JIT build creates managed IL and an app host; AOT publish additionally compiles native code, links, trims and copies publish output. Not a comparison of individual C# compiler phases.',
          'seconds': {}}
try:
    for mode in ['jit', 'aot']:
        aot = mode == 'aot'
        command = [dotnet, 'publish' if aot else 'build', *common, '--no-restore',
                   f'-p:PublishAot={str(aot).lower()}', '--self-contained', str(aot).lower()]
        if aot:
            command += ['-p:StripSymbols=true', '-o', 'artifacts/publish/aot-timing']
        for case in ['clean', 'no_change', 'edit']:
            samples = []
            for i in range(3):
                if case == 'clean':
                    run([dotnet, 'clean', *common, f'-p:PublishAot={str(aot).lower()}'])
                if case == 'edit':
                    source.write_text(original.replace(phrase, f'{phrase} {mode}-{i}'))
                elapsed = run(command)
                samples.append(elapsed)
                print(mode, case, i + 1, f'{elapsed:.3f}s', flush=True)
            report['seconds'][f'{mode}_{case}'] = {'samples': samples, 'median': round(statistics.median(samples), 4)}
        source.write_text(original)
finally:
    source.write_text(original)
Path('artifacts/aot-build-times.json').write_text(json.dumps(report, indent=2) + '\n')
print(json.dumps(report, indent=2), flush=True)
