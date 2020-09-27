using mccsx.Helpers;
using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

// Workaround for the c# 9.0 preview feature (record)
// Will be removed upon .NET 5.0 release
namespace System.Runtime.CompilerServices
{
    public class IsExternalInit { }
}

namespace mccsx
{
    internal static class Program
    {
        private static string? ValidateDirectory(this CommandResult result, string alias, bool required)
        {
            var opt = result[alias];

            if (opt == null)
                return required ? $"Required option missing: {alias}" : null;

            // Next validator middleware in the pipeline will check for the missing argument
            if (opt.Tokens.Count == 0)
                return null;

            if (!opt.GetValueOrDefault<DirectoryInfo>()!.Exists)
                return $"Directory {result[alias]?.Tokens[0].Value} does not exist for option: {alias}";

            return null;
        }

        private static string? ValidateCommonArguments(this CommandResult result)
        {
            string? msg = result.ValidateDirectory("--library", true);
            if (msg != null) return msg;

            if (result["--categories"]?.Tokens.Count == 0)
                return null;

            var categories = result.ValueForOption<string[]>("--categories");

            if (categories != null)
            {
                var allCategories = EnumAnnotationHelper<Category>.Enums
                    .Select(o => o.ToString()).ToHashSet();

                foreach (var category in categories)
                {
                    if (!allCategories.Contains(category))
                        return $"Unrecognized category '{category}' for: --categories";
                }
            }

            return null;
        }
        private static void AddHandler<TAction, TOptions>(this Command command)
            where TAction : IAction<TOptions>, new()
        {
            // The parameters of the handler method are matched according to the names of the options
            command.Handler = CommandHandler.Create<TOptions>(options =>
            {
                var action = new TAction();

                int retCode = action.Setup(options);

                if (retCode != 0)
                    return retCode;

                var sw = Stopwatch.StartNew();
                retCode = action.Run();

                if (retCode == 0)
                    Logger.Info($"Total time used: {sw.Elapsed}");

                return retCode;
            });
        }

