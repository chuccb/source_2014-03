namespace KncWX2Server.Core.Common;

/// <summary>
/// Managed identity/name base corresponding to the legacy KSimObject.
/// The native implementation exposed its smart-pointer use count; .NET GC does not expose an
/// equivalent count, so that diagnostic is intentionally represented as unavailable.
/// </summary>
public class SimObject
{
    private static long _nameSequence;

    public SimObject()
    {
        var sequence = Interlocked.Increment(ref _nameSequence) - 1;
        var now = DateTime.Now;
        Name = $"SOB_{now:MM/dd/yy}_{now:HH:mm:ss}_{sequence:00000000000000000000}";
    }

    public string Name { get; set; }

    public long Uid { get; set; }

    public int GetReferenceCount() => -1;
}
