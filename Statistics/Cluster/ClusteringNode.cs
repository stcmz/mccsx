namespace mccsx.Statistics
{
    public class ClusteringNode
    {
        public int ClusterIdx1;
        public int ClusterIdx2;
        public int Observations1;
        public int Observations2;
        public int Observations => Observations1 + Observations2;
        public int Depth;
        public double Distance;
    }
}
