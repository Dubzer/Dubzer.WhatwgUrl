using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Dubzer.WhatwgUrl.Codegen;

/// <summary>
/// Parses DerivedBidiClass.txt from the Unicode Character Database and generates
/// the DirectionTable for UnicodeTables.cs.
/// https://www.unicode.org/Public/UNIDATA/extracted/DerivedBidiClass.txt
/// </summary>
internal static class DirectionTableGenerator
{
    /// <summary>
    /// Generates the DirectionTable array from parsed DerivedBidiClass.txt lines.
    /// </summary>
    public static string Generate(IEnumerable<string[]> lines)
    {
        var ranges = new List<(int Start, int End, string Direction)>();

        foreach (var fields in lines)
        {
            if (fields.Length < 2)
                continue;

            var codepointStr = fields[0].Trim();

            int start, end;
            if (codepointStr.Contains("..", StringComparison.Ordinal))
            {
                var parts = codepointStr.Split("..");
                start = int.Parse(parts[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                end = int.Parse(parts[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }
            else
            {
                start = int.Parse(codepointStr, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                end = start;
            }

            var bidiClass = fields[1].Trim();
#pragma warning disable CA1308
            bidiClass = $"{bidiClass[0]}{bidiClass[1..].ToLowerInvariant()}";
#pragma warning restore CA1308

            ranges.Add((start, end, bidiClass));
        }

        ranges = ranges.OrderBy(r => r.End).ToList();

        var merged = new List<(int Start, int End, string Direction)>
        {
            ranges[0]
        };

        foreach (var current in ranges.Skip(1))
        {
            if (current.Start >= 0x2E00)
            {
                Debugger.Break();
            }
            var lastMerged = merged[^1];
            if (current.Direction == lastMerged.Direction && lastMerged.End + 1 == current.Start)
            {
                lastMerged.End = current.End;
                merged[^1] = lastMerged;
            }
            else
            {
                merged.Add(current);
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine(
            """
                // https://www.unicode.org/Public/UNIDATA/extracted/DerivedBidiClass.txt
                internal static readonly DirectionData[] DirectionTable =
                [
            """);

        const int secondColumnStart = 36;

        for (var i = 0; i < merged.Count; i++)
        {
            var (start, end, direction) = merged[i];
            var entry = $"new(0x{start:x}, 0x{end:x}, {direction})";

            var isLastEntry = i == merged.Count - 1;
            var entryWithComma = isLastEntry ? entry : entry + ",";

            if (i % 2 == 0)
            {
                // First entry on the line
                sb.Append("        ");
                sb.Append(entryWithComma);

                if (!isLastEntry)
                {
                    // Pad for second column alignment
                    var currentCol = 8 + entryWithComma.Length;
                    var padding = Math.Max(1, secondColumnStart - currentCol);
                    sb.Append(new string(' ', padding));
                }
                else
                {
                    sb.AppendLine();
                }
            }
            else
            {
                // Second entry on the line
                sb.AppendLine(entryWithComma);
            }
        }

        sb.Append("    ];");

        return sb.ToString();
    }

    public static string GenerateNamespaceTypes()
    {
        return 
            """
            // Bidi_Class — source: https://en.wikipedia.org/wiki/Template:Bidi_Class_(Unicode)
            internal enum Direction : byte
            {
                None, // unset / not assigned

                // BN: Boundary Neutral — Default ignorables, non-characters, control characters
                Bn,

                // CS: Common Number Separator — colon, comma, full stop, no-break space, …
                Cs,

                // ES: European Separator — plus sign, minus sign, …
                Es,

                // ON: Other Neutrals — All other characters (e.g., object replacement)
                On,

                // EN: European Number — European digits, Eastern Arabic-Indic digits, …
                En,

                // L: Left-to-Right — Most alphabetic/syllabic characters, Chinese, LRM, …
                L,

                // R: Right-to-Left — Hebrew, Adlam, N'Ko, RLM, …
                R,

                // NSM: Nonspacing Mark — nonspacing marks (Mn, Me)
                Nsm,

                // AL: Arabic Letter — Arabic, Syriac, Thaana alphabets, ALM, …
                Al,

                // AN: Arabic Number — Arabic-Indic digits, Arabic separators, Rumi digits, …
                An,

                // ET: European Number Terminator — degree sign, currency symbols, …
                Et,

                // WS: Whitespace — space, figure space, line separator, form feed, …
                Ws,

                // RLO: Right-to-Left Override — RLO character only (U+202E)
                Rlo,

                // LRO: Left-to-Right Override — LRO character only (U+202D)
                Lro,

                // PDF: Pop Directional Format — PDF character only (U+202C)
                Pdf,

                // RLE: Right-to-Left Embedding — RLE character only (U+202B)
                Rle,

                // RLI: Right-to-Left Isolate — RLI character only (U+2067)
                Rli,

                // FSI: First Strong Isolate — FSI character only (U+2068)
                Fsi,

                // PDI: Pop Directional Isolate — PDI character only (U+2069)
                Pdi,

                // LRI: Left-to-Right Isolate — LRI character only (U+2066)
                Lri,

                // B: Paragraph Separator — paragraph separator, newline handling
                B,

                // S: Segment Separator — tabs
                S,

                // LRE: Left-to-Right Embedding — LRE character only (U+202A)
                Lre
            }

            internal readonly struct DirectionData(int rangeStart, int rangeEnd, Direction direction)
            {
                public readonly int RangeStart = rangeStart;
                public readonly int RangeEnd = rangeEnd;
                public readonly Direction Direction = direction;
            }
            """;
    }
}
