namespace mccsx.Statistics;

/// <summary>
/// https://docs.scipy.org/doc/scipy/reference/generated/scipy.cluster.hierarchy.linkage.html#scipy.cluster.hierarchy.linkage
/// </summary>
public interface IClusterDistanceMeasure
{
    string Name { get; }
    double Distance<TKey>(IVectorDistanceMeasure vdm, ICluster<TKey> cluster1, ICluster<TKey> cluster2) where TKey : notnull;
}
