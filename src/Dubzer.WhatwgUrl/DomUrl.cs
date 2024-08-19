using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Dubzer.WhatwgUrl;

/// <summary>
/// This class represents a parsed URL object
/// </summary>
public class DomUrl
{
    /// <summary>
    /// Returns the origin of this URL
    /// </summary>
    /// <include file='DomUrl.Doc.xml' path='links/remarks[@name="Origin"]'/>
    public string Origin => _internalUrl.SerializeOrigin();

    /// <summary>
    /// Returns the username of this URL
    /// </summary>
    /// <include file='DomUrl.Doc.xml' path='links/remarks[@name="Username"]'/>
    public string Username => _internalUrl.Username;

    /// <summary>
    /// Returns the password of this URL
    /// </summary>
    /// <include file='DomUrl.Doc.xml' path='links/remarks[@name="Password"]'/>
    public string Password => _internalUrl.Password;

    /// <summary>
    /// Returns the host of this URL. It includes the port
    /// </summary>
    /// <include file='DomUrl.Doc.xml' path='links/remarks[@name="Host"]'/>
    public string Host => _internalUrl.SerializeHost();

    /// <summary>
    /// Returns the hostname of this URL
    /// </summary>
    /// <include file='DomUrl.Doc.xml' path='links/remarks[@name="Hostname"]'/>
    public string Hostname => _internalUrl.Host ?? "";

    /// <summary>
    /// Returns the port of this URL
    /// </summary>
    /// <include file='DomUrl.Doc.xml' path='links/remarks[@name="Port"]'/>
    public string Port => _internalUrl.Port?.ToString(CultureInfo.InvariantCulture) ?? "";

    /// <summary>
    /// Returns the path of this URL
    /// </summary>
    /// <include file='DomUrl.Doc.xml' path='links/remarks[@name="Pathname"]'/>
    public string Pathname => _internalUrl.SerializePathname();

    /// <summary>
    /// Returns the search (query) of this URL
    /// </summary>
    /// <include file='DomUrl.Doc.xml' path='links/remarks[@name="Search"]'/>
    public string Search => string.IsNullOrEmpty(_internalUrl.Query) ? "" : $"?{_internalUrl.Query}";

    /// <summary>
    /// Returns the hash (fragment) of this URL
    /// </summary>
    /// <include file='DomUrl.Doc.xml' path='links/remarks[@name="Hash"]'/>
    public string Hash => string.IsNullOrEmpty(_internalUrl.Fragment) ? "" : $"#{_internalUrl.Fragment}";

    /// <summary>
    /// Returns the protocol (scheme) of this URL
    /// </summary>
    /// <include file='DomUrl.Doc.xml' path='links/remarks[@name="Protocol"]'/>
    public string Protocol => $"{_internalUrl.Scheme}:";

    /// <summary>
    /// Returns a serialized representation of this URL
    /// </summary>
    /// <include file='DomUrl.Doc.xml' path='links/remarks[@name="Href"]'/>
    public string Href => _internalUrl.SerializeUrl();

    private readonly InternalUrl _internalUrl;

    // Used for TryCreate methods
    private DomUrl(InternalUrl parsed)
    {
        _internalUrl = parsed;
    }

    /// <summary>
    /// Parses the <paramref name="input"/> and the <paramref name="baseUrl"/>,
    /// and initializes an instance of <see cref="DomUrl" />.
    /// </summary>
    /// <param name="input">The URL to be parsed</param>
    /// <param name="baseUrl">The base URL to resolve <paramref name="input"/> against</param>
    /// <exception cref="InvalidUrlException">
    /// Thrown when <paramref name="input"/> or <paramref name="baseUrl"/> are invalid
    /// </exception>
    public DomUrl(string input, string? baseUrl = null)
    {
        ArgumentNullException.ThrowIfNull(input);

        InternalUrl? parsedBaseUrl = null;
        if (!string.IsNullOrEmpty(baseUrl))
        {
            parsedBaseUrl = ParseUrl(baseUrl);
        }

        _internalUrl = ParseUrl(input, parsedBaseUrl);
    }

