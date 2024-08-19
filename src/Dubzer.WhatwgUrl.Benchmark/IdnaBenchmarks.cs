using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using BenchmarkDotNet.Attributes;
using Dubzer.WhatwgUrl.Uts46;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Dubzer.WhatwgUrl.Benchmark;

[MemoryDiagnoser]
public class IdnaBenchmarks
{
	private string[] _data = null!;

	public enum TestSet
	{
		FullIdnaTestV2,
		ValidOnlyIdnaTestV2,
		AdaBenchmark
	}

	[Params(TestSet.FullIdnaTestV2, TestSet.ValidOnlyIdnaTestV2, TestSet.AdaBenchmark)]
	public TestSet DataSet { get; set; }

	[GlobalSetup]
	public void Setup()
	{
		switch (DataSet)
		{
			case TestSet.FullIdnaTestV2 or TestSet.ValidOnlyIdnaTestV2:
			{
				var serializerOptions = new JsonSerializerOptions {PropertyNameCaseInsensitive = true};
				var file = File.ReadAllText("Resources/IdnaTestV2.json");
				var nodes = JsonNode.Parse(file)!.AsArray();
				var temp = nodes
					.Where(x => x!.GetValueKind() != JsonValueKind.String)
					.Select(x => x.Deserialize<Uts46TestCase>(serializerOptions)!);

				if (DataSet == TestSet.ValidOnlyIdnaTestV2)
				{
					temp = temp.Where(x => x.Output != null);
				}

				_data = temp.Select(x => x.Input).ToArray();

				break;
			}
			case TestSet.AdaBenchmark:
				_data = [
					"-x.xn--zca",
					"xn--zca.xn--zca",
					"xn--mgba3gch31f060k",
					"xn--1ch",
					"x-.\xc3\x9f",
					"me\xc3\x9f\x61\x67\x65\x66\x61\x63\x74\x6f\x72\x79\x2e\x63\x61"
				];

				break;
		}
	}

	[Benchmark]
	public void Idna_ToAscii()
	{
		foreach (var input in _data)
		{
			_ = Idna.ToAscii(input);
		}
	}
}

public record Uts46TestCase
{
	public required string Input { get; init; }
	public string? Output { get; init; }
}
