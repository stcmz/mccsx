using System;
using System.Collections.Generic;
using System.Linq;

namespace mccsx.Statistics
{
    public class ConvolutionalArrayVector<TOriginalKey> : IVector<int, TOriginalKey, string?>
        where TOriginalKey : notnull
    {
        private readonly IReadOnlyList<IVector<TOriginalKey>> _vectors;

        public ConvolutionalArrayVector(IReadOnlyList<IVector<TOriginalKey>> vectors, TOriginalKey slicingkey, string? tag = null)
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
            // original index (in array) => new key (convolutional key)
            // original tag => discarded
            // tag argument => new tag

            _vectors = vectors;
            Name = slicingkey;
            Tag = tag;
            IsZero = Length == 0 || Values.All(o => o == 0.0);
            IsNaN = Length > 0 && Values.All(o => double.IsNaN(o));
        }

        public double this[int key] => _vectors[key][Name];

        public IEnumerable<int> Keys => Enumerable.Range(0, Length);

        public IEnumerable<double> Values => Keys.Select(o => this[o]);

        public TOriginalKey Name { get; }

        public string? Tag { get; }

        public int Length => _vectors.Count;

        public bool IsZero { get; }

        public bool IsNaN { get; }

        public ICluster<int> Cluster()
        {
            return new ClusterBase<IVector<int, TOriginalKey, string?>, int>(new[] { this });
        }

        public IEnumerable<int> UnionKeys(params IVector<int>[] vecs)
        {
            return Enumerable.Range(0, Math.Max(Length, vecs.Cast<ConvolutionalArrayVector<TOriginalKey>>().Max(o => o.Length)));
        }

        public IEnumerable<int> UnionKeys(IEnumerable<IVector<int>> vecs)
        {
            return Enumerable.Range(0, Math.Max(Length, vecs.Cast<ConvolutionalArrayVector<TOriginalKey>>().Max(o => o.Length)));
        }
    }
}
