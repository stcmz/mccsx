using System;

namespace mccsx.Statistics;

public class FarthestPointMethod : IClusterDistanceMeasure
{
    public string Name => "Farthest";

    public double Distance<TKey>(IVectorDistanceMeasure vdm, ICluster<TKey> cluster1, ICluster<TKey> cluster2)
        where TKey : notnull
    {
        double dist = 0;
        for (int i = 0; i < cluster1.Length; i++)
        {
            for (int j = 0; j < cluster2.Length; j++)
            {
                dist = Math.Max(dist, vdm.Measure(cluster1[i], cluster2[j]));
            }
        }
        return dist;
    }
}
