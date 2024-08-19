using System;
using System.Collections.Generic;
using System.Linq;

namespace Dubzer.WhatwgUrl.Codegen;

/// <summary>
/// Parses unicode tables
/// </summary>
internal static class Parser
{
    internal static IEnumerable<string[]> Parse(IEnumerable<string> lines)
    {
        foreach (var line in lines)
        {
            if (line.StartsWith('#') || string.IsNullOrWhiteSpace(line))
                continue;

            var content = line.Split('#')[0].Trim();
            yield return content.Split(';', StringSplitOptions.TrimEntries).ToArray();
        }
    }
}
