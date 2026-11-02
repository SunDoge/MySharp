#!/usr/bin/env python3
"""Native X11 shader prototypes: actual pixels, pointer/keyboard, pause and lifetime."""
from pathlib import Path
import json, os, shutil, subprocess, time

root = Path(__file__).resolve().parents[1]
dotnet = shutil.which("dotnet") or str(Path.home() / ".dotnet/dotnet")
subprocess.run([dotnet, "build", "-c", "Release", "examples/ShaderLab"], cwd=root, check=True)
(root / "artifacts/shaders/summary.json").unlink(missing_ok=True)
results = []
for name, scale, software in [("gl-1", 1, False), ("gl-1.5", 1.5, False), ("raster-1", 1, True)]:
    output = root / "artifacts" / "shaders" / name
    output.mkdir(parents=True, exist_ok=True)
    for stale in ["targets.json", "input-done", "distortion-ready", "distortion-input-done", "report.json"]:
        (output / stale).unlink(missing_ok=True)
    xserver = subprocess.Popen(["Xvfb", "-displayfd", "1", "-screen", "0", "2200x1500x24", "-noreset", "-nolisten", "tcp"],
        stdout=subprocess.PIPE, stderr=subprocess.DEVNULL, text=True)
    env = {**os.environ, "DISPLAY": ":" + xserver.stdout.readline().strip(),
        "AVALONIA_GLOBAL_SCALE_FACTOR": str(scale), "MYSHARP_SHADER_NATIVE_INPUT": "1",
        "MYSHARP_SHADER_SOFTWARE": "1" if software else "0", "MYSHARP_SHADER_GL_TEST": "0" if software else "1"}
    app = None
    try:
        with (output / "glxinfo.txt").open("w") as info:
            subprocess.run(["glxinfo", "-B"], env=env, stdout=info, stderr=subprocess.STDOUT, check=False)
        with (output / "app.log").open("w") as log:
            app = subprocess.Popen([dotnet, str(root / "examples/ShaderLab/bin/Release/net10.0/MySharp.ShaderLab.dll"),
                "--smoke", str(output)], cwd=root, env=env, stdout=log, stderr=log)
            deadline = time.monotonic() + 30
            while not (output / "targets.json").exists():
                if app.poll() is not None or time.monotonic() >= deadline:
                    raise RuntimeError(f"{name}: app failed before native input; inspect {output}")
                time.sleep(.05)
            targets = json.loads((output / "targets.json").read_text())
            wid = subprocess.check_output(["xdotool", "search", "--onlyvisible", "--pid", str(app.pid)], env=env, text=True).splitlines()[0]
            def x(*args):
                subprocess.run(["xdotool", *map(str, args)], env=env, check=True)
            def click(point):
                x("mousemove", "--window", wid, round(point["X"] * scale), round(point["Y"] * scale), "click", "1")
                time.sleep(.15)
            click(targets["button"])
            click(targets["input"])
            x("key", "ctrl+a")
            x("type", "--clearmodifiers", "typed-through-shader")
            time.sleep(.2)
            (output / "input-done").touch()
            deadline = time.monotonic() + 15
            while not (output / "distortion-ready").exists():
                if app.poll() is not None or time.monotonic() >= deadline:
                    raise RuntimeError(f"{name}: distortion phase failed; inspect {output}")
                time.sleep(.05)
            click(targets["button"])
            (output / "distortion-input-done").touch()
            app.wait(timeout=30)
            report = json.loads((output / "report.json").read_text())
            if app.returncode != 0 or "error" in report["checks"]:
                raise RuntimeError(f"{name}: {report['checks'].get('error', 'process failed')}")
            assert report["scale"] == scale, "Requested scale was not applied"
            if software:
                assert not report["layer"]["GpuContext"] and not report["layer"]["GpuLayer"]
            else:
                assert report["layer"]["GpuContext"] and report["layer"]["GpuLayer"], "OpenGL prototype did not exercise GPU-context layer path"
            results.append({"name": name, **report})
            print(f"PASS {name}: native input, grayscale/distortion pixels, retained layer animation, paused idle and render resource release", flush=True)
    finally:
        if app and app.poll() is None:
            app.terminate(); app.wait(timeout=5)
        xserver.terminate(); xserver.wait(timeout=5)
(root / "artifacts/shaders/summary.json").write_text(json.dumps(results, ensure_ascii=False, indent=2) + "\n")
