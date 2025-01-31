using System;
using System.Collections.Generic;
using System.Linq;

namespace Dubzer.WhatwgUrl.Codegen;

/// <summary>
/// Reads unicode tables
/// </summary>
internal static class Reader
{
    internal static IEnumerable<string[]> Read(IEnumerable<string> lines)
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
