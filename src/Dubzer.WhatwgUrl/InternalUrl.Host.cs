using System;
using System.Buffers;

namespace Dubzer.WhatwgUrl;

internal partial class InternalUrl
{
    private static readonly SearchValues<char> HostStateEnd = SearchValues.Create(['/', '?', '#', ':', '[']);
    private static readonly SearchValues<char> SpecialHostStateEnd = SearchValues.Create(['/', '?', '#', ':', '\\', '[']);
    private static readonly SearchValues<char> HostStateEndInIpv6 = SearchValues.Create(['/', '?', '#', ']']);
    private static readonly SearchValues<char> SpecialHostStateEndInIpv6 = SearchValues.Create(['/', '?', '#', '\\', ']']);

    // https://url.spec.whatwg.org/#host-state
    protected virtual void HostState(char c)
    {
        var isSpecial = IsSpecial;

        var rawHost = Remainder;
        var endsAtChar = '\0';
        var searchOffset = 0;
        var insideBrackets = false;

        while (true)
        {
            SearchValues<char> searchValues;
            if (!insideBrackets)
                searchValues = isSpecial ? SpecialHostStateEnd : HostStateEnd;
            else
                searchValues = isSpecial ? SpecialHostStateEndInIpv6 : HostStateEndInIpv6;

            var index = rawHost[searchOffset..].IndexOfAny(searchValues);
            if (index == -1)
                break;

            var charOffset = searchOffset + index;
            var currentChar = rawHost[charOffset];

            switch (currentChar)
            {
                case '[':
                    insideBrackets = true;
                    searchOffset = charOffset + 1;
                    continue;
                case ']':
                    insideBrackets = false;
                    searchOffset = charOffset + 1;
                    continue;
            }

            rawHost = rawHost[..charOffset];
            endsAtChar = currentChar;
            break;
        }

        if (rawHost.Length == 0 && (endsAtChar == ':' || isSpecial))
        {
            Error = UrlErrorCode.HostMissing;
            return;
        }

        var parseResult = endsAtChar == ':'
            ? HostParser.Parse(rawHost, true)
            : HostParser.Parse(rawHost, !isSpecial);

        if (!parseResult)
        {
            Error = parseResult.Error;
            return;
        }

        Host = parseResult.Value.ToComponent(Pointer, rawHost.Length);
        if (endsAtChar == ':')
        {
            Pointer += rawHost.Length;
            State = InternalUrlParserState.Port;
            return;
        }

        Pointer += rawHost.Length - 1;
        State = InternalUrlParserState.PathStart;
    }

    private static UrlComponent CloneHost(InternalUrl source) =>
        source.Host.Materialize(source.Input);
}
