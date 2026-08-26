namespace KncWX2Server.Core.Common;

public sealed class KBuffInfo
{
    public KBuffFactor FactorInfo { get; set; } = new();
    public long BuffStartTime { get; set; }
    public long BuffEndTime { get; set; }
}
