using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
using System.Text;

namespace Dubzer.WhatwgUrl;

internal static class PercentEncoding
{
    // additional characters that are not in the c0 control percent encode set
    private static readonly FrozenSet<char> QueryEncodeSet = new[]
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
        if (!InC0ControlPercentEncodeSet(c))
        {
            sb.AppendRune(input);
            return;
        }

        Encode(input, sb);
    }

    internal static void AppendEncodedInC0(char input, StringBuilder sb)
    {
        if (!InC0ControlPercentEncodeSet(input))
        {
            sb.Append(input);
            return;
        }

        Encode(input, sb);
    }

    public static void AppendEncodedPath(ReadOnlySpan<char> input, StringBuilder sb)
    {
        var (iterations, rest) = Math.DivRem(input.Length, Vector128<ushort>.Count);
        for (var i = 0; i < iterations; i++)
        {
            var slice = input.Slice(i * Vector128<ushort>.Count, Vector128<ushort>.Count);
            var vecX = Vector128.Create(MemoryMarshal.Cast<char, ushort>(slice));
            var xFromY = Vector128<ushort>.Zero;

            xFromY |= Vector128.Equals(vecX, Vector128.Create((ushort)'"'));
            xFromY |= Vector128.Equals(vecX, Vector128.Create((ushort)'#'));
            xFromY |= Vector128.Equals(vecX, Vector128.Create((ushort)'<'));
            xFromY |= Vector128.Equals(vecX, Vector128.Create((ushort)'>'));
            xFromY |= Vector128.Equals(vecX, Vector128.Create((ushort)'?'));
            xFromY |= Vector128.Equals(vecX, Vector128.Create((ushort)'`'));
            xFromY |= Vector128.Equals(vecX, Vector128.Create((ushort)'{'));
            xFromY |= Vector128.Equals(vecX, Vector128.Create((ushort)'}'));

            xFromY |= Vector128.LessThanOrEqual(vecX, Vector128.Create((ushort)0x20));
            xFromY |= Vector128.GreaterThan(vecX, Vector128.Create((ushort)0x7E));

            // the result is represented by the 8 most significant bits
            // where bit is set when the character has passed any checks
            var result = xFromY.ExtractMostSignificantBits();

            // the mask will be 0x0000_0000
            // which means no characters have passed the checks,
            // and they don't need to be encoded
            if (result == 0)
            {
                sb.Append(slice);
                continue;
            }

            for (var bit = 0; bit < Vector128<ushort>.Count; bit++)
            {
                var c = slice[bit];
                if ((result & (1 << bit)) == 0)
                {
                    sb.Append(c);
                }
                else
                {
                    AppendPercentChar(c, sb);
                }
            }
        }

        foreach (var c in input[^rest..])
        {
            if (!(c <= 0x1F || c > 0x7E) && !PathEncodeSet.Contains(c))
            {
                sb.Append(c);
            }
            else
            {
                AppendPercentChar(c, sb);
            }
        }
    }

    private static void AppendPercentChar(char c, StringBuilder sb)
    {
        Span<byte> buf = stackalloc byte[3];
        var written = EncodeCharToUtf8(c, buf);

        // a buffer to store hex representation of the current number
        Span<char> hex = stackalloc char[2];
        for (var w = 0; w < written; w++)
        {
            sb.Append('%');
            Util.ByteFormatX2(buf[w], hex);
            sb.Append(hex);
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

        // a buffer to store hex representation of the current number
        Span<char> hex = stackalloc char[2];
        for (var i = 0; i < written; i++)
        {
            sb.Append('%');
            Util.ByteFormatX2(buf[i], hex);
            sb.Append(hex);
        }
    }

    internal static void AppendEncoded(char input, StringBuilder sb, FrozenSet<char> set)
    {
        if (!InC0ControlPercentEncodeSet(input) && !set.Contains(input))
        {
            sb.Append(input);
            return;
        }

        AppendPercentChar(input, sb);
    }

    // Inlined Rune.TryEncodeToUtf8
    private static int EncodeCharToUtf8(char c, Span<byte> destination)
    {
        if (char.IsAscii(c))
        {
            destination[0] = (byte) c;

            return 1;
        }

        if (c <= 0x7FFu)
        {
            // Scalar 00000yyy yyxxxxxx -> bytes [ 110yyyyy 10xxxxxx ]
            destination[0] = (byte)((c + (0b110u << 11)) >> 6);
            destination[1] = (byte)((c & 0x3Fu) + 0x80u);
            return 2;
        }

        // Scalar zzzzyyyy yyxxxxxx -> bytes [ 1110zzzz 10yyyyyy 10xxxxxx ]
        destination[0] = (byte)((c + (0b1110 << 16)) >> 12);
        destination[1] = (byte)(((c & (0x3Fu << 6)) >> 6) + 0x80u);
        destination[2] = (byte)((c & 0x3Fu) + 0x80u);
        return 3;
    }

    private static void Encode(Rune r, StringBuilder sb)
    {
        Span<byte> buf = stackalloc byte[4];

        var written = r.EncodeToUtf8(buf);

        for (var i = 0; i < written; i++)
        {
            sb.Append(CultureInfo.InvariantCulture, $"%{buf[i]:X2}");
        }
    }

    private static void Encode(char c, StringBuilder sb)
    {
        Span<byte> buf = stackalloc byte[3];

        var written = EncodeCharToUtf8(c, buf);

        for (var i = 0; i < written; i++)
        {
            sb.Append(CultureInfo.InvariantCulture, $"%{buf[i]:X2}");
        }
    }

    internal static void PercentEncode(Rune input, Func<char, bool> predicate, StringBuilder sb)
    {
        if (!predicate(input.ToChar()))
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

    // https://url.spec.whatwg.org/#percent-encode
    internal static void PercentEncode(string input, Func<char, bool> predicate, StringBuilder sb)
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

            var bytePoint = byte.Parse(spanToDecode, NumberStyles.HexNumber, CultureInfo.InvariantCulture);

            output.Add(bytePoint);
            i += 2;
        }

        return output.ToArray();
    }
}
