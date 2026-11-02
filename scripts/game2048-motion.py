#!/usr/bin/env python3
"""Check rendered merge overshoot with real X11 frames, not UI-thread property values."""
import json, os, subprocess, time
from pathlib import Path
root = Path(__file__).resolve().parents[1]
out = root/'artifacts/2048-motion'; out.mkdir(parents=True, exist_ok=True)
(out/'report.json').unlink(missing_ok=True)
state = out/'state.json'
state.write_text(json.dumps(dict(Version=1, Best=0, Score=0, Cells=[2,0,0,2]+[0]*12, Won=False, KeepGoing=False)))
server = subprocess.Popen(['Xvfb','-displayfd','1','-screen','0','1280x900x24','-noreset','-nolisten','tcp'], stdout=subprocess.PIPE, stderr=subprocess.DEVNULL, text=True)
env = {**os.environ, 'DISPLAY': ':'+server.stdout.readline().strip()}
app = recorder = None
try:
    with (out/'app.log').open('w') as log:
        app = subprocess.Popen(['dotnet',str(root/'bin/Debug/net10.0/MySharp.Workspace.dll'),'--page=2048','--game-save='+str(state)],env=env,cwd=root,stdout=log,stderr=log)
    def x(*args): return subprocess.check_output(['xdotool',*map(str,args)],env=env,text=True)
    for _ in range(100):
        result = subprocess.run(['xdotool','search','--onlyvisible','--pid',str(app.pid)],env=env,stdout=subprocess.PIPE,text=True)
        if result.returncode == 0: break
        time.sleep(.05)
    wid = result.stdout.splitlines()[0]; time.sleep(.8)
    raw = out/'frames.rgb'; raw.unlink(missing_ok=True)
    with (out/'capture.log').open('w') as log:
        recorder = subprocess.Popen(['ffmpeg','-y','-probesize','32','-analyzeduration','0','-f','x11grab','-framerate','120','-window_id',wid,'-video_size','1120x820','-i',env['DISPLAY'],'-frames:v','180','-r','120','-fps_mode','cfr','-vf','crop=200:160:400:180','-pix_fmt','rgb24','-f','rawvideo',str(raw)],env=env,stdout=log,stderr=log)

    for _ in range(100):
        if raw.exists() and raw.stat().st_size > 0: break
        time.sleep(.01)
    time.sleep(.1); x('key','Left'); recorder.wait(timeout=8)
    data = raw.read_bytes(); size = 200*160*3; widths = []
    for offset in range(0,len(data)-size+1,size):
        frame = data[offset:offset+size]
        # The merged 4 at row 0 / col 0, safely away from any possible spawned tile.
        pixels = [x for x in range(15,155) if all(abs(frame[(85*200+x)*3+c]-rgb)<5 for c,rgb in enumerate((237,224,200))) ]
        widths.append(max(pixels)-min(pixels)+1 if pixels else 0)
    final = widths[-1]; peak = max(widths)
    assert final > 90 and peak > final*1.12, f'Merge did not visibly pop: final={final}, peak={peak}'
    assert any(5 < w < final*.8 for w in widths), 'Missing intermediate merge growth frames'
    subprocess.run(['ffmpeg','-y','-f','rawvideo','-pixel_format','rgb24','-video_size','200x160','-framerate','120','-i',str(raw),'-vf','fps=30','-loop','0',str(out/'merge.gif')],stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL,check=True)
    (out/'report.json').write_text(json.dumps(dict(frames=len(widths), settled_width=final, peak_width=peak, visible_pop=True, intermediate_frames=True),indent=2))
    raw.unlink()
    print((out/'report.json').read_text())
finally:
    if recorder is not None and recorder.poll() is None: recorder.kill(); recorder.wait(timeout=5)
    if app is not None: app.terminate(); app.wait(timeout=5)
    server.terminate(); server.wait(timeout=5)
