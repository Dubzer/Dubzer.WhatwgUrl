
namespace Dubzer.WhatwgUrl.Benchmark;

[MemoryDiagnoser]
{
    [Benchmark]
    public DomUrl AllParts()
    {
        // All parts with bidi host

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
