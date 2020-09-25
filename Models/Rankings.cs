using mccsx.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace mccsx.Models
{
    internal class Rankings
    {
        public IReadOnlyList<RawRecvData> RecvData { get; set; }
        public string? StateName { get; }
        public string? IndexName { get; }
        public int TopN { get; }

        public Rankings(
            IReadOnlyList<RawRecvData> recvData,
            string? stateName,
            string? indexName,
            int topN)
        {
            RecvData = recvData;
            StateName = stateName;
            IndexName = indexName;
            TopN = topN;
        }

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

                orderedRecvs = RecvData.OrderBy(o => o.InputState).ThenBy(o => o.VectorName).ToArray();
                inputStates = orderedRecvs.Select(o => o.InputState!).ToArray();
            }
            else
            {
                Debug.Assert(RecvData.All(o => o.InputState == null));

                orderedRecvs = RecvData.OrderBy(o => o.VectorName).ToArray();
                inputStates = Array.Empty<string>();
            }

            string[] inputNames = orderedRecvs.Select(o => o.VectorName).ToArray();

            // Compute ranking data
            var data = orderedRecvs.Select
            (
                recv => recv.ResidueScores
                    .OrderBy(o => o.Score)
                    .Take(TopN)
                    .ToArray()
            );

            // Scores - input names
            dataRows = new[] { new[] { "Scores" }.Concat(inputNames) };

            // Scores - states
            if (StateName != null)
                dataRows = dataRows.Append(new[] { $"{StateName}->" }.Concat(inputStates));

            // Scores - ranking lines
            dataRows = dataRows.Concat(
                Enumerable.Range(0, TopN).Select(
                    i => new object?[] { i + 1 }.Concat(
                        data.Select(
                            res => i < res.Length ? res[i].Score : (object)0.0))));

            if (IndexName != null)
            {
                // Indices - input names
                dataRows = dataRows.Append(Array.Empty<object>()).Append(new[] { IndexName }.Concat(inputNames));

                // Indices - states
                if (StateName != null)
                    dataRows = dataRows.Append(new[] { $"{StateName}->" }.Concat(inputStates));

                // Indices - ranking lines
                dataRows = dataRows.Concat(
                    Enumerable.Range(0, TopN).Select(
                        i => new object[] { i + 1 }.Concat(
                            data.Select(
                                res => i < res.Length ? res[i].Index ?? $"{res[i].Residue.GetCode()}{res[i].ResidueSeq}" : (object?)null))));
            }

            // Residue sequences - input names
            dataRows = dataRows.Append(Array.Empty<object>()).Append(new[] { "Residue sequences" }.Concat(inputNames));

            // Residue sequences - states
            if (StateName != null)
                dataRows = dataRows.Append(new[] { $"{StateName}->" }.Concat(inputStates));

            // Residue sequences - ranking lines
            dataRows = dataRows.Concat(
                Enumerable.Range(0, TopN).Select(
                    i => new object[] { i + 1 }.Concat(
                        data.Select(
                            res => i < res.Length ? $"{res[i].Residue.GetCode()}{res[i].ResidueSeq}" : (object?)null))));

            // Residue names - input names
            dataRows = dataRows.Append(Array.Empty<object>()).Append(new[] { "Residue names" }.Concat(inputNames));

            // Residue names - states
            if (StateName != null)
                dataRows = dataRows.Append(new[] { $"{StateName}->" }.Concat(inputStates));

            // Residue names - ranking lines
            dataRows = dataRows.Concat(
                Enumerable.Range(0, TopN).Select(
                    i => new object[] { i + 1 }.Concat(
                        data.Select(
                            res => i < res.Length ? res[i].Residue.GetShortName() : (object?)null))));

            int top = StateName != null ? 3 : 2;

            return new(1, top, inputNames.Length + 1, top + TopN - 1);
        }
    }
}
