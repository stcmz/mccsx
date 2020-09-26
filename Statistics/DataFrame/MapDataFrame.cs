using System;
using System.Collections.Generic;
using System.Linq;

namespace mccsx.Statistics
{
    public class MapDataFrame<TRowKey, TColumnKey>
        : IDataFrame<TRowKey, TColumnKey, IVector<TColumnKey, TRowKey, string?>, IVector<TRowKey, TColumnKey, string?>>
        where TRowKey : notnull
        where TColumnKey : notnull
    {
        private readonly IReadOnlyList<IVector<TRowKey, TColumnKey, string?>> _colVectors;
        private readonly IReadOnlyDictionary<TColumnKey, IVector<TRowKey, TColumnKey, string?>> _cols;
        private readonly IReadOnlyDictionary<TRowKey, IVector<TColumnKey, TRowKey, string?>> _rows;

        public MapDataFrame(
            IEnumerable<IVector<TRowKey, TColumnKey, string?>> colVectors,
            IReadOnlyDictionary<TRowKey, string>? rowTags = null,
            string? rowTagName = null,
            string? colTagName = null)
        {
            // No particular order is required or maintained in the raw column vectors
            _colVectors = colVectors.ToList();
            RowKeys = _colVectors[0].UnionKeys(_colVectors.Skip(1)).ToList();
            _cols = _colVectors.ToDictionary(o => o.Name, o => o);
            ColumnKeys = _cols.Keys.ToList();
            RowTagName = rowTagName;
            ColumnTagName = colTagName;
            _rows = RowKeys.ToDictionary(key => key, key => (IVector<TColumnKey, TRowKey, string?>)new ConvolutionalMapVector<TColumnKey, TRowKey>(_cols, key, rowTags?[key]));
        }

        public double this[TRowKey rowKey, TColumnKey colKey] => _rows[rowKey][colKey];

        /// <summary>
        /// Get the raw row vectors. The returned order is not affected by OrderRowsBy()
        /// </summary>
        public IEnumerable<IVector<TColumnKey, TRowKey, string?>> Rows => _rows.Values;

        /// <summary>
        /// Get the raw column vectors. The returned order is not affected by OrderColumnsBy()
        /// </summary>
        public IEnumerable<IVector<TRowKey, TColumnKey, string?>> Columns => _cols.Values;

        public IReadOnlyList<TRowKey> RowKeys { get; private set; }
        public IReadOnlyList<TColumnKey> ColumnKeys { get; private set; }

        public int RowCount => _rows.Count;
        public int ColumnCount => _cols.Count;

        public string? RowTagName { get; }
        public string? ColumnTagName { get; }

        public IVector<TColumnKey, TRowKey, string?> GetRow(TRowKey rowKey) => _rows[rowKey];
        public IVector<TRowKey, TColumnKey, string?> GetColumn(TColumnKey colKey) => _cols[colKey];

        public IVector<TColumnKey, TRowKey, string?> GetRowAt(int index) => _rows[RowKeys[index]];
        public IVector<TRowKey, TColumnKey, string?> GetColumnAt(int index) => _cols[ColumnKeys[index]];

        public double GetAt(int rowIndex, int colIndex) => _rows[RowKeys[rowIndex]][ColumnKeys[colIndex]];

        public bool Has(TRowKey rowKey, TColumnKey columnKey)
        {
            return _cols.TryGetValue(columnKey, out var vector) && vector.Has(rowKey);
        }

        public void OrderRowsBy<TKey>(Func<IVector<TColumnKey, TRowKey>, TKey> keySelector)
        {
            RowKeys = RowKeys.OrderBy(o => keySelector(_rows[o])).ToList();
        }

        public void OrderColumnsBy<TKey>(Func<IVector<TRowKey, TColumnKey>, TKey> keySelector)
        {
            ColumnKeys = ColumnKeys.OrderBy(o => keySelector(_cols[o])).ToList();
        }
    }
}
