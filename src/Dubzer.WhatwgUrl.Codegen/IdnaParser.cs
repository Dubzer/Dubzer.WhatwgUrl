using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Dubzer.WhatwgUrl.Codegen;

internal static class IdnaParser
{
    public static IEnumerable<IdnaMappingTableRow> GetValues(IEnumerable<string[]> lines)
    {
        foreach (var line in lines)
        {
            var codePoint = GetCodePoint(line[0]);
            var status = GetStatus(line[1]);
            var mapping = string.Empty;

            if (line.Length > 2)
            {
                var mappingData = line[2];
                var mappingCodePoints = mappingData
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => int.Parse(x,
                        NumberStyles.HexNumber | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite,
                        CultureInfo.InvariantCulture))
                    .ToArray();

                mapping = string.Join("", mappingCodePoints.Select(x => new Rune(x).ToString()));
                mapping = mapping
                    .Replace(@"\", @"\\", StringComparison.Ordinal)
                    .Replace("\"", "\\\"", StringComparison.Ordinal);
            }

            for (var i = codePoint.Start; i <= (codePoint.End ?? codePoint.Start); i++)
            {
                yield return new IdnaMappingTableRow(status, $"{mapping}");
            }
        }
    }

    private static (uint Start, uint? End) GetCodePoint(string input)
    {
        const string separator = "..";
        var separatorIndex = input.IndexOf(separator, StringComparison.Ordinal);
        if (separatorIndex == -1)
        {
            return (uint.Parse(input, NumberStyles.HexNumber, CultureInfo.InvariantCulture), null);
        }

        return (uint.Parse(input[..separatorIndex], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            uint.Parse(input[(separatorIndex + separator.Length)..], NumberStyles.HexNumber,
                CultureInfo.InvariantCulture));
    }

    private static IdnaStatus GetStatus(string input) => input switch
    {
        "valid" => IdnaStatus.Valid,
        "ignored" => IdnaStatus.Ignored,
        "mapped" => IdnaStatus.Mapped,
        "deviation" => IdnaStatus.Deviation,
        "disallowed" => IdnaStatus.Disallowed,
        "disallowed_STD3_valid" => IdnaStatus.Valid,
        "disallowed_STD3_mapped" => IdnaStatus.Mapped,
        _ => throw new ArgumentOutOfRangeException(nameof(input))
    };
}