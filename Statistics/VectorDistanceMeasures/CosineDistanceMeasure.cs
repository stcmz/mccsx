namespace mccsx.Statistics;

/// <summary>
/// 1 - Cosine Similarity
/// </summary>
public class CosineDistanceMeasure : IVectorDistanceMeasure
{
    public string Name => "Cosine";

    public double Measure<TKey>(IVector<TKey> vec1, IVector<TKey> vec2)
        where TKey : notnull
    {
        return 1 - new CosineSimilarityMeasure().Measure(vec1, vec2);
    }
}
