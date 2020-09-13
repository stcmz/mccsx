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
        public SearchModel? Model { get; private set; }

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
                Console.Error.WriteLine($"ERROR: Cannot find pattern name in {options.Pattern.FullName}");
                return 1;
            }

            Console.WriteLine($"Found pattern name {patternName}");

            // Try to find the pattern CSV files in the pattern directory
            var patternCsvs = new Dictionary<Category, FileInfo>();
            var errorList = new List<Category>();

            foreach (var category in EnumAnnotationHelper<Category>.Enums)
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
                Console.Error.WriteLine($"ERROR: No pattern found in {options.Pattern.FullName}");
                return 2;
            }

            if (errorList.Count > 0)
                Console.Error.WriteLine($"WARN: Some patterns are missing: {string.Join(", ", errorList)}");

            // Finally, set the model for running this action
            Model = new SearchModel(
                options.Library,
                options.Out,
                options.Count,
                new(options.Measure, options.Measure.SimilarityMeasure()),
                patternName,
                patternCsvs
            );

            return 0;
        }

        private record Result(string Ligand, string ConfName, double Similarity);

        public int Run()
        {
            // Must have been set in the Setup method
            Debug.Assert(Model != null);

            Console.WriteLine($"Using {Model.Similarity.Type} similarity measure");

            Parallel.ForEach(Model.PatternCsvs, o =>
            {
                var (category, patternFile) = o;
                Console.WriteLine($"Searching in category {category}");

                // Load the pattern vector for matching
                var patternResDict = File.ReadAllLines(patternFile.FullName)
                    .SkipLast(5) // Skip the 5 trailing summary lines
                    .ParseCsvRows(2, 3) // "Residue sequence" column and the first conformation
                    .ToDictionary(o => o[0], o => double.TryParse(o[1], out double val) ? val : 0.0);

                var patternVec = new MapVector<string>(patternResDict, Model.PatternName);
                var results = new List<Result>();

                int count = 0;
                foreach (var candidateFile in Model.LibraryDir.EnumerateFiles($"*_{category}.csv", SearchOption.TopDirectoryOnly))
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
                        .Select // column vectors, no specific order required since they're being reordered while clustering
                        (
                            i => new MapVector<string>
                            (
                                Enumerable.Range(0, resSeq.Length)
                                    .ToDictionary(
                                        j => resSeq[j], // row key
                                        j => double.TryParse(data[j][i + 1], out double val) ? val : 0.0 // score value
                                    ),
                                confNames[i] // column key
                            )
                        ).ToArray();

                    // Sort to find the best conformation
                    var best = vecs
                        .Select(o => new { ConfName = o.Name, Similarity = Model.Similarity.Measure.Measure(o, patternVec) })
                        .OrderByDescending(o => o.Similarity)
                        .First();

                    string ligandName = candidateFile.Name[..^(1 + category.ToString().Length + 4)];

                    results.Add(new(ligandName, best.ConfName, best.Similarity));
                    count++;
                }

                Console.WriteLine($"Generating top {Model.ResultCount} matches out of {count} {category} vectors");

                // Prepare the category specific directory for storing output
                string categoryDir = Path.Combine(Model.OutputDir.FullName, category.ToString());
                Directory.CreateDirectory(categoryDir);

                results = results.OrderByDescending(o => o.Similarity).ToList();

                var bestMatches = results.Take(Model.ResultCount);

                // Output top N best matches in separate directories
                int rank = 1;
                foreach (var (ligand, confName, similarity) in bestMatches)
                {
                    // Extract the conformation id
                    int confId = int.Parse(Regex.Match(confName, @"(\d+)").Groups[1].Value);

                    string outputDir = Path.Combine(categoryDir, $"D{rank++}C{confId}_{ligand}");
                    Directory.CreateDirectory(outputDir);

                    // Copy the best matched conformation to the output directory
                    string inputPdbqtFile = Path.Combine(Model.LibraryDir.FullName, $"{ligand}.pdbqt");
                    string outputPdbqtFile = Path.Combine(outputDir, $"{Model.PatternName}.pdbqt");
                    CopyBestConformation(inputPdbqtFile, outputPdbqtFile, confId);

                    // Copy the best matched vector to the output directory
                    string inputCsvFile = Path.Combine(Model.LibraryDir.FullName, $"{ligand}_{category}.csv");
                    string outputCsvFile = Path.Combine(outputDir, $"{Model.PatternName}_{category}.csv");

                    string[] headers = new[] { "Chain ID", "Residue name", "Residue sequence", confName };
                    string[][]? csvContent = File.ReadAllLines(inputCsvFile)
                        .ParseCsvRows(headers)
                        .ToArray();
                    File.WriteAllLines(outputCsvFile, csvContent.FormatCsvRows(headers));
                }

                // Output summarized search report in CSV
                string outputReportFile = Path.Combine(Model.OutputDir.FullName, $"searchreport_{category}.csv");
                File.WriteAllLines(outputReportFile, results
                    .Select(o => new[] { o.Ligand, o.ConfName, o.Similarity.ToString() })
                    .FormatCsvRows(new[] { "Drug", "Best Conf", $"{Model.Similarity.Measure.Name} Similarity" }));
            });

            return 0;
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
                Console.Error.WriteLine($"ERROR: Failed to find conformation {confId} in {inputPdbqt}");
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
