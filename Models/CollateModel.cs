using mccsx.Statistics;
using System.IO;

namespace mccsx
{
    internal record CollateModel
    (
        DirectoryInfo LibraryDir,
        DirectoryInfo OutputDir,
        int ResultCount,
        SimilarityModel MatrixSimilarity,
        ClusteringModel InputVectorClustering,
        ClusteringModel MatrixRowVectorClustering,
        ClusteringModel MatrixColumnVectorClustering,
        bool InputVectorsEnabled,
        bool SimilarityMatricesEnabled,
        bool ClusteringEnabled,
        bool HeatmapEnabled,
        bool WorkbookEnabled,
        string? FilterScript
    );
}
