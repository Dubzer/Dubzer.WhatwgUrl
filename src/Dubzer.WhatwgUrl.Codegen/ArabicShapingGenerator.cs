using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Dubzer.WhatwgUrl.Codegen;

internal static class ArabicShapingGenerator
{
    /// <summary>
    /// Parses ArabicShaping.txt and generates the joining type tables (RSet, DSet, LSet)
    /// for UnicodeTables.cs.
    /// </summary>
    public static string Generate(IEnumerable<string[]> lines)
    {
        var rSet = new List<int>();
        var dSet = new List<int>();
        var lSet = new List<int>();

        foreach (var fields in lines)
        {
            if (fields.Length < 3)
                continue;

            var codePoint = int.Parse(fields[0].Trim(), NumberStyles.HexNumber);
            var joiningType = fields[2].Trim();

            switch (joiningType)
            {
                case "R":
                    rSet.Add(codePoint);
                    break;
                case "D":
                    dSet.Add(codePoint);
                    break;
                case "L":
                    lSet.Add(codePoint);
                    break;
                // C (Join_Causing), U (Non_Joining), T (Transparent) are not used
            }
        }

        rSet.Sort();
        dSet.Sort();

        var lSetFormatted = string.Join(", ", lSet.Order().Select(x => $"0x{x:X}"));

        return $"""
                {CodegenHelpers.GenerateFrozenSet("RSet", rSet)}

                    public static ReadOnlySpan<int> LSet => [{lSetFormatted}];

                {CodegenHelpers.GenerateFrozenSet("DSet", dSet)}
                """;
    }
}
