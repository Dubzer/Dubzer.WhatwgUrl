using System;
using System.Globalization;
using System.Text;

namespace Dubzer.WhatwgUrl;

internal static class Ipv6Parser
{
    // https://url.spec.whatwg.org/#concept-ipv6-parser
    internal static Result<string> Parse(string input)
    {
        // not in the spec
        if (string.IsNullOrEmpty(input))
            return Result<string>.Failure(UrlErrorCode.Ipv6TooFewPieces);

        // 1. Let address be a new IPv6 address whose IPv6 pieces are all 0.
        Span<ushort> address = stackalloc ushort[8];

        // 2. Let pieceIndex be 0.
        int pieceIndex = 0;

        // 3. Let compress be null.
        int? compress = null;

        var pointer = 0;

        // 5. If c is U+003A (:), then:
        if (input[pointer] == ':')
        {
            // 5.1 If remaining does not start with U+003A (:),
            if (input.Length == 1 || input[1] != ':')
                return Result<string>.Failure(UrlErrorCode.Ipv6InvalidCompression);

            // 5.2. Increase pieceIndex by 1 and then set compress to pieceIndex.
            pointer += 2;

            // 5.3. Increase pieceIndex by 1 and then set compress to pieceIndex.
            compress = ++pieceIndex;
        }

        // 6. While c is not the EOF code point:
        while (pointer < input.Length)
        {

            // 6.1 If pieceIndex is 8, IPv6-too-many-pieces validation error, return failure.
            if (pieceIndex == 8)
                return Result<string>.Failure(UrlErrorCode.Ipv6TooManyPieces);

            // 6.2 If c is U+003A (:), then:
            if (input[pointer] == ':')
            {
                if (compress.HasValue)
                    return Result<string>.Failure(UrlErrorCode.Ipv6MultipleCompressions);

                pointer++;
                compress = ++pieceIndex;
                continue;
            }

            var value = 0;
            var length = 0;

            // 6.4 While length is less than 4 and c is an ASCII hex digit,
            while (length < 4 && pointer < input.Length && char.IsAsciiHexDigit(input[pointer]))
            {
                // set value to value × 0x10 + c interpreted as hexadecimal number,
                value = value * 0x10 + input[pointer].ParseHexNumber();
                // and increase pointer and length by 1.
                pointer++;
                length++;
            }

            // this step is processing IPv4 in IPv6
            // for example: http://[::127.0.0.1]
            // 6.5 If c is U+002E (.), then:
            if (pointer < input.Length && input[pointer] == '.')
            {
                if (length == 0)
                    return Result<string>.Failure(UrlErrorCode.Ipv4InIpv6InvalidCodePoint);

                pointer -= length;
                if (pieceIndex > 6)
                    return Result<string>.Failure(UrlErrorCode.Ipv4InIpv6TooManyPieces);

                var numberSeen = 0;

                // 6.5.5. While c is not the EOF code point:
                while (pointer < input.Length)
                {
                    short? ipv4Piece = null;

                    // 6.5.5.2. If numbersSeen is greater than 0, then:
                    if (numberSeen > 0)
                    {
                        if (input[pointer] == '.' && numberSeen < 4)
                            pointer++;
                        else
                            return Result<string>.Failure(UrlErrorCode.Ipv4InIpv6InvalidCodePoint);
                    }

                    if (pointer >= input.Length || !char.IsAsciiDigit(input[pointer]))
                        return Result<string>.Failure(UrlErrorCode.Ipv4InIpv6InvalidCodePoint);

                    while (pointer < input.Length && char.IsAsciiDigit(input[pointer]))
                    {
                        var number = input[pointer].ParseHexNumber();
                        if (ipv4Piece == null)
                            ipv4Piece = number;
                        else
                            ipv4Piece = (short) (ipv4Piece.Value * 10 + number);

                        if (ipv4Piece > 255)
                            return Result<string>.Failure(UrlErrorCode.Ipv4InIpv6OutOfRangePart);

                        pointer++;
                    }

                    address[pieceIndex] = (ushort) (address[pieceIndex] * 0x100 + ipv4Piece!.Value);
                    numberSeen++;
                    if (numberSeen is 2 or 4)
                        pieceIndex++;
                }

                if (numberSeen < 4)
                    return Result<string>.Failure(UrlErrorCode.Ipv4InIpv6TooFewParts);

                break;
            }
            else if (pointer < input.Length && input[pointer] == ':')
            {
                pointer++;

                // 6.6.2. If c is the EOF code point
                if (pointer == input.Length)
                    return Result<string>.Failure(UrlErrorCode.Ipv6InvalidCodePoint);
            }
            // 6.7 Otherwise, if c is not the EOF code point
            else if (pointer != input.Length)
                return Result<string>.Failure(UrlErrorCode.Ipv6InvalidCodePoint);

            // 6.8 Set address[pieceIndex] to value.
            address[pieceIndex] = (ushort) value;
            pieceIndex++;
        }

        // 7. If compress is non-null, then:
        if (compress.HasValue)
        {
            var swaps = pieceIndex - compress.Value;
            pieceIndex = 7;
            while (pieceIndex != 0 && swaps > 0)    // 7.3 While pieceIndex is not 0 and swaps is greater than 0
            {
                // swap address[pieceIndex] with address[compress + swaps − 1]
                (address[pieceIndex], address[compress.Value + swaps - 1]) =
                    (address[compress.Value + swaps - 1], address[pieceIndex]);

                pieceIndex--;
                swaps--;
            }
        }
        // 8. Otherwise, if compress is null and pieceIndex is not 8,
        else if (pieceIndex != 8)
            return Result<string>.Failure(UrlErrorCode.Ipv6TooFewPieces);

        return Result<string>.Success(SerializeIpv6(address));
    }

