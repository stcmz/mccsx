using System;
using System.Collections.Generic;
using System.Linq;

namespace mccsx.Statistics;

public class ArrayVector : IVector<int, string, string?>
{
    private readonly double[] _data;

    public ArrayVector(double[] data, string name, string? tag = null)
    {
        _data = data;
        Name = name;
        Tag = tag;
        IsZero = Length == 0 || Values.All(o => o == 0.0);
        IsNaN = Length > 0 && Values.All(double.IsNaN);
    }

    public double this[int key] => key < Length ? _data[key] : 0.0;

    public IEnumerable<int> Keys => Enumerable.Range(0, Length);

    public IEnumerable<double> Values => _data;

    public string Name { get; }

    public string? Tag { get; }

    public int Length => _data.Length;

    public bool IsZero { get; }

    public bool IsNaN { get; }

    public bool Has(int key)
    {
        return 0 <= key && key < _data.Length;
    }

    public ICluster<int> Cluster()
    {
        return new ClusterBase<IVector<int>, int>([ this ]);
    }

    public IEnumerable<int> UnionKeys(params IVector<int>[] vecs)
    {
        return Enumerable.Range(0, Math.Max(Length, vecs.Cast<ArrayVector>().Max(o => o.Length)));
    }

    public IEnumerable<int> UnionKeys(IEnumerable<IVector<int>> vecs)
    {
        return Enumerable.Range(0, Math.Max(Length, vecs.Cast<ArrayVector>().Max(o => o.Length)));
    }
}
