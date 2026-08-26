namespace KncWX2Server.Core.Common;

/// <summary>Legacy three-slot item attribute-enchant state.</summary>
public readonly record struct KItemAttributeEnchantInfo(
    sbyte AttribEnchant0,
    sbyte AttribEnchant1,
    sbyte AttribEnchant2);
