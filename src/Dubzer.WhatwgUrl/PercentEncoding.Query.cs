using System;
using System.Buffers;
using Dubzer.WhatwgUrl.BclInternal;

namespace Dubzer.WhatwgUrl;

internal static partial class PercentEncoding
{
    // https://url.spec.whatwg.org/#query-percent-encode-set
    public static readonly SearchValues<char> QueryEncodeSet = SearchValues.Create([
        '!', '$', '%', '&', '\'', '(', ')', '*', '+', ',', '-', '.', '/', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', ':', ';', '=', '?', '@', 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z', '[', '\\', ']', '^', '_', '`', 'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z', '{', '|', '}', '~'
    ]);

    // https://url.spec.whatwg.org/#special-query-percent-encode-set
    public static readonly SearchValues<char> SpecialQueryEncodeSet = SearchValues.Create([
        '!', '$', '%', '&', '(', ')', '*', '+', ',', '-', '.', '/', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', ':', ';', '=', '?', '@', 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z', '[', '\\', ']', '^', '_', '`', 'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z', '{', '|', '}', '~'
    ]);

    // <returns>Handled - if true, doesn't require to fallback</returns>
    public static bool AppendEncodedQuery(ReadOnlySpan<char> input, ref ValueStringBuilder vsb, SearchValues<char> set)
    {
        while (!input.IsEmpty)
        {
            // 1. Find the next character that NEEDS encoding (anything outside the safe set)
            int encodeIndex = input.IndexOfAnyExcept(set);

            if (encodeIndex == -1)
            {
                // No more encoding needed, append the rest and bail out
                vsb.Append(input);
                break;
            }

            // 2. Append the safe segment (if any)
            if (encodeIndex > 0)
            {
                vsb.Append(input[..encodeIndex]);
                input = input[encodeIndex..];
            }

            // 3. Find where the unsafe block ends
            int safeIndex = input.IndexOfAny(set);
            if (safeIndex == -1) safeIndex = input.Length;

            // 4. Fast, char-by-char encoding
            // Note: Dropped 'ref readonly var' to avoid unnecessary pointer dereferencing overhead for a 2-byte struct
            foreach (char c in input[..safeIndex])
            {
                EncodeToUtf8HexWithPercent(c, ref vsb);
            }

            input = input[safeIndex..];
        }

        return true;
    }
}