using System;
using System.Linq;

namespace mccsx.Statistics;

/// <summary>
/// https://en.wikipedia.org/wiki/Euclidean_distance
/// </summary>
public class EuclideanDistanceMeasure : IVectorDistanceMeasure
{
    public string Name => "Euclidean";

    public double Measure<TKey>(IVector<TKey> vec1, IVector<TKey> vec2)
        where TKey : notnull
    {
        TKey[] keys = vec1.UnionKeys(vec2).Where(key => !double.IsNaN(vec1[key]) && !double.IsNaN(vec2[key])).ToArray();

        if (keys.Length == 0)
            return double.NaN;

        return Math.Sqrt(keys.Sum(o => Math.Pow(vec1[o] - vec2[o], 2)));
    }
}
