// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Dubzer.WhatwgUrl.BclInternal;

/// <summary>Methods for parsing numbers and strings.</summary>
internal static class ParseNumbers
{
    private const int TreatAsUnsigned = 0x0200;
    internal const int IsTight = 0x1000;

    public static long StringToLong(ReadOnlySpan<char> s, int radix, int flags)
    {
        int i = 0;
        int r = radix;
        int length = s.Length;

        // Check for a sign
        int sign = 1;
        if (s[i] == '-')
        {
            if (r != 10)
                throw new ArgumentException();

            if ((flags & TreatAsUnsigned) != 0)
                throw new OverflowException();

            sign = -1;
            i++;
        }
        else if (s[i] == '+')
        {
            i++;
        }

        if (radix is -1 or 16 && (i + 1 < length) && s[i] == '0')
        {
            if (s[i + 1] == 'x' || s[i + 1] == 'X')
            {
                r = 16;
                i += 2;
            }
        }

        int grabNumbersStart = i;
        long result = GrabLongs(r, s, ref i, (flags & TreatAsUnsigned) != 0);

        // Check if they passed us a string with no parsable digits.
        if (i == grabNumbersStart)
            throw new FormatException();

        if ((flags & IsTight) != 0)
        {
            // If we've got effluvia left at the end of the string, complain.
            if (i < length)
                throw new FormatException();
        }

        // Return the value properly signed.
        if ((ulong)result == 0x8000000000000000 && sign == 1 && r == 10 && ((flags & TreatAsUnsigned) == 0))
            throw new OverflowException();

        if (r == 10)
        {
            result *= sign;
        }

        return result;
    }

    private static long GrabLongs(int radix, ReadOnlySpan<char> s, ref int i, bool isUnsigned)
    {
        ulong result = 0;
        ulong maxVal;

        // Allow all non-decimal numbers to set the sign bit.
        if (radix == 10 && !isUnsigned)
        {
            maxVal = 0x7FFFFFFFFFFFFFFF / 10;

            // Read all of the digits and convert to a number
            while (i < s.Length && IsDigit(s[i], radix, out int value))
            {
                // Check for overflows - this is sufficient & correct.
                if (result > maxVal || ((long)result) < 0)
                    throw new OverflowException();

                result = result * (ulong)radix + (ulong)value;
                i++;
            }

            if ((long)result < 0 && result != 0x8000000000000000)
                throw new OverflowException();
        }
        else
        {
            Debug.Assert(radix is 2 or 8 or 10 or 16);
            maxVal =
                radix switch
                {
                    10 => 0xffffffffffffffff / 10,
                    16 => 0xffffffffffffffff / 16,
                    8 => 0xffffffffffffffff / 8,
                    _ => 0xffffffffffffffff / 2
                };

            // Read all of the digits and convert to a number
            while (i < s.Length && IsDigit(s[i], radix, out int value))
            {
                // Check for overflows - this is sufficient & correct.
                if (result > maxVal)
                    throw new OverflowException();

                ulong temp = result * (ulong)radix + (ulong)value;

                if (temp < result) // this means overflow as well
                    throw new OverflowException();

                result = temp;
                i++;
            }
        }

        return (long)result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsDigit(char c, int radix, out int result)
    {
        int tmp;
        if ((uint)(c - '0') <= 9)
        {
            result = tmp = c - '0';
        }
        else if ((uint)(c - 'A') <= 'Z' - 'A')
        {
            result = tmp = c - 'A' + 10;
        }
        else if ((uint)(c - 'a') <= 'z' - 'a')
        {
            result = tmp = c - 'a' + 10;
        }
        else
        {
            result = -1;
            return false;
        }

        return tmp < radix;
    }
}
