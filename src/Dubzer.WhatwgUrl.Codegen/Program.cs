using System.Globalization;
using System.IO;
using System.Text;
using Dubzer.WhatwgUrl.Codegen;

var lines = Reader.Read(File.ReadLines("IdnaMappingTable.txt"));
var rows = IdnaParser.GetValues(lines);
var arrays = IdnaTableGenerator.GetValues(rows);

var sb = new StringBuilder();
sb.Append(
    /*lang=c#*/
    """
    using System;

    namespace Dubzer.WhatwgUrl.Uts46;

    internal class IdnaMappingTable
    {
        /// <summary>
        /// This flag is used in <see cref="MainRefs"/> table
        /// </summary>
        internal const uint RefBoolPackFlag = 0b1000000000000000;
        
    """);

sb.AppendLine();
sb.Append(
    /*lang=c#*/
    """
        internal static ReadOnlySpan<uint> MainRefs =>
        [
           
    """);

AppendFormattedNumbers(sb, arrays.MainRefs, 14, "0x{0:X4}");

sb.AppendLine();
sb.AppendLine("    ];");
sb.AppendLine();
sb.Append(
    /*lang=c#*/
    """
        internal static ReadOnlySpan<ulong> BoolPacks =>
        [
           
    """);

AppendFormattedNumbers(sb, arrays.BoolPacks, 5, "0x{0:X16}");

sb.AppendLine();
sb.AppendLine("    ];");
sb.AppendLine();

sb.Append(
    /*lang=c#*/
    """
        internal const uint RefBlockValid = 0x80000002;

        internal const uint RefBlockInvalid = 0x80000003;

        internal const uint RefBlockIgnored = 0x80000004;
        
        internal static ReadOnlySpan<uint> RefBlocks =>
        [
           
    """);

AppendFormattedNumbers(sb, arrays.RefBlocks, 9, "0x{0:X8}");

sb.AppendLine();
sb.AppendLine("    ];");
sb.AppendLine();

sb.Append(
    /*lang=c#*/
    """
        internal static readonly string Mappings =
           "
    """);

var inRow = 0;
for (var i = 0; i < arrays.Mappings.Length; i++)
{
    var mapping = arrays.Mappings[i];
    // TODO: i'm not sure why, but when encoding it as a string something breaks
    // compiler bug lol?
    if (inRow >= 18)// && !char.IsLowSurrogate(mapping))
    {
        sb.Append("\" +");
        sb.AppendLine();
        sb.Append("       \"");
        inRow = 0;
    }

    sb.Append($"\\u{(ushort)mapping:X4}");
    inRow++;
}

sb.Append("\";");
sb.AppendLine();
sb.AppendLine("}");

File.WriteAllText("IdnaMappingTable.g.cs", sb.ToString(), Encoding.UTF8);
sb.Clear();

static void AppendFormattedNumbers<T>(StringBuilder sb, T[] numbers, int maxInRow, string format)
{
    var inRow = 0;
    for (var i = 0; i < numbers.Length; i++)
    {
        var number = numbers[i];

        if (inRow >= maxInRow)
        {
            sb.AppendLine();
            sb.Append("       ");
            inRow = 0;
        }

        sb.Append(' ');
        sb.AppendFormat(CultureInfo.InvariantCulture, format, number);

        if (i != numbers.Length - 1)
            sb.Append(',');

        inRow++;
    }
}