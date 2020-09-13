using System;

namespace mccsx
{
    [AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
    internal sealed class MeasureImplAttribute : Attribute
    {
        public MeasureImplAttribute(Type similarityClass, Type distanceType)
            => (SimilarityClass, DistanceClass) = (similarityClass, distanceType);

        public Type SimilarityClass { get; }

        public Type DistanceClass { get; }
    }
}
