namespace MySharp;

// No allocation on the successful path. Cleanup continues before errors reach the caller.
internal struct CleanupErrors
{
    private List<Exception>? errors;
    internal void Add(Exception error)
    {
        errors ??= [];
        if (error is AggregateException aggregate) errors.AddRange(aggregate.InnerExceptions);
        else errors.Add(error);
    }
    internal void ThrowIfAny()
    {
        if (errors is { Count: > 0 }) throw new AggregateException("One or more MySharp cleanup operations failed.", errors);
    }
}
