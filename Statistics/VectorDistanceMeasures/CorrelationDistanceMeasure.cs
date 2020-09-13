namespace mccsx.Statistics
{
    /// <summary>
    /// 1 - Pearson Correlation Coefficient
    /// https://en.wikipedia.org/wiki/Pearson_correlation_coefficient#Pearson's_distance
    /// </summary>
    public class CorrelationDistanceMeasure : IVectorDistanceMeasure
    {
        public string Name => "Correlation";

        public double Measure<TKey>(IVector<TKey> vec1, IVector<TKey> vec2)
            where TKey : notnull
        {
            return 1 - new CorrelationCoefficientMeasure().Measure(vec1, vec2);
        }
    }
}
