namespace Dubzer.WhatwgUrl.Uts46;

internal readonly struct MappingTableRow(IdnaStatus status, string mapping)
{
    internal readonly IdnaStatus Status = status;
    internal readonly string Mapping = mapping;
}