    private static InternalUrl ParseUrl(string input, InternalUrl? baseUrl = null)
    {
        Result<InternalUrl> urlResult = InputUtils.GetParser(input).Parse(input, baseUrl);

        if (!urlResult)
            throw new InvalidUrlException("The URL is invalid.", urlResult.Error!.Value);

        return urlResult.Value!;
    }

    /// <summary>
    /// Parses the <paramref name="input"/> relative to <paramref name="baseUrl"/>,
    /// and initializes an instance of <see cref="DomUrl" />.
    /// </summary>
    /// <exception cref="InvalidUrlException">
    /// Thrown when <paramref name="input"/> is invalid
    /// </exception>
    public DomUrl(string input, DomUrl baseUrl)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(baseUrl);

        _internalUrl = ParseUrl(input, baseUrl._internalUrl);
    }

    /// <summary>Gets a serialized representation of this URL.
    /// Is equivalent to getting the <see cref="Href"/> property.
    /// </summary>
    /// <seealso cref="Href"/>
    public override string ToString() => Href;

    /// <summary>
    /// Parses the <paramref name="input"/>. Returns <see langword="true"/> if the URL is valid
    /// </summary>
    /// <include file='DomUrl.Doc.xml' path='links/remarks[@name="TryCreate"]'/>
    /// <param name="input">The URL to be parsed</param>
    /// <param name="result">When this method returns <see langword="true"/>,
    /// contains the created DomUrl</param>
    /// <returns><see langword="true" /> if the <see cref="DomUrl" /> was successfully created;
    /// otherwise, <see langword="false" /></returns>
    public static bool TryCreate(string input, [NotNullWhen(true)] out DomUrl? result)
    {
        ArgumentNullException.ThrowIfNull(input);

        var parsed = InputUtils.GetParser(input).Parse(input);
        if (!parsed)
        {
            result = null;
            return false;
        }

        result = new DomUrl(parsed.Value!);
        return true;
    }

    /// <summary>
    /// Parses the <paramref name="input"/>. Returns <see langword="true"/> if the URL is valid
    /// </summary>
    /// <include file='DomUrl.Doc.xml' path='links/remarks[@name="TryCreateObjectBase"]'/>
    /// <param name="input">The URL to be parsed</param>
    /// <param name="baseUrl">The base URL to resolve <paramref name="input"/> against</param>
    /// <param name="result">When this method returns <see langword="true"/>,
    /// contains the created DomUrl</param>
    /// <returns><see langword="true" /> if the <see cref="DomUrl" /> was successfully created;
    /// otherwise, <see langword="false" /></returns>
    public static bool TryCreate(string input, DomUrl baseUrl, [NotNullWhen(true)] out DomUrl? result)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(baseUrl);

        var parsed = InputUtils.GetParser(input).Parse(input, baseUrl._internalUrl);
        if (!parsed)
        {
            result = null;
            return false;
        }

        result = new DomUrl(parsed.Value!);
        return true;
    }

    /// <summary>
    /// Parses the <paramref name="input"/> and <paramref name="baseUrl"/>. Returns <see langword="true"/> if the URL is valid
    /// </summary>
    /// <include file='DomUrl.Doc.xml' path='links/remarks[@name="TryCreateStringBase"]'/>
    /// <param name="input">The URL to be parsed</param>
    /// <param name="baseUrl">The base URL to resolve <paramref name="input"/> against</param>
    /// <param name="result">When this method returns <see langword="true"/>,
    /// contains the created DomUrl</param>
    /// <returns><see langword="true" /> if the <see cref="DomUrl" /> was successfully created;
    /// otherwise, <see langword="false" /></returns>
    public static bool TryCreate(string input, string? baseUrl, [NotNullWhen(true)] out DomUrl? result)
    {
        InternalUrl? parsedBaseUrl = null;
        if (!string.IsNullOrEmpty(baseUrl))
        {
            var baseResult = InputUtils.GetParser(input).Parse(baseUrl);
            if (!baseResult)
            {
                result = null;
                return false;
            }

            parsedBaseUrl = baseResult.Value;
        }

        var urlResult = InputUtils.GetParser(input).Parse(input, parsedBaseUrl);
        if (!urlResult)
        {
            result = null;
            return false;
        }

        result = new DomUrl(urlResult.Value!);
        return true;
    }
}
