using System;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using Dubzer.WhatwgUrl.BclInternal;

namespace Dubzer.WhatwgUrl;

internal static class PercentEncoding
{
    // https://url.spec.whatwg.org/#c0-control-percent-encode-set
    // additional characters that are not in the c0 control percent encode set
    // ' ', '"', '#', '<', '>'
    private static ReadOnlySpan<byte> QueryEncodeSetLookup =>
    [
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 0, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 1, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1
    ];

    // ' ', '"', '#', '<', '>', '\'' + C0
    private static ReadOnlySpan<byte> SpecialQueryEncodeSetLookup =>
    [
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 0, 1, 1, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 1, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1
    ];

    // https://url.spec.whatwg.org/#path-percent-encode-set
    // ' ', '"', '#', '<', '>', '?', '`', '{', '}' + C0
    internal static ReadOnlySpan<byte> PathEncodeSetLookup =>
    [
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 0, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 1, 1,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 1, 0, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1
    ];

    // https://url.spec.whatwg.org/#fragment-percent-encode-set
    // ' ', '"', '<', '>', '`' + C0
    internal static ReadOnlySpan<byte> FragmentEncodeSetLookup =>
    [
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 1, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1
    ];

    // https://url.spec.whatwg.org/#userinfo-percent-encode-set
    // ' ', '"', '#', '<', '>', '?', '`', '{', '}', '/', ':', ';', '=', '@', '[', '\\', ']', '^', '|' + C0
    internal static ReadOnlySpan<byte> UserInfoEncodeSetLookup =>
    [
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 0, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1,
        1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 0,
        1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 0, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1
    ];

    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    public static bool InC0ControlPercentEncodeSet(char c) => (uint) c is <= 0x1Fu or > 0x7Eu;

    // https://url.spec.whatwg.org/#query-percent-encode-set
    public static bool InQueryEncodeSet(char c) => c > 0x7Eu || QueryEncodeSetLookup[(byte) c] != 0;

    public static bool InSpecialQueryEncodeSet(char c) => c > 0x7Eu || SpecialQueryEncodeSetLookup[(byte) c] != 0;

    internal static void AppendEncodedInC0(Rune input, StringBuilder sb)
    {
        var c = input.ToChar();
        if (InC0ControlPercentEncodeSet(c))
        {
            Encode(input, sb);
        }
        else
        {
            sb.AppendRune(input);
        }
    }

    internal static void AppendEncodedInC0(char input, StringBuilder sb)
    {
        if (InC0ControlPercentEncodeSet(input))
        {
            Encode(input, sb);
        }
        else
        {
            sb.Append(input);
        }
    }

    // https://url.spec.whatwg.org/#path-percent-encode-set
    private static readonly SearchValues<char> PathEncodeSet = SearchValues.Create([
        '\x00', '\x01', '\x02', '\x03', '\x04', '\x05', '\x06', '\x07', '\x08', '\x09', '\x0A', '\x0B', '\x0C', '\x0D', '\x0E', '\x0F',
        '\x10', '\x11', '\x12', '\x13', '\x14', '\x15', '\x16', '\x17', '\x18', '\x19', '\x1A', '\x1B', '\x1C', '\x1D', '\x1E', '\x1F',
        '\x20', '"', '#', '<', '>', '?', '`', '{', '}'
    ]);

    // <returns>Handled - if true, doesn't require to fallback</returns>
    public static bool AppendEncodedPath(ReadOnlySpan<char> input, ref ValueStringBuilder vsb)
    {
        if (input.IsEmpty) return true;
        if (RequiresDotHandling(input)) return false;

        var processing = input;

        while (!processing.IsEmpty)
        {
            var highC0Index = processing.IndexOfAnyInRange('\x7F', char.MaxValue);
            var requireEncodeIndex = processing.IndexOfAny(PathEncodeSet);

            var requireEncodeUnifiedIndex = MakeUnifiedIndex(highC0Index, requireEncodeIndex);
            switch (requireEncodeUnifiedIndex)
            {
                case -1: // No characters to encode
                    vsb.Append(processing);
                    return true;

                case 0: // Missing fragment that DOES NOT require character encoding
                    var notRequireEncodeIndex = processing.IndexOfAnyExcept(PathEncodeSet);
                    var notRequireEncodeInHighC0Index = processing.IndexOfAnyExceptInRange('\x7F', char.MaxValue);

                    var notRequireEncodeUnifiedIndex = MakeInvertedUnifiedIndex(notRequireEncodeInHighC0Index, notRequireEncodeIndex);

                    switch (notRequireEncodeUnifiedIndex)
                    {
                        case -1: // The entire string up to the end needs to be encoded
                            foreach (ref readonly var c in processing) EncodeToUtf8HexWithPercent(c, ref vsb);
                            return true;

                        default: // The string partially requires character encoding
                            Debug.Assert(notRequireEncodeUnifiedIndex != 0);

                            foreach (ref readonly var c in processing[..notRequireEncodeUnifiedIndex]) EncodeToUtf8HexWithPercent(c, ref vsb);

                            processing = processing[notRequireEncodeUnifiedIndex..];
                            continue;
                    }

                default: // Has a part that can be copied without further processing
                    vsb.Append(processing[..requireEncodeUnifiedIndex]);
                    processing = processing[requireEncodeUnifiedIndex..];
                    goto case 0;
            }
        }

        return true;

        static int MakeUnifiedIndex(int firstSetIndex, int secondSetIndex)
        {
            return firstSetIndex == -1 ? secondSetIndex : secondSetIndex == -1 ? firstSetIndex : firstSetIndex < secondSetIndex ? firstSetIndex : secondSetIndex;
        }

        static int MakeInvertedUnifiedIndex(int firstSetIndex, int secondSetIndex)
        {
            return firstSetIndex == -1 || secondSetIndex == -1 ? -1 : firstSetIndex > secondSetIndex ? firstSetIndex : secondSetIndex;
        }
    }

