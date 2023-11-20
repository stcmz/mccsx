using System;
using System.Collections.Generic;
using System.Linq;

namespace mccsx;

internal class StateFilter
{
    public string StateName { get; }

    private readonly Dictionary<string, string> _data;

    public StateFilter(string[] lines)
    {
        if (lines.Length < 2)
            throw new FilterException($"Insufficient lines", "state");

        string[]? headers = lines[0].Split(" \t".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

        if (headers.Length >= 2)
        {
            _data = [];

            // Parse input names and states, allowing space if quoted
            foreach (string? line in lines.Skip(1))
            {
                int nameBegin = 0, nameEnd = 0;
                if (line[0] == '"' || line[0] == '\'')
                {
                    int idx = line.IndexOf(line[0], 1);
                    if (idx != -1)
                    {
                        nameBegin = 1;
                        nameEnd = idx;
                    }
                }

                if (nameEnd == 0)
                {
                    int idx = line.IndexOf(' ');
                    if (idx == -1)
                        idx = line.IndexOf('\t');
                    if (idx != -1)
                    {
                        nameBegin = 0;
                        nameEnd = idx;
                    }
                }

                if (nameEnd == 0)
                {
                    throw new FilterException($"Insufficient columns on line: {line}");
                }

                _data[line[nameBegin..nameEnd]] = line[nameEnd..].Trim();
            }
            StateName = headers[1];
        }
        else
        {
            throw new FilterException("Insufficient columns");
        }
    }

    public string? GetState(string inputName)
    {
        if (_data.TryGetValue(inputName, out string? value))
            return value;
        return null;
    }
}
