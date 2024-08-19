using System;
using System.Buffers;

namespace Dubzer.WhatwgUrl;

internal static class InputUtils
{
    private static readonly SearchValues<char> InvalidUrlUnitSearchValues = SearchValues.Create(['\x09', '\x0a', '\x0d']);

    /// <summary>
    /// Returns the correct parser implementation
    /// when input contains surrogate pairs
    /// </summary>
    /// <returns></returns>
    public static InternalUrl GetParser(string input) =>
        input.AsSpan().ContainsAnyInRange('\ud800', '\udbff')
            ? new InternalUrlRune()
            : new InternalUrl();

    public static string Format(string input)
    {
        var inputSpan = input.AsSpan();

        //If input contains any leading or trailing C0 control or space, invalid-URL-unit validation error.
        var start = inputSpan.IndexOfAnyExceptInRange('\x00', '\x20');
        if (start == -1)
        {
            return "";
        }

        var end = inputSpan.LastIndexOfAnyExceptInRange('\x00', '\x20');
        if (end == -1)
        {
            end = inputSpan.Length - 1;
        }

        inputSpan = inputSpan[start..(end + 1)];

        //  invalid-URL-unit
        var invalidUrlUnitPosition = inputSpan.IndexOfAny(InvalidUrlUnitSearchValues);
        if (invalidUrlUnitPosition == -1)
        {
            return inputSpan.Length == input.Length ? input : inputSpan.ToString();
        }

        var length = inputSpan.Length;
        var buffer = length <= Consts.MaxLengthOnStack.Char
            ? stackalloc char[length]
            : new char[length];

        var bufferOffset = 0;
        var prevOffset = 0;
        do
        {
            invalidUrlUnitPosition += prevOffset;
            inputSpan[prevOffset..invalidUrlUnitPosition].CopyTo(buffer[bufferOffset..]);
            bufferOffset += invalidUrlUnitPosition - prevOffset;
            prevOffset = invalidUrlUnitPosition + 1;
            invalidUrlUnitPosition = inputSpan[prevOffset..].IndexOfAny(InvalidUrlUnitSearchValues);
        } while (invalidUrlUnitPosition != -1);

        inputSpan[prevOffset..].CopyTo(buffer[bufferOffset..]);
        bufferOffset += inputSpan.Length - prevOffset;
        return new string(buffer[..bufferOffset]);
    }
}