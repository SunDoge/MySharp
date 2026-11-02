#!/usr/bin/env python3
"""Native X11 Pulse gallery captures. Outputs stay in ignored artifacts."""
from pathlib import Path
import os, subprocess, time, json
root = Path(__file__).resolve().parents[1]
out = root / 'artifacts/pulse'; out.mkdir(parents=True, exist_ok=True)
server = subprocess.Popen(['Xvfb','-displayfd','1','-screen','0','1400x1000x24','-noreset','-nolisten','tcp'], stdout=subprocess.PIPE, text=True)
env = {**os.environ, 'DISPLAY': ':' + server.stdout.readline().strip()}
app = None
try:
    with (out/'native.log').open('w') as log:
        app = subprocess.Popen(['dotnet',str(root/'bin/Debug/net10.0/MySharp.Workspace.dll'),'--page=pulse'],cwd=root,env=env,stdout=log,stderr=log)
    def x(*args): return subprocess.check_output(['xdotool',*map(str,args)],env=env,text=True)
    for _ in range(100):
        found = subprocess.run(['xdotool','search','--onlyvisible','--pid',str(app.pid)],env=env,stdout=subprocess.PIPE,text=True)
        if found.returncode == 0: break
        assert app.poll() is None, 'App exited; see native.log'
        time.sleep(.05)
    wid=found.stdout.splitlines()[0]; time.sleep(.8)
    def capture(name):
        time.sleep(.25); subprocess.run(['import','-window',wid,str(out/(name+'.png'))],env=env,check=True)
    def click(xpos,ypos): x('mousemove','--window',wid,xpos,ypos,'click',1); time.sleep(.2)
    capture('overview')
    click(80,72); capture('processes')
    click(340,170); capture('inspector')
    click(80,190); capture('cpu')
    x('windowsize','--sync',wid,960,680); capture('minimum')
    assert app.poll() is None
    (out/'report.json').write_text(json.dumps({'native_window_survived_navigation_resize':True,'captures':['overview','processes','inspector','cpu','minimum']},indent=2))
finally:
    if app is not None: app.terminate(); app.wait(timeout=5)
    server.terminate(); server.wait(timeout=5)
