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
        bool Recursive,
        NamingScheme Naming,
        Dictionary<Category, FileInfo> PatternCsvs
    );
}
