using System;
using System.Collections.Generic;

namespace mccsx.Statistics;

public interface IDataFrame<TRowKey, TColumnKey, out TRowVector, out TColumnVector>
    where TRowKey : notnull
    where TColumnKey : notnull
    where TRowVector : IVector<TColumnKey, TRowKey>
    where TColumnVector : IVector<TRowKey, TColumnKey>
{
    double this[TRowKey rowKey, TColumnKey colKey] { get; }

    IEnumerable<TRowVector> Rows { get; }
    IEnumerable<TColumnVector> Columns { get; }

    IReadOnlyList<TRowKey> RowKeys { get; }
    IReadOnlyList<TColumnKey> ColumnKeys { get; }

    int RowCount { get; }
    int ColumnCount { get; }

    string? RowTagName { get; }
    string? ColumnTagName { get; }

    TRowVector GetRow(TRowKey rowKey);
    TColumnVector GetColumn(TColumnKey colKey);

    TRowVector GetRowAt(int index);
    TColumnVector GetColumnAt(int index);

    double GetAt(int rowIndex, int colIndex);

    bool Has(TRowKey rowKey, TColumnKey columnKey);

    void OrderRowsBy<TKey>(Func<IVector<TColumnKey, TRowKey>, TKey> keySelector);
    void OrderColumnsBy<TKey>(Func<IVector<TRowKey, TColumnKey>, TKey> keySelector);
}
