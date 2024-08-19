using System;
using System.Text;

namespace Dubzer.WhatwgUrl.Uts46;

internal static class RuneDirection
{
    private readonly struct DirectionDataComparable(int value) : IComparable<DirectionData>
    {
        public int CompareTo(DirectionData other) => value.CompareTo(other.RangeEnd);
    }

    internal static Direction GetDirection(this Rune rune)
    {
        var codepoint = rune.Value;
        var index = UnicodeTables.DirectionTable.AsSpan().BinarySearch(new DirectionDataComparable(codepoint));

        if (index > -1)
            return UnicodeTables.DirectionTable[index].Direction;

        var nearest = ~index;
        if (UnicodeTables.DirectionTable[nearest].RangeStart <= rune.Value)
            return UnicodeTables.DirectionTable[nearest].Direction;

        return Direction.Al;
    }
}
