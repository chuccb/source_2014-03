namespace KncWX2Server.Core.Common;

/// <summary>Legacy inventory item state embedded in KUnitInfo.</summary>
public readonly record struct KInventoryItemInfo(
    long ItemUid,
    sbyte SlotCategory,
    short SlotId,
    KItemInfo ItemInfo);
