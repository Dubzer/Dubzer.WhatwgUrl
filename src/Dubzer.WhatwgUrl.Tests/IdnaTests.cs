using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dubzer.WhatwgUrl.Tests.Models;
using Dubzer.WhatwgUrl.Uts46;
using Xunit;
using Xunit.Abstractions;

namespace Dubzer.WhatwgUrl.Tests;

public class IdnaTests
{
    private readonly ITestOutputHelper _output;

    public IdnaTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void ToAscii(Uts46TestCase testCase)
    {
        _output.WriteLine($"Input: {testCase.Input}");
        if (testCase.Comment != null)
        {
            _output.WriteLine($"Comment: {testCase.Comment}");
        }

        var result = Idna.ToAscii(testCase.Input);

        Assert.Equal(testCase.Output, result);
    }

    public static IEnumerable<object[]> Cases()
    {
        var file = File.ReadAllText("Resources/IdnaTestV2.json");
        var nodes = JsonNode.Parse(file)!.AsArray();
        return nodes
            .Where(static x => x!.GetValueKind() != JsonValueKind.String)
            .Select(static x => x.Deserialize<Uts46TestCase>((JsonSerializerOptions) new() {PropertyNameCaseInsensitive = true}))
            // this is fine for URL parsing
            .Where(x => !x!.Input.Contains('?', StringComparison.InvariantCulture))
            .Select(static x => new object[] { x! });
    }
}
