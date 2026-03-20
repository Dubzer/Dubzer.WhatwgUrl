using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Dubzer.WhatwgUrl.Codegen;

/// <summary>
/// Parses DerivedCombiningClass.txt and generates the ViramaSet (Canonical_Combining_Class=9)
/// for UnicodeTables.cs
/// https://www.unicode.org/Public/UNIDATA/extracted/DerivedCombiningClass.txt
/// </summary>
internal static class ViramaSetGenerator
{
    public static string Generate(IEnumerable<string[]> lines)
    {
        var codepoints = new List<int>();

        foreach (var fields in lines)
        {
            if (fields.Length < 2 || fields[1].Trim() != "9")
                continue;

            var codepoint = fields[0].Trim();
            if (codepoint.Contains(".."))
            {
                var parts = codepoint.Split("..");
                var start = int.Parse(parts[0], NumberStyles.HexNumber);
                var end = int.Parse(parts[1], NumberStyles.HexNumber);
                codepoints.AddRange(Enumerable.Range(start, end - start + 1));
            }
            else
            {
                codepoints.Add(int.Parse(codepoint, NumberStyles.HexNumber));
            }
        }

        return CodegenHelpers.GenerateFrozenSet("ViramaSet", codepoints);
    }
}
