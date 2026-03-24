using System;
using System.Buffers;
using System.Diagnostics;
using Dubzer.WhatwgUrl.BclInternal;

namespace Dubzer.WhatwgUrl;

internal static partial class PercentEncoding
{
    // https://url.spec.whatwg.org/#path-percent-encode-set
    // ' ', '"', '#', '<', '>', '?', '^', '`', '{', '}' + C0
    internal static ReadOnlySpan<byte> PathEncodeSetLookup =>
    [
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 0, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 1, 1,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0,
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

        // https://url.spec.whatwg.org/#path-percent-encode-set
    private static readonly SearchValues<char> PathEncodeSet = SearchValues.Create([
        '\x00', '\x01', '\x02', '\x03', '\x04', '\x05', '\x06', '\x07', '\x08', '\x09', '\x0A', '\x0B', '\x0C', '\x0D', '\x0E', '\x0F',
        '\x10', '\x11', '\x12', '\x13', '\x14', '\x15', '\x16', '\x17', '\x18', '\x19', '\x1A', '\x1B', '\x1C', '\x1D', '\x1E', '\x1F',
        ' ', '"', '#', '<', '>', '?', '^', '`', '{', '}'
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
                            foreach (ref readonly var c in processing)
                                EncodeToUtf8HexWithPercent(c, ref vsb);
                            return true;
                        default: // The string partially requires character encoding
                            Debug.Assert(notRequireEncodeUnifiedIndex != 0);

                            foreach (ref readonly var c in processing[..notRequireEncodeUnifiedIndex])
                                EncodeToUtf8HexWithPercent(c, ref vsb);

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
            if (firstSetIndex == -1)
                return secondSetIndex;

            if (secondSetIndex == -1)
                return firstSetIndex;

            return Math.Min(firstSetIndex, secondSetIndex);
        }

        static int MakeInvertedUnifiedIndex(int firstSetIndex, int secondSetIndex) =>
            firstSetIndex == -1 || secondSetIndex == -1
                ? -1
                : Math.Max(firstSetIndex, secondSetIndex);
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
}