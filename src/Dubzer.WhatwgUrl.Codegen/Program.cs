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

    /*lang=c#*/
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
            .ToArray();

        mapping = string.Join("", mappingCodePoints.Select(char.ConvertFromUtf32));
        mapping = mapping
    }

}

sb.Clear();



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
