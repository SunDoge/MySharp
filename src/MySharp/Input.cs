using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;

namespace MySharp;

public readonly partial struct Element
{
    public Element Focusable(bool value = true) => Set(InputElement.FocusableProperty, value);
    public Element TabIndex(int value) => Set(InputElement.TabIndexProperty, value);
    public Element TabStop(bool value = true) => Set(InputElement.IsTabStopProperty, value);
    public Element Cursor(Cursor? value) => Set(InputElement.CursorProperty, value);
    public Element AutomationId(string value) => Set(AutomationProperties.AutomationIdProperty, value);
    public Element HotKey(KeyGesture? value)
    {
        var control = Node.Inner;
        if (control is Button) return Set(Button.HotKeyProperty, value);
        if (control is MenuItem) return Set(MenuItem.HotKeyProperty, value);
        if (control is SplitButton) return Set(SplitButton.HotKeyProperty, value);
        throw new InvalidOperationException("HotKey requires a Button, MenuItem or SplitButton.");
    }
}
