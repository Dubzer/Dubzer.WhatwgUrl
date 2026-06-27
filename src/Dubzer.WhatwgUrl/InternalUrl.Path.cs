using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Dubzer.WhatwgUrl;

internal partial class InternalUrl
{
    private bool _triedFastPath;
    private protected PathBuffer Path;

    private static PathBuffer ClonePath(InternalUrl source)
    {
        var copy = new PathBuffer();
        for (var i = 0; i < source.Path.Count; i++)
            copy.Add(source.Path[i].Materialize(source.Input));

        return copy;
    }

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

            var str = _buf?.ToString() ?? "";
            if (Util.IsDoubleDot(str))
            {
                ShortenPath();

                if (c != '/' && !(c == '\\' && IsSpecial))
                    Path.Add(UrlComponent.Empty);
            }
            else if (Util.IsSingleDot(str) && c != '/' && !(c == '\\' && IsSpecial))
            {
                Path.Add(UrlComponent.Empty);
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

                Path.Add(new UrlComponent(str));
            }

            _buf?.Clear();
            switch (c)
            {
                case '?':
                    State = InternalUrlParserState.Query;
                    break;
                case '#':
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
        if (Path.Count != 0 || Scheme == Schemes.File)
        {
            Pointer--;
            return;
        }

        var remainder = Remainder;

        if (remainder.Length == 0)
        {
            Path.Add(UrlComponent.Empty);
            return;
        }

        var lastInPath = remainder.IndexOfAny(LastInPathSearchValues);
        var endsWithChar = '\u0000';
        ReadOnlySpan<char> path;
        // a trick to avoid a bound check
        if ((uint)lastInPath >= (uint)remainder.Length)
        {
            lastInPath = remainder.Length;
            path = remainder;
        }
        else
        {
            path = remainder[..lastInPath];
            endsWithChar = remainder[lastInPath];
        }

        var handled = PercentEncoding.AppendEncodedPath(path, out var encodedPath);

        // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
        switch (handled)
        {
            case PercentEncoding.AppendEncodedPathResult.Handled:
                Path.Add(new UrlComponent(encodedPath));
                break;
            case PercentEncoding.AppendEncodedPathResult.NoProcessing:
                Path.Add(new UrlComponent(Pointer, path.Length));
                break;
            case PercentEncoding.AppendEncodedPathResult.Fallback:
                Pointer--;
                return;
        }

        Pointer += lastInPath;

        switch (endsWithChar)
        {
            case '?':
                State = InternalUrlParserState.Query;
                break;
            case '#':
                State = InternalUrlParserState.Fragment;
                break;
        }
    }

    // https://url.spec.whatwg.org/#shorten-a-urls-path
    protected void ShortenPath()
    {
        // If url’s scheme is "file", path’s size is 1, and path[0] is a normalized Windows drive letter, then return.
        if (Scheme == Schemes.File && Path.Count == 1 && IsNormalizedWindowDriveLetter(Path[0].AsSpan(Input)))
            return;

        // Remove path’s last item, if any.
        if (Path.Count == 0)
            return;

        var lastPart = Path[^1];
        var lastPartSpan = lastPart.AsSpan(Input);
        var slashInPart = lastPartSpan.LastIndexOf('/');
        if (slashInPart != -1)
        {
            Path[^1] = new UrlComponent(lastPartSpan[..slashInPart].ToString());
        }
        else
        {
            Path.RemoveLast();
        }
    }

    private void AppendSerializedPath(StringBuilder sb)
    {
        for (var i = 0; i < Path.Count; i++)
            AppendSerializedComponent(sb, Path[i], Input, '/');
    }

    // https://url.spec.whatwg.org/#url-path-serializer
    internal string SerializePathname()
    {
        if (_opaquePath != null)
            return _opaquePath;

        if (Path.Count == 0)
            return "";

        if (Path.Count == 1)
            return SerializeComponent(Path[0], Input, '/');

        var sb = new StringBuilder();
        AppendSerializedPath(sb);

        return sb.ToString();
    }

    private protected struct PathBuffer
    {
        private UrlComponent _first;
        private List<UrlComponent>? _overflow;

        internal int Count { get; private set; }

        internal UrlComponent this[int index]
        {
            readonly get
            {
                if ((uint)index >= (uint)Count)
                    throw new ArgumentOutOfRangeException(nameof(index));

                if (index == 0)
                    return _first;

                return _overflow![index - 1];
            }
            set
            {
                if ((uint)index >= (uint)Count)
                    throw new ArgumentOutOfRangeException(nameof(index));

                if (index == 0)
                {
                    _first = value;
                    return;
                }

                _overflow![index - 1] = value;
            }
        }

        internal UrlComponent this[Index index]
        {
            readonly get => this[index.GetOffset(Count)];
            set => this[index.GetOffset(Count)] = value;
        }

        internal void Add(UrlComponent component)
        {
            if (Count == 0)
            {
                _first = component;
                Count = 1;
                return;
            }

            _overflow ??= new List<UrlComponent>(4);

            _overflow.Add(component);
            Count++;
        }

        internal void Clear()
        {
            _first = default;
            _overflow = null;
            Count = 0;
        }

        internal void RemoveLast()
        {
            Debug.Assert(Count > 0);

            if (Count == 1)
            {
                Clear();
                return;
            }

            _overflow!.RemoveAt(Count - 2);
            Count--;
        }
    }
}
