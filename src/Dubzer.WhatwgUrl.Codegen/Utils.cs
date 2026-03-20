using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Dubzer.WhatwgUrl.Codegen;

internal sealed class UintArrayComparer : IEqualityComparer<uint[]>
{
    public bool Equals(uint[]? x, uint[]? y)
    {
        if (x == null || y == null)
            return x == y;

        return x.Length == y.Length && x.SequenceEqual(y);
    }

    public int GetHashCode(uint[] obj)
    {
        int hash = 17;
        foreach (var item in obj)
        {
            hash = hash * 31 + item.GetHashCode();
        }

        return hash;
    }
}

internal static class CodegenHelpers
{
    internal static string GenerateFrozenSet(string name, List<int> values, string hexFormat = "X5")
    {
        var sb = new StringBuilder();
        sb.AppendLine(
            /*lang=c#*/
            $$"""
                  public static readonly FrozenSet<int> {{name}} = new[]
                  {
              """);
        sb.Append("        ");

        var lineLength = 8; // initial indent length
        const int maxLineLength = 95;

        for (var i = 0; i < values.Count; i++)
        {
            var item = string.Format(CultureInfo.InvariantCulture, $"0x{{0:{hexFormat}}}", values[i]);

            if (i != values.Count - 1)
                item += ", ";

            if (lineLength + item.Length > maxLineLength)
            {
                sb.AppendLine();
                sb.Append("        ");
                lineLength = 8;
            }

            sb.Append(item);
            lineLength += item.Length;
        }

        sb.AppendLine();
        sb.AppendLine("    }.ToFrozenSet();");
        return sb.ToString();
    }
}
