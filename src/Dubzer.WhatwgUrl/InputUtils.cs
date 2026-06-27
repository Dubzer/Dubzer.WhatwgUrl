using System;
using System.Buffers;

namespace Dubzer.WhatwgUrl;

internal static class InputUtils
{
    private static readonly SearchValues<char> InvalidUrlUnitSearchValues = SearchValues.Create(['\x09', '\x0a', '\x0d']);

    public static string Format(string input) => Format(input, out _);

    public static string Format(string input, out bool containsHighSurrogate)
    {
        var inputSpan = input.AsSpan();

        containsHighSurrogate = false;
        // fast path for inputs that cannot contain trim/control characters or UTF-16 surrogates
        var formatOrSurrogateStart = inputSpan.IndexOfAnyExceptInRange('\x21', '\ud7ff');
        if (formatOrSurrogateStart < 0)
            return input;

        containsHighSurrogate = inputSpan.ContainsAnyInRange('\ud800', '\udbff');

        var controlCharStart = inputSpan[formatOrSurrogateStart] <= '\x20'
            ? 0
            : inputSpan[formatOrSurrogateStart..].IndexOfAnyInRange('\x00', '\x20');
        if (controlCharStart < 0)
        {
            // we can safely return because InvalidUrlUnitSearchValues is also in that range
            return input;
        }

        controlCharStart += formatOrSurrogateStart;

        if (controlCharStart == 0)
        {
            // we have leading control chars
            var start = inputSpan.IndexOfAnyExceptInRange('\x00', '\x20');
            if (start == -1)
                return "";

            inputSpan = inputSpan[start..];
        }

        // we may also have trailing control chars
        var end = inputSpan.LastIndexOfAnyExceptInRange('\x00', '\x20');
        if (end == -1)
            return "";

        inputSpan = inputSpan[..(end+1)];

        //  invalid-URL-unit
        var invalidUrlUnitPosition = inputSpan.IndexOfAny(InvalidUrlUnitSearchValues);
        if (invalidUrlUnitPosition == -1)
            return inputSpan.Length == input.Length ? input : inputSpan.ToString();

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