    private static readonly SearchValues<char> SpecialHandlingChars = SearchValues.Create(['\\', '%']);

#if NET9_0_OR_GREATER
    private static readonly SearchValues<string> SpecialHandlingStrings = SearchValues.Create(["..", "/.", "./"], StringComparison.Ordinal);

    private static bool RequiresDotHandling(ReadOnlySpan<char> input)
    {
        // .NET will start using the much slower Aho-Corasick variant with dictionary validation, so you should not combine one-character and two-character strings into a single set
        return input[0] == '.' || input[^1] == '.' || input.ContainsAny(SpecialHandlingChars) || input.ContainsAny(SpecialHandlingStrings);
    }
#else
    private static bool RequiresDotHandling(ReadOnlySpan<char> input)
    {
        // Maybe a little bit sped up by repeating the implementation from .NET 9, but overall, not much of a bottleneck in the current implementation
        return input[0] == '.' || input[^1] == '.' || input.ContainsAny(SpecialHandlingChars) ||
               input.Contains("..", StringComparison.Ordinal) || input.Contains("/.", StringComparison.Ordinal) ||
               input.Contains("./", StringComparison.Ordinal);
    }

#endif

    internal static void AppendEncoded(Rune input, StringBuilder sb, in ReadOnlySpan<byte> c0Set)
    {
        var c = input.ToChar();
        if ((uint) c > 0x7E || c0Set[(byte) c] != 0)
        {
            EncodeToUtf8HexWithPercent(input, sb);
        }
        else
        {
            sb.AppendRune(input);
        }
    }

    internal static void AppendEncoded(char input, StringBuilder sb, in ReadOnlySpan<byte> c0Set)
    {
        if ((uint) input > 0x7E || ((byte) input < c0Set.Length && c0Set[(byte) input] != 0))
        {
            EncodeToUtf8HexWithPercent(input, sb);
        }
        else
        {
            sb.Append(input);
        }
    }

    // Based on HexConverter.ToCharsBuffer
    // See also Util.ByteFormatX2
    private static void ToHexCharsWithPercent(byte value, StringBuilder sb)
    {
        var difference = ((value & 0xF0U) << 4) + (value & 0x0FU) - 0x8989U;
        var packedResult = (((uint) -(int) difference & 0x7070U) >> 4) + difference + 0xB9B9U;

        sb.Append('%').Append((char) (packedResult >> 8)).Append((char) (packedResult & 0xFF));
    }

    // Based on HexConverter.ToCharsBuffer
    // See also Util.ByteFormatX2
    private static void ToHexCharsWithPercent(byte value, ref ValueStringBuilder vsb)
    {
        var difference = ((value & 0xF0U) << 4) + (value & 0x0FU) - 0x8989U;
        var packedResult = (((uint) -(int) difference & 0x7070U) >> 4) + difference + 0xB9B9U;

        vsb.Append('%');
        vsb.Append((char) (packedResult >> 8));
        vsb.Append((char) (packedResult & 0xFF));
    }

