using mccsx.Helpers;
using mccsx.Statistics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace mccsx
{
    internal class SearchAction : IAction<SearchOptions>
    {
        public SearchParameters? Parameters { get; private set; }

        public int Setup(SearchOptions options)
        {
            // Must have been checked in the command line pipeline
            Debug.Assert(options.Library != null);
            Debug.Assert(options.Pattern != null);
            Debug.Assert(options.Library.Exists);
            Debug.Assert(options.Pattern.Exists);

            // Default output directory to work directory if not set
            if (options.Out == null)
                options.Out = new DirectoryInfo(Environment.CurrentDirectory);

            // Try to find the pattern name in the pattern directory
            string? patternName = GetPatternName(options.Pattern);

            if (patternName == null)
            {
                Logger.Error($"Cannot find pattern name in {options.Pattern.FullName}");
                return 1;
            }

            Logger.Info($"Found pattern name {patternName}");

            // Try to find the pattern CSV files in the pattern directory
            var patternCsvs = new Dictionary<Category, FileInfo>();
            var errorList = new List<Category>();

            var categories = options.Categories?
                .Select(o => Enum.Parse<Category>(o))
                .Distinct()
                .ToArray() ?? EnumAnnotationHelper<Category>.Enums;

            foreach (var category in categories)
            {
                var fi = new FileInfo(Path.Combine(options.Pattern.FullName, $"{patternName}_{category}.csv"));
                if (!fi.Exists)
                {
                    errorList.Add(category);
                    continue;
                }
                patternCsvs.Add(category, fi);
            }

            if (patternCsvs.Count == 0)
            {
                Logger.Error($"No pattern found in {options.Pattern.FullName}");
                return 2;
            }

            if (errorList.Count > 0)
                Logger.Warning($"Some patterns are missing: {string.Join(", ", errorList)}");

            // Finally, set the model for running this action
            Parameters = new SearchParameters(
                options.Library,
                options.Out,
                options.Count,
                new(options.Measure),
                patternName,
                options.Recursive,
                options.Naming,
                patternCsvs
            );

            return 0;
        }

        private record Result(string InputCsvFile, string InputName, string ConfName, double Similarity);

        public int Run()
        {
            // Must have been set in the Setup method
            Debug.Assert(Parameters != null);

            // Print out key parameters
            Logger.Info($"Using {Parameters.Similarity.Type} similarity measure");

            Parallel.ForEach(Parameters.PatternCsvs, o =>
            {
                var (category, patternFile) = o;
                Logger.Info($"Searching in category {category}");

                // Load the pattern vector for matching
                var patternResDict = File.ReadAllLines(patternFile.FullName)
                    .SkipLast(5) // Skip the 5 trailing summary lines
                    .ParseCsvRows(2, 3) // "Residue sequence" column and the first conformation
                    .ToDictionary(o => o[0], o => double.TryParse(o[1], out double val) ? val : 0.0);

                var patternVec = new MapVector<string>(patternResDict, Parameters.PatternName);
                var results = new List<Result>();

                int count = 0;
                var option = Parameters.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

                foreach (var candidateFile in Parameters.LibraryDir.EnumerateFiles($"*_{category}.csv", option))
                {
                    // Read and parse the csv file
                    string[] csvLines = File.ReadAllLines(candidateFile.FullName);

                    string[] confNames = csvLines[0]
                        .SplitCsvFields()
                        .Skip(3) // Skip the Chain ID, Residue name, Residue sequence columns
                        .ToArray();

                    string[][] data = csvLines.Skip(1)
                        .SkipLast(5) // Skip the 5 trailing summary lines
                        .Select(o => o.SplitCsvFields().Skip(2).ToArray())
                        .ToArray();

                    string[] resSeq = data.Select(o => o[0]).ToArray();

                    // Load vectors for all conformations
                    var vecs = Enumerable.Range(0, confNames.Length)
                        .Select // Column vectors
                        (
                            i => new MapVector<string>
                            (
                                Enumerable.Range(0, resSeq.Length)
                                    .ToDictionary(
                                        j => resSeq[j], // Row key
                                        j => double.TryParse(data[j][i + 1], out double val) ? val : 0.0 // Score value
                                    ),
                                confNames[i] // Column key
                            )
                        ).ToArray();

                    // Sort to find the best conformation
                    var best = vecs
                        .Select(o => new { ConfName = o.Name, Similarity = Parameters.Similarity.Measure.Measure(o, patternVec) })
                        .OrderByDescending(o => o.Similarity)
                        .First();

                    string inputName = GetInputName(candidateFile.FullName, 1 + category.ToString().Length);

                    results.Add(new(candidateFile.FullName, inputName, best.ConfName, best.Similarity));
                    count++;
                }

                Logger.Info($"Generating top {Parameters.ResultCount} matches out of {count} {category} vectors");

                // Prepare the category specific directory for storing output
                string categoryDir = Path.Combine(Parameters.OutputDir.FullName, category.ToString());
                Directory.CreateDirectory(categoryDir);

                results = results.OrderByDescending(o => o.Similarity).ToList();

                var bestMatches = results.Take(Parameters.ResultCount);

                // Output top N best matches in separate directories
                int rank = 1;
                foreach (var (inputCsvFile, inputName, confName, similarity) in bestMatches)
                {
                    // Extract the conformation id
                    int confId = int.Parse(Regex.Match(confName, @"(\d+)").Groups[1].Value);

                    string secureLigandName = inputName.Replace('/', '_').Replace('\\', '_');

                    string outputDir = Path.Combine(categoryDir, $"D{rank++}C{confId}_{secureLigandName}");
                    Directory.CreateDirectory(outputDir);

                    // Copy the best matched conformation to the output directory
                    string inputPdbqtFile = $"{inputCsvFile[..^(1 + category.ToString().Length + 4)]}.pdbqt";
                    string outputPdbqtFile = Path.Combine(outputDir, $"{Parameters.PatternName}.pdbqt");
                    CopyBestConformation(inputPdbqtFile, outputPdbqtFile, confId);

                    // Copy the best matched vector to the output directory
                    string outputCsvFile = Path.Combine(outputDir, $"{Parameters.PatternName}_{category}.csv");

                    string[] headers = new[] { "Chain ID", "Residue name", "Residue sequence", confName };
                    string[][]? csvContent = File.ReadAllLines(inputCsvFile)
                        .ParseCsvRows(headers)
                        .ToArray();
                    File.WriteAllLines(outputCsvFile, csvContent.FormatCsvRows(headers));
                }

                // Output summarized search report in CSV
                string outputReportFile = Path.Combine(Parameters.OutputDir.FullName, $"searchreport_{category}.csv");
                File.WriteAllLines(outputReportFile, results
                    .Select(o => new[] { o.InputName, o.ConfName, o.Similarity.ToString() })
                    .FormatCsvRows(new[] { "Drug", "Best Conf", $"{Parameters.Similarity.Measure.Name} Similarity" }));
            });

            return 0;
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

        private static string? GetPatternName(DirectoryInfo di)
        {
            foreach (var fi in di.EnumerateFiles("*.pdbqt", SearchOption.TopDirectoryOnly))
                return fi.Name[..^6];
            return null;
        }

        private static void CopyBestConformation(string inputPdbqt, string outputPdbqt, int confId)
        {
            string text = File.ReadAllText(inputPdbqt);

            int startIndex = text.IndexOf($"MODEL {confId,8}");
            if (startIndex == -1)
            {
                Logger.Error($"Failed to find conformation {confId} in {inputPdbqt}");
                return;
            }

            int endIndex = text.IndexOf($"MODEL {confId + 1,8}", startIndex);
            if (endIndex == -1)
            {
                endIndex = text.Length;
            }

            File.WriteAllText(outputPdbqt, text[startIndex..endIndex]);
        }
    }
}
