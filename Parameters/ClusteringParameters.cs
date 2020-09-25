using mccsx.Extensions;
using mccsx.Statistics;

namespace mccsx
{
    internal record ClusteringParameters
    {
        public Measure DistanceType { get; }
        public IVectorDistanceMeasure DistanceMeasure { get; }
        public Linkage LinkageType { get; }
        public IClusterDistanceMeasure LinkageAlgorithm { get; }

        public ClusteringParameters(Measure measure, Linkage linkage)
            => (DistanceType, DistanceMeasure, LinkageType, LinkageAlgorithm) = (measure, measure.DistanceMeasure(), linkage, linkage.LinkageAlgorithm());
    }
}
