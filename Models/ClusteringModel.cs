using mccsx.Statistics;

namespace mccsx
{
    internal record ClusteringModel
    (
        Measure DistanceType,
        IVectorDistanceMeasure DistanceMeasure,
        Linkage LinkageType,
        IClusterDistanceMeasure LinkageAlgorithm
    );
}
