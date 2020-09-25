using System;
using System.IO;

namespace mccsx
{
    internal record CollateParameters
    (
        DirectoryInfo LibraryDir,
        DirectoryInfo OutputDir,
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
        Func<string, IndexFilter>? GetIndexFilter,
        Func<StateFilter>? GetStateFilter
    );
}
