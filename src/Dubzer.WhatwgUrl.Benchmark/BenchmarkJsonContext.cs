using System.Text.Json.Serialization;

namespace Dubzer.WhatwgUrl.Benchmark;

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(BenchmarkUrlTestCase))]
[JsonSerializable(typeof(Uts46TestCase))]
internal sealed partial class BenchmarkJsonContext : JsonSerializerContext;
