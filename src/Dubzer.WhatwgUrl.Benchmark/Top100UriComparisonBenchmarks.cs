using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BenchmarkDotNet.Attributes;

namespace Dubzer.WhatwgUrl.Benchmark;

[MemoryDiagnoser]
public class Top100UriComparisonBenchmarks
{
	private string[] _top100 = null!;
	private string[] _bothValid = null!;

	[GlobalSetup]
	public void Setup()
	{
		var top100 = File.ReadLines("Resources/top100.txt").ToArray();
		var bothValid = new List<string>(top100.Length);
		var domOnly = 0;
		var systemUriOnly = 0;
		var bothInvalid = 0;

		foreach (var input in top100)
		{
			var domValid = DomUrl.TryCreate(input, out _);
			var systemUriValid = Uri.TryCreate(input, UriKind.Absolute, out _);

			if (domValid && systemUriValid)
			{
				bothValid.Add(input);
			}
			else if (domValid)
			{
				domOnly++;
			}
			else if (systemUriValid)
			{
				systemUriOnly++;
			}
			else
			{
				bothInvalid++;
			}
		}

		_top100 = top100;
		_bothValid = [.. bothValid];

		Console.WriteLine($"Top100 total: {_top100.Length}");
		Console.WriteLine($"Both valid: {_bothValid.Length}");
		Console.WriteLine($"DomUrl only: {domOnly}");
		Console.WriteLine($"System.Uri only: {systemUriOnly}");
		Console.WriteLine($"Both invalid: {bothInvalid}");
	}

	[Benchmark(Baseline = true)]
	public void DomUrl_TryCreate_BothValid()
	{
		foreach (var input in _bothValid)
		{
			DomUrl.TryCreate(input, out _);
		}
	}

	[Benchmark]
	public void SystemUri_TryCreate_BothValid()
	{
		foreach (var input in _bothValid)
		{
			Uri.TryCreate(input, UriKind.Absolute, out _);
		}
	}

	[Benchmark]
	public void DomUrl_TryCreate_AllTop100()
	{
		foreach (var input in _top100)
		{
			DomUrl.TryCreate(input, out _);
		}
	}

	[Benchmark]
	public void SystemUri_TryCreate_AllTop100()
	{
		foreach (var input in _top100)
		{
			Uri.TryCreate(input, UriKind.Absolute, out _);
		}
	}
}
