using mccsx.Extensions;
using mccsx.Helpers;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace mccsx;

internal record ResidueScore(string ChainId, AminoAcid Residue, int ResidueSeq, double Score, string? Index);

/// <summary>
/// Residue Energy Contribution Vector
/// </summary>
internal class RawRecvData(
    string vectorName,
    ResidueScore[] residueScores,
    double[] summaryFields,
    string? inputState,
    string? indexName)
{
    public string VectorName { get; } = vectorName;
    public ResidueScore[] ResidueScores { get; } = residueScores;
    public double[] SummaryFields { get; } = summaryFields;
    public string? InputState { get; } = inputState;
    public string? IndexName { get; } = indexName;

    public static IReadOnlyList<string> CsvColumnHeaders { get; }
        = [ "Chain ID", "Residue name", "Residue sequence" ];

    public static IReadOnlyList<string> SummaryRowHeaders { get; }
        = [ "Intra-Ligand Free", "Inter-Ligand Free", "Total Free Energy", "Normalized Total Free Energy" ];

    public static RawRecvData FromCsvFile(string csvFilePath, string inputName, string? inputState, IndexFilter? indexFilter)
    {
        // Read and parse the csv file
        string[][] rows = File.ReadAllLines(csvFilePath)
            .Select(o => CsvHelper.SplitCsvFields(o))
            .Where(o => o.Length > 1)
            .ToArray();

        // Validate RECV format integration
        if (!rows.All(o => o.Length >= 4))
        {
            throw new RawRecvDataFormatException($"Not all rows are with 4 or more fields", csvFilePath);
        }

        for (int i = 0; i < CsvColumnHeaders.Count; i++)
        {
            if (CsvColumnHeaders[i] != rows[0][i])
            {
                throw new RawRecvDataFormatException($"Unrecognized column header '{rows[0][i]}'", csvFilePath);
            }
        }

        int summaryRowCount = 0;
        double[] summaryFields = new double[SummaryRowHeaders.Count];
        List<ResidueScore> residueScores = [];

        // Skip the header row
        foreach (string[] row in rows.Skip(1))
        {
            // For a score row.
            if (!string.IsNullOrEmpty(row[1]) && !string.IsNullOrEmpty(row[2]))
            {
                string chain = row[0], resName = row[1];

                if (!int.TryParse(row[2], out int resSeq))
                {
                    throw new RawRecvDataFormatException($"Invalid residue sequence '{row[2]}'", csvFilePath);
                }

                if (!double.TryParse(row[3], out double score))
                {
                    score = 0;
                }

                residueScores.Add(new(chain, resName.ParseAminoAcid(), resSeq, score, indexFilter?.GetIndex(resSeq)));
            }
            // For a summary row.
            else
            {
                if (SummaryRowHeaders[summaryRowCount] != row[0])
                {
                    throw new RawRecvDataFormatException($"Unrecognized summary header '{row[0]}'", csvFilePath);
                }
                summaryFields[summaryRowCount++] = double.Parse(row[3]);
            }
        }

        if (summaryRowCount != 4)
        {
            throw new RawRecvDataFormatException($"Not exactly 4 summary rows", csvFilePath);
        }

        return new(inputName, [.. residueScores], summaryFields, inputState, indexFilter?.IndexName);
    }
}
