using mccsx.Extensions;
using mccsx.Helpers;
using mccsx.Models;
using mccsx.Statistics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace mccsx
{
    internal class CollateAction : IAction<CollateOptions>
    {
        public CollateParameters? Parameters { get; private set; }

        // File name templates for output
        protected const string InputVectorsCsv = "inputvectors_{0}.csv";
        protected const string InputVectorsClustersCsv = "clustered_inputvectors_[{2}+{3}]_{0}.csv";
        protected const string InputVectorsHeatmap = "inputvectors_{0}.png";
        protected const string InputVectorsClusteredHeatmap = "inputvectors_[{2}+{3}]_{0}.png";

        protected const string SimilarityMatrixCsv = "similaritymatrix_{0}.csv";
        protected const string SimilarityMatrixClustersCsv = "clustered_similaritymatrix_{1}_[{4}+{5}]_{0}.csv";
        protected const string SimilarityMatrixHeatmap = "similaritymatrix_{1}_{0}.png";
        protected const string SimilarityMatrixClusteredHeatmap = "similaritymatrix_{1}_row[{4}+{5}]_col[{6}+{7}]_{0}.png";

        protected const string SummaryWorkbook = "summary_{1}_{0}.xlsx";

        // Limits on the number of vectors
        protected const int MinClusteringVectors = 2;
        protected const int MaxClusteringVectors = 256;

        protected const int MinPlottingVectors = 2;
        protected const int MaxPlottingVectors = 256;

        // Color scale for jdock scores and similarity scores in heatmap
        protected static readonly (double scale, Color color)[] ScoreColorScalesHeatmap = new[]
        {
            (-1,   ColorHelper.ToColor((1.0, 0.0, 0.0, 0.3))), // 00004D, very dark blue
            (-0.5, ColorHelper.ToColor((1.0, 0.0, 0.0, 1.0))), // 0000FF, blue
            ( 0,   ColorHelper.ToColor((1.0, 1.0, 1.0, 1.0))), // FFFFFF, white
            ( 0.5, ColorHelper.ToColor((1.0, 1.0, 0.0, 0.0))), // FF0000, red
            ( 1,   ColorHelper.ToColor((1.0, 0.5, 0.0, 0.0))), // 800000, maroon
        };

        // Color scale for scores and similarity in Excel conditional formatting
        protected static readonly (double scale, string color)[] ScoreColorScalesExcel = new[]
        {
            (-2, "FF0000FF" ),
            (.0, "FFFFFFFF" ),
            ( 2, "FFFF0000" ),
        };

        public int Setup(CollateOptions options)
        {
            Debug.Assert(options.Library != null);
            Debug.Assert(options.Library.Exists);

            // Default output directory to work directory if not set
            if (options.Out == null)
                options.Out = new DirectoryInfo(Environment.CurrentDirectory);

            // Setup categories
            var categories = options.Categories?
                .Select(o => Enum.Parse<Category>(o))
                .Distinct()
                .ToArray() ?? EnumAnnotationHelper<Category>.Enums;

            // Setup residue filter
            Func<string, IndexFilter>? getIndexFilter = null;

            if (!string.IsNullOrEmpty(options.Filter))
            {
                string commandLine = options.Filter.Trim().Replace("{}", "{0}");

                var (program, arguments) = commandLine.SplitCommandLine();

                getIndexFilter = inputName =>
                {
                    string.Format(program, inputName).RunCommand(string.Format(arguments, inputName), out string? stdout, out string? stderr);

                    if (!string.IsNullOrWhiteSpace(stderr))
                        throw new FilterException(stderr.Trim(), "index");

                    string[] lines = stdout.Split("\r\n".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

                    return new IndexFilter(lines);
                };
            }

            // Setup state filter
            Func<StateFilter>? getStateFilter = null;

            if (!string.IsNullOrEmpty(options.State_Filter))
            {
                var (program, arguments) = options.State_Filter.SplitCommandLine();

                getStateFilter = () =>
                {
                    program.RunCommand(arguments, out string? stdout, out string? stderr);

                    if (!string.IsNullOrWhiteSpace(stderr))
                        throw new FilterException(stderr.Trim(), "state");

                    string[] lines = stdout.Split("\r\n".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

                    return new StateFilter(lines);
                };
            }

            Parameters = new CollateParameters
            (
                options.Library,
                options.Out,
                categories,
                new(options.Measure),
                new(options.IV_Measure, options.IV_Linkage),
                new(options.SMRow_Measure, options.SMRow_Linkage),
                new(options.SMCol_Measure, options.SMCol_Linkage),
                options.Vector,
                options.Matrix,
                options.Cluster,
                options.Heatmap,
                options.Workbook,
                options.Top,
                options.Overwrite,
                options.Recursive,
                options.Naming,
                options.Sort_IV_Rows,
                getIndexFilter,
                getStateFilter
            );

            return 0;
        }

        public int Run()
        {
            // Must have been set in the Setup method
            Debug.Assert(Parameters != null);

            // Print out key parameters
            PrintKeyParameters();

            // Prefetch inputs
            var option = Parameters.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            string[] inputNames = Parameters.LibraryDir
                .EnumerateFiles($"*.pdbqt", option)
                .Select(o => GetInputName(o.FullName))
                .ToArray();

            Logger.Info($"Found {inputNames.Length} input in library");

            // Load the index filters and state filter if specified
            StateFilter? stateFilter = null;
            Dictionary<string, IndexFilter?>? indexFilters = null;

            try
            {
                stateFilter = Parameters.GetStateFilter?.Invoke();
                if (Parameters.GetIndexFilter != null)
                {
                    indexFilters = new Dictionary<string, IndexFilter?>();
                    foreach (string inputName in inputNames)
                    {
                        indexFilters[inputName] = Parameters.GetIndexFilter.Invoke(inputName);
                    }
                }
            }
            catch (FilterException ex)
            {
                Logger.Error($"Failed in calling {ex.FilterName} filter: {ex.Message}");
                return 3;
            }

            // Run the collation in a multi-threaded manner
            Parallel.ForEach(Parameters.Categories, category =>
            {
                Logger.Info($"Collecting data in category {category}");

                // Collect vectors for the category
                var vecData = new List<RawRecvData>();

                foreach (var inputCsvFile in Parameters.LibraryDir.EnumerateFiles($"*_{category}.csv", option))
                {
                    try
                    {
                        string inputName = GetInputName(inputCsvFile.FullName, 1 + category.ToString().Length);

                        // Read and validate the state
                        string? state = null;
                        IndexFilter? indexFilter = null;

                        if (stateFilter != null)
                        {
                            state = stateFilter.GetState(inputName);

                            // Verify completeness of states
                            if (state == null)
                                Logger.Warning($"No state found for {inputName}");
                        }

                        if (indexFilters != null)
                        {
                            if (!indexFilters.ContainsKey(inputName))
                            {
                                Logger.Error($"No index filter found for {inputName}");
                                return;
                            }
                            indexFilter = indexFilters[inputName];
                        }

                        vecData.Add(RawRecvData.FromCsvFile(inputCsvFile.FullName, inputName, state, indexFilter));
                    }
                    catch (IOException ex)
                    {
                        Logger.Error(ex.Message);
                        return;
                    }
                    catch (RawRecvDataFormatException ex)
                    {
                        Logger.Error($"{ex.Message} in {ex.FileName}");
                        return;
                    }
                }

                // Without the generation of input vectors, similarity matrices, or Excel workbook
                if (!Parameters.InputVectorsEnabled && !Parameters.SimilarityMatricesEnabled && !Parameters.WorkbookEnabled)
                {
                    return;
                }

                // Create the input vectors from the vector data and filters
                var inputVectors = InputVectors.FromRawRecvData(vecData, stateFilter?.StateName);

                foreach (string msg in inputVectors.ErrorMessages)
                {
                    Logger.Warning($"{msg}");
                }
                inputVectors.ErrorMessages.Clear();

                // Sort the rows of input vectors
                switch (Parameters.InputVectorRowsOrdering)
                {
                    case RowOrdering.score:
                        // The length of a row vector is defined by the number of input vectors,
                        // so the average is simply the sum over the row length.
                        inputVectors.OrderRowsBy(o => o.Values.Sum() / o.Length);
                        break;
                    case RowOrdering.sequence:
                        inputVectors.OrderRowsBy(o => inputVectors.ResidueInfo[o.Name].ResidueSeq);
                        break;
                    default:
                        break;
                }

                // Sort the columns of input vectors
                // The specific order will affect the column order in the heatmaps with clustering off.
                // It will NOT affect the clustering log because clustering runs on the raw vector list.
                inputVectors.OrderColumnsBy(o => o.Name);

                // Create the output directory if not exist
                if (!Parameters.OutputDir.Exists)
                    Parameters.OutputDir.Create();

                if (Parameters.InputVectorsEnabled)
                {
                    try
                    {
                        OutputInputVectors(category, inputVectors);
                    }
                    catch (IOException ex)
                    {
                        Logger.Error(ex.Message);
                        return;
                    }
                }

                SimilarityMatrix? similarityMatrix = null;

                if (Parameters.SimilarityMatricesEnabled || Parameters.WorkbookEnabled)
                {
                    Logger.Info($"Computing similarity matrices in category {category}");

                    // Generate the similarity matrix
                    similarityMatrix = SimilarityMatrix.FromInputVectors(inputVectors, Parameters.MatrixSimilarity.Measure);

                    // Sort the rows and columns in the same way, so that the matrix looks symmetric.
                    // When clustering is on and the same measure+linkage is used on rows and columns, clustering will be performed only once for both.
                    // In this case, inconsistent order in rows and columns will result in wrong plotting in the heatmap columns.
                    // The specific order will affect the row/column order in the heatmaps with clustering off.
                    // It will NOT affect the clustering log because clustering runs on the raw vector list.
                    similarityMatrix.OrderRowsBy(o => o.Name);
                    similarityMatrix.OrderColumnsBy(o => o.Name);

                    if (Parameters.SimilarityMatricesEnabled)
                    {
                        try
                        {
                            OutputSimilarityMatrix(category, similarityMatrix);
                        }
                        catch (IOException ex)
                        {
                            Logger.Error(ex.Message);
                            return;
                        }
                    }
                }

                if (Parameters.WorkbookEnabled)
                {
                    try
                    {
                        OutputWorkbook(category, vecData, inputVectors, similarityMatrix);
                    }
                    catch (IOException ex)
                    {
                        Logger.Error(ex.Message);
                        return;
                    }
                }
            });

            return 0;
        }

        private void PrintKeyParameters()
        {
            Debug.Assert(Parameters != null);

            string filters;
            if (Parameters.GetIndexFilter != null)
            {
                filters = (Parameters.GetStateFilter != null ? "index+state" : "index");
            }
            else
            {
                filters = (Parameters.GetStateFilter != null ? "state" : "none");
            }

            Logger.Info($"Using filters: {filters}");

            Logger.Info($"Output of input vectors: {(Parameters.InputVectorsEnabled ? "on" : "off")}");

            if (Parameters.InputVectorsEnabled)
            {
                Logger.Info($"Clustering for input vectors: {(Parameters.ClusteringEnabled ? "on" : "off")}");
                if (Parameters.ClusteringEnabled)
                    Logger.Info($"Clustering settings for input vectors: {Parameters.InputVectorClustering.DistanceType}+{Parameters.InputVectorClustering.LinkageType}");
                Logger.Info($"Heatmap for input vectors: {(Parameters.HeatmapEnabled ? "on" : "off")}");
            }

            Logger.Info($"Output of similarity matrices: {(Parameters.SimilarityMatricesEnabled ? "on" : "off")}");

            if (Parameters.SimilarityMatricesEnabled || Parameters.WorkbookEnabled)
            {
                Logger.Info($"Measure for similarity matrices: {Parameters.MatrixSimilarity.Type}");

                if (Parameters.SimilarityMatricesEnabled)
                {
                    Logger.Info($"Clustering for similarity matrices: {(Parameters.ClusteringEnabled ? "on" : "off")}");
                    if (Parameters.ClusteringEnabled)
                    {
                        Logger.Info($"Clustering settings for row vectors: {Parameters.MatrixRowVectorClustering.DistanceType}+{Parameters.MatrixRowVectorClustering.LinkageType}");
                        Logger.Info($"Clustering settings for column vectors: {Parameters.MatrixColumnVectorClustering.DistanceType}+{Parameters.MatrixColumnVectorClustering.LinkageType}");
                    }
                    Logger.Info($"Heatmap for similarity matrices: {(Parameters.HeatmapEnabled ? "on" : "off")}");
                }
            }

            Logger.Info($"Output of Excel workbooks: {(Parameters.WorkbookEnabled ? "on" : "off")}");
        }

        private void OutputInputVectors(Category category, InputVectors inputVectors)
        {
            Debug.Assert(Parameters != null);

            // Output the input vectors to a csv file
            string ivCsvFileName = GetFileName(InputVectorsCsv, category);

            if (!Parameters.Overwrite && File.Exists(ivCsvFileName))
            {
                Logger.Warning($"Skipped existing input vectors {ivCsvFileName}");
            }
            else
            {
                Logger.Info($"Writing input vectors in category {category}");

                inputVectors.GetFormattedReport(out var headerRows, out var dataRows);
                File.WriteAllLines(ivCsvFileName, dataRows.FormatCsvRows(headerRows));
            }

            ClusteringInfo<string, string>? clusteringInfo = null;

            if (Parameters.ClusteringEnabled)
            {
                if (inputVectors.ColumnCount < MinClusteringVectors || inputVectors.Columns.Count(o => !o.IsZero && !o.IsNaN) < MinClusteringVectors)
                {
                    Logger.Warning($"Too few data to cluster input vectors in category {category}");
                }
                else if (inputVectors.ColumnCount > MaxClusteringVectors)
                {
                    Logger.Warning($"Too many data to cluster input vectors in category {category}");
                }
                else
                {
                    Logger.Info($"Clustering input vectors in category {category}");

                    var clus = Parameters.InputVectorClustering;

                    // Cluster input vectors
                    var distMeasure = new CachedVectorDistanceMeasure(clus.DistanceMeasure);
                    clusteringInfo = new ClusteringInfo<string, string>(clus.LinkageAlgorithm, distMeasure, inputVectors.Columns);

                    // Output the clustering information to a csv file
                    string ivcCsvFileName = GetFileName(InputVectorsClustersCsv, category);

                    if (!Parameters.Overwrite && File.Exists(ivcCsvFileName))
                    {
                        Logger.Warning($"Skipped existing clustering log for input vectors {ivcCsvFileName}");
                    }
                    else
                    {
                        clusteringInfo.WriteToCsvFile(ivcCsvFileName);
                    }
                }
            }

            if (Parameters.HeatmapEnabled)
            {
                if (inputVectors.ColumnCount < MinPlottingVectors)
                {
                    Logger.Warning($"Too few data to plot input vectors in category {category}");
                }
                else if (inputVectors.ColumnCount > MaxPlottingVectors)
                {
                    Logger.Warning($"Too many data to plot input vectors in category {category}");
                }
                else
                {
                    string ivPngFileName = GetFileName(clusteringInfo != null ? InputVectorsClusteredHeatmap : InputVectorsHeatmap, category);

                    if (!Parameters.Overwrite && File.Exists(ivPngFileName))
                    {
                        Logger.Warning($"Skipped existing heatmap {ivPngFileName}");
                    }
                    else
                    {
                        Debug.Assert(clusteringInfo == null || clusteringInfo.Nodes.Count >= MinClusteringVectors - 1);

                        Logger.Info($"Plotting heatmap for input vectors in category {category}");

                        // Prepare the color scheme for column tags
                        (string tag, Color color)[]? colorLegend = null;
                        Func<string?, Color>? colorFn = null;

                        if (inputVectors.ColumnTagName != null)
                        {
                            Debug.Assert(inputVectors.Columns.All(o => o.Tag != null));

                            string[] columnTags = inputVectors.Columns.Select(o => o.Tag!).Distinct().OrderBy(o => o).ToArray();

                            GetHueColorScheme(columnTags, 0.3, 0.9, out colorLegend, out colorFn);
                        }

                        // Perform the plotting
                        ClusteringPlot.PlotHeatmap(
                            ivPngFileName,
                            inputVectors,
                            6000, // Canvas width
                            true, // Auto adjust canvas height
                            false, // Do not left align NaN or Zero vectors
                            false, // Vertical text goes upwards
                            ScoreColorScalesHeatmap, // Heatmap color scheme
                            colorLegend, // Tag color legend
                            Color.Black, // Line color
                            Color.Black, // Label color
                            null, // Row clustering info
                            clusteringInfo, // Column clustering info
                            null, // Row tag colors, null to hide the tags
                            colorFn // Column tag colors
                        );
                    }
                }
            }
        }

        private void OutputSimilarityMatrix(Category category, SimilarityMatrix similarityMatrix)
        {
            Debug.Assert(Parameters != null);

            string smCsvFileName = GetFileName(SimilarityMatrixCsv, category);

            if (!Parameters.Overwrite && File.Exists(smCsvFileName))
            {
                Logger.Warning($"Skipped existing similarity matrix {smCsvFileName}");
            }
            else
            {
                Logger.Info($"Writing similarity matrix in category {category}");

                // Output the similarity matrix to a csv file
                similarityMatrix.GetFormattedReport(out var headerRows, out var dataRows);
                File.WriteAllLines(smCsvFileName, dataRows.FormatCsvRows(headerRows));
            }

            ClusteringInfo<string, string>? clusInfoRow = null, clusInfoCol = null;

            if (Parameters.ClusteringEnabled)
            {
                if (similarityMatrix.ColumnCount < MinClusteringVectors || similarityMatrix.Columns.Count(o => !o.IsZero && !o.IsNaN) < MinClusteringVectors)
                {
                    Logger.Warning($"Too few data to cluster similarity matrix in category {category}");
                }
                else if (similarityMatrix.ColumnCount > MaxClusteringVectors)
                {
                    Logger.Warning($"Too many data to cluster similarity matrix in category {category}");
                }
                else
                {
                    Logger.Info($"Clustering similarity matrix in category {category}");

                    var clusRow = Parameters.MatrixRowVectorClustering;
                    var clusCol = Parameters.MatrixColumnVectorClustering;

                    // Create cached distance measure for clustering
                    IVectorDistanceMeasure distRow, distCol;
                    if (clusRow.DistanceType != clusCol.DistanceType)
                    {
                        distRow = new CachedVectorDistanceMeasure(clusRow.DistanceMeasure);
                        distCol = new CachedVectorDistanceMeasure(clusCol.DistanceMeasure);
                    }
                    else
                    {
                        Debug.Assert(Enumerable.SequenceEqual(similarityMatrix.RowKeys, similarityMatrix.ColumnKeys));
                        distRow = distCol = new CachedVectorDistanceMeasure(clusRow.DistanceMeasure);
                    }

                    // Cluster matrix row and column vectors
                    if (clusRow.LinkageType != clusCol.LinkageType || distRow != distCol)
                    {
                        clusInfoRow = new ClusteringInfo<string, string>(clusRow.LinkageAlgorithm, distRow, similarityMatrix.Rows);
                        clusInfoCol = new ClusteringInfo<string, string>(clusCol.LinkageAlgorithm, distCol, similarityMatrix.Columns);
                    }
                    else
                    {
                        clusInfoRow = clusInfoCol = new ClusteringInfo<string, string>(clusRow.LinkageAlgorithm, distRow, similarityMatrix.Rows);
                    }

                    // Output the clustering information to csv file(s)
                    string csmCsvFileName = GetFileName(SimilarityMatrixClustersCsv, category);
                    if (!Parameters.Overwrite && File.Exists(csmCsvFileName))
                    {
                        Logger.Warning($"Skipped existing clustering log for similarity matrix {csmCsvFileName}");
                    }
                    else
                    {
                        clusInfoRow.WriteToCsvFile(csmCsvFileName);
                    }

                    if (clusInfoRow != clusInfoCol)
                    {
                        csmCsvFileName = GetFileName(SimilarityMatrixClustersCsv, category);

                        if (!Parameters.Overwrite && File.Exists(csmCsvFileName))
                        {
                            Logger.Warning($"Skipped existing clustering log for similarity matrix {csmCsvFileName}");
                        }
                        else
                        {
                            clusInfoCol.WriteToCsvFile(csmCsvFileName);
                        }
                    }
                }
            }

            if (Parameters.HeatmapEnabled)
            {
                if (similarityMatrix.ColumnCount < MinPlottingVectors)
                {
                    Logger.Warning($"Too few data to plot similarity matrix in category {category}");
                }
                else if (similarityMatrix.ColumnCount > MaxPlottingVectors)
                {
                    Logger.Warning($"Too many data to plot similarity matrix in category {category}");
                }
                else
                {
                    Debug.Assert((clusInfoRow == null) == (clusInfoCol == null));

                    string smPngFileName = GetFileName(clusInfoRow != null ? SimilarityMatrixClusteredHeatmap : SimilarityMatrixHeatmap, category);

                    if (!Parameters.Overwrite && File.Exists(smPngFileName))
                    {
                        Logger.Warning($"Skipped existing heatmap {smPngFileName}");
                    }
                    else
                    {
                        Debug.Assert(clusInfoRow == null || clusInfoRow.Nodes.Count >= MinClusteringVectors - 1);

                        Logger.Info($"Plotting heatmap for similarity matrix in category {category}");

                        // Prepare the color scheme for column tags
                        (string tag, Color color)[]? colorLegend = null;
                        Func<string?, Color>? colorFn = null;

                        if (similarityMatrix.ColumnTagName != null)
                        {
                            Debug.Assert(similarityMatrix.Columns.All(o => o.Tag != null));
                            Debug.Assert(similarityMatrix.Rows.All(o => o.Tag != null));

                            string[] columnTags = similarityMatrix.Columns.Select(o => o.Tag!).Distinct().OrderBy(o => o).ToArray();

                            GetHueColorScheme(columnTags, 0.3, 0.9, out colorLegend, out colorFn);
                        }

                        // Perform the plotting
                        ClusteringPlot.PlotHeatmap(
                            smPngFileName,
                            similarityMatrix,
                            6000, // Canvas width
                            false, // Do not auto adjust canvas height, i.e. aspect ratio = 1:1
                            false, // Do not left align NaN or Zero vectors
                            false, // Vertical text goes upwards
                            ScoreColorScalesHeatmap, // Heatmap color scheme
                            colorLegend, // Tag color legend
                            Color.Black, // Line color
                            Color.Black, // Label color
                            clusInfoRow, // Row clustering info
                            clusInfoCol, // Column clustering info
                            colorFn, // Row tag colors
                            colorFn // Column tag colors
                        );
                    }
                }
            }
        }

        private void OutputWorkbook(Category category, List<RawRecvData> vecData, InputVectors? inputVectors, SimilarityMatrix? similarityMatrix)
        {
            Debug.Assert(Parameters != null);

            string xlsxFileName = GetFileName(SummaryWorkbook, category);

            if (!Parameters.Overwrite && File.Exists(xlsxFileName))
            {
                Logger.Warning($"Skipped existing workbook {xlsxFileName}");
            }
            else
            {
                Logger.Info($"Writing workbook in category {category}");

                // Write out to an xlsx document.
                using var doc = xlsxFileName.OpenXlsxFile(true);
                int sheetIndex = 0;

                if (inputVectors != null)
                {
                    var rect = inputVectors.GetFormattedReport(out var headerRows, out var dataRows);

                    doc.AppendWorksheet($"Input Vectors ({category})", dataRows, headerRows)
                        .FreezePanel(sheetIndex, (uint)(rect.Left - 1), (uint)(rect.Top - 1))
                        .AddColorScales(
                            sheetIndex,
                            (uint)rect.Left,
                            (uint)rect.Width,
                            (uint)rect.Top,
                            (uint)(rect.Height - RawRecvData.SummaryRowHeaders.Count - 1),
                            ScoreColorScalesExcel);

                    sheetIndex++;
                }

                if (similarityMatrix != null)
                {
                    var rect = similarityMatrix.GetFormattedReport(out var headerRows, out var dataRows);

                    doc.AppendWorksheet($"Similarity Matrix ({Parameters.MatrixSimilarity.Type})", dataRows, headerRows)
                        .FreezePanel(sheetIndex, (uint)(rect.Left - 1), (uint)(rect.Top - 1))
                        .AddColorScales(sheetIndex, (uint)rect.Left, (uint)rect.Width, (uint)rect.Top, (uint)rect.Height, ScoreColorScalesExcel);

                    sheetIndex++;
                }

                if (inputVectors != null)
                {
                    var rankings = new Rankings(vecData, inputVectors.StateName, inputVectors.IndexName, Parameters.TopN);

                    var rect = rankings.GetFormattedReport(out var dataRows);

                    doc.AppendWorksheet($"Top {Parameters.TopN} Ranking", dataRows)
                        .AddColorScales(sheetIndex, (uint)rect.Left, (uint)rect.Width, (uint)rect.Top, (uint)rect.Height, ScoreColorScalesExcel);

                    sheetIndex++;
                }

                doc.Close();
            }
        }

        private string GetFileName(string pattern, Category category)
        {
            Debug.Assert(Parameters != null);

            string fileName = string.Format
            (
                pattern,
                /*0*/ category,
                /*1*/ Parameters.MatrixSimilarity.Type,
                /*2*/ Parameters.InputVectorClustering.DistanceType,
                /*3*/ Parameters.InputVectorClustering.LinkageType,
                /*4*/ Parameters.MatrixRowVectorClustering.DistanceType,
                /*5*/ Parameters.MatrixRowVectorClustering.LinkageType,
                /*6*/ Parameters.MatrixColumnVectorClustering.DistanceType,
                /*7*/ Parameters.MatrixColumnVectorClustering.LinkageType
            );

            return Path.Combine(Parameters.OutputDir.FullName, fileName);
        }

        private string GetInputName(string inputPath, int tailLen = 0)
        {
            Debug.Assert(Parameters != null);

            string relativePath = Path.GetRelativePath(Parameters.LibraryDir.FullName, inputPath);
            int extLen = Path.GetExtension(relativePath).Length;
            relativePath = relativePath[..^(tailLen + extLen)];

            return Parameters.Naming switch
            {
                NamingScheme.dirpath => Path.GetDirectoryName(relativePath)!,
                NamingScheme.dirname => Path.GetFileName(Path.GetDirectoryName(relativePath))!,
                NamingScheme.filepath => relativePath,
                NamingScheme.filestem => Path.GetFileName(relativePath)!,
                _ => throw new NotSupportedException(),
            };
        }

        private static void GetHueColorScheme(
            IReadOnlyList<string> tags,
            double saturation,
            double value,
            out (string tag, Color color)[] colorLegend,
            out Func<string?, Color> colorFn)
        {
            colorLegend = Enumerable
                .Range(0, tags.Count)
                .Select(i => (tags[i], ColorHelper.FromHsv(1.0 / tags.Count * i, saturation, value)))
                .ToArray();

            var colorDict = colorLegend.ToDictionary(o => o.tag, o => o.color);
            colorFn = o => o != null ? colorDict.GetValueOrDefault(o, Color.Black) : Color.Black;
        }
    }
}
