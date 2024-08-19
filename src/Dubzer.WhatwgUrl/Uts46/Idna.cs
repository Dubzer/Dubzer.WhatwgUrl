using System;
using System.Buffers;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Dubzer.WhatwgUrl.Uts46;

internal static class Idna
{
    /// <returns>null when invalid</returns>
    internal static string? ToAscii(string input)
    {
        var result = Map(input);
        result = result.Normalize();

        var labels = result.Split('.');

        for (var i = 0; i < labels.Length; i++)
        {
            var label = labels[i];
            if (label.StartsWith("xn--", StringComparison.Ordinal))
            {
                var span = label.AsSpan();
                if (!Ascii.IsValid(span))
                    return null;

                if (span.Length == 4)
                    return null;

                var decodedLabel = Punycode.Decode(span[4..]);
                if (decodedLabel is null)
                    return null;

                labels[i] = decodedLabel;
            }

            if (!IsValidLabel(labels[i]))
                return null;
        }

        for (int i = 0; i < labels.Length; i++)
        {
            var encodedLabel = EncodeLabel(labels[i]);
            if (encodedLabel == null)
                return null;

            labels[i] = encodedLabel;
        }

        return string.Join(".", labels);
    }

    // the separate method allows to use the stackalloc optimization
    private static string? EncodeLabel(string input)
    {
        var labelSpan = input.AsSpan();
        if (Ascii.IsValid(labelSpan))
            return input;

        scoped ReadOnlySpan<Rune> runes;
        if (input.Length * 2 < Consts.MaxLengthOnStack.Rune)
        {
            Span<byte> codepointsBytes = stackalloc byte[input.Length * 4];
            var codepointsBytesLength = Encoding.UTF32.GetBytes(labelSpan, codepointsBytes);

            runes = MemoryMarshal.Cast<byte, Rune>(codepointsBytes[..codepointsBytesLength]);
        }
        else
        {
            runes = input.EnumerateRunes().ToArray();
        }

        var encodedLabel = Punycode.Encode(runes);
        return encodedLabel is null
            ? null :
            $"xn--{encodedLabel}";
    }

    private static MappingTableRow FindMapping(uint val)
    {
        if (IdnaMappingTable.Dictionary.TryGetValue(val, out var map))
        {
            return map;
        }

        var index = IdnaMappingTable.Rows.AsSpan().BinarySearch(val);
        if (index < 0)
        {
            index = ~index;
        }

        if (index < IdnaMappingTable.Rows.Length)
        {
            return IdnaMappingTable.Dictionary[IdnaMappingTable.Rows[index]];
        }

        return default;
    }

    private static string Map(string input)
    {
        var result = new StringBuilder(input.Length);
        foreach (var rune in input.EnumerateRunes())
        {
            var mapping = FindMapping((uint)rune.Value);
            switch (mapping.Status)
            {
                case IdnaStatus.Deviation:
                case IdnaStatus.Valid:
                case IdnaStatus.DisallowedSTD3Valid:
                case IdnaStatus.Disallowed:
                    result.AppendRune(rune);
                    break;
                case IdnaStatus.DisallowedSTD3Mapped:
                case IdnaStatus.Mapped:
                    result.Append(mapping.Mapping);
                    break;
                case IdnaStatus.Ignored:
                    break;
            }
        }

        return result.ToString();
    }

    private static bool IsValidLabel(string label)
    {
        if (label.Length == 0)
            return true;

        // 1. The label must be in Unicode Normalization Form NFC.
        // (C = NFC)
        if (!label.IsNormalized(NormalizationForm.FormC))
            return false;

        // CheckHyphens is always false in our case, so we skip 2 and 3

        // 4. If not CheckHyphens, the label must not begin with “xn--”.
        // TODO: https://github.com/whatwg/url/issues/803
        /* if (label.StartsWith("xn--"))
            return false; */

        // Skipping this step since we split by '.' before
        // 5. The label must not contain a U+002E ( . ) FULL STOP.

        var runes = label.Length <= Consts.MaxLengthOnStack.Rune
            ? stackalloc Rune[label.Length]
            : new Rune[label.Length];

        var i = 0;
        foreach (var rune in label.EnumerateRunes())
        {
            runes[i++] = rune;
        }

        runes = runes[..i];

        // 6. The label must not begin with a combining mark, that is: General_Category=Mark.
        if (Rune.GetUnicodeCategory(runes[0]).IsMark())
            return false;

        // 7. Each code point in the label must only have certain Status values according to Section 5, IDNA Mapping Table:
        foreach (var codepoint in runes)
        {
            var status = FindMapping((uint)codepoint.Value).Status;
            if (status is not (IdnaStatus.Valid or IdnaStatus.Deviation or IdnaStatus.DisallowedSTD3Valid))
            {
                return false;
            }
        }

        // (CheckJoiners is always true in WHATWG URL)
        // 8. If CheckJoiners, the label must satisify the ContextJ rules from Appendix A,
        // in The Unicode Code Points and Internationalized Domain Names for Applications (IDNA) [IDNA2008].
        if (!ValidJoiners(runes))
            return false;

        // 9. If CheckBidi, and if the domain name is a Bidi domain name,
        // then the label must satisfy all six of the numbered conditions in [IDNA2008] RFC 5893, Section 2.
        // (https://datatracker.ietf.org/doc/html/rfc5893#section-2)
        // (CheckBidi is always true in WHATWG URL)
        if (!ValidBidi(runes))
            return false;

        return true;
    }

