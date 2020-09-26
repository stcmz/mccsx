using mccsx.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace mccsx
{
    internal class IndexFilter
    {
        public string IndexName { get; }

        private readonly Dictionary<int, string> _data;

        public IndexFilter(string[] lines)
        {
            if (lines.Length < 2)
                throw new FilterException($"Insufficient lines", "index");

            string[][] fieldLines = lines
                .Select(o => o.Split(" \t".ToCharArray(), StringSplitOptions.RemoveEmptyEntries))
                .ToArray();

            string[] headers = fieldLines[0];
            if (headers.Length >= 2)
            {
                _data = fieldLines
                    .Skip(1)
                    .ToDictionary(o => o[0].ParseResidueSequence(), o => o[1]);
                IndexName = headers[1];
            }
            else
            {
                throw new FilterException("Insufficient columns");
            }
        }

        public string? GetIndex(int residueSeq)
        {
            if (_data.TryGetValue(residueSeq, out var value))
                return value;
            return null;
        }
    }
}
