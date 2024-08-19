using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Dubzer.WhatwgUrl.Codegen;

var sb = new StringBuilder();
sb.Append(
    /*lang=c#*/
    """
    using System.Collections.Frozen;
    using System.Collections.Generic;

    namespace Dubzer.WhatwgUrl.Uts46;

    internal class IdnaMappingTable
    {
        internal static readonly uint[] Rows =
        [
           
    """);

var dictSb = new StringBuilder();
dictSb.AppendLine(
    /*lang=c#*/
    """
        internal static readonly FrozenDictionary<uint, MappingTableRow> Dictionary = new Dictionary<uint, MappingTableRow>(8192)
        {
    """);

var numbersInRow = 0;
(uint CodePoint, IdnaStatus Status, string Mapping) rangeState = (0, IdnaStatus.DisallowedSTD3Valid, "");

foreach (var data in Parser.Parse(File.ReadLines("IdnaMappingTable.txt")))
{
    var codePoint = GetCodePoint(data[0]);
    var status = GetStatus(data[1]);
    var mapping = string.Empty;
    if (data.Length > 2)
    {
        var mappingData = data[2];
        var mappingCodePoints = mappingData
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => int.Parse(x, NumberStyles.HexNumber | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite, CultureInfo.InvariantCulture))
            .ToArray();

        mapping = string.Join("", mappingCodePoints.Select(char.ConvertFromUtf32));
        mapping = mapping
            .Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    if (codePoint.End == null)
    {
        dictSb.AppendLine(CultureInfo.InvariantCulture, $"        [0x{codePoint.Start:X}] = new MappingTableRow(IdnaStatus.{status}, \"{mapping}\"),");
    }
    else
    {
        if ((rangeState.Status, rangeState.Mapping) != (status, mapping))
        {
            if (numbersInRow >= 13)
            {
                sb.AppendLine();
                sb.Append("       ");
                numbersInRow = 0;
            }

            sb.Append(CultureInfo.InvariantCulture, $" 0x{rangeState.CodePoint:X},");
            dictSb.AppendLine(CultureInfo.InvariantCulture, $"        [0x{rangeState.CodePoint:X}] = new MappingTableRow(IdnaStatus.{rangeState.Status}, \"{rangeState.Mapping}\"),");
            numbersInRow++;
        }

        rangeState = (codePoint.End.Value, status, mapping);
    }
}

sb.AppendLine(CultureInfo.InvariantCulture, $" 0x{rangeState.CodePoint:X}");
dictSb.AppendLine(CultureInfo.InvariantCulture, $"        [0x{rangeState.CodePoint:X}] = new MappingTableRow(IdnaStatus.{rangeState.Status}, \"{rangeState.Mapping}\")");
dictSb.Append("    }.ToFrozenDictionary();");

sb.AppendLine("    ];");
sb.AppendLine();
sb.AppendLine(dictSb.ToString());
sb.AppendLine("}");

File.WriteAllText("IdnaMappingTable.g.cs", sb.ToString());
sb.Clear();


(uint Start, uint? End) GetCodePoint(string input)
{
    const string Separator = "..";
    var separatorIndex = input.IndexOf(Separator, StringComparison.Ordinal);
    if (separatorIndex == -1)
    {
        return (uint.Parse(input, NumberStyles.HexNumber, CultureInfo.InvariantCulture), null);
    }

    return (uint.Parse(input[..separatorIndex], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
        uint.Parse(input[(separatorIndex + Separator.Length)..], NumberStyles.HexNumber, CultureInfo.InvariantCulture));
}

IdnaStatus GetStatus(string input) => input switch
{
    "valid" => IdnaStatus.Valid,
    "ignored" => IdnaStatus.Ignored,
    "mapped" => IdnaStatus.Mapped,
    "deviation" => IdnaStatus.Deviation,
    "disallowed" => IdnaStatus.Disallowed,
    "disallowed_STD3_valid" => IdnaStatus.DisallowedSTD3Valid,
    "disallowed_STD3_mapped" => IdnaStatus.DisallowedSTD3Mapped,
    _ => throw new ArgumentOutOfRangeException(nameof(input))
};
