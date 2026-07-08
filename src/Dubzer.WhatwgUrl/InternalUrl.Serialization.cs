using System;
using System.Diagnostics;
using System.Globalization;

namespace Dubzer.WhatwgUrl;

internal partial class InternalUrl
{
    // https://url.spec.whatwg.org/#url-serializing
    internal string SerializeUrl(bool excludeFragment = false)
    {
        return string.Create(
            GetSerializedUrlLength(excludeFragment),
            (Url: this, ExcludeFragment: excludeFragment),
            static (buffer, state) => state.Url.WriteSerializedUrl(buffer, state.ExcludeFragment));
    }

    private void WriteSerializedUrl(Span<char> destination, bool excludeFragment)
    {
        var offset = 0;

        // 1. Let output be url’s scheme and U+003A (:) concatenated.
        Scheme.CopyTo(destination);
        offset += Scheme.Length;
        destination[offset++] = ':';

        if (Host.HasValue)
        {
            destination[offset++] = '/';
            destination[offset++] = '/';
            if (!string.IsNullOrEmpty(Username) || !string.IsNullOrEmpty(Password))
            {
                Username.CopyTo(destination[offset..]);
                offset += Username.Length;
                if (!string.IsNullOrEmpty(Password))
                {
                    destination[offset++] = ':';
                    Password.CopyTo(destination[offset..]);
                    offset += Password.Length;
                }

                destination[offset++] = '@';
            }

            var host = Host.AsSpan(Input);
            host.CopyTo(destination[offset..]);
            offset += host.Length;
            if (Port.HasValue)
            {
                destination[offset++] = ':';
                var formatted = Port.GetValueOrDefault()
                    .TryFormat(destination[offset..], out var written, provider: CultureInfo.InvariantCulture);
                Debug.Assert(formatted);
                offset += written;
            }
        }

        // This prevents web+demo:/.//not-a-host/ or web+demo:/path/..//not-a-host/,
        // when parsed and then serialized,
        // from ending up as web+demo://not-a-host/ (they end up as web+demo:/.//not-a-host/).
        if (ShouldPrefixPathWithDotSegment())
        {
            destination[offset++] = '/';
            destination[offset++] = '.';
        }

        if (_opaquePath != null)
        {
            _opaquePath.CopyTo(destination[offset..]);
            offset += _opaquePath.Length;
        }
        else
        {
            for (var i = 0; i < Path.Count; i++)
            {
                destination[offset++] = '/';

                var pathComponent = Path[i].AsSpan(Input);
                pathComponent.CopyTo(destination[offset..]);
                offset += pathComponent.Length;
            }
        }

        if (Query.HasValue)
        {
            destination[offset++] = '?';

            var query = Query.AsSpan(Input);
            query.CopyTo(destination[offset..]);
            offset += query.Length;
        }

        if (!excludeFragment && Fragment.HasValue)
        {
            destination[offset++] = '#';

            var fragment = Fragment.AsSpan(Input);
            fragment.CopyTo(destination[offset..]);
            offset += fragment.Length;
        }

        Debug.Assert(offset == destination.Length);
    }

    private int GetSerializedUrlLength(bool excludeFragment)
    {
        // 1. Let output be url’s scheme and U+003A (:) concatenated.
        var length = Scheme.Length + 1;

        if (Host.HasValue)
        {
            // Append "//" to output.
            length += 2 + Host.SerializedLength;

            if (!string.IsNullOrEmpty(Username) || !string.IsNullOrEmpty(Password))
            {
                length += Username.Length + 1;
                if (!string.IsNullOrEmpty(Password))
                    length += Password.Length + 1;
            }

            if (Port.HasValue)
                length += Port.GetValueOrDefault() switch
                {
                    < 10 => 2,
                    < 100 => 3,
                    < 1000 => 4,
                    < 10000 => 5,
                    _ => 6
                };
        }

        // This prevents web+demo:/.//not-a-host/ or web+demo:/path/..//not-a-host/,
        // when parsed and then serialized,
        // from ending up as web+demo://not-a-host/ (they end up as web+demo:/.//not-a-host/).
        if (ShouldPrefixPathWithDotSegment())
            length += 2;

        if (_opaquePath != null)
        {
            length += _opaquePath.Length;
        }
        else
        {
            for (var i = 0; i < Path.Count; i++)
                length += Path[i].SerializedLength + 1;
        }

        if (Query.HasValue)
            length += Query.SerializedLength + 1;

        if (!excludeFragment && Fragment.HasValue)
            length += Fragment.SerializedLength + 1;

        return length;
    }
    
    private bool ShouldPrefixPathWithDotSegment()
    {
        if (Host.HasValue || _opaquePath != null || Path.Count == 0)
            return false;

        // The spec checks whether the path list has more than one segment and starts with an empty segment.
        // Our fast path can store multiple slash-separated segments in one component, so check whether the
        // serialized path would start with "//" instead.
        var firstPathComponent = Path[0].AsSpan(Input);
        if (firstPathComponent.IsEmpty)
            return Path.Count > 1;

        return firstPathComponent[0] == '/';
    }
}
