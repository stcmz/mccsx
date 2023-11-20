using mccsx.Statistics;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace mccsx.Models;

internal class SimilarityMatrix : MapDataFrame<string, string>
{
    private SimilarityMatrix(
        IEnumerable<IVector<string, string, string?>> colVectors,
        IReadOnlyDictionary<string, string>? rowTags,
        string? rowTagName,
        string? colTagName)
        : base(colVectors, rowTags, rowTagName, colTagName)
    {
    }

    public string? StateName => ColumnTagName;

    public static SimilarityMatrix FromInputVectors(
        InputVectors inputVectors,
        IVectorDistanceMeasure similarityMeasure)
    {
        SimilarityMatrix obj = new(
            inputVectors.ColumnKeys.Select
            (
                colKey => new MapVector<string>
                (
                    inputVectors.ColumnKeys.ToDictionary
                    (
                        rowKey => rowKey, // Row key
                        rowKey => similarityMeasure.Measure(inputVectors.GetColumn(colKey), inputVectors.GetColumn(rowKey)) // Similarity value
                    ),
                    colKey, // Column key
                    inputVectors.GetColumn(colKey).Tag // Column tag
                )
            ),
            inputVectors.GetRowTags(), // Row tags
            inputVectors.ColumnTagName, // Row tag type
            inputVectors.ColumnTagName // Column tag type
        );

        Debug.Assert(obj.RowCount == obj.ColumnCount);
        Debug.Assert(obj.RowTagName == obj.ColumnTagName);

        return obj;
    }

    public Rect GetFormattedReport(
        out IEnumerable<string?>[] headerRows,
        out IEnumerable<IEnumerable<object?>> dataRows)
    {
        // Row 1
        // Column 1: *[State name]
        // Column 2: Input
        // Column 2..: [Input names]...

        // *Row 2
        // Column 1: null
        // Column 2: [State name]->
        // Column 2..: [State names]...

        // *Column 1:
        // Row 1: [State name]
        // Row 2: null
        // Row 2..: [State names]...

        // Column 2:
        // Row 1: Input
        // Row 2: *[State name]->
        // Row 2..: [Input names]...

        // Note: *is optional, [xxx] is a placeholder, ... is an expansion

        // Sort the columns first by state, then by input name
        string[] columnKeys, columnStates;

        Debug.Assert(RowTagName == ColumnTagName);

        if (StateName != null)
        {
            Debug.Assert(Columns.All(o => o.Tag != null));
            Debug.Assert(Rows.All(o => o.Tag != null));

            columnKeys = Columns.OrderBy(o => o.Tag!).ThenBy(o => o.Name).Select(o => o.Name).ToArray();
            columnStates = Columns.OrderBy(o => o.Tag!).Select(o => o.Tag!).ToArray();

            Debug.Assert(Enumerable.SequenceEqual(columnKeys, Rows.OrderBy(o => o.Tag!).ThenBy(o => o.Name).Select(o => o.Name)));
            Debug.Assert(Enumerable.SequenceEqual(columnStates, Rows.OrderBy(o => o.Tag!).Select(o => o.Tag!)));

            // Header row 1
            IEnumerable<string> headerRow1 = [StateName, "Input", .. columnKeys];

            // Header row 2
            IEnumerable<string?> headerRow2 = [null, $"{StateName}->", .. columnStates];

            headerRows = [headerRow1, headerRow2];
        }
        else
        {
            Debug.Assert(Columns.All(o => o.Tag == null));
            Debug.Assert(Rows.All(o => o.Tag == null));

            columnKeys = [.. ColumnKeys.OrderBy(o => o)];
            columnStates = [];

            Debug.Assert(Enumerable.SequenceEqual(columnKeys, RowKeys.OrderBy(o => o)));

            // Only one header row
            IEnumerable<string> headerRow = ["Input", .. columnKeys];

            headerRows = [headerRow];
        }

        // Build the data rows, in the same ordering of columns
        dataRows = columnKeys.Select((rowKey, i) =>
        {
            IEnumerable<object?> row = [rowKey];

            // Vector state
            if (StateName != null)
                row = row.Prepend(columnStates[i]);

            // Similarity data
            row = row.Concat(columnKeys.Select(colKey => (object)this[rowKey, colKey]));

            return row;
        });

        int pos = StateName != null ? 3 : 2;
        int size = pos + columnKeys.Length - 1;

        return new(pos, pos, size, size);
    }
}
