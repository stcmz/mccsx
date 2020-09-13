using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace mccsx.Statistics
{
    public class ClusteringInfo<TRowKey, TColumnKey>
        where TRowKey : notnull
        where TColumnKey : notnull
    {
        public ClusteringInfo(IClusterDistanceMeasure cdm, IVectorDistanceMeasure vdm, IEnumerable<IVector<TRowKey, TColumnKey>> vectors)
        {
            var clusters = vectors.Select(o => (ICluster<TRowKey>?)o.Cluster()).ToList();

            BadVectorCount = clusters.Count(o => o![0].IsZero || o[0].IsNaN);
            VectorCount = clusters.Count;

            MetricName = vdm.Name;
            ClusterMethod = cdm.Name;

            var nodes = new List<ClusteringNode>();
            var depths = Enumerable.Repeat(0, VectorCount).ToList();

            while (true)
            {
                double minDist = double.PositiveInfinity;
                int mini = -1, minj = -1;

                for (int i = 0; i < clusters.Count; i++)
                {
                    var clusi = clusters[i];
                    if (clusi == null || clusi.Length == 1 && (clusi[0].IsZero || clusi[0].IsNaN))
                        continue;
                    for (int j = i + 1; j < clusters.Count; j++)
                    {
                        var clusj = clusters[j];
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

                var clusmini = clusters[mini];
                var clusminj = clusters[minj];

                Debug.Assert(clusmini != null);
                Debug.Assert(clusminj != null);

                var cn = new ClusteringNode
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

        public IReadOnlyList<ClusteringNode> Nodes { get; }
        public ClusteringNode this[int index] => Nodes[index];

        public int VectorCount { get; }
        public int NodeCount { get; }
        public int BadVectorCount { get; }
        public int Depth => Nodes.Last().Depth;

        public string MetricName { get; }
        public string ClusterMethod { get; }

        public int[] GetSortedIndex(bool leftAlignedBadVectors)
        {
            int[] result = Enumerable.Repeat(-1, VectorCount).ToArray();
            var q = new Queue<(int idx, int pos)>();
            q.Enqueue((Nodes.Count - 1, leftAlignedBadVectors ? BadVectorCount : 0));

            while (q.Count > 0)
            {
                var (idx, pos) = q.Dequeue();
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
    }
}
