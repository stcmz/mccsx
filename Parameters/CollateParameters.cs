using System;
using System.IO;

namespace mccsx
{
    internal record CollateParameters
    (
        DirectoryInfo LibraryDir,
        DirectoryInfo OutputDir,
        Category[] Categories,
        SimilarityParameters MatrixSimilarity,
        ClusteringParameters InputVectorClustering,
        ClusteringParameters MatrixRowVectorClustering,
        ClusteringParameters MatrixColumnVectorClustering,
        bool InputVectorsEnabled,
        bool SimilarityMatricesEnabled,
        bool ClusteringEnabled,
        bool HeatmapEnabled,
        bool WorkbookEnabled,
        int TopN,
        bool Overwrite,
        bool Recursive,
        RowOrdering InputVectorRowsOrdering,
        Func<string, IndexFilter>? GetIndexFilter,
        Func<StateFilter>? GetStateFilter
    );
}
