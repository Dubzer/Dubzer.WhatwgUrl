using System;
using System.Buffers;
using System.Text;
using Dubzer.WhatwgUrl.Uts46;

namespace Dubzer.WhatwgUrl;

internal static class HostParser
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(false);

    private static readonly SearchValues<char> ForbiddenHostCodePoints = SearchValues.Create([
        '\u0000', '\u0009', '\u000A', '\u000D', '\u0020', '#', '/', ':', '<', '>', '?', '@', '[', '\\', ']', '^', '|'
    ]);

    private static readonly SearchValues<char> ForbiddenDomainCodePoints = SearchValues.Create([
        '\x00', '\x01', '\x02', '\x03', '\x04', '\x05', '\x06', '\x07', '\x08', '\x09', '\x0A', '\x0B', '\x0C', '\x0D', '\x0E', '\x0F',
        '\x10', '\x11', '\x12', '\x13', '\x14', '\x15', '\x16', '\x17', '\x18', '\x19', '\x1A', '\x1B', '\x1C', '\x1D', '\x1E', '\x1F',
        '\x20', '#', '/', ':', '<', '>', '?', '@', '[', '\\', ']', '^', '|', '%', '\x7F'
    ]);

    // https://url.spec.whatwg.org/#ends-in-a-number-checker
    private static bool EndsInANumber(string input)
    {
        var parts = input.Split('.');

        var last = parts[^1];
        if (string.IsNullOrEmpty(last))
        {
            if (parts.Length == 1)
                return false;

            last = parts[^2];
        }

        var lastSpan = last.AsSpan();

        if (!string.IsNullOrEmpty(last) && !lastSpan.ContainsAnyExceptInRange('0', '9'))
            return true;

        return Ipv4Parser.ParseNumber(last) != -1;
    }

    // https://url.spec.whatwg.org/#concept-opaque-host-parser
    private static Result<string> ParseOpaqueHost(string input)
    {
        var span = input.AsSpan();
        if (span.ContainsAny(ForbiddenHostCodePoints))
            return Result<string>.Failure(UrlErrorCode.HostInvalidCodePoint);

        // TODO: If input contains a code point that is not a URL code point and not U+0025 (%), invalid-URL-unit validation error.

        var sb = new StringBuilder(input.Length);
        PercentEncoding.PercentEncode(input, PercentEncoding.InC0ControlPercentEncodeSet, sb);

        return Result<string>.Success(sb.ToString());
    }

    // https://url.spec.whatwg.org/#host-parsing
    public static Result<string> Parse(string input, bool isOpaque)
    {
        // 1. If input starts with U+005B ([), then:
        if (input.Length > 0 && input[0] == '[')
        {
            if (input[^1] != ']')
                return Result<string>.Failure(UrlErrorCode.Ipv6Unclosed);

            // Return IPv6 as a string here, unlike the spec,
            // which states to serialize a number to a string
            // only when serializing the host.
            var ipv6Result = Ipv6Parser.Parse(input[1..^1]);
            return ipv6Result
                ? Result<string>.Success(ipv6Result.Value!)
                : ipv6Result;
        }

        if (isOpaque)
            return ParseOpaqueHost(input);

        // 4.Let domain be the result of running UTF-8 decode without BOM on the percent-decoding of input.
        var domain = Utf8WithoutBom.GetString(PercentEncoding.PercentDecode(input));

        var asciiDomain = Idna.ToAscii(domain);
        if (string.IsNullOrEmpty(asciiDomain))
            return Result<string>.Failure(UrlErrorCode.DomainToAscii);

        var asciiDomainSpan = asciiDomain.AsSpan();
        // 7. If asciiDomain contains a forbidden domain code point, ..., return failure.
        if (asciiDomainSpan.ContainsAny(ForbiddenDomainCodePoints))
            return Result<string>.Failure(UrlErrorCode.DomainInvalidCodePoint);

        // Return IPv6 as a string here, unlike the spec,
        // which states to serialize a number to a string
        // only when serializing the host.
        if (EndsInANumber(asciiDomain))
            return Ipv4Parser.Parse(asciiDomain);

        return Result<string>.Success(asciiDomain.ToLowerInvariant());
    }
}
