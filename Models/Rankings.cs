using mccsx.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace mccsx.Models;

internal class Rankings(
    IReadOnlyList<RawRecvData> recvData,
    string? stateName,
    string? indexName,
    int topN)
{
    public IReadOnlyList<RawRecvData> RecvData { get; set; } = recvData;
    public string? StateName { get; } = stateName;
    public string? IndexName { get; } = indexName;
    public int TopN { get; } = topN;

    public Rect GetFormattedReport(out IEnumerable<IEnumerable<object?>> dataRows)
    {
        // Row 1
        // Column 1: Scores/*[Index name]/Residue sequences/Residue names
        // Column 2..: [Input names]...

        // *Row 2
        // Column 1: [State name]->
        // Column 2..: [State names]...

        // Column 1
        // Row 1: Top [n] scores
        // Row 2: *[State name]->
        // Row 3..(2 + [n]): 1..[n]

        // Note: *is optional, [xxx] is a placeholder, ... is an expansion

        // Sort the RECVs first by state, then by input name
        string[] inputStates;
        RawRecvData[] orderedRecvs;

        if (StateName != null)
        {
            Debug.Assert(RecvData.All(o => o.InputState != null));

            orderedRecvs = [.. RecvData.OrderBy(o => o.InputState).ThenBy(o => o.VectorName)];
            inputStates = [.. orderedRecvs.Select(o => o.InputState!)];
        }
        else
        {
            Debug.Assert(RecvData.All(o => o.InputState == null));

            orderedRecvs = [.. RecvData.OrderBy(o => o.VectorName)];
            inputStates = [];
        }

        string[] inputNames = orderedRecvs.Select(o => o.VectorName).ToArray();

        // Compute ranking data
        IEnumerable<ResidueScore[]> data = orderedRecvs.Select
        (
            recv => recv.ResidueScores
                .OrderBy(o => o.Score)
                .Take(TopN)
                .ToArray()
        );

        // Initialize empty data rows
        dataRows = Array.Empty<object?[]>();

        // Residue scores
        dataRows = AddDataRows(
            dataRows,
            "Scores",
            inputStates,
            inputNames,
            data,
            o => o.Score,
            0.0);

        if (IndexName != null)
        {
            // Blank row
            dataRows = dataRows.Append(Array.Empty<object?>());

            // Input indices
            dataRows = AddDataRows(
                dataRows,
                IndexName,
                inputStates,
                inputNames,
                data,
                o => o.Index ?? $"{o.Residue.GetCode()}{o.ResidueSeq}",
                null);
        }

        // Blank row
        dataRows = dataRows.Append(Array.Empty<object?>());

        // Residue sequences
        dataRows = AddDataRows(
            dataRows,
            "Residue sequences",
            inputStates,
            inputNames,
            data,
            o => $"{o.Residue.GetCode()}{o.ResidueSeq}",
            null);

        // Blank row
        dataRows = dataRows.Append(Array.Empty<object?>());

        // Residue names
        dataRows = AddDataRows(
            dataRows,
            "Residue names",
            inputStates,
            inputNames,
            data,
            o => o.Residue.GetShortName().ToUpper(),
            null);

        int top = StateName != null ? 3 : 2;

        return new(2, top, inputNames.Length + 1, top + TopN - 1);
    }

    private IEnumerable<IEnumerable<object?>> AddDataRows(
        IEnumerable<IEnumerable<object?>> dataRows,
        string blockName,
        string[] inputStates,
        string[] inputNames,
        IEnumerable<ResidueScore[]> data,
        Func<ResidueScore, object?> valueSelector,
        object? defaultValue)
    {
        // Input names
        dataRows = dataRows.Append([blockName, .. inputNames]);

        // Input states
        if (StateName != null)
            dataRows = dataRows.Append([$"{StateName}->", .. inputStates]);

        // Ranking lines
        dataRows = dataRows.Concat
        (
            Enumerable.Range(0, TopN).Select
            (
                i => (object?[])
                [
                    i + 1,
                    .. data.Select(res => i < res.Length ? valueSelector(res[i]) : defaultValue)
                ]
            )
        );

        return dataRows;
    }
}
