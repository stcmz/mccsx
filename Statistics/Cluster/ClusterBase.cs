﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace mccsx.Statistics;

public class ClusterBase<TVector, TKey>(IEnumerable<TVector> vectors) : ICluster<TKey>
    where TVector : IVector<TKey>
    where TKey : notnull
{
    private readonly IList<IVector<TKey>> Data = vectors.Cast<IVector<TKey>>().ToList();

    public IVector<TKey> this[int index] => Data[index];

    public int Length => Data.Count;

    public IEnumerable<IVector<TKey>> Vectors => Data;

    public void Add(IVector<TKey> vector)
    {
        if (vector is TVector av)
            Data.Add(av);
        else
            throw new NotSupportedException();
    }

    public void Add(ICluster<TKey> cluster)
    {
        foreach (IVector<TKey> vector in cluster.Vectors)
            Add(vector);
    }

    public void Clear()
    {
        Data.Clear();
    }
}
