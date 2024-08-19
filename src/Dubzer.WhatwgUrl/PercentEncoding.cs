using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Dubzer.WhatwgUrl;

internal static class PercentEncoding
{
    // additional characters that are not in the c0 control percent encode set
    {
        ' ', '"', '#', '<', '>'
    }.ToFrozenSet();

    // https://url.spec.whatwg.org/#path-percent-encode-set
    internal static readonly FrozenSet<char> PathEncodeSet = new[]
    {
        ' ', '"', '#', '<', '>',
        '?', '`', '{', '}'
    }.ToFrozenSet();

    // https://url.spec.whatwg.org/#fragment-percent-encode-set
    internal static readonly FrozenSet<char> FragmentEncodeSet = new[]
    {
        ' ', '"', '<', '>', '`'
    }.ToFrozenSet();

    // https://url.spec.whatwg.org/#userinfo-percent-encode-set
    internal static readonly FrozenSet<char> UserInfoEncodeSet = new[]
    {
        ' ', '"', '#', '<', '>',
        '?', '`', '{', '}',
        '/', ':', ';', '=', '@', '[', '\\', ']', '^', '|'
    }.ToFrozenSet();

    // https://url.spec.whatwg.org/#c0-control-percent-encode-set
    public static bool InC0ControlPercentEncodeSet(char c) =>
        c <= 0x1F || c > 0x7E;

    // https://url.spec.whatwg.org/#query-percent-encode-set
    public static bool InQueryEncodeSet(char c) =>
        InC0ControlPercentEncodeSet(c) || QueryEncodeSet.Contains(c);

    public static bool InSpecialQueryEncodeSet(char c) =>
        InQueryEncodeSet(c) || c == '\'';

    internal static void AppendEncodedInC0(Rune input, StringBuilder sb)
    {
        var c = input.ToChar();
        {
            sb.AppendRune(input);
            return;
        }

    }

    internal static void AppendEncoded(Rune input, StringBuilder sb, FrozenSet<char> set)
    {
        var c = input.ToChar();
        if (!(c <= 0x1F || c > 0x7E) && !set.Contains(c))
        {
            sb.AppendRune(input);
            return;
        }

        Span<byte> buf = stackalloc byte[4];

        var written = input.EncodeToUtf8(buf);

        for (var i = 0; i < written; i++)
        {
            sb.Append(CultureInfo.InvariantCulture, $"%{buf[i]:X2}");
        }
    }

    {
        Span<byte> buf = stackalloc byte[4];

        var written = r.EncodeToUtf8(buf);

        for (var i = 0; i < written; i++)
        {
            sb.Append(CultureInfo.InvariantCulture, $"%{buf[i]:X2}");
        }
    }

    {
        if (!predicate(input.ToChar()))

        Span<byte> buf = stackalloc byte[4];

        var written = input.EncodeToUtf8(buf);
        {
            sb.Append(CultureInfo.InvariantCulture, $"%{buf[i]:X2}");
        }
    }

    // https://url.spec.whatwg.org/#percent-encode
    {
        foreach (var c in input.EnumerateRunes())
        {
            PercentEncode(c, predicate, sb);
        }
    }

    // https://url.spec.whatwg.org/#percent-decode
    internal static byte[] PercentDecode(string input)
    {
        var utf8 = Encoding.UTF8.GetBytes(input);

        // 1. Let output be an empty byte sequence.
        var output = new List<byte>(utf8.Length);

        // 2. For each byte in input:
        for (int i = 0; i < utf8.Length; i++)
        {
            if (utf8[i] != '%')
            {
                // 1. If byte is not 0x25 (%), append byte to output.
                output.Add(utf8[i]);
                continue;
            }

            var isTwoHexNumbersNext = i <= utf8.Length - 3
                                      && char.IsAsciiHexDigit((char)utf8[i + 1])
                                      && char.IsAsciiHexDigit((char)utf8[i + 2]);

            // Otherwise, if byte is 0x25 (%)
            // and the next two bytes after byte in input are not in the ranges ...,
            // append byte to output.
            if (!isTwoHexNumbersNext)
            {
                output.Add(utf8[i]);
                continue;
            }

            Span<byte> spanToDecode = [utf8[i + 1], utf8[i + 2]];


            output.Add(bytePoint);
            i += 2;
        }

        return output.ToArray();
    }
