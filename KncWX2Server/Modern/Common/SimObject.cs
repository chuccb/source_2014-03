using System.Threading;

namespace KncWX2Server.Common;

/// <summary>
/// Managed equivalent of the legacy KSimObject identity/name base class.
/// .NET does not expose GC reference counts, so reference-count diagnostics are intentionally
/// not reproduced as a false value.
/// </summary>
public class SimObject
{
    private static long _nameSequence;

    public SimObject()
    {
        var sequence = Interlocked.Increment(ref _nameSequence) - 1;
        Name = $"SOB_{DateTime.Now:MM/dd/yy}_{DateTime.Now:HH:mm:ss}_{sequence:00000000000000000000}";
    }

    public string Name { get; set; }

    public long Uid { get; set; }

    public int GetReferenceCount() => -1;
}
