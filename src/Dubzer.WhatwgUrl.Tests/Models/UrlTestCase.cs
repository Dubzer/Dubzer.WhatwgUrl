using Xunit.Abstractions;

namespace Dubzer.WhatwgUrl.Tests.Models;

public record UrlTestCase : IXunitSerializable
{
    public bool Failure { get; set; }

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

    public void Deserialize(IXunitSerializationInfo info) { }

    public void Serialize(IXunitSerializationInfo info)
    {
        info.AddValue("Failure", Failure);
        info.AddValue("Input", Input);
        info.AddValue("Base", Base);
        info.AddValue("Host", Host);
        info.AddValue("Hash", Hash);
        info.AddValue("Hostname", Hostname);
        info.AddValue("Href", Href);
        info.AddValue("Origin", Origin);
        info.AddValue("Password", Password);
        info.AddValue("Pathname", Pathname);
        info.AddValue("Port", Port);
        info.AddValue("Protocol", Protocol);
        info.AddValue("Search", Search);
        info.AddValue("Username", Username);
    }
}