    // Direct port of https://github.com/ada-url/idna/blob/fff988508f659ef5c6494572ebea3d5db2466ed0/src/validity.cpp#L1192
    private static bool ValidJoiners(ReadOnlySpan<Rune> label)
    {
        for (var i = 0; i < label.Length; i++)
        {
            int c = label[i].Value;
            if (c == 0x200c)
            {
                if (i > 0)
                {
                    if (UnicodeTables.ViramaSet.Contains(label[i - 1].Value))
                    {
                        return true;
                    }
                }
                if (i == 0 || i + 1 >= label.Length)
                {
                    return false;
                }

                // we go backward looking for L or D
                Func<int, bool> is_l_or_d = static code =>
                    UnicodeTables.LChar == code || UnicodeTables.DSet.Contains(code);
                Func<int, bool> is_r_or_d = static code =>
                    UnicodeTables.RSet.Contains(code) || UnicodeTables.DSet.Contains(code);

                return RuneAny(label[..i], is_l_or_d) && RuneAny(label[(i + 1)..], is_r_or_d);
            }

            if (c == 0x200d)
            {
                if (i > 0)
                {
                    if (UnicodeTables.ViramaSet.Contains(label[i - 1].Value))
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        return true;
    }

    private static bool RuneAny(ReadOnlySpan<Rune> runes, Func<int, bool> predicate)
    {
        foreach (var rune in runes)
        {
            if (predicate(rune.Value))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ValidBidi(ReadOnlySpan<Rune> label)
    {
        // GetDirection returns an enum, but we have to cast
        // to byte because of the MemoryExtensions method signatures
        var labelLength = label.Length;
        Span<byte> directions = labelLength <= Consts.MaxLengthOnStack.Byte
            ? stackalloc byte[labelLength]
            : new byte[labelLength];

        for (var i = 0; i < label.Length; i++)
        {
            directions[i] = (byte) label[i].GetDirection();
        }

        // this label is not RTL
        if (!directions.ContainsAny((byte) Direction.R, (byte) Direction.Al, (byte) Direction.An))
            return true;

        // 1. The first character must be a character with Bidi property L, R, or AL ...
        if (directions[0] is not ((byte) Direction.L or (byte) Direction.R or (byte) Direction.Al))
            return false;

        var lastNonNsm = directions.LastIndexOfAnyExcept((byte) Direction.Nsm);
        if (lastNonNsm == -1)   // just in case
            return false;

        // ... If it has the R or AL property, it is an RTL label; if it has the L property, it is an LTR label.
        if (directions[0] is (byte)Direction.R or (byte)Direction.Al)  // RTL
        {
            var enPresent = false;
            var anPresent = false;

            for (int i = 0; i < lastNonNsm; i++)
            {
                // 2. In an RTL label, only characters with the Bidi properties ... are allowed.
                if (!RtlAllowedDirections.Contains(directions[i]))
                    return false;

                if (directions[i] == (byte)Direction.En)
                    enPresent = true;
                else if (directions[i] == (byte)Direction.An)
                    anPresent = true;
            }

            // 4. In an RTL label, if an EN is present, no AN may be present, and vice versa.
            if (enPresent && anPresent)
                return false;

            // 3. In an RTL label, the end of the label must be a character with
            // Bidi property R, AL, EN, or AN, followed by zero or more characters with Bidi property NSM.
            if (directions[lastNonNsm] is not ((byte)Direction.R
                or (byte)Direction.Al
                or (byte)Direction.En
                or (byte)Direction.An))
            {
                return false;
            }

        }
        else  // LTR
        {
            // 5. In an LTR label, only characters with the Bidi properties ... are allowed.
            if (directions.ContainsAnyExcept(LtrAllowedDirections))
                return false;

            // 6. In an LTR label, the end of the label must be a character
            // with Bidi property L or EN, followed by zero or more characters with Bidi property NSM.
            if (directions[lastNonNsm] is not ((byte)Direction.L or (byte)Direction.En))
                return false;
        }

        return true;
    }



    private static readonly SearchValues<byte> RtlAllowedDirections =
        SearchValues.Create(
        [
            (byte) Direction.R,
            (byte) Direction.Al,
            (byte) Direction.An,
            (byte) Direction.En,
            (byte) Direction.Es,
            (byte) Direction.Cs,
            (byte) Direction.Et,
            (byte) Direction.On,
            (byte) Direction.Bn,
            (byte) Direction.Nsm
        ]);

    private static readonly SearchValues<byte> LtrAllowedDirections =
        SearchValues.Create(
        [
            (byte) Direction.L,
            (byte) Direction.En,
            (byte) Direction.Es,
            (byte) Direction.Cs,
            (byte) Direction.Et,
            (byte) Direction.On,
            (byte) Direction.Bn,
            (byte) Direction.Nsm
        ]);
}
