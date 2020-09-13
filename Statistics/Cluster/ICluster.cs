using System.Collections.Generic;

namespace mccsx.Statistics
{
    public interface ICluster<TKey>
        where TKey : notnull
    {
        IVector<TKey> this[int index] { get; }
        int Length { get; }

        IEnumerable<IVector<TKey>> Vectors { get; }

        void Add(IVector<TKey> vector);
        void Add(ICluster<TKey> cluster);
        void Clear();
    }
}
