using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Dubzer.WhatwgUrl.BclInternal;

namespace Dubzer.WhatwgUrl;

internal partial class InternalUrl
{
    private bool _triedFastPath;
    protected List<string> Path = [];

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

            AppendCurrentEncoded(c, PercentEncoding.PathEncodeSet);
        }
    }

    /// This implementation handles the whole path in one state machine iteration
    private void PathStateFast()
    {
        if (Path.Count != 0 || Scheme == Schemes.File)
        {
            Pointer--;
            return;
        }

        var inputRemainder = Input.AsSpan()[Pointer..];

        if (inputRemainder.Length == 0)
        {
            Path.Add("");
            return;
        }

        var lastInPath = inputRemainder.IndexOfAny('?', '#');
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


        var vsb = new ValueStringBuilder(stackalloc char[Consts.MaxLengthOnStack.Char]);
        var handled = PercentEncoding.AppendEncodedPath(path, ref vsb);

        if (!handled)
        {
            // fallback to slow path
            Pointer--;

            vsb.Dispose();
            return;
        }

        Path.Add(vsb.Length == 0 ? path.ToString() : vsb.ToString());

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

    // https://url.spec.whatwg.org/#url-path-serializer
    internal string SerializePathname()
    {
        if (_opaquePath != null)
            return _opaquePath;

        var sb = new StringBuilder();
        foreach (var segment in Path)
        {
            sb.Append('/');
            sb.Append(segment);
        }

        return sb.ToString();
    }
}