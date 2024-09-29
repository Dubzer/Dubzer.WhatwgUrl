using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace Dubzer.WhatwgUrl;

internal static class Util
{
    // https://url.spec.whatwg.org/#single-dot-path-segment
    internal static bool IsSingleDot(ReadOnlySpan<char> input) =>
        input.Length <= 3
        && (input is "." || input.Equals("%2e", StringComparison.OrdinalIgnoreCase));

    // https://url.spec.whatwg.org/#double-dot-path-segment
    // this implementation was benched against a FrozenSet<string>.
    // it is faster on such a small amount of strings.
    internal static bool IsDoubleDot(ReadOnlySpan<char> input) =>
        input.Length <= 6
        && (input is ".."
            || input.Equals(".%2e", StringComparison.OrdinalIgnoreCase)
            || input.Equals("%2e.", StringComparison.OrdinalIgnoreCase)
            || input.Equals("%2e%2e", StringComparison.OrdinalIgnoreCase));

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
        return byte.Parse(digitString, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }

    // Taken from https://github.com/dotnet/runtime/blob/619d4b35eeac857a178dd1246b07d27c08c263a2/src/libraries/Common/src/System/HexConverter.cs#L83
    // because runtime doesn't inline `sb.Append(CultureInfo.InvariantCulture, $"%{buf[i]:X2}");`
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ByteFormatX2(byte value, Span<char> buffer)
    {
        var difference = ((value & 0xF0U) << 4) + (value & 0x0FU) - 0x8989U;
        var packedResult = ((((uint)-(int)difference & 0x7070U) >> 4) + difference + 0xB9B9U) | 0;

        buffer[1] = (char)(packedResult & 0xFF);
        buffer[0] = (char)(packedResult >> 8);
    }
}
