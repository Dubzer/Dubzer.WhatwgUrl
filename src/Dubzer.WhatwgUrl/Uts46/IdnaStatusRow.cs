using System;

namespace Dubzer.WhatwgUrl.Uts46;

internal ref struct IdnaStatusRow(IdnaStatus status, ReadOnlySpan<char> mapping)
{
    internal readonly IdnaStatus Status = status;
    internal readonly ReadOnlySpan<char> Mapping = mapping;
}
