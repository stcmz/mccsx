using System;

namespace mccsx;

[AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
internal sealed class MeasureImplAttribute(Type similarityClass, Type distanceType) : Attribute
{
    public Type SimilarityClass { get; } = similarityClass;

    public Type DistanceClass { get; } = distanceType;
}
