using System;

namespace mccsx;

[Serializable]
public class RawRecvDataFormatException : Exception
{
    public RawRecvDataFormatException() { }
    public RawRecvDataFormatException(string message) : base(message) { }
    public RawRecvDataFormatException(string message, Exception inner) : base(message, inner) { }
    public RawRecvDataFormatException(string message, string fileName) : base(message) => FileName = fileName;
    public RawRecvDataFormatException(string message, string fileName, Exception inner) : base(message, inner) => FileName = fileName;

    public string? FileName { get; }
}
