using System.IO;

namespace mccsx
{
    internal class CollateOptions
    {
        public DirectoryInfo? Library { get; set; }
        public DirectoryInfo? Out { get; set; }
        public Measure Measure { get; set; }
        public Measure IV_Measure { get; set; }
        public Measure SMRow_Measure { get; set; }
        public Measure SMCol_Measure { get; set; }
        public Linkage IV_Linkage { get; set; }
        public Linkage SMRow_Linkage { get; set; }
        public Linkage SMCol_Linkage { get; set; }
        public bool Vector { get; set; }
        public bool Matrix { get; set; }
        public bool Cluster { get; set; }
        public bool Heatmap { get; set; }
        public bool Workbook { get; set; }
        public int Top { get; set; }
        public bool Overwrite { get; set; }
        public RowOrdering Sort_IV_Rows { get; set; }
        public string[]? Categories { get; set; }
        public string? Filter { get; set; }
        public string? State_Filter { get; set; }
    }
}
