using mccsx.Extensions;
using mccsx.Helpers;
using mccsx.Statistics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace mccsx.Models
{
    /// <summary>
    ///           ChainIds  ResidueNames  ResidueSeqs  ResidueSeq  RowKey  ResidueIndex <br/>
    /// w/o index:   A/B      ALA/SER        null        [392]     A/S392      null     <br/>
    ///  w/ index:   A/B      ALA/SER       392/393       null      3.32      [3.32]    <br/>
    /// where [key] indicates the access key and grouping key to the RECV input data.
    /// </summary>
    internal record ResidueInfo(string ChainIds, string ResidueNames, string ResidueSeqs, int? ResidueSeq, string RowKey, string? ResidueIndex);

    internal record Rect(int Left, int Top, int Width, int Height);

    internal sealed class InputVectors : MapDataFrame<string, string>
    {
        private InputVectors(
            IEnumerable<IVector<string, string, string?>> colVectors,
            IReadOnlyDictionary<string, string>? rowTags,
            string? rowTagName,
            string? colTagName,
            string? indexName,
            IReadOnlyDictionary<string, ResidueInfo> residueInfo,
            IReadOnlyDictionary<string, double[]> summaryFields,
            ICollection<string> errorMessages)
            : base(colVectors, rowTags, rowTagName, colTagName)
        {
            IndexName = indexName;
            ResidueInfo = residueInfo;
            SummaryFields = summaryFields;
            ErrorMessages = errorMessages;
        }

        public string? IndexName { get; }

        public string? StateName => ColumnTagName;

        /// <summary>
        /// Residue information by row keys
        /// </summary>
        public IReadOnlyDictionary<string, ResidueInfo> ResidueInfo { get; }

        /// <summary>
        /// Summary fields by vector names
        /// </summary>
        public IReadOnlyDictionary<string, double[]> SummaryFields { get; }

        /// <summary>
        /// Error messages
        /// </summary>
        public ICollection<string> ErrorMessages { get; }

        /// <summary>
        /// Create an <see cref="InputVectors"/> instance with the given <see cref="RawRecvData"/> list.<br/>
        /// 
        /// The row keys of the input vectors depend on whether a residue index is used (i.e. whether a index filter is specified).<br/><br/>
        /// 
        /// The row keys are used in:<br/>
        ///   1) heatmap row name,
        ///   2) alignment of input vectors in comparison,<br/>
        /// but not in:<br/>
        ///   1) csv columns,
        ///   2) xlsx columns.<br/><br/>
        /// 
        /// Without index:<br/>
        ///   Vector elements with same residue sequence are considered the same residue, even they have different residue names.
        ///   Row keys are in A392 format; multiple residue names in a sequence number result in A/S392 row key format.
        ///   No additional residue index column appears in csv/xlsx files.<br/><br/>
        /// 
        /// With index:<br/>
        ///   Vector elements with same residue index are considered the same residue, even they have different residue sequences.
        ///   Row keys are in 3.52 format (index dependent); row keys missing indices are in A/S392 format, i.e. fall back to the case without index.
        ///   Multiple residue sequences in an index are in 392/393 format.
        ///   Additional residue index column appears.
        /// </summary>
        /// <param name="vecData">The raw RECV input data</param>
        /// <param name="stateName">An optional input state name</param>
        /// <returns>The newly created <see cref="InputVectors"/> instance</returns>
        public static InputVectors FromRawRecvData(
            IReadOnlyList<RawRecvData> vecData,
            string? stateName)
        {
            var errorMessages = new List<string>();

            string? indexName = null;

            // Verify consistency of index names
            foreach (var recv in vecData)
            {
                if (indexName == null)
                {
                    indexName = recv.IndexName;
                }
                else if (indexName != recv.IndexName)
                {
                    errorMessages.Add($"Index name '{recv.IndexName}' different from previous ones '{indexName}' for {recv.VectorName}");
                }
            }

            // Prepare residue info and summary fields for the input vectors
            SummarizeResidues(vecData, out var residueInfoBySeq, out var residueInfoByIndex);

            var residueInfoByRowKey = residueInfoByIndex
                .Select(o => new { o.Value.RowKey, o.Value })
                .Concat(residueInfoBySeq.Select(o => new { o.Value.RowKey, o.Value }))
                .ToDictionary(o => o.RowKey, o => o.Value);

            var summaryFields = vecData.ToDictionary(o => o.VectorName, o => o.SummaryFields);

            // Create the instance
            var obj = new InputVectors
            (
                vecData.Select // Column vectors
                (
                    o => new MapVector<string>
                    (
                        o.ResidueScores.ToDictionary
                        (
                            p => (p.Index == null ? residueInfoBySeq[p.ResidueSeq] : residueInfoByIndex[p.Index]).RowKey, // Row key
                            p => p.Score // Score value
                        ),
                        o.VectorName, // Column key
                        o.InputState // Column tag
                    )
                ),
                null, // No row tags
                null, // No row tag type
                stateName, // Column tag type
                indexName, // Index name
                residueInfoByRowKey, // Residue info
                summaryFields, // Summary fields
                errorMessages // Error messages
            );

            Debug.Assert(obj.ColumnCount == vecData.Count);

            return obj;
        }

        private static void SummarizeResidues(
            IReadOnlyList<RawRecvData> vecData,
            out Dictionary<int, ResidueInfo> residueInfoBySeq,
            out Dictionary<string, ResidueInfo> residueInfoByIndex)
        {
            var chainDictByIndex = new Dictionary<string, HashSet<string>>();
            var resNameDictByIndex = new Dictionary<string, HashSet<AminoAcid>>();
            var resSeqDictByIndex = new Dictionary<string, HashSet<int>>();

            var chainDictBySeq = new Dictionary<int, HashSet<string>>();
            var resNameDictBySeq = new Dictionary<int, HashSet<AminoAcid>>();

            foreach (var vec in vecData)
            {
                foreach (var res in vec.ResidueScores)
                {
                    string? key = res.Index;

                    if (key != null)
                    {
                        // Chain IDs
                        if (chainDictByIndex.TryGetValue(key, out var hsChains))
                            hsChains.Add(res.ChainId);
                        else
                            chainDictByIndex[key] = new HashSet<string> { res.ChainId };

                        // Residue names
                        if (resNameDictByIndex.TryGetValue(key, out var hsResNames))
                            hsResNames.Add(res.Residue);
                        else
                            resNameDictByIndex[key] = new HashSet<AminoAcid> { res.Residue };

                        // Residue sequences
                        if (resSeqDictByIndex.TryGetValue(key, out var hsResSeqs))
                            hsResSeqs.Add(res.ResidueSeq);
                        else
                            resSeqDictByIndex[key] = new HashSet<int> { res.ResidueSeq };
                    }
                    else
                    {
                        int key2 = res.ResidueSeq;

                        // Chain IDs
                        if (chainDictBySeq.TryGetValue(key2, out var hsChains))
                            hsChains.Add(res.ChainId);
                        else
                            chainDictBySeq[key2] = new HashSet<string> { res.ChainId };

                        // Residue names
                        if (resNameDictBySeq.TryGetValue(key2, out var hsResNames))
                            hsResNames.Add(res.Residue);
                        else
                            resNameDictBySeq[key2] = new HashSet<AminoAcid> { res.Residue };
                    }
                }
            }

            // With index
            residueInfoByIndex = chainDictByIndex.Keys.ToDictionary(idx => idx, idx => new ResidueInfo
            (
                string.Join('/', chainDictByIndex[idx].OrderBy(p => p)),
                string.Join('/', resNameDictByIndex[idx].Select(p => p.GetShortName().ToUpper()).OrderBy(p => p)),
                string.Join('/', resSeqDictByIndex[idx].OrderBy(p => p)),
                resSeqDictByIndex[idx].Count == 1 ? resSeqDictByIndex[idx].First() : (int?)null,
                idx,
                idx
            ));

            // Without index
            residueInfoBySeq = chainDictBySeq.Keys.ToDictionary(seq => seq, seq => new ResidueInfo
            (
                string.Join('/', chainDictBySeq[seq].OrderBy(p => p)),
                string.Join('/', resNameDictBySeq[seq].Select(p => p.GetShortName().ToUpper()).OrderBy(p => p)),
                seq.ToString(),
                seq,
                $"{string.Join('/', resNameDictBySeq[seq].Select(p => p.GetCode()).OrderBy(p => p))}{seq}",
                null
            ));
        }

        public Rect GetFormattedReport(
            out IEnumerable<string?>[] headerRows,
            out IEnumerable<IEnumerable<object?>> dataRows)
        {
            // Row 1
            // Column 1: Chain ID
            // Column 2: Residue name
            // Column 3: Residue sequence
            // Column 4: *[Residue index name]
            // Column ^([state count] + 1)..: Average([column count])

            // *Row 2
            // Column 1..3: null
            // Column 4: *[Input state name]->
            // Column 5..^([state count] + 1): [Input states]...
            // Column ^([state count] + 1)..: [State names]...
            // Column ^1: All

            // Row 1: Chain ID, Residue name, Residue sequence, *[Residue index name], [Input names]..., Average([column count])...
            // *Row 2: null, null, null, *[Input state name->], [Input states]...
            // Row ^5: null
            // Row ^4: Intra-Ligand Free, null, null, *null, [summary fields]..., [average fields]...
            // Row ^3: Inter-Ligand Free, null, null, *null, [summary fields]..., [average fields]...
            // Row ^2: Total Free Energy, null, null, *null, [summary fields]..., [average fields]...
            // Row ^1: Normalized Total Free Energy, null, null, *null, [summary fields]..., [average fields]...

            // Note: *is optional, [xxx] is a placeholder, ... is an expansion

            // Sort the columns first by state, then by input name
            string[] columnKeys;
            (string State, int Count, int Pos)[] stateCounts;

            if (StateName != null)
            {
                Debug.Assert(Columns.All(o => o.Tag != null));

                columnKeys = Columns.OrderBy(o => o.Tag!).ThenBy(o => o.Name).Select(o => o.Name).ToArray();
                stateCounts = Columns.GroupBy(o => o.Tag!).Select(o => (State: o.Key, o.Count(), 0)).OrderBy(o => o.State).ToArray();

                // Calculate offset for state columns
                for (int i = 0, col = 0; i < stateCounts.Length; i++)
                {
                    stateCounts[i] = (stateCounts[i].State, stateCounts[i].Count, col);
                    col += stateCounts[i].Count;
                }

                // Header row 1
                IEnumerable<string?> headerRow1 = RawRecvData.CsvColumnHeaders;

                if (IndexName != null)
                    headerRow1 = headerRow1.Append(IndexName);

                headerRow1 = headerRow1.Concat(columnKeys)
                    .Concat(stateCounts.Select(o => $"Average({o.Count})"))
                    .Append($"Average({ColumnCount})");

                // Header row 2
                IEnumerable<string?> headerRow2 = RawRecvData.CsvColumnHeaders.SkipLast(1)
                    .Select(o => (string?)null);

                if (IndexName != null)
                    headerRow2 = headerRow2.Append(null);

                headerRow2 = headerRow2.Append($"{StateName}->")
                    .Concat(columnKeys.Select(o => GetColumn(o).Tag))
                    .Concat(stateCounts.Select(o => o.State))
                    .Append("All");

                headerRows = new[] { headerRow1, headerRow2 };
            }
            else
            {
                Debug.Assert(Columns.All(o => o.Tag == null));

                columnKeys = ColumnKeys.OrderBy(o => o).ToArray();
                stateCounts = Array.Empty<(string State, int Count, int Pos)>();

                // Only one header row
                IEnumerable<string?> headerRow = RawRecvData.CsvColumnHeaders;

                if (IndexName != null)
                    headerRow = headerRow.Append(IndexName);

                headerRow = headerRow.Concat(columnKeys)
                    .Append($"Average({ColumnCount})");

                headerRows = new[] { headerRow };
            }

            // Check if all residues have unique sequence number
            bool uniqueSeq = Rows.All(o => ResidueInfo[o.Name].ResidueSeq != null);

            // Top left of the score area
            int left = IndexName != null ? 5 : 4;
            int top = StateName != null ? 3 : 2;
            int width = left + ColumnCount + (StateName != null ? stateCounts.Length : 0);
            int height = top + RowCount + RawRecvData.SummaryRowHeaders.Count;

            // Build the data rows, in the ascending ordinal ordering
            dataRows = Rows
                .OrderBy(vec => ResidueInfo[vec.Name].ResidueSeq)
                .Select((vec, i) =>
                {
                    var resInfo = ResidueInfo[vec.Name];

                    // Chain ID, Residue name
                    IEnumerable<object?> row = new[] { resInfo.ChainIds, resInfo.ResidueNames, };

                    // Residue sequence, Residue index
                    if (IndexName != null)
                        row = row.Concat(new[] { uniqueSeq ? (object?)resInfo.ResidueSeq : resInfo.ResidueSeqs, resInfo.ResidueIndex });
                    else
                        row = row.Append(resInfo.ResidueSeq);

                    // Score data
                    row = row.Concat(columnKeys.Select(colKey => Has(vec.Name, colKey) ? (object)base[vec.Name, colKey] : 0.0));

                    // Average for each state
                    if (StateName != null)
                        row = row.Concat(stateCounts.Select(state => GetRowAverage(top + i, left + state.Pos, state.Count)));

                    // Average for all vectors
                    row = row.Append(GetRowAverage(top + i, left, ColumnCount));

                    return row;
                })
                // 1 blank row, for the convenience of sorting in Excel
                .Append(Array.Empty<object>())
                // 4 summary rows
                .Concat(RawRecvData.SummaryRowHeaders.Select((field, i) =>
                {
                    // Chain ID, Residue name, Residue sequence
                    IEnumerable<object?> row = new[] { field, null, null, };

                    // Residue index
                    if (IndexName != null)
                        row = row.Append(null);

                    // Summary data
                    row = row.Concat(columnKeys.Select(colKey => (object)SummaryFields[colKey][i]));

                    // Average for each state
                    if (StateName != null)
                        row = row.Concat(stateCounts.Select(state => GetRowAverage(top + RowCount + 1 + i, left + state.Pos, state.Count)));

                    // Average for all summary fields
                    row = row.Append(GetRowAverage(top + RowCount + 1 + i, left, ColumnCount));

                    return row;
                }));

            return new(left, top, width, height);
        }

        /// <summary>
        /// Get the average expression for a Excel row range
        /// </summary>
        /// <param name="rowNum">The row number</param>
        /// <param name="colNum">Begin of column number, inclusive</param>
        /// <param name="cols">The number of columns</param>
        /// <returns></returns>
        private static string GetRowAverage(int rowNum, int colNum, int cols)
        {
            return $"=AVERAGE({ExcelColumnHelper.ToCellRef(colNum, rowNum)}:{ExcelColumnHelper.ToCellRef(colNum + cols - 1, rowNum)})";
        }

        public IReadOnlyDictionary<string, string>? GetRowTags()
        {
            if (StateName == null)
                return null;

            Debug.Assert(Columns.All(o => o.Tag != null));

            return ColumnKeys.ToDictionary(o => o, o => GetColumn(o).Tag!);
        }
    }
}
