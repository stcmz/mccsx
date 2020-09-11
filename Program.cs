using mccsx;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.IO;

namespace System.CommandLine.Builder
{
    internal class Program
    {
        private static string? ValidateDirectory(CommandResult result, string alias, bool required)
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

        private static int Main(string[] args)
        {
            // See .NET command line API here: https://github.com/dotnet/command-line-api

            // Create the search sub-command with some options
            var searchCommand = new Command("search", "To perform similarity search for a target vector in a RECV (residue energy contribution vector) library and generate search reports")
            {
                new Option<DirectoryInfo>(
                    new[] { "-l", "--library" },
                    "(required) A directory containing RECVs (in jdock output layout) of the conformations of a ligand library to similarity search in"),
                new Option<DirectoryInfo>(
                    new[] { "-t", "--target" },
                    "(required) A directory containing RECVs (in jdock output layout) of the target conformation to similarity searched for"),
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
                    "The similarity measurement algorithm to be used in matching a candidate RECV in the library with a target RECV"),
            };

            // The parameters of the handler method are matched according to the names of the options
            searchCommand.Handler = CommandHandler.Create<SearchOptions>(options =>
            {
                if (options.IsValid)
                    return new SearchAction().Run(options);

                Console.Error.WriteLine("Invalid options for: mccsx search");

                return -1;
            });

            // Add a validator to the pipeline for validating directory options
            searchCommand.AddValidator(o =>
            {
                string? msg = ValidateDirectory(o, "--library", true);
                if (msg != null) return msg;

                msg = ValidateDirectory(o, "--target", true);
                if (msg != null) return msg;

                msg = ValidateDirectory(o, "--out", false);
                if (msg != null) return msg;

                return null;
            });

            // Create the collate sub-command with some options
            var collateCommand = new Command("collate", "To collect input vectors from a RECV (residue energy contribution vector) library and optionally generate similarity matrices, heatmaps and Excel workbooks")
            {
                new Option<DirectoryInfo>(
                    new[] { "-l", "--library" },
                    "(required) A directory containing RECVs (in jdock output layout) of the conformations of a ligand library to collect input vectors from"),
                new Option<DirectoryInfo>(
                    new[] { "-o", "--out" },
                    "A directory to store the collected input vectors and the computed outputs like similarity matrices [default: .]"),
                new Option<Measure>(
                    new[] { "-m", "--measure" },
                    () => Measure.cosine,
                    "The similarity measurement algorithm to be used in computing similarity matrices (required --matrix)"),
                new Option<Measure>(
                    new[] { "-M", "--iv_measure" },
                    () => Measure.cosine,
                    "A distance measurement algorithm to be used in clustering input vectors"),
                new Option<Measure>(
                    new[] { "-s", "--smrow_measure" },
                    () => Measure.cosine,
                    "A distance measurement algorithm to be used in clustering row vectors of a similarity matrix"),
                new Option<Measure>(
                    new[] { "-S", "--smcol_measure" },
                    () => Measure.cosine,
                    "A distance measurement algorithm to be used in clustering column vectors of a similarity matrix"),
                new Option<Linkage>(
                    new[] { "-L", "--iv_linkage" },
                    () => Linkage.farthest,
                    "A linkage algorithm to be used in clustering the input vectors"),
                new Option<Linkage>(
                    new[] { "-k", "--smrow_linkage" },
                    () => Linkage.farthest,
                    "A linkage algorithm to be used in clustering row vectors of a similarity matrix"),
                new Option<Linkage>(
                    new[] { "-K", "--smcol_linkage" },
                    () => Linkage.farthest,
                    "A linkage algorithm to be used in clustering column vectors of a similarity matrix"),
                new Option<bool>(
                    new[] { "-v", "--vector" },
                    "Enable the output of input vectors in CSV format which are collected from the RECV library"),
                new Option<bool>(
                    new[] { "-x", "--matrix" },
                    "Enable the output of similarity matrices in CSV format"),
                new Option<bool>(
                    new[] { "-c", "--cluster" },
                    "Enable clustering of input vectors and/or similarity matrices"),
                new Option<bool>(
                    new[] { "-H", "--heatmap" },
                    "Enable the generation of heatmaps for input vectors and/or similarity matrices"),
                new Option<bool>(
                    new[] { "-w", "--workbook" },
                    "Enable the generation of Excel workbooks for input vectors and/or similarity matrices"),
                new Option<string>(
                    new[] { "-f", "--filter" },
                    "A filter script to be applied to the residue names that is invoked with the prefix name of input conformation substituting the {} placeholder if present or appended to the script if otherwise (The script must return a table with two columns: the residue sequence and numbering)"),
            };

            // The parameters of the handler method are matched according to the names of the options
            collateCommand.Handler = CommandHandler.Create<CollateOptions>(options =>
            {
                if (options.IsValid)
                    return new CollateAction().Run(options);

                Console.Error.WriteLine("Invalid options for: mccsx collate");

                return -1;
            });

            // Add a validator to the pipeline for validating directory options
            collateCommand.AddValidator(o =>
            {
                string? msg = ValidateDirectory(o, "--library", true);
                if (msg != null) return msg;

                msg = ValidateDirectory(o, "--out", false);
                if (msg != null) return msg;

                return null;
            });

            // Create a root command with two sub-commands
            var rootCommand = new RootCommand("A cross-platform analysis tool for residue energy contribution vectors by ryan@imozo.cn")
            {
                searchCommand,
                collateCommand,
            };

            // Parse the incoming args and invoke the handler
            return rootCommand.InvokeAsync(args).Result;
        }
    }
}
