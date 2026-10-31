using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace MySharp;

public sealed partial class UiContext
{
    /// <summary>Native host services become available after attachment; use them from UI events.</summary>
    public TopLevel? TopLevel { get { Dispatcher.UIThread.VerifyAccess(); return Avalonia.Controls.TopLevel.GetTopLevel(root); } }
    public IClipboard? Clipboard => TopLevel?.Clipboard;
    public IStorageProvider? Storage => TopLevel?.StorageProvider;
    public Screens? Screens => TopLevel?.Screens;
    public ILauncher? Launcher => TopLevel?.Launcher;
    public IPlatformHandle? PlatformHandle => TopLevel?.TryGetPlatformHandle();
}
