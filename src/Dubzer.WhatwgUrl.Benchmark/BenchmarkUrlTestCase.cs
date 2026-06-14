namespace Dubzer.WhatwgUrl.Benchmark;

internal sealed class BenchmarkUrlTestCase
{
	public bool Failure { get; set; }
	public required string Input { get; init; }
	public string? Base { get; init; }
}
