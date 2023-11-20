using System.Collections.Generic;

namespace mccsx.Statistics;

public interface IVector<TKey>
    where TKey : notnull
{
    double this[TKey key] { get; }
    IEnumerable<TKey> Keys { get; }
    IEnumerable<double> Values { get; }

    int Length { get; }
    bool IsZero { get; }
    bool IsNaN { get; }

    bool Has(TKey key);
    ICluster<TKey> Cluster();
    IEnumerable<TKey> UnionKeys(params IVector<TKey>[] vecs);
    IEnumerable<TKey> UnionKeys(IEnumerable<IVector<TKey>> vecs);
}

public interface IVector<TKey, out TName> : IVector<TKey>
    where TKey : notnull
    where TName : notnull
{
    TName Name { get; }
}

public interface IVector<TKey, out TName, out TTag> : IVector<TKey, TName>
    where TKey : notnull
    where TName : notnull
{
    TTag Tag { get; }
}
