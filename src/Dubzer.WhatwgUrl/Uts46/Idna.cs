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

    public static IdnaStatusRow FindMapping(uint codepoint)
    {
        // fast path for the most common case in ASCII range
        if (codepoint is <= 0x0040 or >= 0x005B and <= 0x007F)
            return new(IdnaStatus.Valid, ReadOnlySpan<char>.Empty);

        var flaggedMainRef = IdnaMappingTable.MainRefs[(int)codepoint >> 6];

        // reference without a flag
        var mainRef = flaggedMainRef & ~IdnaMappingTable.RefBoolPackFlag;

        // this is an offset within codepoints block
        var offsetWithinBlock = codepoint & 0b111111;

        // if the most significant bit is set, then it's a bool pack
        if (mainRef != flaggedMainRef)
        {
            var pack = IdnaMappingTable.BoolPacks[(int) mainRef];

            return ((pack >> (int) offsetWithinBlock) & 1ul) == 1
                ? new(IdnaStatus.Valid, ReadOnlySpan<char>.Empty)
                : new(IdnaStatus.Disallowed, ReadOnlySpan<char>.Empty);
        }

        // in this case, cleanIdnaRef is a reference to the start of the block we need, within contiguous array.
        // so by adding the offset, we can get an index of the codepoint status in the array
        var index = mainRef + offsetWithinBlock;
        var result = IdnaMappingTable.RefBlocks[(int) index];

        switch (result)
        {
            case IdnaMappingTable.RefBlockValid:
                return new(IdnaStatus.Valid, ReadOnlySpan<char>.Empty);
            case IdnaMappingTable.RefBlockInvalid:
                return new(IdnaStatus.Disallowed, ReadOnlySpan<char>.Empty);
            case IdnaMappingTable.RefBlockIgnored:
                return new(IdnaStatus.Ignored, ReadOnlySpan<char>.Empty);
            default:
                var mappingLength = (int) result >> 16;
                var mappingOffset = (int) result & 0xFFFF;

                var span = IdnaMappingTable.Mappings.AsSpan()[mappingOffset..(mappingOffset + mappingLength)];
                return new (IdnaStatus.Mapped, span);
        }
    }

    private static string Map(string input)
    {
        // 13 is the max mapping length. Presumably that's the worst case scenario
        if (input.Length < Consts.MaxLengthOnStack.Char / 13)
        {
            Span<char> chars = stackalloc char[Consts.MaxLengthOnStack.Char];

            var nextCharI = 0;
            foreach (var rune in input.EnumerateRunes())
            {
                var mapping = FindMapping((uint)rune.Value);
                switch (mapping.Status)
                {
                    case IdnaStatus.Valid:
                    case IdnaStatus.Disallowed:
                        var codepoint = rune.Value;

                        // Inlined Rune.IsBmp
                        if (codepoint <= ushort.MaxValue)
                        {
                            chars[nextCharI] = (char) codepoint;
                            nextCharI++;
                        }
                        else
                        {
                            // Inlined Rune.EncodeToUtf16 => UnicodeUtility.GetUtf16SurrogatesFromSupplementaryPlaneScalar
                            chars[nextCharI] = (char) (codepoint + 56557568U >> 10);
                            chars[nextCharI + 1] = (char) ((codepoint & 1023) + 56320);
                            nextCharI += 2;
                        }

                        break;
                    case IdnaStatus.Mapped:
                        mapping.Mapping.CopyTo(chars[nextCharI..]);
                        nextCharI += mapping.Mapping.Length;
                        break;
                    case IdnaStatus.Ignored:
                        break;
                }
            }

            return new string(chars[..nextCharI]);
        }

        var result = new StringBuilder(input.Length);
        foreach (var rune in input.EnumerateRunes())
        {
            var mapping = FindMapping((uint)rune.Value);
            switch (mapping.Status)
            {
                case IdnaStatus.Valid:
                case IdnaStatus.Disallowed:
                    result.AppendRune(rune);
                    break;
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
            if (status is not IdnaStatus.Valid)
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

    private static bool ValidJoiners(ReadOnlySpan<Rune> label)
    {
        const int ZWNJ = 0x200C;
        const int ZWJ = 0x200D;

        var codepoints = MemoryMarshal.Cast<Rune, int>(label);
        var index = codepoints.IndexOfAny(ZWNJ, ZWJ);;

        if (index == -1)
            return true;

        switch (codepoints[index])
        {
            case ZWNJ:
                if (index > 0 && UnicodeTables.ViramaSet.Contains(codepoints[index - 1]))
                    return true;

                if (index == 0 || index + 1 >= label.Length)
                    return false;

                var found = false;
                foreach (var c in label[..index])
                {
                    if (UnicodeTables.LChar == c.Value || UnicodeTables.DSet.Contains(c.Value))
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                    return false;

                found = false;
                foreach (var c in label[(index + 1)..])
                {
                    if (UnicodeTables.RSet.Contains(c.Value) || UnicodeTables.DSet.Contains(c.Value))
                    {
                        found = true;
                        break;
                    }
                }

                return found;
            case ZWJ:
                return index > 0 && UnicodeTables.ViramaSet.Contains(codepoints[index - 1]);
        }

        return true;
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
