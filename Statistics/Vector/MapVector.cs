using System;
using System.Collections.Generic;
using System.Linq;

namespace mccsx.Statistics
{
    public class MapVector<TKey> : IVector<TKey, string, string?>
        where TKey : notnull
    {
        private readonly IDictionary<TKey, double> _data;

        public MapVector(IDictionary<TKey, double> data, string name, string? tag = null)
        {
            _data = data;
            Name = name;
            Tag = tag;
            IsZero = Length == 0 || Values.All(o => o == 0.0);
            IsNaN = Length > 0 && Values.All(o => double.IsNaN(o));
        }

        public double this[TKey key] => _data.TryGetValue(key, out double val) ? val : 0.0;

        public IEnumerable<TKey> Keys => _data.Keys;

        public IEnumerable<double> Values => _data.Values;

        public int Length => _data.Count;

        public string Name { get; }

        public string? Tag { get; }

        public bool IsZero { get; }

        public bool IsNaN { get; }

        public bool Has(TKey key)
        {
            return _data.ContainsKey(key);
        }

        public ICluster<TKey> Cluster()
        {
            return new ClusterBase<IVector<TKey, string, string?>, TKey>(new[] { this });
        }

        public IEnumerable<TKey> UnionKeys(params IVector<TKey>[] vecs)
        {
            return Keys.Concat(vecs.SelectMany(o => o.Keys)).Distinct().OrderBy(o => o);
        }

        public IEnumerable<TKey> UnionKeys(IEnumerable<IVector<TKey>> vecs)
        {
            return Keys.Concat(vecs.SelectMany(o => o.Keys)).Distinct().OrderBy(o => o);
        }
    }
}
