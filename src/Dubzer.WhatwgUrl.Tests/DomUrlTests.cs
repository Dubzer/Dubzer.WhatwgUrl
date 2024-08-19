using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dubzer.WhatwgUrl.Tests.Models;
using Xunit;
using Xunit.Abstractions;

namespace Dubzer.WhatwgUrl.Tests;

public class DomUrlTests
{
    private readonly ITestOutputHelper _output;
    private readonly JsonSerializerOptions _jsonSerializerOptions = new() {WriteIndented = true};

    public DomUrlTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [MemberData(nameof(UrlCases))]
    public void Constructor(UrlTestCase testCase)
    {
        _output.WriteLine(JsonSerializer.Serialize(testCase, _jsonSerializerOptions));

        if (testCase.Failure)
        {
            Assert.Throws<InvalidUrlException>(() => new DomUrl(testCase.Input, testCase.Base));
            return;
        }

        var domUrl = new DomUrl(testCase.Input, testCase.Base);

        _output.WriteLine(JsonSerializer.Serialize(domUrl, _jsonSerializerOptions));


        Assert.Equal(testCase.Port, domUrl.Port);
        Assert.Equal(testCase.Username, domUrl.Username);
        Assert.Equal(testCase.Password, domUrl.Password);
        Assert.Equal(testCase.Search, domUrl.Search);
        Assert.Equal(testCase.Host, domUrl.Host);
        Assert.Equal(testCase.Pathname, domUrl.Pathname);
        Assert.Equal(testCase.Protocol, domUrl.Protocol);
        Assert.Equal(testCase.Host, domUrl.Host);
        Assert.Equal(testCase.Hostname, domUrl.Hostname);
        Assert.Equal(testCase.Hash, domUrl.Hash);
        Assert.Equal(testCase.Href, domUrl.Href);

        if (testCase.Origin != null)
            Assert.Equal(testCase.Origin, domUrl.Origin);
    }

    [Theory]
    [MemberData(nameof(UrlCases))]
    public void TryCreate(UrlTestCase testCase)
    {
        var success = DomUrl.TryCreate(testCase.Input, testCase.Base, out var domUrl);

        if (!success)
        {
            Assert.Equal(!success, testCase.Failure);
            return;
        }

        Assert.Equal(testCase.Port, domUrl!.Port);
        Assert.Equal(testCase.Username, domUrl.Username);
        Assert.Equal(testCase.Password, domUrl.Password);
        Assert.Equal(testCase.Search, domUrl.Search);
        Assert.Equal(testCase.Host, domUrl.Host);
        Assert.Equal(testCase.Pathname, domUrl.Pathname);
        Assert.Equal(testCase.Protocol, domUrl.Protocol);
        Assert.Equal(testCase.Host, domUrl.Host);
        Assert.Equal(testCase.Hostname, domUrl.Hostname);
        Assert.Equal(testCase.Hash, domUrl.Hash);
        Assert.Equal(testCase.Href, domUrl.Href);

        if (testCase.Origin != null)
            Assert.Equal(testCase.Origin, domUrl.Origin);
    }

    [Fact]
    public void ToStringReturnsHref()
    {
        const string testCase = "http://www.google.com/foo?bar=baz# »";
        const string expected = "http://www.google.com/foo?bar=baz#%20%C2%BB";

        var domUrl = new DomUrl(testCase);

        Assert.Equal(expected, domUrl.Href);
        Assert.Equal(expected, domUrl.ToString());
    }

    [Fact]
    public void DomUrlAsBaseInConstructor()
    {
        const string url = "search?q=cat";
        const string baseUrl = "https://google.com";
        var parsedBaseUrl = new DomUrl(baseUrl);

        var withDomUrlBase = new DomUrl(url, parsedBaseUrl);
        var withStringUrlBase = new DomUrl(url, baseUrl);

        Assert.Equivalent(withStringUrlBase, withDomUrlBase, true);
    }

    [Fact]
    public void DomUrlAsBaseInTryCreate()
    {
        const string url = "search?q=cat";
        const string baseUrl = "https://google.com";
        var parsedBaseUrl = new DomUrl(baseUrl);

        DomUrl.TryCreate(url, parsedBaseUrl, out var withDomUrlBase);
        DomUrl.TryCreate(url, baseUrl, out var withStringUrlBase);

        Assert.Equivalent(withStringUrlBase, withDomUrlBase, true);
    }

    public static TheoryData<UrlTestCase> UrlCases()
    {
        var file = File.ReadAllText("Resources/urltestdata.json");
        var nodes = JsonNode.Parse(file)!.AsArray();

        return new TheoryData<UrlTestCase>(nodes
            .Where(static x => x!.GetValueKind() != JsonValueKind.String)
            .Select(static x => x.Deserialize<UrlTestCase>((JsonSerializerOptions) new() {PropertyNameCaseInsensitive = true})!));
    }
}
