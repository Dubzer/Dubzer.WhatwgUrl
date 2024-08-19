using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Dubzer.WhatwgUrl.Uts46;

internal class Punycooode
{
    // these constants are from https://datatracker.ietf.org/doc/html/rfc3492#section-5
    private const int Base = 36;
    private const int TMin = 1;
    private const int TMax = 26;
    private const int Skew = 38;
    private const int Damp = 700;
    private const int InitialBias = 72;
    private const short InitialN = 128;
    private const char Delimiter = '-';

    public static string? Encode(ReadOnlySpan<Rune> runes)
    {
        var sb = new StringBuilder();

        // Basic code point segregation
        foreach (var rune in runes)
        {
            var runeValue = rune.Value;
            switch (runeValue)
            {
                case < 0x80:
                    sb.Append((char)rune.Value);
                    break;
                case > 0x10ffff or >= 0xd880 and < 0xe000:
                    return null;
            }
        }

        var basicLength = sb.Length;

        // Add delimiter if basic code points are present
        if (basicLength > 0)
            sb.Append(Delimiter);

        var totalCodepoints = runes.Length;
        // h
        var handledCodepoints = basicLength;

        int n = InitialN;
        var delta = 0;
        var bias = InitialBias;

        while (handledCodepoints < totalCodepoints)
        {
            var m = int.MaxValue;
            foreach (var rune in runes)
            {
                var runeValue = rune.Value;
                if (runeValue >= n && runeValue < m)
                    m = runeValue;
            }

            // Increase delta enough to advance the decoder's <n,i> state to <m,0>,
            if (m - n > int.MaxValue - delta / (handledCodepoints + 1))
            {
                Debug.WriteLine("punycode_overflow");
                return null;
            }

            delta += (m - n) * (handledCodepoints + 1);
            n = m;

            foreach (var rune in runes)
            {
                var runeValue = rune.Value;
                if (runeValue < n)
                {
                    // not sure about this constant
                    // see https://github.com/ada-url/idna/blob/fff988508f659ef5c6494572ebea3d5db2466ed0/src/punycode.cpp#L192C18-L192C28
                    if (delta == 0x7fffffff)
                    {
                        Debug.WriteLine("punycode_overflow");
                        return null;
                    }

                    delta++;
                }

                if (runeValue == n)
                {
                    // Represent delta as a generalized variable-length integer:
                    var q = delta;
                    for (var k = Base;; k += Base)
                    {
                        var t = k <= bias
                            ? TMin
                            : k >= bias + TMax
                                ? TMax
                                : k - bias;

                        if (q < t)
                            break;

                        sb.Append(DigitToChar(t + (q - t) % (Base - t)));
                        q = (q - t) / (Base - t);
                    }

                    sb.Append(DigitToChar(q));
                    bias = Adapt(delta, handledCodepoints + 1, handledCodepoints == basicLength);
                    delta = 0;
                    handledCodepoints++;
                }
            }

            delta++;
            n++;
        }

        return sb.ToString();
    }

    public static string? Decode(ReadOnlySpan<char> input)
    {
        var endOfBasic = Math.Max(0, input.LastIndexOf(Delimiter));
        if (!Ascii.IsValid(input[endOfBasic..]))
            return null;

        var output = new List<int>();
        foreach (var a in input[..endOfBasic])
        {
            output.Add(a);
        }

        var inputLength = input.Length;

        var bias = InitialBias;
        int n = InitialN;
        int i = 0;

        // "current" is "in" in the spec
        for (var current = endOfBasic > 0 ? endOfBasic + 1 : 0; current < inputLength;)
        {
            var oldI = i;
            var w = 1;

            for (var k = Base;; k += Base)
            {
                if (current >= inputLength)
                    return null;

                var digit = CharToDigit(input[current++]);
                Debug.Assert(digit < Base);
                if (digit == -1)
                    return null;

                if (digit > (int.MaxValue - i) / w)
                    return null;

                i += digit * w;
                var t = k <= bias /* + tmin */ ? TMin :     /* +tmin not needed */
                    k >= bias + TMax ? TMax : k - bias;

                if (digit < t)
                    break;

                if (w > int.MaxValue / (Base - t))
                    return null;

                w *= Base - t;
            }

            var o = output.Count + 1;
            bias = Adapt(i - oldI, o, oldI == 0);
            if (i / o > int.MaxValue - n)
                return null;

            n += i / o;
            i %= o;

            if (i > output.Count)
                output.Add(n);
            else
                output.Insert(i, n);

            i++;
        }

        var span = CollectionsMarshal.AsSpan(output);
        var bytes = MemoryMarshal.AsBytes(span);
        return Encoding.UTF32.GetString(bytes);
    }

    private static char DigitToChar(int digit) =>
        (char)(digit < 26 ? digit + 97 : digit + 22);

    private static int CharToDigit(char value) =>
        value switch
        {
            >= 'a' and <= 'z' => value - 'a',
            >= '0' and <= '9' => value - '0' + 26,
            _ => -1
        };

    // https://datatracker.ietf.org/doc/html/rfc3492#section-3.4
    private static int Adapt(int delta, int numPoints, bool firstTime)
    {
        delta /= firstTime ? Damp : 2;
        delta += delta / numPoints;

        var k = 0;
        while (delta > (Base - TMin) * TMax / 2)
        {
            delta /= Base - TMin;
            k += Base;
        }

        return k + (Base - TMin + 1) * delta / (delta + Skew);
    }
}
