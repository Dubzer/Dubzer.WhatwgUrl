using System;
using System.Buffers;
using System.Diagnostics;
using Dubzer.WhatwgUrl.BclInternal;

namespace Dubzer.WhatwgUrl;

internal static partial class PercentEncoding
{
    // https://url.spec.whatwg.org/#query-percent-encode-set
    public static readonly SearchValues<char> QueryEncodeSet = SearchValues.Create([
        '\x00', '\x01', '\x02', '\x03', '\x04', '\x05', '\x06', '\x07', '\x08', '\x09', '\x0A', '\x0B', '\x0C', '\x0D', '\x0E', '\x0F',
        '\x10', '\x11', '\x12', '\x13', '\x14', '\x15', '\x16', '\x17', '\x18', '\x19', '\x1A', '\x1B', '\x1C', '\x1D', '\x1E', '\x1F',
        ' ', '"', '#', '<', '>'
    ]);

    // https://url.spec.whatwg.org/#special-query-percent-encode-set
    public static readonly SearchValues<char> SpecialQueryEncodeSet = SearchValues.Create([
        '\x00', '\x01', '\x02', '\x03', '\x04', '\x05', '\x06', '\x07', '\x08', '\x09', '\x0A', '\x0B', '\x0C', '\x0D', '\x0E', '\x0F',
        '\x10', '\x11', '\x12', '\x13', '\x14', '\x15', '\x16', '\x17', '\x18', '\x19', '\x1A', '\x1B', '\x1C', '\x1D', '\x1E', '\x1F',
        ' ', '"', '#', '<', '>', '\'' 
    ]);

    // <returns>Handled - if true, doesn't require to fallback</returns>
    public static bool AppendEncodedQuery(ReadOnlySpan<char> input, ref ValueStringBuilder vsb, SearchValues<char> set)
    {
        if (input.IsEmpty) return true;
        var processing = input;

        while (!processing.IsEmpty)
        {
            var highC0Index = processing.IndexOfAnyInRange('\x7F', char.MaxValue);
            var requireEncodeIndex = processing.IndexOfAny(set);

            var requireEncodeUnifiedIndex = MakeUnifiedIndex(highC0Index, requireEncodeIndex);

            switch (requireEncodeUnifiedIndex)
            {
                case -1: // No characters to encode
                    vsb.Append(processing);
                    return true;

                case 0: // Missing fragment that DOES NOT require character encoding
                    var notRequireEncodeIndex = MakeInvertedUnifiedIndex(processing.IndexOfAnyExcept(set), processing.IndexOfAnyExceptInRange('\x7F', char.MaxValue));

                    switch (notRequireEncodeIndex)
                    {
                        case -1: // The entire string up to the end needs to be encoded
                            foreach (ref readonly var c in processing)
                                EncodeToUtf8HexWithPercent(c, ref vsb);
                            return true;
                        default: // The string partially requires character encoding
                            Debug.Assert(notRequireEncodeIndex != 0);

                            foreach (ref readonly var c in processing[..notRequireEncodeIndex])
                                EncodeToUtf8HexWithPercent(c, ref vsb);

                            processing = processing[notRequireEncodeIndex..];
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
}