using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using MySharp;

internal static class HotReloadTests
{
    internal static int Run()
    {
        var caption="before";
        State<int>? state=null;
        var builds=0;
        using(var view=new UiView(cx=>
        {
            builds++;
            state=cx.State(7);
            Ui.Column(cx).Children(()=>
            {
                Ui.Text(cx,caption);
                Ui.Button(cx,"increment").OnClick(()=>state.Value++);
            });
        }))
        {
            var window=new Window{Content=view};window.Show();Dispatcher.UIThread.RunJobs();window.UpdateLayout();
            try
            {
                var button=view.GetLogicalDescendants().OfType<Button>().Single();
                var originalState=state;
                state!.Value=42;Dispatcher.UIThread.RunJobs();caption="after";
                Task.Run(()=>MySharpHotReload.Refresh(window)).GetAwaiter().GetResult();Dispatcher.UIThread.RunJobs();
                Check(view.GetLogicalDescendants().OfType<TextBlock>().Any(t=>t.Text=="after"),"Refresh must execute current declaration on UI thread.");
                Check(ReferenceEquals(originalState,state)&&state.Value==42,"Unchanged identity must preserve local state.");
                Check(ReferenceEquals(button,view.GetLogicalDescendants().OfType<Button>().Single()),"Retained native controls must survive a refresh.");
                var previous=builds;
                for(var i=0;i<10;i++)Dispatcher.UIThread.RunJobs();
                Check(builds==previous,"Hot Reload must not install an idle rebuild loop.");
                window.Hide();caption="hidden";MySharpHotReload.Refresh(window);Dispatcher.UIThread.RunJobs();
                Check(builds==previous,"Hidden/closed windows must not be rebuilt.");
            }
            finally {window.Close();}
        }
        Console.WriteLine("PASS hot reload explicit refresh dispatches current declarations and retains state/control identity without idle work");

        var fail=false;var goodCaption="before";
        using(var bad=new UiView(cx=>{if(fail)throw new InvalidOperationException("bad hot edit");Ui.Text(cx,"old");}))
        using(var good=new UiView(cx=>Ui.Text(cx,goodCaption)))
        {
            var panel=new StackPanel();panel.Children.Add(bad);panel.Children.Add(good);
            var window=new Window{Content=panel};window.Show();Dispatcher.UIThread.RunJobs();window.UpdateLayout();
            try
            {
                fail=true;goodCaption="after";MySharpHotReload.Refresh(window);Dispatcher.UIThread.RunJobs();
                Check(MySharpHotReload.LastError?.Message=="bad hot edit","A failed declaration must be reported without crashing the dispatcher.");
                Check(good.GetLogicalDescendants().OfType<TextBlock>().Single().Text=="after","One failing view must not prevent other views refreshing.");
                fail=false;MySharpHotReload.Refresh(window);Dispatcher.UIThread.RunJobs();
                Check(MySharpHotReload.LastError is null,"The next valid update must recover and clear the prior error.");
            }
            finally {window.Close();}
        }
        Console.WriteLine("PASS hot reload isolates failed edits and recovers on a later valid refresh");

        using(var removed=new UiView(cx=>Ui.Text(cx,"disposed")))
        {
            var window=new Window{Content=removed};window.Show();Dispatcher.UIThread.RunJobs();
            MySharpHotReload.Refresh(window);removed.Dispose();window.Close();Dispatcher.UIThread.RunJobs();
            Check(MySharpHotReload.LastError is null,"Queued refresh must ignore a view closed before dispatch.");
        }
        Console.WriteLine("PASS queued hot reload ignores closed and disposed views");
        return 3;
    }
    private static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
}
