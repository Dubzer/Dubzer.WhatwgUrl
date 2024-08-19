using Xunit.Abstractions;

namespace Dubzer.WhatwgUrl.Tests.Models;

public record Uts46TestCase : IXunitSerializable
{
    public required string Input { get; init; }
    public string? Output { get; init; }

    public void Deserialize(IXunitSerializationInfo info) { }

    public void Serialize(IXunitSerializationInfo info)
    {
        info.AddValue(nameof(Input), Input);
        info.AddValue(nameof(Output), Output);
        info.AddValue(nameof(Comment), Comment);
    }
