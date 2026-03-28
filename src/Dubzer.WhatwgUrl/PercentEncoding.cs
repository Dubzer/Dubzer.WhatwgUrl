using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;
using Dubzer.WhatwgUrl.BclInternal;

namespace Dubzer.WhatwgUrl;

internal static partial class PercentEncoding
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
    // ' ', '"', '#', '<', '>', '?', '^', '`', '{', '}', '/', ':', ';', '=', '@', '[', '\\', ']', '|' + C0
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

    internal static void AppendEncoded(Rune input, StringBuilder sb, ReadOnlySpan<byte> c0Set)
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

    internal static void AppendEncoded(char input, StringBuilder sb, ReadOnlySpan<byte> c0Set)
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ToHexCharsWithPercent(byte value, Span<char> buffer, int startingIndex = 0)
    {
        var difference = ((value & 0xF0U) << 4) + (value & 0x0FU) - 0x8989U;
        var packedResult = (((uint) -(int) difference & 0x7070U) >> 4) + difference + 0xB9B9U;

        buffer[startingIndex] = '%';
        buffer[startingIndex + 2] = (char)(packedResult & 0xFF);
        buffer[startingIndex + 1] = (char) (packedResult >> 8);
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
    // TODO: Better to unify on one builder type
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void EncodeToUtf8HexWithPercent(char c, ref ValueStringBuilder vsb)
    {
        Span<char> buffer;
        switch ((uint) c)
        {
            case <= 0x7Fu:
                buffer = vsb.AppendSpan(3);
                ToHexCharsWithPercent((byte) c, buffer);
                return;

            case <= 0x7FFu:
                buffer = vsb.AppendSpan(6);
                // Scalar 00000yyy yyxxxxxx -> bytes [ 110yyyyy 10xxxxxx ]
                ToHexCharsWithPercent((byte) ((c + (0b110u << 11)) >> 6), buffer);
                ToHexCharsWithPercent((byte) ((c & 0x3Fu) + 0x80u), buffer, 3);
                return;

            case <= 0xFFFFu:
                buffer = vsb.AppendSpan(9);
                // Scalar zzzzyyyy yyxxxxxx -> bytes [ 1110zzzz 10yyyyyy 10xxxxxx ]
                ToHexCharsWithPercent((byte) ((c + (0b1110 << 16)) >> 12), buffer);
                ToHexCharsWithPercent((byte) (((c & (0x3Fu << 6)) >> 6) + 0x80u), buffer, 3);
                ToHexCharsWithPercent((byte) ((c & 0x3Fu) + 0x80u), buffer, 6);
                return;
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

                            if (!HexConverter.IsHexChar(b))
                            {
                                output[outputIndex++] = (byte) '%'; // % recovery

                                processingState = 0;
                                goto case 0;
                            }

                            hexByte = (byte) (HexConverter.FromChar(b) << 4);
                            processingState = 2;
                            continue;

                        case 2: // Found first hex character after %
                            if (!HexConverter.IsHexChar(b))
                            {
                                output[outputIndex + 0] = (byte) '%'; // % recovery
                                output[outputIndex + 1] = prevByte; // previous byte recovery

                                outputIndex += 2;
                                processingState = 0;
                                goto case 0;
                            }

                            hexByte |= (byte) HexConverter.FromChar(b);
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
}