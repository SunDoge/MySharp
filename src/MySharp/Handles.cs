namespace MySharp;

// A saved build handle retains only this small record after the view closes.
internal sealed class ElementOwner(UiContext context)
{
    internal UiContext? Context = context;
    internal Node Resolve(int slot, int generation)
    {
        var current = Context ?? throw new InvalidOperationException("The element's view has been disposed or its generation retired.");
        return current.ResolveElement(slot, generation);
    }
}
