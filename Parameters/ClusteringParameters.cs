using mccsx.Extensions;
using mccsx.Statistics;

namespace mccsx;

internal class ClusteringParameters(Measure measure, Linkage linkage)
{
    public Measure DistanceType { get; } = measure;
    public IVectorDistanceMeasure DistanceMeasure { get; } = measure.DistanceMeasure();
    public Linkage LinkageType { get; } = linkage;
    public IClusterDistanceMeasure LinkageAlgorithm { get; } = linkage.LinkageAlgorithm();
}
