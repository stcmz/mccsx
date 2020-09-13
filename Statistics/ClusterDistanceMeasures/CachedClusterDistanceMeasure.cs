using System.Collections.Generic;

namespace mccsx.Statistics
{
    public class CachedClusterDistanceMeasure : IClusterDistanceMeasure
    {
        private readonly Dictionary<IVectorDistanceMeasure, Dictionary<object, Dictionary<object, (double dist, int length1, int length2)>>> _cache;
        private readonly IClusterDistanceMeasure _measure;

        public CachedClusterDistanceMeasure(IClusterDistanceMeasure measure)
        {
            _measure = measure;
            _cache = new Dictionary<IVectorDistanceMeasure, Dictionary<object, Dictionary<object, (double dist, int length1, int length2)>>>();
        }

        public string Name => _measure.Name;

        public double Distance<TKey>(IVectorDistanceMeasure vdm, ICluster<TKey> cluster1, ICluster<TKey> cluster2)
            where TKey : notnull
        {
            if (!_cache.TryGetValue(vdm, out var cache))
                _cache.Add(vdm, cache = new Dictionary<object, Dictionary<object, (double dist, int length1, int length2)>>());

            if (!cache.TryGetValue(cluster1, out var dict1))
            {
                cache.Add(cluster1, dict1 = new Dictionary<object, (double, int, int)>());
            }
            else if (dict1.TryGetValue(cluster2, out var vals) && vals.length1 == cluster1.Length && vals.length2 == cluster2.Length)
            {
                return vals.dist;
            }

            if (!cache.TryGetValue(cluster2, out var dict2))
            {
                cache.Add(cluster2, dict2 = new Dictionary<object, (double, int, int)>());
            }

            double dist = _measure.Distance(vdm, cluster1, cluster2);
            dict1[cluster2] = (dist, cluster1.Length, cluster2.Length);
            dict2[cluster1] = (dist, cluster2.Length, cluster1.Length);

            return dist;
        }
    }
}
