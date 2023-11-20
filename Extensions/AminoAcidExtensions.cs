using mccsx.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;

namespace mccsx.Extensions;

internal static class AminoAcidExtensions
{
    private static readonly IDictionary<string, AminoAcid> NameDict = EnumAnnotationHelper<AminoAcid>
        .Enums
        .ToDictionary(o => EnumAnnotationHelper<AminoAcid>.GetAttribute<AminoAcidNamesAttribute>(o).ShortName.ToUpper());

    public static AminoAcid ParseAminoAcid(this string s)
    {
        if (s.Length != 3)
            throw new InvalidCastException();
        s = s.ToUpper();
        if (!NameDict.ContainsKey(s))
            throw new InvalidCastException();
        return NameDict[s];
    }

    public static bool TryParseAminoAcid(this string s, out AminoAcid value)
    {
        return NameDict.TryGetValue(s.ToUpper(), out value);
    }

    public static int ParseResidueSequence(this string s)
    {
        // Ala379
        if (s.Length > 3 && char.IsLetter(s[2]))
            return int.Parse(s[3..]);
        // A379
        if (s.Length > 1 && char.IsLetter(s[0]))
            return int.Parse(s[1..]);
        // 379
        return int.Parse(s);
    }

    public static bool TryParseResidueSequence(this string s, out int resSeq)
    {
        // Ala379
        if (s.Length > 3 && char.IsLetter(s[2]))
            return int.TryParse(s[3..], out resSeq);
        // A379
        if (s.Length > 1 && char.IsLetter(s[0]))
            return int.TryParse(s[1..], out resSeq);
        // 379
        return int.TryParse(s, out resSeq);
    }

    public static string GetShortName(this AminoAcid value)
    {
        return EnumAnnotationHelper<AminoAcid>.GetAttribute<AminoAcidNamesAttribute>(value).ShortName;
    }

    public static char GetCode(this AminoAcid value)
    {
        return EnumAnnotationHelper<AminoAcid>.GetAttribute<AminoAcidNamesAttribute>(value).Code;
    }
}
