using System;

namespace mccsx.Statistics;

public class NearestPointMethod : IClusterDistanceMeasure
{
    public string Name => "Nearest";

    public double Distance<TKey>(IVectorDistanceMeasure vdm, ICluster<TKey> cluster1, ICluster<TKey> cluster2)
        where TKey : notnull
    {
        double dist = double.PositiveInfinity;
        for (int i = 0; i < cluster1.Length; i++)
        {
            for (int j = 0; j < cluster2.Length; j++)
            {
                dist = Math.Min(dist, vdm.Measure(cluster1[i], cluster2[j]));
            }
        }
        return dist;
    }
}
