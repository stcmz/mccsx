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
        protected const string InputVectorsClustersCsv = "clustered_inputvectors_{2}+{3}_{0}.csv";
        protected const string InputVectorsHeatmap = "inputvectors_{0}.png";
        protected const string InputVectorsClusteredHeatmap = "inputvectors_{2}+{3}_{0}.png";

        protected const string SimilarityMatrixCsv = "similaritymatrix_{0}.csv";
        protected const string SimilarityMatrixClustersCsv = "clustered_similaritymatrix_{1}_{4}+{5}_{0}.csv";
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

            // Setup residue filter
            Func<string, IndexFilter>? getIndexFilter = null;

            if (!string.IsNullOrEmpty(options.Filter))
            {
                string commandLine;
                if (options.Filter.Contains("{}"))
                    commandLine = options.Filter.Trim().Replace("{}", "{0}");
                else
                    commandLine = options.Filter.Trim() + " {0}";

                var (program, arguments) = commandLine.SplitCommandLine();

                getIndexFilter = inputName =>
                {
                    string.Format(program, inputName).RunCommand(string.Format(arguments, inputName), out string? stdout, out string? stderr);

                    string[] lines = stdout.Split("\r\n".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

                    return new IndexFilter(lines);
                };
            }

            // Setup state filter
            Func<StateFilter>? getStateFilter = null;

            if (!string.IsNullOrEmpty(options.States))
            {
                var (program, arguments) = options.States.SplitCommandLine();

                getStateFilter = () =>
                {
                    program.RunCommand(arguments, out string? stdout, out string? stderr);

                    string[] lines = stdout.Split("\r\n".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

                    return new StateFilter(lines);
                };
            }

            Parameters = new CollateParameters
            (
                options.Library,
                options.Out,
                new(options.Measure),
                new(options.IvMeasure, options.IvLinkage),
                new(options.SmrowMeasure, options.SmrowLinkage),
                new(options.SmcolMeasure, options.SmcolLinkage),
                options.Vector,
                options.Matrix,
                options.Cluster,
                options.Heatmap,
                options.Workbook,
                options.Top,
                getIndexFilter,
                getStateFilter
            );

            return 0;
        }

        public int Run()
        {
            // Must have been set in the Setup method
            Debug.Assert(Parameters != null);

            Parallel.ForEach(EnumAnnotationHelper<Category>.Enums, category =>
            {
                Console.WriteLine($"Collecting data in category {category}");

                // Load the state filter if specified
                var stateFilter = Parameters.GetStateFilter?.Invoke();

                // Collect vectors for the category
                var vecData = new List<RawRecvData>();

                foreach (var inputCsvFile in Parameters.LibraryDir.EnumerateFiles($"*_{category}.csv", SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        string inputName = inputCsvFile.Name[0..(category.ToString().Length + 5)];

                        // Load the index filter if specified
                        var indexFilter = Parameters.GetIndexFilter?.Invoke(inputName);

                        // Read and validate the state
                        string? state = null;
                        if (stateFilter != null)
                        {
                            state = stateFilter.GetState(inputName);

                            // Verify completeness of states
                            if (state == null)
                                Console.Error.WriteLine($"WARN: no state found for {inputName}");
                        }

                        vecData.Add(RawRecvData.FromCsvFile(inputCsvFile.FullName, inputName, state, indexFilter));
                    }
                    catch (RawRecvDataFormatException ex)
                    {
                        Console.Error.WriteLine($"ERROR: {ex.Message} in {ex.FileName}");
                    }
                    catch (FilterColumnException ex)
                    {
                        Console.Error.WriteLine($"ERROR: failed in calling {ex.FilterName} filter: {ex.Message}");
                    }
                }

                // Without the generation of input vectors, similarity matrices, or Excel workbook
                if (!Parameters.InputVectorsEnabled && !Parameters.SimilarityMatricesEnabled && !Parameters.WorkbookEnabled)
                {
                    return;
                }

                Console.WriteLine($"Running input vectors in category {category}");

                // Create the input vectors from the vector data and filters
                var inputVectors = InputVectors.FromRawRecvData(vecData, stateFilter?.StateName);

                foreach (string msg in inputVectors.ErrorMessages)
                {
                    Console.Error.WriteLine($"WARN: {msg}");
                }
                inputVectors.ErrorMessages.Clear();

                // Create the output directory if not exist
                if (!Parameters.OutputDir.Exists)
                    Parameters.OutputDir.Create();

                if (Parameters.InputVectorsEnabled)
                {
                    OutputInputVectors(category, inputVectors);
                }

                SimilarityMatrix? similarityMatrix = null;

                if (Parameters.SimilarityMatricesEnabled || Parameters.WorkbookEnabled)
                {
                    Console.WriteLine($"Computing similarity matrices in category {category}");

                    // Generate the similarity matrix
                    similarityMatrix = SimilarityMatrix.FromInputVectors(inputVectors, Parameters.MatrixSimilarity.Measure);

                    if (Parameters.SimilarityMatricesEnabled)
                        OutputSimilarityMatrix(category, similarityMatrix);
                }

                if (Parameters.WorkbookEnabled)
                {
                    string xlsxFileName = GetFileName(SummaryWorkbook, category);

                    if (File.Exists(xlsxFileName))
                    {
                        Console.Error.WriteLine($"WARN: skipped existing workbook {xlsxFileName}");
                    }
                    else
                    {
                        Console.WriteLine($"Writing workbook in category {category}");

                        // Write out to an xlsx document.
                        using var doc = xlsxFileName.OpenXlsxFile();
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

                            doc.AppendWorksheet($"{Parameters.MatrixSimilarity.Measure.Name} Similarity Matrix", dataRows, headerRows)
                                .FreezePanel(sheetIndex, (uint)(rect.Left - 1), (uint)(rect.Top - 1))
                                .AddColorScales(sheetIndex, (uint)rect.Left, (uint)rect.Width, (uint)rect.Top, (uint)rect.Height, ScoreColorScalesExcel);

                            sheetIndex++;
                        }

                        if (true)
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
            });

            return 0;
        }

        private void OutputInputVectors(Category category, InputVectors inputVectors)
        {
            Debug.Assert(Parameters != null);

            Console.WriteLine($"Writing input vectors in category {category}");

            // Output the input vectors to a csv file
            string ivCsvFileName = GetFileName(InputVectorsCsv, category);

            inputVectors.GetFormattedReport(out var headerRows, out var dataRows);
            File.WriteAllLines(ivCsvFileName, dataRows.FormatCsvRows(headerRows));

            ClusteringInfo<string, string>? clusteringInfo = null;

            if (Parameters.ClusteringEnabled)
            {
                if (inputVectors.ColumnCount < MinClusteringVectors || inputVectors.Columns.Count(o => !o.IsZero && !o.IsNaN) < MinClusteringVectors)
                {
                    Console.Error.WriteLine($"WARN: too few data to cluster input vectors in category {category}");
                }
                else if (inputVectors.ColumnCount > MaxClusteringVectors)
                {
                    Console.Error.WriteLine($"WARN: too many data to cluster input vectors in category {category}");
                }
                else
                {
                    Console.WriteLine($"Clustering input vectors in category {category}");

                    var clus = Parameters.InputVectorClustering;

                    // Cluster input vectors
                    var distMeasure = new CachedVectorDistanceMeasure(clus.DistanceMeasure);
                    clusteringInfo = new ClusteringInfo<string, string>(clus.LinkageAlgorithm, distMeasure, inputVectors.Columns);

                    // Output the clustering information to a csv file
                    string ivcCsvFileName = GetFileName(InputVectorsClustersCsv, category);
                    clusteringInfo.WriteToCsvFile(ivcCsvFileName);
                }
            }

            if (Parameters.HeatmapEnabled)
            {
                if (inputVectors.ColumnCount < MinPlottingVectors)
                {
                    Console.Error.WriteLine($"WARN: too few data to plot input vectors in category {category}");
                }
                else if (inputVectors.ColumnCount > MaxPlottingVectors)
                {
                    Console.Error.WriteLine($"WARN: too many data to plot input vectors in category {category}");
                }
                else
                {
                    string ivPngFileName = GetFileName(clusteringInfo != null ? InputVectorsClusteredHeatmap : InputVectorsHeatmap, category);

                    if (File.Exists(ivPngFileName))
                    {
                        Console.Error.WriteLine($"WARN: skipped existing heatmap {ivPngFileName}");
                    }
                    else
                    {
                        // TODO:
                        // The length of a row vector is defined by the number of input vectors,
                        // so the average is simply the sum over the row length.
                        inputVectors.OrderRowsBy(o => o.Values.Sum() / o.Length);

                        Debug.Assert(clusteringInfo == null || clusteringInfo.Nodes.Count >= MinClusteringVectors - 1);

                        Console.WriteLine($"Plotting heatmap for input vectors in category {category}");

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

            Console.WriteLine($"Writing similarity matrix in category {category}");

            // Output the similarity matrix to a csv file
            string smCsvFileName = GetFileName(SimilarityMatrixCsv, category);

            similarityMatrix.GetFormattedReport(out var headerRows, out var dataRows);
            File.WriteAllLines(smCsvFileName, dataRows.FormatCsvRows(headerRows));

            ClusteringInfo<string, string>? clusInfoRow = null, clusInfoCol = null;

            if (Parameters.ClusteringEnabled)
            {
                if (similarityMatrix.ColumnCount < MinClusteringVectors || similarityMatrix.Columns.Count(o => !o.IsZero && !o.IsNaN) < MinClusteringVectors)
                {
                    Console.Error.WriteLine($"WARN: too few data to cluster similarity matrix in category {category}");
                }
                else if (similarityMatrix.ColumnCount > MaxClusteringVectors)
                {
                    Console.Error.WriteLine($"WARN: too many data to cluster similarity matrix in category {category}");
                }
                else
                {
                    Console.WriteLine($"Clustering similarity matrix in category {category}");

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
                    clusInfoRow.WriteToCsvFile(csmCsvFileName);

                    if (clusInfoRow != clusInfoCol)
                    {
                        csmCsvFileName = GetFileName(SimilarityMatrixClustersCsv, category);
                        clusInfoCol.WriteToCsvFile(csmCsvFileName);
                    }
                }
            }

            if (Parameters.HeatmapEnabled)
            {
                if (similarityMatrix.ColumnCount < MinPlottingVectors)
                {
                    Console.Error.WriteLine($"WARN: too few data to plot similarity matrix in category {category}");
                }
                else if (similarityMatrix.ColumnCount > MaxPlottingVectors)
                {
                    Console.Error.WriteLine($"WARN: too many data to plot similarity matrix in category {category}");
                }
                else
                {
                    Debug.Assert((clusInfoRow == null) == (clusInfoCol == null));

                    string smPngFileName = GetFileName(clusInfoRow != null ? SimilarityMatrixClusteredHeatmap : SimilarityMatrixHeatmap, category);

                    if (File.Exists(smPngFileName))
                    {
                        Console.Error.WriteLine($"WARN: skipped existing heatmap {smPngFileName}");
                    }
                    else
                    {
                        // TODO: Ordering of row/column vectors

                        Debug.Assert(clusInfoRow == null || clusInfoRow.Nodes.Count >= MinClusteringVectors - 1);

                        Console.WriteLine($"Plotting heatmap for similarity matrix in category {category}");

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

        private void GetHueColorScheme(
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
