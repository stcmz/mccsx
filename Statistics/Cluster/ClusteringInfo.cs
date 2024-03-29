﻿using mccsx.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace mccsx.Statistics;

public class ClusteringInfo<TRowKey, TColumnKey>
    where TRowKey : notnull
    where TColumnKey : notnull
{
    public ClusteringInfo(
        IClusterDistanceMeasure cdm,
        IVectorDistanceMeasure vdm,
        IEnumerable<IVector<TRowKey, TColumnKey>> vectors)
    {
        _vectors = vectors;
        List<ICluster<TRowKey>?> clusters = vectors.Select(o => (ICluster<TRowKey>?)o.Cluster()).ToList();

        BadVectorCount = clusters.Count(o => o![0].IsZero || o[0].IsNaN);
        VectorCount = clusters.Count;

        MetricName = vdm.Name;
        ClusterMethod = cdm.Name;

        List<ClusteringNode> nodes = [];
        List<int> depths = Enumerable.Repeat(0, VectorCount).ToList();

        while (true)
        {
            double minDist = double.PositiveInfinity;
            int mini = -1, minj = -1;

            for (int i = 0; i < clusters.Count; i++)
            {
                ICluster<TRowKey>? clusi = clusters[i];
                if (clusi == null || clusi.Length == 1 && (clusi[0].IsZero || clusi[0].IsNaN))
                    continue;
                for (int j = i + 1; j < clusters.Count; j++)
                {
                    ICluster<TRowKey>? clusj = clusters[j];
                    if (clusj == null || clusj.Length == 1 && (clusj[0].IsZero || clusj[0].IsNaN))
                        continue;
                    double dist = cdm.Distance(vdm, clusi, clusj);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        mini = i;
                        minj = j;
                    }
                }
            }

            if (mini == -1 || minj == -1)
                break;

            ICluster<TRowKey>? clusmini = clusters[mini];
            ICluster<TRowKey>? clusminj = clusters[minj];

            Debug.Assert(clusmini != null);
            Debug.Assert(clusminj != null);

            ClusteringNode cn = new()
            {
                ClusterIdx1 = mini,
                ClusterIdx2 = minj,
                Observations1 = clusmini.Length,
                Observations2 = clusminj.Length,
                Depth = Math.Max(depths[mini], depths[minj]) + 1,
                Distance = minDist,
            };

            clusmini.Add(clusminj);
            clusters.Add(clusmini);
            clusminj.Clear();
            clusters[mini] = clusters[minj] = null;

            depths.Add(cn.Depth);
            nodes.Add(cn);
        }

        Nodes = nodes.ToArray();
        NodeCount = nodes.Count;
    }

    private readonly IEnumerable<IVector<TRowKey, TColumnKey>> _vectors;

    public IReadOnlyList<ClusteringNode> Nodes { get; }
    public ClusteringNode this[int index] => Nodes[index];

    public int VectorCount { get; }
    public int NodeCount { get; }
    public int BadVectorCount { get; }
    public int Depth => Nodes[Nodes.Count - 1].Depth;

    public string MetricName { get; }
    public string ClusterMethod { get; }

    public int[] GetSortedIndex(bool leftAlignedBadVectors)
    {
        int[] result = Enumerable.Repeat(-1, VectorCount).ToArray();
        Queue<(int idx, int pos)> q = new();
        q.Enqueue((Nodes.Count - 1, leftAlignedBadVectors ? BadVectorCount : 0));

        while (q.Count > 0)
        {
            (int idx, int pos) = q.Dequeue();
            if (Nodes[idx].Observations1 == 1)
                result[Nodes[idx].ClusterIdx1] = pos;
            else
                q.Enqueue((Nodes[idx].ClusterIdx1 - VectorCount, pos));
            if (Nodes[idx].Observations2 == 1)
                result[Nodes[idx].ClusterIdx2] = pos + Nodes[idx].Observations1;
            else
                q.Enqueue((Nodes[idx].ClusterIdx2 - VectorCount, pos + Nodes[idx].Observations1));
        }

        for (int i = 0, p = leftAlignedBadVectors ? 0 : result.Length - BadVectorCount; i < VectorCount; i++)
            if (result[i] == -1)
                result[i] = p++;

        return result;
    }

    public static IReadOnlyList<string> CsvColumnHeaders { get; }
        = ["Group ID", "Left", "Right", "Observ 1", "Observ 2", "Depth", "Distance"];

    public void WriteToCsvFile(string fileName)
    {
        List<object?[]> list = [];

        int id = 0;
        foreach (IVector<TRowKey, TColumnKey> vec in _vectors)
        {
            list.Add([id++, vec.Name, null, 1, null, 0, null]);
        }

        foreach (ClusteringNode node in Nodes)
        {
            list.Add(
            [
                id++,
                node.ClusterIdx1,
                node.ClusterIdx2,
                node.Observations1,
                node.Observations2,
                node.Depth,
                node.Distance,
            ]);
        }

        File.WriteAllLines(fileName, list.FormatCsvRows(CsvColumnHeaders));
    }
}
