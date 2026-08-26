using KncWX2Server.Core.Common;

namespace KncWX2Server.Core.Common.Serialization;

public static class KBuffInfoSerialization
{
    public static bool Put(this KSerializer serializer, KBuffBehaviorFactor value) =>
        serializer.Put(value.Type)
        && serializer.PutVector(value.Values, static (KSerializer s, float item) => s.Put(item));

    public static bool Get(this KSerializer serializer, out KBuffBehaviorFactor value)
    {
        value = new();

        if (!serializer.Get(out uint type)
            || !serializer.GetVector(value.Values, static (KSerializer s, out float item) => s.Get(out item)))
            return false;

        value.Type = type;
        return true;
    }

    public static bool Put(this KSerializer serializer, KBuffFinalizerFactor value) =>
        serializer.Put(value.Type)
        && serializer.PutVector(value.Values, static (KSerializer s, float item) => s.Put(item));

    public static bool Get(this KSerializer serializer, out KBuffFinalizerFactor value)
    {
        value = new();

        if (!serializer.Get(out uint type)
            || !serializer.GetVector(value.Values, static (KSerializer s, out float item) => s.Get(out item)))
            return false;

        value.Type = type;
        return true;
    }

    public static bool Put(this KSerializer serializer, in KBuffIdentity value) =>
        serializer.Put(value.BuffTemplateId)
        && serializer.Put(value.UniqueNumber);

    public static bool Get(this KSerializer serializer, out KBuffIdentity value)
    {
        value = default;

        if (!serializer.Get(out int buffTemplateId)
            || !serializer.Get(out uint uniqueNumber))
            return false;

        value = new(buffTemplateId, uniqueNumber);
        return true;
    }

    public static bool Put(this KSerializer serializer, KBuffFactor value) =>
        serializer.PutVector(value.BehaviorFactors, KBuffBehaviorFactorSerialization.Put)
        && serializer.PutVector(value.FinalizerFactors, KBuffFinalizerFactorSerialization.Put)
        && serializer.Put(value.BuffIdentity)
        && serializer.Put(value.AccumulationMultiplier)
        && serializer.Put(value.AccumulationCountNow)
        && serializer.Put(value.FactorId);

    public static bool Get(this KSerializer serializer, out KBuffFactor value)
    {
        value = new();

        if (!serializer.GetVector(value.BehaviorFactors, KBuffBehaviorFactorSerialization.Get)
            || !serializer.GetVector(value.FinalizerFactors, KBuffFinalizerFactorSerialization.Get)
            || !serializer.Get(out KBuffIdentity buffIdentity)
            || !serializer.Get(out float accumulationMultiplier)
            || !serializer.Get(out byte accumulationCountNow)
            || !serializer.Get(out int factorId))
            return false;

        value.BuffIdentity = buffIdentity;
        value.AccumulationMultiplier = accumulationMultiplier;
        value.AccumulationCountNow = accumulationCountNow;
        value.FactorId = factorId;
        return true;
    }

    public static bool Put(this KSerializer serializer, KBuffInfo value) =>
        serializer.Put(value.FactorInfo)
        && serializer.Put(value.BuffStartTime)
        && serializer.Put(value.BuffEndTime);

    public static bool Get(this KSerializer serializer, out KBuffInfo value)
    {
        value = new();

        if (!serializer.Get(out KBuffFactor factorInfo)
            || !serializer.Get(out long buffStartTime)
            || !serializer.Get(out long buffEndTime))
            return false;

        value.FactorInfo = factorInfo;
        value.BuffStartTime = buffStartTime;
        value.BuffEndTime = buffEndTime;
        return true;
    }
}

public static class KBuffBehaviorFactorSerialization
{
    public static bool Put(this KSerializer serializer, KBuffBehaviorFactor value) =>
        KBuffInfoSerialization.Put(serializer, value);

    public static bool Get(this KSerializer serializer, out KBuffBehaviorFactor value) =>
        KBuffInfoSerialization.Get(serializer, out value);
}

public static class KBuffFinalizerFactorSerialization
{
    public static bool Put(this KSerializer serializer, KBuffFinalizerFactor value) =>
        KBuffInfoSerialization.Put(serializer, value);

    public static bool Get(this KSerializer serializer, out KBuffFinalizerFactor value) =>
        KBuffInfoSerialization.Get(serializer, out value);
}
