using mccsx.Extensions;
using mccsx.Statistics;

namespace mccsx
{
    internal record SimilarityParameters
    {
        public Measure Type { get; }
        public IVectorDistanceMeasure Measure { get; }

        public SimilarityParameters(Measure measure) => (Type, Measure) = (measure, measure.SimilarityMeasure());
    }
}
