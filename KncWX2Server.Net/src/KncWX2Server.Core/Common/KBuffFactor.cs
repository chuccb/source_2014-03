namespace KncWX2Server.Core.Common;

public sealed class KBuffFactor
{
    public List<KBuffBehaviorFactor> BehaviorFactors { get; } = [];
    public List<KBuffFinalizerFactor> FinalizerFactors { get; } = [];
    public KBuffIdentity BuffIdentity { get; set; }
    public float AccumulationMultiplier { get; set; }
    public byte AccumulationCountNow { get; set; }
    public int FactorId { get; set; }
}
