using mccsx.Statistics;

namespace mccsx
{
    internal record SimilarityModel
    (
        Measure Type,
        IVectorDistanceMeasure Measure
    );
}
