using System;

namespace mccsx;

[AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
internal sealed class AminoAcidNamesAttribute(string shortName, char code) : Attribute
{
    public string ShortName { get; } = shortName;

    public char Code { get; } = code;
}
