
/// <summary>
/// Parses unicode tables
/// </summary>
{
    {
        foreach (var line in lines)
        {
            if (line.StartsWith('#') || string.IsNullOrWhiteSpace(line))
                continue;

            var content = line.Split('#')[0].Trim();
            yield return content.Split(';', StringSplitOptions.TrimEntries).ToArray();
        }
    }
