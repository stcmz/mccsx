using System.Collections.Generic;
using System.IO;

namespace mccsx
{
    internal record SearchModel
    (
        DirectoryInfo LibraryDir,
        DirectoryInfo OutputDir,
        int ResultCount,
        SimilarityModel Similarity,
        string PatternName,
        Dictionary<Category, FileInfo> PatternCsvs
    );
}
