namespace Multiboxer.Core.Diagnostics;

/// <summary>
/// Simple logger that is always included in Release builds.
/// Redirects existing Debug.WriteLine calls to Trace so file logging works everywhere.
/// </summary>
public static class DebugLogger
{
    public static void WriteLine(string? message)
    {
        System.Diagnostics.Trace.WriteLine(message);
    }

    public static void WriteLine(object? value)
    {
        System.Diagnostics.Trace.WriteLine(value);
    }

    public static void WriteLine(string format, params object?[] args)
    {
        System.Diagnostics.Trace.WriteLine(string.Format(format, args));
    }
}

