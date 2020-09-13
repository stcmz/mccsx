using System.Collections.Generic;
using System.Linq;

namespace mccsx.Statistics
{
    public class ConvolutionalMapVector<TKey, TName> : IVector<TKey, TName, string?>
        where TKey : notnull
        where TName : notnull
    {
        private readonly IReadOnlyDictionary<TKey, IVector<TName, TKey, string?>> _vectors;

        public ConvolutionalMapVector(IReadOnlyDictionary<TKey, IVector<TName, TKey, string?>> vectors, TName slicingkey, string? tag = null)
        {
            // original vectors:
            //   0    1    2
            //   |    |    | -> key
            //   |    |    | -> key
            //   |    |    | -> key
            // name name name
            //  tag  tag  tag
            //      =>
            // convolutional vectors
            // -------------- -> name tag
            // -------------- -> name tag
            // -------------- -> name tag
            //   0    1    2
            //  key  key  key
            //
            // Mappings:
            // original key (slicing key) => convolutional name
            // original name => new key (convolutional key)
            // original tag => discarded
            // tag argument => new tag

            _vectors = vectors;
            Name = slicingkey;
            Tag = tag;
            IsZero = Length == 0 || Values.All(o => o == 0.0);
            IsNaN = Length > 0 && Values.All(o => double.IsNaN(o));
        }

        public double this[TKey key] => _vectors[key][Name];

        public IEnumerable<TKey> Keys => _vectors.Keys;

        public IEnumerable<double> Values => Keys.Select(o => this[o]);

        public TName Name { get; }

        public string? Tag { get; }

        public int Length => _vectors.Count;

        public bool IsZero { get; }

        public bool IsNaN { get; }

        public ICluster<TKey> Cluster()
        {
            return new ClusterBase<IVector<TKey, TName, string?>, TKey>(new[] { this });
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
