using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using BenchmarkDotNet.Attributes;

namespace Dubzer.WhatwgUrl.Benchmark;

[MemoryDiagnoser]
public class ParseUrlBenchmarks
{
	public enum TestSet
	{
		UrlTestData,
		UrlTestDataValidOnly,
		Top100
	}

	[Params(TestSet.UrlTestData, TestSet.UrlTestDataValidOnly, TestSet.Top100)]
	[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
	public TestSet DataSet { get; set; }

	private (string, string?)[] _data = null!;

	[GlobalSetup]
	public void Setup()
	{
		if (DataSet is TestSet.UrlTestData or TestSet.UrlTestDataValidOnly)
		{
			using var file = File.OpenRead("Resources/urltestdata.json");
			var nodes = JsonNode.Parse(file)!.AsArray();
			var serializerOptions = new JsonSerializerOptions {PropertyNameCaseInsensitive = true};

			var temp = nodes
				.Where(static node => node!.GetValueKind() != JsonValueKind.String)
				.Select(node => node.Deserialize<UrlTestCase>(serializerOptions)!);

			if (DataSet == TestSet.UrlTestDataValidOnly)
			{
				temp = temp.Where(static urlTestCase => !urlTestCase.Failure);
			}

			_data = temp.Select(static urlTestCase => (urlTestCase.Input, urlTestCase.Base))
				.ToArray();
		}
		else if (DataSet == TestSet.Top100)
		{
			_data = File.ReadLines("Resources/top100.txt")
				.Select(static url => (url, (string?) null))
				.ToArray();
		}
	}

	[Benchmark]
	public void DomUrl_TryCreate()
	{
		foreach (var (input, baseUrl) in _data)
		{
			DomUrl.TryCreate(input, baseUrl, out _);
		}
	}
}