    // Inlined Rune.TryEncodeToUtf8
    private static void EncodeToUtf8HexWithPercent(char c, StringBuilder sb)
    {
        switch ((uint) c)
        {
            case <= 0x7Fu:
                ToHexCharsWithPercent((byte) c, sb);
                return;

            case <= 0x7FFu:
                // Scalar 00000yyy yyxxxxxx -> bytes [ 110yyyyy 10xxxxxx ]
                ToHexCharsWithPercent((byte) ((c + (0b110u << 11)) >> 6), sb);
                ToHexCharsWithPercent((byte) ((c & 0x3Fu) + 0x80u), sb);
                return;

            case <= 0xFFFFu:
                // Scalar zzzzyyyy yyxxxxxx -> bytes [ 1110zzzz 10yyyyyy 10xxxxxx ]
                ToHexCharsWithPercent((byte) ((c + (0b1110 << 16)) >> 12), sb);
                ToHexCharsWithPercent((byte) (((c & (0x3Fu << 6)) >> 6) + 0x80u), sb);
                ToHexCharsWithPercent((byte) ((c & 0x3Fu) + 0x80u), sb);
                return;

            default:
                throw new ArgumentOutOfRangeException(nameof(c));
        }
    }

    // Inlined Rune.TryEncodeToUtf8
    // Better to unify on one builder type
    private static void EncodeToUtf8HexWithPercent(char c, ref ValueStringBuilder vsb)
    {
        switch ((uint) c)
        {
            case <= 0x7Fu:
                ToHexCharsWithPercent((byte) c, ref vsb);
                return;

            case <= 0x7FFu:
                // Scalar 00000yyy yyxxxxxx -> bytes [ 110yyyyy 10xxxxxx ]
                ToHexCharsWithPercent((byte) ((c + (0b110u << 11)) >> 6), ref vsb);
                ToHexCharsWithPercent((byte) ((c & 0x3Fu) + 0x80u), ref vsb);
                return;

            case <= 0xFFFFu:
                // Scalar zzzzyyyy yyxxxxxx -> bytes [ 1110zzzz 10yyyyyy 10xxxxxx ]
                ToHexCharsWithPercent((byte) ((c + (0b1110 << 16)) >> 12), ref vsb);
                ToHexCharsWithPercent((byte) (((c & (0x3Fu << 6)) >> 6) + 0x80u), ref vsb);
                ToHexCharsWithPercent((byte) ((c & 0x3Fu) + 0x80u), ref vsb);
                return;

            default:
                throw new ArgumentOutOfRangeException(nameof(c));
        }
    }

    // Inlined Rune.TryEncodeToUtf8
    private static void EncodeToUtf8HexWithPercent(Rune rune, StringBuilder sb)
    {
        var c = (uint) rune.Value;
        switch (c)
        {
            case <= 0x7Fu:
                ToHexCharsWithPercent((byte) c, sb);
                return;

            case <= 0x7FFu:
                // Scalar 00000yyy yyxxxxxx -> bytes [ 110yyyyy 10xxxxxx ]
                ToHexCharsWithPercent((byte) ((c + (0b110u << 11)) >> 6), sb);
                ToHexCharsWithPercent((byte) ((c & 0x3Fu) + 0x80u), sb);
                return;

            case <= 0xFFFFu:
                // Scalar zzzzyyyy yyxxxxxx -> bytes [ 1110zzzz 10yyyyyy 10xxxxxx ]
                ToHexCharsWithPercent((byte) ((c + (0b1110 << 16)) >> 12), sb);
                ToHexCharsWithPercent((byte) (((c & (0x3Fu << 6)) >> 6) + 0x80u), sb);
                ToHexCharsWithPercent((byte) ((c & 0x3Fu) + 0x80u), sb);
                return;

            default:
                // Scalar 000uuuuu zzzzyyyy yyxxxxxx -> bytes [ 11110uuu 10uuzzzz 10yyyyyy 10xxxxxx ]
                ToHexCharsWithPercent((byte) ((c + (0b11110 << 21)) >> 18), sb);
                ToHexCharsWithPercent((byte) (((c & (0x3Fu << 12)) >> 12) + 0x80u), sb);
                ToHexCharsWithPercent((byte) (((c & (0x3Fu << 6)) >> 6) + 0x80u), sb);
                ToHexCharsWithPercent((byte) ((c & 0x3Fu) + 0x80u), sb);
                return;
        }
    }

    private static void Encode(Rune r, StringBuilder sb)
    {
        EncodeToUtf8HexWithPercent(r, sb);
    }

    private static void Encode(char c, StringBuilder sb)
    {
        EncodeToUtf8HexWithPercent(c, sb);
    }

    private static void PercentEncode(Rune input, Func<char, bool> predicate, StringBuilder sb)
    {
        if (!predicate(input.ToChar()))
        {
            sb.AppendRune(input);
            return;
        }

        EncodeToUtf8HexWithPercent(input, sb);
    }

    // https://url.spec.whatwg.org/#percent-encode
    internal static void PercentEncode(string input, Func<char, bool> predicate, StringBuilder sb)
    {
        using var runeEnumerator = input.EnumerateRunes();
        foreach (var c in runeEnumerator)
        {
            PercentEncode(c, predicate, sb);
        }
    }

