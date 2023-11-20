namespace mccsx.Statistics;

public class AverageDistanceMethod : IClusterDistanceMeasure
{
    public string Name => "Average";

    public double Distance<TKey>(IVectorDistanceMeasure vdm, ICluster<TKey> cluster1, ICluster<TKey> cluster2)
        where TKey : notnull
    {
        double sum = 0;
        for (int i = 0; i < cluster1.Length; i++)
        {
            for (int j = 0; j < cluster2.Length; j++)
            {
                sum += vdm.Measure(cluster1[i], cluster2[j]);
            }
        }
        return sum / cluster1.Length / cluster2.Length;
    }
}
