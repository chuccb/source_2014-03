namespace KncWX2Server.Core.Common;

/// <summary>Legacy item payload embedded in inventory item information.</summary>
public sealed class KItemInfo
{
    public int ItemId { get; set; }
    public sbyte UsageType { get; set; }
    public int Quantity { get; set; } = 1;
    public short Endurance { get; set; }
    public byte SealData { get; set; }
    public sbyte EnchantLevel { get; set; }

    public KItemAttributeEnchantInfo AttribEnchantInfo { get; set; }
    public List<int> ItemSockets { get; } = [];

    public short Period { get; set; }
    public string ExpirationDate { get; set; } = string.Empty;
    public long GoldTicketKeyUid { get; set; }
    public byte ExpandedSocketNum { get; set; }
}
