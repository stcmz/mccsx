using System.Collections.Generic;

namespace mccsx.Statistics;

public class CachedVectorDistanceMeasure(IVectorDistanceMeasure realMeasure) : IVectorDistanceMeasure
{
    private readonly Dictionary<object, Dictionary<object, double>> _cache = [];
    private readonly IVectorDistanceMeasure _measure = realMeasure;

    public string Name => _measure.Name;

    public double Measure<TKey>(IVector<TKey> vec1, IVector<TKey> vec2)
        where TKey : notnull
    {
        if (!_cache.TryGetValue(vec1, out Dictionary<object, double>? dict1))
        {
            _cache.Add(vec1, dict1 = []);
        }
        else if (dict1.TryGetValue(vec2, out double val))
        {
            return val;
        }

        if (!_cache.TryGetValue(vec2, out Dictionary<object, double>? dict2))
        {
            _cache.Add(vec2, dict2 = []);
        }

        return dict1[vec2] = dict2[vec1] = _measure.Measure(vec1, vec2);
    }
}