    // https://url.spec.whatwg.org/#percent-decode
    internal static string PercentDecode(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return input;

        var byteCount = Utf8WithoutBom.GetByteCount(input);
        var sourceBytes = ArrayPool<byte>.Shared.Rent(byteCount);

        try
        {
            byteCount = Utf8WithoutBom.GetBytes(input.AsSpan(), sourceBytes);

            var output = ArrayPool<byte>.Shared.Rent(byteCount);

            try
            {
                var outputIndex = 0;
                var processingState = 0;
                var hexByte = (byte) 0;
                ref var prevByte = ref hexByte;
                foreach (ref var b in sourceBytes.AsSpan(0, byteCount))
                {
                    switch (processingState)
                    {
                        case 0: // Routine processing
                            if (b != '%')
                            {
                                // 1. If byte is not 0x25 (%), append byte to output.
                                output[outputIndex++] = b;
                                continue;
                            }

                            processingState = 1;
                            continue;

                        case 1: // Found the %
                            prevByte = ref b;

                            if (!IsHexChar(b))
                            {
                                output[outputIndex++] = (byte) '%'; // % recovery

                                processingState = 0;
                                goto case 0;
                            }

                            hexByte = (byte) (FromChar(b) << 4);
                            processingState = 2;
                            continue;

                        case 2: // Found first hex character after %
                            if (!IsHexChar(b))
                            {
                                output[outputIndex + 0] = (byte) '%'; // % recovery
                                output[outputIndex + 1] = prevByte; // previous byte recovery

                                outputIndex += 2;
                                processingState = 0;
                                goto case 0;
                            }

                            hexByte |= (byte) FromChar(b);
                            output[outputIndex++] = hexByte;
                            processingState = 0;
                            continue;
                    }
                }

                // Restore fragmented if needed
                switch (processingState)
                {
                    case 1:
                        output[outputIndex++] = (byte) '%'; // % recovery
                        break;

                    case 2:
                        output[outputIndex + 0] = (byte) '%'; // % recovery
                        output[outputIndex + 1] = prevByte; // previous byte recovery
                        outputIndex += 2;
                        break;
                }

                return Utf8WithoutBom.GetString(output.AsSpan(0, outputIndex));
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(output);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(sourceBytes);
        }
    }

    // TODO: Maybe you should move all below to Util
    // Source: HexConverter.cs#L433

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsHexChar(int c)
    {
        if (IntPtr.Size == 8)
        {
            // This code path, when used, has no branches and doesn't depend on cache hits,
            // so it's faster and does not vary in speed depending on input data distribution.
            // We only use this logic on 64-bit systems, as using 64 bit values would otherwise
            // be much slower than just using the lookup table anyway (no hardware support).
            // The magic constant 18428868213665201664 is a 64 bit value containing 1s at the
            // indices corresponding to all the valid hex characters (ie. "0123456789ABCDEFabcdef")
            // minus 48 (ie. '0'), and backwards (so from the most significant bit and downwards).
            // The offset of 48 for each bit is necessary so that the entire range fits in 64 bits.
            // First, we subtract '0' to the input digit (after casting to uint to account for any
            // negative inputs). Note that even if this subtraction underflows, this happens before
            // the result is zero-extended to ulong, meaning that `i` will always have upper 32 bits
            // equal to 0. We then left shift the constant with this offset, and apply a bitmask that
            // has the highest bit set (the sign bit) if and only if `c` is in the ['0', '0' + 64) range.
            // Then we only need to check whether this final result is less than 0: this will only be
            // the case if both `i` was in fact the index of a set bit in the magic constant, and also
            // `c` was in the allowed range (this ensures that false positive bit shifts are ignored).
            ulong i = (uint) c - '0';
            var shift = 18428868213665201664UL << (int) i;
            var mask = i - 64;

            return (long) (shift & mask) < 0;
        }

        return FromChar(c) != 0xFF;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int FromChar(int c)
    {
        return c >= CharToHexLookup.Length ? 0xFF : CharToHexLookup[c];
    }

    /// <summary>Map from an ASCII char to its hex value, e.g. arr['b'] == 11. 0xFF means it's not a hex digit.</summary>
    private static ReadOnlySpan<byte> CharToHexLookup =>
    [
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 15
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 31
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 47
        0x0, 0x1, 0x2, 0x3, 0x4, 0x5, 0x6, 0x7, 0x8, 0x9, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 63
        0xFF, 0xA, 0xB, 0xC, 0xD, 0xE, 0xF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 79
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 95
        0xFF, 0xa, 0xb, 0xc, 0xd, 0xe, 0xf, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 111
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 127
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 143
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 159
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 175
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 191
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 207
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 223
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 239
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF // 255
    ];
}