using System.Collections.Generic;
using System.IO;
using System.Linq;
using Argon;
using Dubzer.WhatwgUrl.Tests.Models;
using Dubzer.WhatwgUrl.Uts46;
using Xunit;

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
        return JArray.Parse(file)
            .Where(x => x.Type == JTokenType.Object)
            .Select(x => x.ToObject<Uts46TestCase>())
            .Select(static x => new object[] { x! });

    }
}
