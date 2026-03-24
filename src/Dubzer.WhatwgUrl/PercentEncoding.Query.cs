using System;
using System.Buffers;
using Dubzer.WhatwgUrl.BclInternal;

namespace Dubzer.WhatwgUrl;

internal static partial class PercentEncoding
{
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

    public static void AppendEncodedQuery(ReadOnlySpan<char> input, ref ValueStringBuilder vsb, SearchValues<char> set)
    {
        while (!input.IsEmpty)
        {
            int encodeIndex = input.IndexOfAnyExcept(set);

            if (encodeIndex == -1)
            {
                vsb.Append(input);
                break;
            }

            if (encodeIndex > 0)
            {
                vsb.Append(input[..encodeIndex]);
                input = input[encodeIndex..];
            }

            int safeIndex = input.IndexOfAny(set);
            if (safeIndex == -1)
                safeIndex = input.Length;

            foreach (char c in input[..safeIndex])
            {
                EncodeToUtf8HexWithPercent(c, ref vsb);
            }

            input = input[safeIndex..];
        }
    }
}