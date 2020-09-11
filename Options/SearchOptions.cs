using System.IO;

namespace mccsx
{
    internal class SearchOptions : IOptions
    {
        public DirectoryInfo? Library { get; set; }
        public DirectoryInfo? Target { get; set; }
        public DirectoryInfo? Out { get; set; }
        public int Count { get; set; }
        public Measure Measure { get; set; }

        public bool IsValid => Library != null && Library.Exists && Target != null && Target.Exists && (Out == null || Out.Exists);
    }
}