        private static async Task<int> Main(string[] args)
        {
            // See .NET command line API here: https://github.com/dotnet/command-line-api

            // Create the search sub-command with some options
            var searchCommand = new Command("search", "To perform similarity search for a pattern vector in a RECV (residue energy contribution vector) library and generate search reports")
            {
                new Option<DirectoryInfo>(
                    new[] { "-l", "--library" },
                    "(required) A directory containing RECVs (in jdock output layout) of the conformations of a ligand library to similarity search in"),
                new Option<DirectoryInfo>(
                    new[] { "-p", "--pattern" },
                    "(required) A directory containing RECVs (in jdock output layout) of the pattern conformation to similarity search for"),
                new Option<DirectoryInfo>(
                    new[] { "-o", "--out" },
                    "A directory to store the similarity report and the most similar conformations [default: .]"),
                new Option<int>(
                    new[] { "-n", "--count" },
                    () => 100,
                    "The number of the most similar conformations to be emitted into the output directory"),
                new Option<Measure>(
                    new[] { "-m", "--measure" },
                    () => Measure.cosine,
                    "The similarity measurement algorithm to be used in matching a candidate RECV in the library with a pattern RECV"),
            };

            // Add a validator to the pipeline for validating directory options
            searchCommand.AddValidator(cr =>
            {
                string? msg = cr.ValidateCommonArguments();
                if (msg != null) return msg;

                msg = cr.ValidateDirectory("--pattern", true);
                if (msg != null) return msg;

                return null;
            });

            searchCommand.AddHandler<SearchAction, SearchOptions>();

            // Create the collate sub-command with some options
            var collateCommand = new Command("collate", "To collect input vectors from a RECV (residue energy contribution vector) library and optionally generate similarity matrices, heatmaps and Excel workbooks")
            {
                new Option<DirectoryInfo>(
                    new[] { "-l", "--library" },
                    "(required) The directory containing RECVs (in jdock output layout) of the conformations of a ligand library to collect input vectors from"),
                new Option<DirectoryInfo>(
                    new[] { "-o", "--out" },
                    "The directory to store the collected input vectors and the computed outputs like similarity matrices [default: .]"),
                new Option<Measure>(
                    new[] { "-m", "--measure" },
                    () => Measure.cosine,
                    "The similarity measure to be used in computing similarity matrices (required --matrix)"),
                new Option<Measure>(
                    new[] { "-M", "--iv_measure" },
                    () => Measure.cosine,
                    "The distance measure to be used in clustering input vectors"),
                new Option<Measure>(
                    new[] { "-s", "--smrow_measure" },
                    () => Measure.cosine,
                    "The distance measure to be used in clustering row vectors of a similarity matrix"),
                new Option<Measure>(
                    new[] { "-S", "--smcol_measure" },
                    () => Measure.cosine,
                    "The distance measure to be used in clustering column vectors of a similarity matrix"),
                new Option<Linkage>(
                    new[] { "-L", "--iv_linkage" },
                    () => Linkage.farthest,
                    "The linkage algorithm to be used in clustering the input vectors"),
                new Option<Linkage>(
                    new[] { "-k", "--smrow_linkage" },
                    () => Linkage.farthest,
                    "The linkage algorithm to be used in clustering row vectors of a similarity matrix"),
                new Option<Linkage>(
                    new[] { "-K", "--smcol_linkage" },
                    () => Linkage.farthest,
                    "The linkage algorithm to be used in clustering column vectors of a similarity matrix"),
                new Option<bool>(
                    new[] { "-v", "--vector" },
                    "Enable the output of input vectors in CSV format which are collected from the RECV library"),
                new Option<bool>(
                    new[] { "-x", "--matrix" },
                    "Enable the output of similarity matrices in CSV format"),
                new Option<bool>(
                    new[] { "-c", "--cluster" },
                    "Enable clustering of input vectors and/or similarity matrices (requires --vector and/or --matrix)"),
                new Option<bool>(
                    new[] { "-H", "--heatmap" },
                    "Enable the generation of PNG heatmaps for input vectors and/or similarity matrices (requires --vector and/or --matrix)"),
                new Option<bool>(
                    new[] { "-w", "--workbook" },
                    "Enable the generation of Excel workbooks for input vectors, similarity matrices and residue rankings"),
                new Option<int>(
                    new[] { "-n", "--top" },
                    () => 20,
                    "The number of top residues to be emitted into the ranking reports (requires --workbook)"),
                new Option<bool>(
                    new[] { "-y", "--overwrite" },
                    "Force to overwriting all existing output files, the default behavior is to skip existing files"),
                new Option<RowOrdering>(
                    new[] { "--sort_iv_rows" },
                    () => RowOrdering.sequence,
                    "The sorting rule for the rows of input vectors in heatmaps"),
                new Option<string>(
                    new[] { "-f", "--filter" },
                    "The filter script to be applied to the residue sequences that is invoked with the prefix name of input conformation substituting the {} placeholder if present or appended to the script if otherwise (The script must return a headed table consisting of two columns: residue sequence, residue index)"),
                new Option<string>(
                    new[] { "-F", "--state_filter" },
                    "The filter script to be used in determining the state of the inputs (The script must return a headed table consisting of two columns: input name, input state)"),
            };

            collateCommand.AddHandler<CollateAction, CollateOptions>();

            // Add a validator to the pipeline for validating directory options
            collateCommand.AddValidator(cr =>
            {
                string? msg = cr.ValidateCommonArguments();
                if (msg != null) return msg;

                if (!cr.ValueForOption<bool>("--vector") &&
                    !cr.ValueForOption<bool>("--matrix"))
                {
                    if (cr.ValueForOption<bool>("--cluster"))
                        return "Clustering must be performed for: --vector, --matrix, or both";
                    if (cr.ValueForOption<bool>("--heatmap"))
                        return "Heatmaps must be generated for: --vector, --matrix, or both";
                }

                return null;
            });

            // Create a root command with two sub-commands
            var rootCommand = new RootCommand("A cross-platform analysis tool for residue energy contribution vectors by ryan@imozo.cn")
            {
                searchCommand,
                collateCommand,
            };

            rootCommand.AddGlobalOption(new Option<string[]>(
                new[] { "-C", "--categories" },
                $"The categories to run the computation with [default: {string.Join(" ", EnumAnnotationHelper<Category>.Enums)}]"));

            // Parse the incoming args and invoke the handler
            return await rootCommand.InvokeAsync(args);
        }
    }
}
