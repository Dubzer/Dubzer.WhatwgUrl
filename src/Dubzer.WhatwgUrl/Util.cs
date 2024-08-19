using System;
using System.Globalization;
using System.Text;

namespace Dubzer.WhatwgUrl;

internal static class Util
{
    // https://url.spec.whatwg.org/#single-dot-path-segment
    internal static bool IsSingleDot(string input) =>
        input == "." || input.Equals("%2e", StringComparison.OrdinalIgnoreCase);

    // https://url.spec.whatwg.org/#double-dot-path-segment
    // this implementation was benched against a FrozenSet<string>.
    // it is faster on such a small amount of strings.
    internal static bool IsDoubleDot(string input) =>
        input == ".."
        || input.Equals(".%2e", StringComparison.OrdinalIgnoreCase)
        || input.Equals("%2e.", StringComparison.OrdinalIgnoreCase)
        || input.Equals("%2e%2e", StringComparison.OrdinalIgnoreCase);

    internal static bool IsAsciiHexDigit(this Rune rune) =>
        rune.TryChar(out var c) && char.IsAsciiHexDigit(c);

    /// <returns>uFFFD if is not representable</returns>
    internal static char ToChar(this Rune rune) => rune.TryChar(out var c) ? c : '\uFFFD';

    internal static bool TryChar(this Rune rune, out char result)
    {
        if (!rune.IsBmp)
        {
            result = default;
            return false;
        }

        result = (char) rune.Value;
        return true;
    }

    internal static bool IsMark(this UnicodeCategory c) =>
        c is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark;

    /// <summary>
    /// Convert a hex digit codepoint to a number, without allocating a string to the heap.
    /// <example>'F' (u0046) -> 0xF</example>
    /// </summary>
    internal static byte ParseHexNumber(this char c)
    {
        Span<byte> digitString = [(byte) c];
    }
