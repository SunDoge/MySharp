#!/usr/bin/env python3
"""Exercise the gallery game with real X11 input and isolated saves."""
from pathlib import Path
import json,os,shutil,subprocess,sys,time

root=Path(__file__).resolve().parents[1]
out=root/'artifacts/2048';out.mkdir(parents=True,exist_ok=True)
(out/'report.json').unlink(missing_ok=True)
dotnet=shutil.which('dotnet') or str(Path.home()/'.dotnet/dotnet')
if '--no-build' not in sys.argv:
    subprocess.run([dotnet,'build',str(root/'MySharp.csproj')],cwd=root,check=True)
xserver=subprocess.Popen(['Xvfb','-displayfd','1','-screen','0','1800x1300x24','-noreset','-nolisten','tcp'],stdout=subprocess.PIPE,text=True,stderr=subprocess.DEVNULL)
env={**os.environ,'DISPLAY':':'+xserver.stdout.readline().strip()}
app=None
state=out/'state.json'
state.write_text(json.dumps(dict(Version=1,Best=768,Score=412,Cells=[2,4,8,16,32,64,128,256,512,0,0,0,2,2,4,8],Won=False,KeepGoing=False)))
def x(*args):return subprocess.check_output(['xdotool',*map(str,args)],env=env,text=True)
def read():return json.loads(state.read_text())
def wait_for(condition):
    for _ in range(100):
        if condition():return
        if app.poll() is not None:raise RuntimeError('application exited; see app log')
        time.sleep(.04)
    raise RuntimeError('native state did not settle')
def start(name):
    global app
    log=(out/(name+'.log')).open('w')
    app=subprocess.Popen([dotnet,str(root/'bin/Debug/net10.0/MySharp.Workspace.dll'),'--page=2048','--game-save='+str(state)],cwd=root,env=env,stdout=log,stderr=log)
    for _ in range(100):
        found=subprocess.run(['xdotool','search','--onlyvisible','--pid',str(app.pid)],env=env,stdout=subprocess.PIPE,text=True)
        if found.returncode==0:
            time.sleep(.7);return found.stdout.splitlines()[0]
        if app.poll() is not None:raise RuntimeError('application exited; see app log')
        time.sleep(.05)
    raise RuntimeError('no gallery window')
def stop():
    global app
    app.terminate();app.wait(timeout=5);app=None
try:
    wid=start('native')
    subprocess.run(['import','-window',wid,str(out/'gallery.png')],env=env,check=True)
    x('key','Left');wait_for(lambda:read()['Score']==416)
    after=read();assert after['Cells'][12:15]==[4,4,8] and after['Best']==768
    time.sleep(.7)
    subprocess.run(['import','-window',wid,str(out/'after-move.png')],env=env,check=True)
    stop();wid=start('restored')
    assert read()['Cells']==after['Cells'] and read()['Score']==416,'Saved game did not restore'
    x('windowsize','--sync',wid,960,680);time.sleep(.3)
    subprocess.run(['import','-window',wid,str(out/'minimum.png')],env=env,check=True)
    # Local board point (300,250) -> (100,250), after the 0.8x gallery fit.
    x('mousemove','--window',wid,628,366,'mousedown','1');time.sleep(.08)
    x('mousemove','--window',wid,468,366,'mouseup','1');time.sleep(.3)
    assert read()['Cells']!=after['Cells'],'Real pointer drag did not move tiles'
    x('key','r');wait_for(lambda:read()['Score']==0 and sum(v!=0 for v in read()['Cells'])==2)
    assert read()['Best']==768
    stop()
    state.write_text(json.dumps(dict(Version=1,Best=768,Score=0,Cells=[1024,1024]+[0]*14,Won=False,KeepGoing=False)))
    wid=start('win');x('key','Left');wait_for(lambda:read()['Won'])
    time.sleep(2.2)
    subprocess.run(['import','-window',wid,str(out/'win.png')],env=env,check=True)
    x('key','Tab','space');wait_for(lambda:read()['KeepGoing'])
    # The previous random spawn may occupy the last column; the won tile must move right, not necessarily reach the wall.
    x('key','Right');wait_for(lambda:2048 in read()['Cells'][1:4])
    stop()
    state.write_text(json.dumps(dict(Version=1,Best=2048,Score=120,Cells=[0,2,4,8,8,16,32,64,128,256,512,1024,32,128,64,256],Won=False,KeepGoing=False)))
    wid=start('loss');x('key','Left');wait_for(lambda:read()['Cells'] is None)
    time.sleep(2.2)
    subprocess.run(['import','-window',wid,str(out/'game-over.png')],env=env,check=True)
    x('key','Tab','space');wait_for(lambda:read()['Score']==0 and read()['Cells'] is not None and sum(v!=0 for v in read()['Cells'])==2)
    assert read()['Best']==2048
    result=dict(native_keyboard=True,native_scaled_drag=True,merge_score=True,reload_saved_game=True,restart_preserves_best=True,native_win_continue=True,native_loss_restart=True,minimum_window=[960,680],limitation='Linux X11 smoke; screenshots and isolated state are local artifacts.')
    (out/'report.json').write_text(json.dumps(result,indent=2)+'\n')
    print(json.dumps(result,indent=2))
finally:
    if app and app.poll() is None:stop()
    xserver.terminate();xserver.wait(timeout=5)
