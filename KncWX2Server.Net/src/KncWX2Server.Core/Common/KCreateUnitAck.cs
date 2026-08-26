namespace KncWX2Server.Core.Common;

/// <summary>Legacy EGS_CREATE_UNIT_ACK payload. EventId is intentionally not part of the model.</summary>
public sealed class KCreateUnitAck
{
    public int Result { get; set; }
    public KUnitInfo UnitInfo { get; set; } = new();
    public List<int> SkillSlots { get; } = [];
    public string EnableDate { get; set; } = string.Empty;
}
