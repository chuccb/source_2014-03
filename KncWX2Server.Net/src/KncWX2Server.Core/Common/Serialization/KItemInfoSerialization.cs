using KncWX2Server.Core.Common;

namespace KncWX2Server.Core.Common.Serialization;

/// <summary>Explicit wire serializers for the legacy item models used by KUnitInfo.</summary>
public static class KItemInfoSerialization
{
    public static bool Put(this KSerializer serializer, in KItemAttributeEnchantInfo value) =>
        serializer.Put(value.AttribEnchant0)
        && serializer.Put(value.AttribEnchant1)
        && serializer.Put(value.AttribEnchant2);

    public static bool Get(this KSerializer serializer, out KItemAttributeEnchantInfo value)
    {
        value = default;

        if (!serializer.Get(out sbyte attribEnchant0)
            || !serializer.Get(out sbyte attribEnchant1)
            || !serializer.Get(out sbyte attribEnchant2))
            return false;

        value = new(attribEnchant0, attribEnchant1, attribEnchant2);
        return true;
    }

    public static bool Put(this KSerializer serializer, KItemInfo value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return serializer.Put(value.ItemId)
            && serializer.Put(value.UsageType)
            && serializer.Put(value.Quantity)
            && serializer.Put(value.Endurance)
            && serializer.Put(value.SealData)
            && serializer.Put(value.EnchantLevel)
            && serializer.Put(value.AttribEnchantInfo)
            && serializer.PutVector(value.ItemSockets, static (s, socket) => s.Put(socket))
            && serializer.Put(value.Period)
            && serializer.PutWString(value.ExpirationDate)
            && serializer.Put(value.GoldTicketKeyUid)
            && serializer.Put(value.ExpandedSocketNum);
    }

    public static bool Get(this KSerializer serializer, out KItemInfo value)
    {
        value = new();

        if (!serializer.Get(out value.ItemId)
            || !serializer.Get(out value.UsageType)
            || !serializer.Get(out value.Quantity)
            || !serializer.Get(out value.Endurance)
            || !serializer.Get(out value.SealData)
            || !serializer.Get(out value.EnchantLevel)
            || !serializer.Get(out value.AttribEnchantInfo)
            || !serializer.GetVector(value.ItemSockets, static (KSerializer s, out int socket) => s.Get(out socket))
            || !serializer.Get(out value.Period)
            || !serializer.GetWString(out value.ExpirationDate)
            || !serializer.Get(out value.GoldTicketKeyUid)
            || !serializer.Get(out value.ExpandedSocketNum))
            return false;

        return true;
    }

    public static bool Put(this KSerializer serializer, in KInventoryItemInfo value) =>
        serializer.Put(value.ItemUid)
        && serializer.Put(value.SlotCategory)
        && serializer.Put(value.SlotId)
        && serializer.Put(value.ItemInfo);

    public static bool Get(this KSerializer serializer, out KInventoryItemInfo value)
    {
        value = default;

        if (!serializer.Get(out long itemUid)
            || !serializer.Get(out sbyte slotCategory)
            || !serializer.Get(out short slotId)
            || !serializer.Get(out KItemInfo itemInfo))
            return false;

        value = new(itemUid, slotCategory, slotId, itemInfo);
        return true;
    }
}
