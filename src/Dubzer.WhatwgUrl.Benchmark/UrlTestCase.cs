namespace Dubzer.WhatwgUrl.Benchmark;

public record UrlTestCase
{
	public bool Failure { get; init; }

	public required string Input { get; init; }
	public string? Base { get; init; }
	public string? Host { get; init; }
	public string? Hash { get; init; }
	public string? Hostname { get; init; }
	public string? Href { get; init; }
	public string? Origin { get; init; }
	public string? Password { get; init; }
	public string? Pathname { get; init; }
	public string? Port { get; init; }
	public string? Protocol { get; init; }
	public string? Search { get; init; }
	public string? Username { get; init; }
}
