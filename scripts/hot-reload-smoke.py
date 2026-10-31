#!/usr/bin/env python3
"""Real dotnet-watch + native X11 proof in an isolated temporary consumer; edits no repository source."""
import json
import os
import re
from pathlib import Path
import shutil
import signal
import subprocess
import tempfile
import time

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'artifacts'
DOTNET=os.environ.get('DOTNET') or shutil.which('dotnet') or str(Path.home()/'.dotnet/dotnet')
PROGRAM=r'''
using System.Diagnostics;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using MySharp;

internal static class Program
{
    private static readonly string Report=Environment.GetEnvironmentVariable("MYSHARP_HOT_RELOAD_REPORT")!;
    private static TextBox? originalInput;
    private static Button? originalButton;
    private static long started=Stopwatch.GetTimestamp();
    [STAThread]
    private static void Main()
    {
        var enabled=MySharpHotReload.Enable();
        MySharpApp.Run(Build,new WindowOptions
        {
            Title="MySharp Hot Reload proof",Width=620,Height=420,
            Opened=window=>
            {
                var view=(UiView)window.Content!;
                originalInput=view.GetLogicalDescendants().OfType<TextBox>().Single();
                originalButton=view.GetLogicalDescendants().OfType<Button>().Single();
                originalInput.Text="typed before reload";
                originalButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(100)};
                timer.Tick+=(_,_)=>
                {
                    var caption=view.GetLogicalDescendants().OfType<TextBlock>().First(t=>t.Text?.Contains("hot reload")==true).Text;
                    var count=view.GetLogicalDescendants().OfType<TextBlock>().First(t=>t.Text?.StartsWith("Count:")==true).Text;
                    var input=view.GetLogicalDescendants().OfType<TextBox>().Single();
                    var after=caption=="After hot reload";
                    var report=new{phase=after?"after":"before",pid=Environment.ProcessId,enabled,caption,count,input=input.Text,
                        sameInput=ReferenceEquals(input,originalInput),sameButton=ReferenceEquals(view.GetLogicalDescendants().OfType<Button>().Single(),originalButton),
                        seconds=Stopwatch.GetElapsedTime(started).TotalSeconds,error=MySharpHotReload.LastError?.Message};
                    File.WriteAllText(Report,JsonSerializer.Serialize(report));
                    if(after)
                    {
                        timer.Stop();window.UpdateLayout();
                        using var image=new RenderTargetBitmap(new PixelSize((int)window.Bounds.Width,(int)window.Bounds.Height));
                        image.Render(window);image.Save(Path.ChangeExtension(Report,"png"),PngBitmapEncoderOptions.Default);
                        window.Close();
                    }
                };
                timer.Start();
            }
        });
    }
    private static void Build(UiContext cx)
    {
        var count=cx.State(0);
        var text=cx.State("seed");
        Ui.Column(cx).Padding(28).Gap(16).Children(()=>
        {
            Ui.Text(cx,"Before hot reload").FontSize(25).Bold();
            Ui.Text(cx,$"Count: {count.Value}").FontSize(18);
            Ui.TextInput(cx,text.Value,value=>text.Value=value);
            Ui.Button(cx,"Increment").OnClick(()=>count.Value++);
        });
    }
}
'''

def stop(process):
    if process is None or process.poll() is not None:return
    os.killpg(process.pid,signal.SIGINT)
    try:process.wait(timeout=8)
    except subprocess.TimeoutExpired:
        os.killpg(process.pid,signal.SIGKILL);process.wait(timeout=5)

def read_report(path):
    try:return json.loads(path.read_text())
    except (FileNotFoundError,json.JSONDecodeError):return None

def wait_report(path,phase,watch,timeout=90):
    deadline=time.monotonic()+timeout
    while time.monotonic()<deadline:
        report=read_report(path)
        if report and report['phase']==phase:return report
        if watch.poll() is not None:raise RuntimeError(f'dotnet watch exited ({watch.returncode}); inspect hot-reload-watch.log')
        time.sleep(.1)
    raise TimeoutError(f'No {phase} report; inspect hot-reload-watch.log')

