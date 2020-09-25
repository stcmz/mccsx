using System.Collections.Generic;
using System.IO;

namespace mccsx
{
    internal record SearchParameters
    (
        DirectoryInfo LibraryDir,
        DirectoryInfo OutputDir,
        int ResultCount,
        SimilarityParameters Similarity,
        string PatternName,
        Dictionary<Category, FileInfo> PatternCsvs
    );
}
