using mccsx.Statistics;

namespace mccsx
{
    internal enum Measure
    {
        [MeasureImpl(typeof(CosineSimilarityMeasure), typeof(CosineDistanceMeasure))]
        cosine,

        [MeasureImpl(typeof(CorrelationCoefficientMeasure), typeof(CorrelationDistanceMeasure))]
        correlation,
    }
}
