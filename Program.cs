using mccsx.Helpers;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace mccsx;

internal static class Program
{
    private static OptionResult? GetOptionResult(this CommandResult result, string alias)
    {
        return result.Children.OfType<OptionResult>().FirstOrDefault(o => o.Option.HasAlias(alias));
    }

    private static bool BoolOption(this CommandResult result, string alias)
    {
        return result.GetOptionResult(alias)?.GetValueOrDefault<bool>() ?? false;
    }

    private static string? ValidateCategories(this CommandResult result)
    {
        OptionResult? opt = result.GetOptionResult("--categories");
        if (opt == null || opt.Tokens.Count == 0)
            return null;

        HashSet<string> allCategories = EnumAnnotationHelper<Category>.Enums
            .Select(o => o.ToString())
            .ToHashSet();

        List<string> errorTokens = [];
        foreach (Token token in opt.Tokens)
            if (!allCategories.Contains(token.Value.ToLower()))
                errorTokens.Add(token.Value);

        if (errorTokens.Count > 0)
            return $"Unrecognized token{(errorTokens.Count > 1 ? "s" : "")} '{string.Join("', '", errorTokens)}' for: --categories";

        return null;
    }

    private static void AddHandler<TAction, TOptions>(this Command command)
        where TAction : IAction<TOptions>, new()
    {
        // The parameters of the handler method are matched according to the names of the options
        command.Handler = CommandHandler.Create<TOptions>(options =>
        {
            TAction action = new();

            int retCode = action.Setup(options);

            if (retCode != 0)
                return retCode;

            Stopwatch sw = Stopwatch.StartNew();
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
        Command searchCommand = new("search", "To perform similarity search for a pattern vector in a RECV (residue energy contribution vector) library and generate search reports")
        {
            new Option<DirectoryInfo>(
                ["--library", "-l"],
                "A directory containing RECVs (in jdock output layout) of the conformations of a ligand library to similarity search in")
            {
                IsRequired = true,
            }.ExistingOnly(),
            new Option<DirectoryInfo>(
                ["--pattern", "-p"],
                "A directory containing RECVs (in jdock output layout) of the pattern conformation to similarity search for")
            {
                IsRequired = true,
            }.ExistingOnly(),
            new Option<DirectoryInfo>(
                ["--out", "-o"],
                "A directory to store the similarity report and the most similar conformations [default: .]"),
            new Option<int>(
                ["--count", "-n"],
                () => 100,
                "The number of the most similar conformations to be emitted into the output directory"),
            new Option<Measure>(
                ["--measure", "-m"],
                () => Measure.cosine,
                "The similarity measurement algorithm to be used in matching a candidate RECV in the library with a pattern RECV"),
            new Option<bool>(
                ["--recursive", "-r"],
                "Locate RECVs in the library subdirectories recursively"),
            new Option<NamingScheme>(
                ["--naming"],
                () => NamingScheme.filepath,
                "The naming scheme for the input vectors"),
        };

        // Add a validator to the pipeline for validating directory options
        searchCommand.AddValidator(cr =>
        {
            cr.ErrorMessage = cr.ValidateCategories();
        });

        searchCommand.AddHandler<SearchAction, SearchOptions>();

        // Create the collate sub-command with some options
        Command collateCommand = new("collate", "To collect input vectors from a RECV (residue energy contribution vector) library and optionally generate similarity matrices, heatmaps and Excel workbooks")
        {
            new Option<DirectoryInfo>(
                ["--library", "-l"],
                "The directory containing RECVs (in jdock output layout) of the conformations of a ligand library to collect input vectors from")
            {
                IsRequired = true,
            }.ExistingOnly(),
            new Option<DirectoryInfo>(
                ["--out", "-o"],
                "The directory to store the collected input vectors and the computed outputs like similarity matrices [default: .]"),
            new Option<Measure>(
                ["--measure", "-m"],
                () => Measure.cosine,
                "The similarity measure to be used in computing similarity matrices (required --matrix)"),
            new Option<Measure>(
                ["--iv_measure", "-M"],
                () => Measure.cosine,
                "The distance measure to be used in clustering input vectors"),
            new Option<Measure>(
                ["--smrow_measure", "-s"],
                () => Measure.cosine,
                "The distance measure to be used in clustering row vectors of a similarity matrix"),
            new Option<Measure>(
                ["--smcol_measure", "-S"],
                () => Measure.cosine,
                "The distance measure to be used in clustering column vectors of a similarity matrix"),
            new Option<Linkage>(
                ["--iv_linkage", "-L"],
                () => Linkage.farthest,
                "The linkage algorithm to be used in clustering the input vectors"),
            new Option<Linkage>(
                ["--smrow_linkage", "-k"],
                () => Linkage.farthest,
                "The linkage algorithm to be used in clustering row vectors of a similarity matrix"),
            new Option<Linkage>(
                ["--smcol_linkage", "-K"],
                () => Linkage.farthest,
                "The linkage algorithm to be used in clustering column vectors of a similarity matrix"),
            new Option<bool>(
                ["--vector", "-v"],
                "Enable the output of input vectors in CSV format which are collected from the RECV library"),
            new Option<bool>(
                ["--matrix", "-x"],
                "Enable the output of similarity matrices in CSV format"),
            new Option<bool>(
                ["--cluster", "-c"],
                "Enable clustering of input vectors and/or similarity matrices (requires --vector and/or --matrix)"),
            new Option<bool>(
                ["--heatmap", "-H"],
                "Enable the generation of PNG heatmaps for input vectors and/or similarity matrices (requires --vector and/or --matrix)"),
            new Option<bool>(
                ["--workbook", "-w"],
                "Enable the generation of Excel workbooks for input vectors, similarity matrices and residue rankings"),
            new Option<int>(
                ["--top", "-n"],
                () => 20,
                "The number of top residues to be emitted into the ranking reports (requires --workbook)"),
            new Option<bool>(
                ["--overwrite", "-y"],
                "Force to overwriting all existing output files, the default behavior is to skip existing files"),
            new Option<bool>(
                ["--recursive", "-r"],
                "Locate RECVs in the library subdirectories recursively"),
            new Option<NamingScheme>(
                ["--naming"],
                () => NamingScheme.filepath,
                "The naming scheme for the input vectors"),
            new Option<RowOrdering>(
                ["--sort_iv_rows"],
                () => RowOrdering.sequence,
                "The sorting rule for the rows of input vectors in heatmaps"),
            new Option<string>(
                ["--filter", "-f"],
                "The filter script to be applied to the residue sequences that is invoked with the prefix name of input conformation substituting the {} placeholder if present (The script must return a headed table consisting of two columns: residue sequence, residue index)"),
            new Option<string>(
                ["--state_filter", "-F"],
                "The filter script to be used in determining the state of the inputs (The script must return a headed table consisting of two columns: input name, input state)"),
        };

        collateCommand.AddHandler<CollateAction, CollateOptions>();

        // Add a validator to the pipeline for validating directory options
        collateCommand.AddValidator(cr =>
        {
            string? msg = cr.ValidateCategories();
            if (msg != null)
            {
                cr.ErrorMessage = msg;
                return;
            }

            if (!cr.BoolOption("--vector") && !cr.BoolOption("--matrix"))
            {
                if (cr.BoolOption("--cluster"))
                {
                    cr.ErrorMessage = "Clustering must be performed for: --vector, --matrix, or both";
                    return;
                }

                if (cr.BoolOption("--heatmap"))
                {
                    cr.ErrorMessage = "Heatmaps must be generated for: --vector, --matrix, or both";
                    return;
                }
            }
        });

        // Create a root command with two sub-commands
        RootCommand rootCommand = new("A cross-platform analysis tool for residue energy contribution vectors by Maozi Chen")
        {
            searchCommand,
            collateCommand,
        };

        rootCommand.AddGlobalOption(new Option<string[]>(
            ["--categories", "-C"],
            $"The categories to run the computation with [default: {string.Join(' ', EnumAnnotationHelper<Category>.Enums)}]")
        {
            AllowMultipleArgumentsPerToken = true,
        });

        // Parse the incoming args and invoke the handler
        return await rootCommand.InvokeAsync(args);
    }
}
