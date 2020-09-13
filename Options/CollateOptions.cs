using System.IO;

namespace mccsx
{
    internal class CollateOptions
    {
        public DirectoryInfo? Library { get; set; }
        public DirectoryInfo? Out { get; set; }
        public Measure Measure { get; set; }
        public Measure IvMeasure { get; set; }
        public Measure SmrowMeasure { get; set; }
        public Measure SmcolMeasure { get; set; }
        public Linkage IvLinkage { get; set; }
        public Linkage SmrowLinkage { get; set; }
        public Linkage SmcolLinkage { get; set; }
        public bool Vector { get; set; }
        public bool Matrix { get; set; }
        public bool Cluster { get; set; }
        public bool Heatmap { get; set; }
        public bool Workbook { get; set; }
        public string? Filter { get; set; }
    }
}
