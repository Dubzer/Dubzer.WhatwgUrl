using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Dubzer.WhatwgUrl.BclInternal;

namespace Dubzer.WhatwgUrl;

internal partial class InternalUrl
{
    private bool _triedFastPath;
    protected List<string> Path = [];

    /// <summary>
    /// this is a special case for the PathStateFast,
    /// which outputs path as a single string
    /// that doesn't require prepending '/' on serialization
    /// </summary>
    private bool _firstPathSegmentWithSlash;

    protected virtual void PathState(char c)
    {
        if (!_triedFastPath)
        {
            _triedFastPath = true;
            PathStateFast();
            return;
        }

        if (Pointer == Length || c is '/' or '?' or '#' || (c == '\\' && IsSpecial))
        {
            if (IsSpecial && c == '\\')
                Debug.WriteLine("invalid-reverse-solidus");

            var str = Buf.ToString();
            if (Util.IsDoubleDot(str))
            {
                ShortenPath();

                if (c != '/' && !(c == '\\' && IsSpecial))
                    Path.Add("");
            }
            else if (Util.IsSingleDot(str) && c != '/' && !(c == '\\' && IsSpecial))
            {
                Path.Add("");
            }
            else if (!Util.IsSingleDot(str))
            {
                if (Scheme == Schemes.File
                    && Path.Count == 0
                    && str.Length == 2
                    && char.IsAsciiLetter(str[0])
                    && str[1] is '|')
                {
                    str = $"{str[0]}:";
                }

                Path.Add(str);
            }

            Buf.Clear();
            switch (c)
            {
                case '?':
                    State = InternalUrlParserState.Query;
                    break;
                case '#':
                    Buf.EnsureCapacity(Length - Pointer);
                    State = InternalUrlParserState.Fragment;
                    break;
            }
        }
        else
        {
            // add parse error here
            if (c == '%' && !char.IsAsciiHexDigit(NextChar(1)) && !char.IsAsciiHexDigit(NextChar(2)))
                Debug.WriteLine("invalid-URL-unit");

            AppendCurrentEncoded(c, PercentEncoding.PathEncodeSetLookup);
        }
    }

    private static readonly SearchValues<char> LastInPathSearchValues = SearchValues.Create("?#");

    // This implementation handles the whole path in one state machine iteration
    private void PathStateFast()
    {
        if (Path.Count != 0 || string.Equals(Scheme, Schemes.File, StringComparison.Ordinal))
        {
            Pointer--;
            return;
        }

        var inputRemainder = Input.AsSpan()[Pointer..];

        if (inputRemainder.Length == 0)
        {
            Path.Add("/");
            _firstPathSegmentWithSlash = true;
            return;
        }

        var lastInPath = inputRemainder.IndexOfAny(LastInPathSearchValues);
        var endsWithChar = '\u0000';
        ReadOnlySpan<char> path;
        if (lastInPath == -1)
        {
            lastInPath = inputRemainder.Length;
            path = inputRemainder;
        }
        else
        {
            path = inputRemainder[..lastInPath];
            endsWithChar = inputRemainder[lastInPath];
        }

        var vsb = new ValueStringBuilder(Consts.MaxLengthOnStack.Char);
        try
        {
            vsb.Append('/');

            var handled = PercentEncoding.AppendEncodedPath(path, ref vsb);

            if (!handled)
            {
                // fallback to slow path
                Pointer--;

                return;
            }

            Path.Add(vsb.Length == 0 ? path.ToString() : vsb.ToString());
            _firstPathSegmentWithSlash = true;

            Pointer += lastInPath;
            switch (endsWithChar)
            {
                case '?':
                    State = InternalUrlParserState.Query;
                    break;
                case '#':
                    Buf.EnsureCapacity(Length - Pointer);
                    State = InternalUrlParserState.Fragment;
                    break;
            }
        }
        finally
        {
            vsb.Dispose();
        }
    }

    // https://url.spec.whatwg.org/#shorten-a-urls-path
    protected void ShortenPath()
    {
        // If url’s scheme is "file", path’s size is 1, and path[0] is a normalized Windows drive letter, then return.
        if (Scheme == Schemes.File && Path.Count == 1 && IsNormalizedWindowDriveLetter(Path[0]))
            return;

        // Remove path’s last item, if any.
        if (Path.Count == 0)
            return;

        var lastPart = Path[^1];
        var slashInPart = lastPart.LastIndexOf('/');
        if (slashInPart > 0)
        {
            Path[^1] = lastPart[..slashInPart];
        }
        else
        {
            // we no longer have a segment with handled slash
            if (Path.Count == 1)
                _firstPathSegmentWithSlash = false;

            Path.RemoveAt(Path.Count - 1);
        }
    }

    // https://url.spec.whatwg.org/#url-path-serializer
    internal string SerializePathname()
    {
        if (_opaquePath != null)
            return _opaquePath;

        // we can skip sb allocation because we know that first segment already starts with '/'
        // and there's only one segment
        if (_firstPathSegmentWithSlash && Path.Count == 1)
        {
            return Path[0];
        }

        var i = 0;
        var sb = new StringBuilder();

        if (_firstPathSegmentWithSlash)
        {
            sb.Append(Path[0]);
            i++;
        }

        for (; i < Path.Count; i++)
        {
            sb.Append('/');
            sb.Append(Path[i]);
        }

        return sb.ToString();
    }
}