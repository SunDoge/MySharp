#!/usr/bin/env python3
"""Warm-cache build benchmarks; dependency restore is excluded."""
import json
import os
from pathlib import Path
import statistics
import subprocess
import time

root = Path(__file__).resolve().parents[1]
os.chdir(root)
dotnet = os.environ.get('DOTNET', str(Path.home() / '.dotnet/dotnet'))
base = [dotnet, 'build', 'MySharp.csproj', '--no-restore', '-c', 'Debug', '-v:q', '--nologo']

def run(args):
    start = time.perf_counter()
    result = subprocess.run(args, capture_output=True, text=True)
    elapsed = time.perf_counter() - start
    if result.returncode:
        raise RuntimeError(result.stdout + result.stderr)
    return round(elapsed, 4)

source = root / 'examples/Workspace/Overview.cs'
original = source.read_text()
results = {'sdk': subprocess.check_output([dotnet, '--version'], text=True).strip(),
           'configuration': 'Debug; no restore; default compiler server; warm filesystem/package cache',
           'identity': 'CallerFilePath + CallerLineNumber + local occurrence; no source generator',
           'rounds': 7, 'seconds': {}}
results['first_build_seconds'] = run(base)
try:
    for mode in ['clean', 'no_change', 'edit']:
        samples = []
        for i in range(results['rounds']):
            if mode == 'clean':
                run([dotnet, 'clean', 'MySharp.csproj', '-c', 'Debug', '-v:q', '--nologo'])
            if mode == 'edit':
                source.write_text(original.replace('Your projects, priorities and recent work in one place.', f'Your projects, priorities and recent work in one place. {i}'))
            elapsed = run(base)
            samples.append(elapsed)
            print(f'{mode} {i + 1}: {elapsed:.3f}s', flush=True)
        results['seconds'][mode] = {'samples': samples, 'median': round(statistics.median(samples), 4),
                                    'min': min(samples), 'max': max(samples)}
finally:
    source.write_text(original)
    run(base)
(root / 'artifacts/build-times.json').write_text(json.dumps(results, indent=2) + '\n')
print(json.dumps(results, indent=2))
