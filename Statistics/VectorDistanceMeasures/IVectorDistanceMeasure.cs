namespace mccsx.Statistics
{
    /// <summary>
    /// Similarity and Distance Metrics
    /// https://en.wikipedia.org/wiki/Category:Similarity_and_distance_measures
    /// https://en.wikipedia.org/wiki/Similarity_measure
    /// </summary>
    public interface IVectorDistanceMeasure
    {
        string Name { get; }
        double Measure<TKey>(IVector<TKey> vec1, IVector<TKey> vec2) where TKey : notnull;
    }
}
