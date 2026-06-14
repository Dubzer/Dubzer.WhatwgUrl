using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using BenchmarkDotNet.Attributes;
using Dubzer.WhatwgUrl.Uts46;

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
	[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
	public TestSet DataSet { get; set; }

	[GlobalSetup]
	public void Setup()
	{
		switch (DataSet)
		{
			case TestSet.FullIdnaTestV2 or TestSet.ValidOnlyIdnaTestV2:
			{
				using var file = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Resources", "IdnaTestV2.json"));
				var nodes = JsonNode.Parse(file)!.AsArray();
				var temp = nodes
					.Where(static node => node!.GetValueKind() != JsonValueKind.String)
					.Select(static node => node.Deserialize(BenchmarkJsonContext.Default.Uts46TestCase)!);

				if (DataSet == TestSet.ValidOnlyIdnaTestV2)
					temp = temp.Where(x => x.Output != null);

				_data = [.. temp.Select(x => x.Input)];

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

internal sealed class Uts46TestCase
{
	public required string Input { get; init; }
	public string? Output { get; init; }
}
