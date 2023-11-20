using mccsx.Helpers;
using mccsx.Statistics;
using System;
using System.Diagnostics;

namespace mccsx.Extensions;

internal static class LinkageExtensions
{
    public static IClusterDistanceMeasure LinkageAlgorithm(this Linkage linkage)
    {
        Type linkClassType = EnumAnnotationHelper<Linkage>.GetAttribute<LinkageImplAttribute>(linkage).LinkageClass;
        object? obj = Activator.CreateInstance(linkClassType);
        Debug.Assert(obj != null && obj is IClusterDistanceMeasure);
        return (obj as IClusterDistanceMeasure)!;
    }
}
