using mccsx.Extensions;
using mccsx.Statistics;

namespace mccsx;

internal class SimilarityParameters(Measure measure)
{
    public Measure Type { get; } = measure;
    public IVectorDistanceMeasure Measure { get; } = measure.SimilarityMeasure();
}