def main():
    OUT.mkdir(exist_ok=True)
    snapshot=OUT/'hot-reload-live.json'
    snapshot.unlink(missing_ok=True)
    xvfb=None;watch=None
    env=os.environ.copy()
    # Resolve the host root even when DOTNET is a tool-manager shim.
    sdk_lines=subprocess.check_output([DOTNET,'--list-sdks'],text=True).splitlines()
    sdk_line=next((line for line in reversed(sdk_lines) if line.startswith('10.')),None)
    match=re.search(r'\[([^\]]+)\]',sdk_line or '')
    if match is None:raise RuntimeError('Install the .NET 10 SDK before running this smoke test')
    dotnet_dir=str(Path(match.group(1)).resolve().parent)
    dotnet=str(Path(dotnet_dir)/'dotnet')
    env.update(DOTNET_ROOT=dotnet_dir,DOTNET_ROOT_X64=dotnet_dir,DOTNET_WATCH_RESTART_ON_RUDE_EDIT='false',
               DOTNET_WATCH_SUPPRESS_EMOJIS='true',MYSHARP_HOT_RELOAD_REPORT=str(snapshot))
    env['PATH']=dotnet_dir+os.pathsep+env.get('PATH','')
    try:
        with tempfile.TemporaryDirectory(prefix='mysharp-hot-reload-') as folder:
            project=Path(folder)
            (project/'Probe.csproj').write_text(f'''<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup><ItemGroup><ProjectReference Include="{ROOT}/src/MySharp.Desktop/MySharp.Desktop.csproj"/><ProjectReference Include="{ROOT}/src/MySharp.HotReload/MySharp.HotReload.csproj"/></ItemGroup></Project>''')
            source=project/'Program.cs';source.write_text(PROGRAM)
            with (OUT/'hot-reload-xvfb.log').open('w') as xlog,(OUT/'hot-reload-watch.log').open('w') as log:
                xvfb=subprocess.Popen(['Xvfb','-displayfd','1','-screen','0','1000x800x24','-nolisten','tcp'],stdout=subprocess.PIPE,stderr=xlog,start_new_session=True)
                display=xvfb.stdout.readline().decode().strip()
                if not display:raise RuntimeError('Xvfb failed to allocate a display')
                env['DISPLAY']=':'+display
                watch=subprocess.Popen([dotnet,'watch','--non-interactive','--project',str(project/'Probe.csproj'),'run'],cwd=project,env=env,stdout=log,stderr=subprocess.STDOUT,start_new_session=True)
                before=wait_report(snapshot,'before',watch)
                assert before['enabled'],before
                assert before['count']=='Count: 1' and before['input']=='typed before reload',before
                changed=time.monotonic()
                source.write_text(PROGRAM.replace('Ui.Text(cx,"Before hot reload")','Ui.Text(cx,"After hot reload")'))
                after=wait_report(snapshot,'after',watch)
                latency=time.monotonic()-changed
                assert before['pid']==after['pid'],(before,after)
                assert after['sameInput'] and after['sameButton'],after
                assert after['count']==before['count'] and after['input']==before['input'],after
                assert after['error'] is None,after
                # Wait for the app's final screenshot before disposing the isolated process group.
                end=time.monotonic()+5
                while not snapshot.with_suffix('.png').is_file() and time.monotonic()<end:time.sleep(.05)
                result={'before':before,'after':after,'same_process':True,'edit_to_visible_seconds':round(latency,3),
                        'limitation':'One supported method-body text edit under Linux X11; not arbitrary structural edits or other OSes.'}
                (OUT/'hot-reload-smoke.json').write_text(json.dumps(result,indent=2)+'\n')
                print(json.dumps(result,indent=2))
    finally:
        stop(watch);stop(xvfb)

if __name__=='__main__':main()
