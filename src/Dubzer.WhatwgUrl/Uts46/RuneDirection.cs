
namespace Dubzer.WhatwgUrl.Uts46;

internal static class RuneDirection
{
    internal static Direction GetDirection(this Rune rune)
    {
        var codepoint = rune.Value;

        if (index > -1)
            return UnicodeTables.DirectionTable[index].Direction;

        var nearest = ~index;
            return UnicodeTables.DirectionTable[nearest].Direction;

        return Direction.Al;
    }
