namespace KncWX2Server.Core.Common.Serialization;

/// <summary>Legacy KNC serialization tags. Numeric values must not be reordered.</summary>
public enum SerializeTag : byte
{
    Char,
    WChar,
    UChar,
    Short,
    UShort,
    Int,
    DWord,
    Int64,
    UInt64,
    Float,
    Double,
    Bool,
    String,
    WString,
    Array,
    RawBytes,
    Pair,
    Vector,
    List,
    Deque,
    Set,
    Multiset,
    Map,
    Multimap,
    Buffer,
    KeyedSerializer,
    UserClass,
    Last,
}
