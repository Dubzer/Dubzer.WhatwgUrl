using System;
using System.Buffers;
using Dubzer.WhatwgUrl.BclInternal;

namespace Dubzer.WhatwgUrl;

// a fast implementation for parts that do not require fallback logic
internal static partial class PercentEncoding
{
    public enum AppendEncodedSimpleResult : byte
    {
        // encoded contains a valid result
        Handled,
        // the input does not require processing and can be used as is
        NoProcessing
    }

    // Inverted https://url.spec.whatwg.org/#query-percent-encode-set
    public static readonly SearchValues<char> QueryEncodeSet = SearchValues.Create([
        '!', '$', '%', '&', '\'', '(', ')', '*', '+', ',', '-', '.', '/', '0', '1', '2', '3', '4', '5', '6', '7', '8','9',
        ':', ';', '=', '?', '@', 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q',
        'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z', '[', '\\', ']', '^', '_', '`', 'a', 'b', 'c', 'd', 'e', 'f', 'g',
        'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z', '{', '|', '}', '~'
    ]);

    // Inverted https://url.spec.whatwg.org/#special-query-percent-encode-set
    public static readonly SearchValues<char> SpecialQueryEncodeSet = SearchValues.Create([
        '!', '$', '%', '&', '(', ')', '*', '+', ',', '-', '.', '/', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9',
        ':', ';', '=', '?', '@', 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q',
        'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z', '[', '\\', ']', '^', '_', '`', 'a', 'b', 'c', 'd', 'e', 'f', 'g',
        'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z', '{', '|', '}',
        '~'
    ]);

    // Inverted https://url.spec.whatwg.org/#fragment-percent-encode-set
    public static readonly SearchValues<char> FragmentEncodeSet = SearchValues.Create([
        '!', '#', '$', '%', '&', '\'', '(', ')', '*', '+', ',', '-', '.', '/', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9',
        ':', ';', '=', '?', '@', 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q',
        'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z', '[', '\\', ']', '^', '_', 'a', 'b', 'c', 'd', 'e', 'f', 'g',
        'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z', '{', '|', '}', '~'
    ]);

    public static AppendEncodedSimpleResult AppendEncodedSimple(ReadOnlySpan<char> input, SearchValues<char> set, out string encoded)
    {
        encoded = "";

        if (input.IsEmpty || input.IndexOfAnyExcept(set) == -1)
            return AppendEncodedSimpleResult.NoProcessing;

        var processing = input;
        var vsb = new ValueStringBuilder(Consts.MaxLengthOnStack.Char);
        try
        {
            while (!processing.IsEmpty)
            {
                var requireEncodeIndex = processing.IndexOfAnyExcept(set);

                switch (requireEncodeIndex)
                {
                    case -1: // No characters to encode
                        vsb.Append(processing);
                        encoded = vsb.ToString();
                        return AppendEncodedSimpleResult.Handled;

                    case 0: // Missing fragment that DOES NOT require character encoding
                        var notRequireEncodeIndex = processing.IndexOfAny(set);

                        if (notRequireEncodeIndex == -1)
                        {
                            // The entire remainder of the string needs encoding
                            foreach (var c in processing)
                                EncodeToUtf8HexWithPercent(c, ref vsb);
                            encoded = vsb.ToString();
                            return AppendEncodedSimpleResult.Handled;
                        }

                        foreach (var c in processing[..notRequireEncodeIndex])
                            EncodeToUtf8HexWithPercent(c, ref vsb);

                        processing = processing[notRequireEncodeIndex..];
                        break;

                    default:
                        // Copy the safe prefix without further processing
                        vsb.Append(processing[..requireEncodeIndex]);
                        processing = processing[requireEncodeIndex..];
                        break;
                }
            }

            encoded = vsb.ToString();
            return AppendEncodedSimpleResult.Handled;
        }
        finally
        {
            vsb.Dispose();
        }
    }
}
