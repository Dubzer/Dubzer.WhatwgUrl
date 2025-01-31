using System.Collections.Generic;
using System.Linq;

namespace Dubzer.WhatwgUrl.Codegen;

internal class UintArrayComparer : IEqualityComparer<uint[]>
{
    public bool Equals(uint[]? x, uint[]? y)
    {
        if (x == null || y == null)
            return x == y;

        return x.Length == y.Length && x.SequenceEqual(y);
    }

    public int GetHashCode(uint[] obj)
    {
        int hash = 17;
        foreach (var item in obj)
        {
            hash = hash * 31 + item.GetHashCode();
        }

        return hash;
    }
}
