using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;
using Dubzer.WhatwgUrl.Uts46;

namespace Dubzer.WhatwgUrl;

internal static class HostParser
{
    // https://url.spec.whatwg.org/#forbidden-host-code-point
    private static readonly SearchValues<char> ForbiddenHostCodePoints = SearchValues.Create([
        '\u0000', '\u0009', '\u000A', '\u000D', '\u0020', '#', '/', ':', '<', '>', '?', '@', '[', '\\', ']', '^', '|'
    ]);

    // https://url.spec.whatwg.org/#forbidden-domain-code-point
    private static readonly SearchValues<char> ForbiddenDomainCodePoints = SearchValues.Create([
        '\x00', '\x01', '\x02', '\x03', '\x04', '\x05', '\x06', '\x07', '\x08', '\x09', '\x0A', '\x0B', '\x0C', '\x0D', '\x0E', '\x0F',
        '\x10', '\x11', '\x12', '\x13', '\x14', '\x15', '\x16', '\x17', '\x18', '\x19', '\x1A', '\x1B', '\x1C', '\x1D', '\x1E', '\x1F',
        '\x20', '#', '/', ':', '<', '>', '?', '@', '[', '\\', ']', '^', '|', '%', '\x7F'
    ]);

    // https://url.spec.whatwg.org/#ends-in-a-number-checker
    private static bool EndsInANumber(ReadOnlySpan<char> input)
    {
        if (input.Length == 0)
            return false;

        char lastChar;
        var end = input.Length;
        // If the last item in parts is the empty string, then:
        if (input[^1] == '.')
        {
            // If parts’s size is 1, then return false
            if (input.Length <= 1)
                return false;

            lastChar = input[^2];
            // Remove the last item from parts.
            end--;
        }
        else
        {
            lastChar = input[^1];
            end = input.Length;
        }

        if (!char.IsAsciiHexDigit(lastChar) && lastChar is not ('x' or 'X'))
            return false;

        // 1. Let parts be the result of strictly splitting input on U+002E (.).
        var lastPartOffset = input[..end].LastIndexOf('.');

        var lastPart = input[(lastPartOffset + 1)..end];

        if (!lastPart.ContainsAnyExceptInRange('0', '9'))
            return true;

        return Ipv4Parser.ParseNumber(lastPart) != -1;
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
    // characters that are not allowed for executing fast path
#if NET9_0_OR_GREATER
    private static readonly SearchValues<string> FastPathInvalid = SearchValues.Create(["%", "--"], StringComparison.Ordinal);
#else
    private static readonly SearchValues<char> FastPathInvalid = SearchValues.Create("-%");
#endif

    // https://url.spec.whatwg.org/#host-parsing
    public static Result<string> Parse(ReadOnlySpan<char> inputSpan, bool isOpaque)
    {
        var input = inputSpan.ToString();
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

        var span = input.AsSpan();

        var asciiFastPath = false;
        // the fast path is valid when we don't need to do any punycode decoding
        if (input.Length < Consts.MaxLengthOnStack.Char
            && RuntimeHelpers.TryEnsureSufficientExecutionStack()
            && Ascii.IsValid(input))
        {
#if NET9_0_OR_GREATER
            asciiFastPath = !span.ContainsAny(FastPathInvalid);
#else

            var currentIndex = 0;
            while (true)
            {
                var slice = span[currentIndex..];
                var index = slice.IndexOfAny(FastPathInvalid);

                if (index == -1)
                {
                    asciiFastPath = true;
                    break;
                }

                if (slice[index] == '%' ||
                    // span[index..] is ['-', '-', ..]
                    slice[index] == '-' && slice.Length > index + 1 && slice[index + 1] == '-')
                    break;

                currentIndex += index + 1;
            }
#endif
        }

        string asciiDomainString;
        scoped ReadOnlySpan<char> asciiDomainSpan;
        if (asciiFastPath)
        {
            // reuse the existing string
            // assuming that most of the domains are already lowercased
            if (!span.ContainsAnyInRange('A', 'Z'))
            {
                asciiDomainString = input;
                asciiDomainSpan = span;
            }
            else
            {
                asciiDomainString = string.Create(input.Length, input, static (dest, src) =>
                {
                    // this is safe because we've already checked that the string is ASCII
                    Ascii.ToLower(src.AsSpan(), dest, out _);
                });
                asciiDomainSpan = asciiDomainString.AsSpan();
            }
        }
        else
        {
            // 4.Let domain be the result of running UTF-8 decode without BOM on the percent-decoding of input.
            var domain = PercentEncoding.PercentDecode(input);

            var asciiDomain = Idna.ToAscii(domain);
            if (string.IsNullOrEmpty(asciiDomain))
                return Result<string>.Failure(UrlErrorCode.DomainToAscii);

            asciiDomainString = asciiDomain;
            asciiDomainSpan = asciiDomain.AsSpan();
        }

        // 7. If asciiDomain contains a forbidden domain code point, ..., return failure.
        if (asciiDomainSpan.ContainsAny(ForbiddenDomainCodePoints))
            return Result<string>.Failure(UrlErrorCode.DomainInvalidCodePoint);

        // Return IPv6 as a string here, unlike the spec,
        // which states to serialize a number to a string
        // only when serializing the host.
        if (EndsInANumber(asciiDomainSpan))
            return Ipv4Parser.Parse(asciiDomainString);

        return Result<string>.Success(asciiDomainString);
    }
}
