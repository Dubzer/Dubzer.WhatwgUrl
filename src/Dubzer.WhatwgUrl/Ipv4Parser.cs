using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace Dubzer.WhatwgUrl;

internal static class Ipv4Parser
{
    private static readonly SearchValues<char> HexDigitSearchValues = SearchValues.Create("0123456789ABCDEFabcdef");

    // this method implements:
    // IPv4 parser: https://url.spec.whatwg.org/#concept-ipv4-parser
    // IPv4 serializer: https://url.spec.whatwg.org/#concept-ipv4-serializer
    internal static Result<string> Parse(string input)
    {
        // 1. Let parts be the result of strictly splitting input on U+002E (.).
        var parts = input.Split('.').AsSpan();
        // 2. If the last item in parts is the empty string, then:
        if (string.IsNullOrEmpty(parts[^1]))
        {
            // If parts’s size is greater than 1, then remove the last item from parts.
            if (parts.Length > 1)
                parts = parts[..^1];
        }

        // 3. If parts’s size is greater than 4, validation error, return failure.
        if (parts.Length > 4)
            return Result<string>.Failure(UrlErrorCode.Ipv4TooManyParts);

        // 4. Let numbers be an empty list.
        var numbers = new List<long>(4);

        // 5. For each part in parts:
        foreach (var part in parts)
        {
            // Let result be the result of parsing part.
            var result = ParseNumber(part);

            // If result is failure, IPv4-non-numeric-part validation error, return failure.
            if (result == -1)
                return Result<string>.Failure(UrlErrorCode.Ipv4NonNumericPart);

            numbers.Add(result);
        }

        // 7. If any but the last item in numbers is greater than 255, then return failure.
        if (numbers.Count > 1)
        {
            for (int i = 0; i < numbers.Count - 1; i++)
            {
                if (numbers[i] > 255)
                    return Result<string>.Failure(UrlErrorCode.Ipv4OutOfRangePart);
            }
        }

        // 9. Let ipv4 be the last item in numbers.
        long ipv4 = numbers[^1];

        // 8. If the last item in numbers is greater than or equal to 256^(5 − numbers’s size), then return failure.
        if (ipv4 >= Math.Pow(256, 5 - numbers.Count))
            return Result<string>.Failure(UrlErrorCode.Ipv4OutOfRangePart);

        // 10. Remove the last item from numbers.
        if (numbers.Count > 1)
        {
            // 12. For each n of numbers
            for (int i = 0; i < numbers.Count - 1; i++)
            {
                // Increment ipv4 by n × 256^(3 − counter).
                ipv4 += numbers[i] * (long)Math.Pow(256, 3 - i);
            }
        }

        // IPv4 serializer starts here. I don't see a reason to separate them
        var sb = new StringBuilder();
        for (int i = 0; i < 4; i++)
        {
            // Prepend n % 256, serialized, to output.
            sb.Insert(0, ipv4 % 256);

            if (i < 3)
                sb.Insert(0, '.');

            // Set n to floor(n / 256).
            ipv4 /= 256;
        }

        return Result<string>.Success(sb.ToString());
    }

    /// <returns>-1 when invalid</returns>
    internal static long ParseNumber(string input)
    {
        if (string.IsNullOrEmpty(input))
            return -1;

        var numBase = 10;   // R in spec
        if (input is ['0', _, ..])
        {
            if (input[1] is 'X' or 'x')
            {
                if (input.Length == 2)
                    return 0;

                numBase = 16;
            }
            else
            {
                if (input.Length == 1)
                    return 0;

                numBase = 8;
            }
        }

        var span = input.AsSpan();
#pragma warning disable CS8509 // The switch expression does not handle all possible values of its input type (it is not exhaustive).
        var valid = numBase switch
#pragma warning restore CS8509 // The switch expression does not handle all possible values of its input type (it is not exhaustive).
        {
            8 => !span[1..].ContainsAnyExceptInRange('0', '7'),
            10 => !span.ContainsAnyExceptInRange('0', '9'),
            16 => !span[2..].ContainsAnyExcept(HexDigitSearchValues)
        };

        if (!valid)
            return -1;

        // 20 is a result of running
        // Convert.ToString(long.MaxValue, 8).Length - 1,
        // which is the longest possible number with our bases
        // this check prevents overflow exception in Convert.ToInt64
        if (input.Length > 20)
            // MaxValue so IP would not be valid on the check later
            return long.MaxValue;

        // this should not throw an exception...
        return Convert.ToInt64(input, numBase);
    }
}
