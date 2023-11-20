using mccsx.Helpers;
using mccsx.Statistics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace mccsx;

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
        options.Out ??= new DirectoryInfo(Environment.CurrentDirectory);

        // Try to find the pattern name in the pattern directory
        string? patternName = GetPatternName(options.Pattern);

        if (patternName == null)
        {
            Logger.Error($"Cannot find pattern name in {options.Pattern.FullName}");
            return 1;
        }

        Logger.Info($"Found pattern name {patternName}");

        // Try to find the pattern CSV files in the pattern directory
        Dictionary<Category, FileInfo> patternCsvs = [];
        List<Category> errorList = [];

        Category[] categories = options.Categories?
            .Select(o => Enum.Parse<Category>(o.ToLower()))
            .Distinct()
            .ToArray() ?? EnumAnnotationHelper<Category>.Enums;

        foreach (Category category in categories)
        {
            FileInfo fi = new(Path.Combine(options.Pattern.FullName, $"{patternName}_{category}.csv"));
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

    private record ConfSimilarity(string ConfName, double Similarity);
    private record SummaryResult(string InputCsvFile, string InputName, ConfSimilarity BestConfSim);
    private record DetailedResult(string InputCsvFile, string InputName, ConfSimilarity[] ConfSimilarities);

    public int Run()
    {
        // Must have been set in the Setup method
        Debug.Assert(Parameters != null);

        // Print out key parameters
        Logger.Info($"Using {Parameters.Similarity.Type} similarity measure");

        Parallel.ForEach(Parameters.PatternCsvs, o =>
        {
            (Category category, FileInfo patternFile) = o;
            Logger.Info($"Searching in category {category}");

            // Load the pattern vector for matching
            Dictionary<string, double> patternResDict = File.ReadAllLines(patternFile.FullName)
                .SkipLast(5) // Skip the 5 trailing summary lines
                .ParseCsvRows(2, 3) // "Residue sequence" column and the first conformation
                .ToDictionary(o => o[0], o => double.TryParse(o[1], out double val) ? val : 0.0);

            MapVector<string> patternVec = new(patternResDict, Parameters.PatternName);

            // Buffers to store overall summary results and top N detailed results
            Comparer<DetailedResult> comparer = Comparer<DetailedResult>.Create((a, b) => b.ConfSimilarities[0].Similarity.CompareTo(a.ConfSimilarities[0].Similarity));
            SortedSet<DetailedResult> topDetailedResults = new(comparer);
            List<SummaryResult> summaryResults = [];

            int count = 0;
            SearchOption option = Parameters.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

            foreach (FileInfo candidateFile in Parameters.LibraryDir.EnumerateFiles($"*_{category}.csv", option))
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
                MapVector<string>[] vecs = Enumerable.Range(0, confNames.Length)
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
                ConfSimilarity[] confSims =
                [
                    .. vecs
                                        .Select(o => new ConfSimilarity(o.Name, Parameters.Similarity.Measure.Measure(o, patternVec)))
                                        .OrderByDescending(o => o.Similarity)
,
                ];

                string inputName = GetInputName(candidateFile.FullName, 1 + category.ToString().Length);

                summaryResults.Add(new(candidateFile.FullName, inputName, confSims[0]));
                topDetailedResults.Add(new(candidateFile.FullName, inputName, confSims));

                if (topDetailedResults.Count > 0 && topDetailedResults.Count > Parameters.ResultCount)
                {
                    topDetailedResults.Remove(topDetailedResults.Max!);
                }

                count++;
            }

            Logger.Info($"Generating top {Parameters.ResultCount} matches out of {count} {category} vectors");

            // Prepare the category specific directory for storing output
            string categoryDir = Path.Combine(Parameters.OutputDir.FullName, category.ToString());
            Directory.CreateDirectory(categoryDir);

            summaryResults = [.. summaryResults.OrderByDescending(o => o.BestConfSim.Similarity)];

            // Output top N best matches in separate directories
            int rank = 1;
            foreach ((string inputCsvFile, string inputName, ConfSimilarity[] confSims) in topDetailedResults)
            {
                // Extract the conformation id
                int confId = int.Parse(Regex.Match(confSims[0].ConfName, @"(\d+)").Groups[1].Value);

                string secureLigandName = inputName.Replace('/', '_').Replace('\\', '_');

                string outputDir = Path.Combine(categoryDir, $"D{rank++}C{confId}_{secureLigandName}");
                Directory.CreateDirectory(outputDir);

                // Copy the best matched conformation to the output directory
                string inputPdbqtFile = $"{inputCsvFile[..^(1 + category.ToString().Length + 4)]}.pdbqt";
                string outputPdbqtFile = Path.Combine(outputDir, $"{Parameters.PatternName}.pdbqt");
                CopyBestConformation(inputPdbqtFile, outputPdbqtFile, confId);

                // Copy the best matched vector to the output directory
                string outputCsvFile = Path.Combine(outputDir, $"{Parameters.PatternName}_{category}.csv");
                string[] vecCsvHeaders = ["Chain ID", "Residue name", "Residue sequence", confSims[0].ConfName];
                string[][]? vecCsvContent = File.ReadAllLines(inputCsvFile)
                    .ParseCsvRows(vecCsvHeaders)
                    .ToArray();
                File.WriteAllLines(outputCsvFile, vecCsvContent.FormatCsvRows(vecCsvHeaders));

                // Write all conformation similarities to the output directory
                string confSimCsvFile = Path.Combine(outputDir, $"{Parameters.PatternName}_confsim_{category}.csv");
                string[] confSimCsvHeaders = ["Conf Name", "Similarity"];
                object[][] confSimCsvContent = confSims
                    .Select(o => new object[] { o.ConfName, o.Similarity })
                    .ToArray();
                File.WriteAllLines(confSimCsvFile, confSimCsvContent.FormatCsvRows(confSimCsvHeaders));
            }

            // Output summarized search report in CSV
            string outputReportFile = Path.Combine(Parameters.OutputDir.FullName, $"searchreport_{category}.csv");
            File.WriteAllLines(outputReportFile, summaryResults
                .Select(o => new[] { o.InputName, o.BestConfSim.ConfName, o.BestConfSim.Similarity.ToString() })
                .FormatCsvRows([ "Drug", "Best Conf", $"{Parameters.Similarity.Measure.Name} Similarity" ]));
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
        foreach (FileInfo fi in di.EnumerateFiles("*.pdbqt", SearchOption.TopDirectoryOnly))
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
