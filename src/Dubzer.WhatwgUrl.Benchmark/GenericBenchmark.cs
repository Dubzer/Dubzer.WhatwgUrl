using System.Diagnostics.CodeAnalysis;
using BenchmarkDotNet.Attributes;

namespace Dubzer.WhatwgUrl.Benchmark;

[MemoryDiagnoser]
[SuppressMessage("Performance", "CA1822:Mark members as static",
    Justification = "BenchmarkDotNet requires non-static members")]
public class GenericBenchmark
{
    [Benchmark]
    public DomUrl AllParts()
    {
        // All parts with bidi host
        var url = new DomUrl("http://user:pass@\u0627\u0645\u062a\u062d\u0627\u0646:21/bar;par?b#c");

        // intentionally calling the getters
        // otherwise they won't be considered when optimizing the code
        _ = url.Pathname;
        _ = url.Origin;
        _ = url.Host;
        _ = url.Hostname;
        _ = url.Href;

        return url;
    }

    [Benchmark]
    public DomUrl Ipv4Parsing() => new("http://192.168.0.1");

    [Benchmark]
    public DomUrl Ipv6Parsing() => new("http://[2606:4700:4700::1111]");
}
