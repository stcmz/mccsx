using mccsx.Helpers;
using mccsx.Statistics;
using System;
using System.Diagnostics;

namespace mccsx.Extensions;

internal static class MeasureExtensions
{
    public static IVectorDistanceMeasure SimilarityMeasure(this Measure measure)
    {
        Type simClassType = EnumAnnotationHelper<Measure>.GetAttribute<MeasureImplAttribute>(measure).SimilarityClass;
        object? obj = Activator.CreateInstance(simClassType);
        Debug.Assert(obj != null && obj is IVectorDistanceMeasure);
        return (obj as IVectorDistanceMeasure)!;
    }

    public static IVectorDistanceMeasure DistanceMeasure(this Measure measure)
    {
        Type simClassType = EnumAnnotationHelper<Measure>.GetAttribute<MeasureImplAttribute>(measure).DistanceClass;
        object? obj = Activator.CreateInstance(simClassType);
        Debug.Assert(obj != null && obj is IVectorDistanceMeasure);
        return (obj as IVectorDistanceMeasure)!;
    }
}
