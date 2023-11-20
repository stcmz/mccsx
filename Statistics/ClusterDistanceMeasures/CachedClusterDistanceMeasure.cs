using System.Collections.Generic;

namespace mccsx.Statistics;

public class CachedClusterDistanceMeasure(IClusterDistanceMeasure measure) : IClusterDistanceMeasure
{
    private readonly Dictionary<IVectorDistanceMeasure, Dictionary<object, Dictionary<object, (double dist, int length1, int length2)>>> _cache = [];
    private readonly IClusterDistanceMeasure _measure = measure;

    public string Name => _measure.Name;

    public double Distance<TKey>(IVectorDistanceMeasure vdm, ICluster<TKey> cluster1, ICluster<TKey> cluster2)
        where TKey : notnull
    {
        if (!_cache.TryGetValue(vdm, out Dictionary<object, Dictionary<object, (double dist, int length1, int length2)>>? cache))
            _cache.Add(vdm, cache = []);

        if (!cache.TryGetValue(cluster1, out Dictionary<object, (double dist, int length1, int length2)>? dict1))
        {
            cache.Add(cluster1, dict1 = new Dictionary<object, (double, int, int)>());
        }
        else if (dict1.TryGetValue(cluster2, out (double dist, int length1, int length2) vals) && vals.length1 == cluster1.Length && vals.length2 == cluster2.Length)
        {
            return vals.dist;
        }

        if (!cache.TryGetValue(cluster2, out Dictionary<object, (double dist, int length1, int length2)>? dict2))
        {
            cache.Add(cluster2, dict2 = new Dictionary<object, (double, int, int)>());
        }

        double dist = _measure.Distance(vdm, cluster1, cluster2);
        dict1[cluster2] = (dist, cluster1.Length, cluster2.Length);
        dict2[cluster1] = (dist, cluster2.Length, cluster1.Length);

        return dist;
    }
}