    // https://url.spec.whatwg.org/#concept-ipv6-serializer
    private static string SerializeIpv6(ReadOnlySpan<ushort> address)
    {
        var sb = new StringBuilder();
        // 2. Let compress be an index
        // to the first IPv6 piece in the first longest sequences of address’s IPv6 pieces that are 0.
        // 3. If there is no sequence of address’s IPv6 pieces that are 0 that is longer than 1,
        // then set compress to null.
        var compress = FindSequenceToCompress(address);

        var ignore0 = false;
        // 5. For each pieceIndex in the range 0 to 7, inclusive:
        for (int pieceIndex = 0; pieceIndex < address.Length; pieceIndex++)
        {
            // 5.1 If ignore0 is true and address[pieceIndex] is 0, then continue.
            if (ignore0 && address[pieceIndex] == 0)
                continue;

            if (ignore0)
                ignore0 = false;

            if (pieceIndex == compress)
            {
                if (pieceIndex == 0)
                    sb.Append("::");
                else
                    sb.Append(':');

                ignore0 = true;
                continue;
            }

            sb.Append(CultureInfo.InvariantCulture, $"{address[pieceIndex]:x}");
            if (pieceIndex != address.Length - 1)
                sb.Append(':');
        }

        return sb.ToString();
    }

    // An index to the first IPv6 piece in the first longest sequences of address’s IPv6 pieces that are 0
    private static int? FindSequenceToCompress(ReadOnlySpan<ushort> address)
    {
        var max = 0;
        var maxIndex = -1;
        var current = 0;
        var currentIndex = -1;

        for (int i = 0; i < address.Length; i++)
        {
            if (address[i] == 0)
            {
                if (currentIndex == -1)
                    currentIndex = i;

                current++;
            }
            else
            {
                if (current > max)
                {
                    max = current;
                    maxIndex = currentIndex;
                }

                current = 0;
                currentIndex = -1;
            }
        }

        if (current > max)
        {
            max = current;
            maxIndex = currentIndex;
        }

        if (max < 2)
            return null;

        return maxIndex;
    }
}
