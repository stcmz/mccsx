using System.IO;

namespace mccsx
{
    internal class SearchOptions
    {
        public DirectoryInfo? Library { get; set; }
        public DirectoryInfo? Pattern { get; set; }
        public DirectoryInfo? Out { get; set; }
        public int Count { get; set; }
        public bool Recursive { get; set; }
        public NamingScheme Naming { get; set; }
        public Measure Measure { get; set; }
        public string[]? Categories { get; set; }
    }
}
