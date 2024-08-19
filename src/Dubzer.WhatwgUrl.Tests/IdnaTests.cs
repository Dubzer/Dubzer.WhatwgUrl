using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Dubzer.WhatwgUrl.Tests.Models;
using Xunit;
using Xunit.Abstractions;

namespace Dubzer.WhatwgUrl.Tests;

{
    [Theory]
    [MemberData(nameof(Cases))]
    public void ToAscii(Uts46TestCase testCase)
    {
        _output.WriteLine($"Input: {testCase.Input}");

        Assert.Equal(testCase.Output, result);
    }

    public static IEnumerable<object[]> Cases()
    {
        var file = File.ReadAllText("Resources/IdnaTestV2.json");
        return nodes
            // this is fine for URL parsing
            .Where(x => !x!.Input.Contains('?', StringComparison.InvariantCulture))
    }
