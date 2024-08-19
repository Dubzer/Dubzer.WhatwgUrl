using System.Collections.Frozen;
using System.Collections.Generic;

namespace Dubzer.WhatwgUrl;

internal static class Schemes
{
    internal const string Ftp = "ftp";
    internal const string File = "file";
    internal const string Http = "http";
    internal const string Https = "https";
    internal const string Ws = "ws";
    internal const string Wss = "wss";
    internal const string Blob = "blob";

    internal static readonly FrozenDictionary<string, int?> Special = new Dictionary<string, int?>
    {
        { Ftp, 21 },
        { File, null },
        { Http, 80 },
        { Https, 443 },
        { Ws, 80 },
        { Wss, 443 }
    }.ToFrozenDictionary();
}