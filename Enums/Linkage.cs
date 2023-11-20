using mccsx.Statistics;

namespace mccsx;

internal enum Linkage
{
    [LinkageImpl(typeof(FarthestPointMethod))]
    farthest,

    [LinkageImpl(typeof(NearestPointMethod))]
    nearest,

    [LinkageImpl(typeof(AverageDistanceMethod))]
    average,
}
