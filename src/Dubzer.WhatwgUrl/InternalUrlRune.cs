using System;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Dubzer.WhatwgUrl;

/// <summary>
/// This class provides support for Unicode URLs
/// </summary>
internal sealed class InternalUrlRune : InternalUrl
{
    private Rune[] _inputRunes = [];
    private Rune _currentRune;
    private bool _arrFlag;

    public override Result<InternalUrl> Parse(string input, InternalUrl? baseUrl = null)
    {
        BaseUrl = baseUrl;

        Input = InputUtils.Format(input);
        Buf = new StringBuilder(Input.Length);

        _inputRunes = Input.EnumerateRunes().ToArray();
        Length = _inputRunes.Length;

        for (; Pointer <= Length; Pointer++)
        {
            _currentRune = Pointer < Length ? _inputRunes[Pointer] : new Rune('\u0000');
            var c = _currentRune.ToChar();

            Debug.WriteLine($"State: {State}, rune: {_currentRune}");
            RunStateMachine(c);
            if (Error != null)
                return Result<InternalUrl>.Failure(Error.Value);
        }

        return Result<InternalUrl>.Success(this);
    }

    // char arguments are not used in these methods.
    // Tried moving them to the class field,
    // but it resulted in worse performance for the
    // 100k benchmark, which is critical
    protected override void AppendCurrent(char c)
    {
        GetBuf().AppendRune(_currentRune);
    }

    protected override void AppendCurrentEncoded(char c, ReadOnlySpan<byte> set)
    {
        PercentEncoding.AppendEncoded(_currentRune, GetBuf(), set);
    }

    protected override void AppendCurrentEncodedInC0(char c)
    {
        PercentEncoding.AppendEncodedInC0(_currentRune, GetBuf());
    }

    protected override void AuthorityState(char c)
    {
        if (c == '@')
        {
            Debug.WriteLine("invalid-credentials");
            if (AtSignSeen)
                GetBuf().Insert(0, "%40");
            else
                AtSignSeen = true;

            AuthorityStringBuilder ??= new StringBuilder();
            foreach (var rune in (Buf?.ToString() ?? "").EnumerateRunes())
            {
                if (rune == new Rune(':') && !PasswordTokenSeen)
                {
                    Username = AuthorityStringBuilder.ToString();
                    AuthorityStringBuilder.Clear();
                    PasswordTokenSeen = true;
                    continue;
                }

                PercentEncoding.AppendEncoded(rune, AuthorityStringBuilder, PercentEncoding.UserInfoEncodeSetLookup);
            }

            Buf?.Clear();
        }
        else if (c is '/' or '?' or '#' || (IsSpecial && c == '\\') || Pointer == Length)
        {
            if (AtSignSeen && BufLength == 0)
            {
                Error = UrlErrorCode.HostMissing;
                return;
            }

            if (AuthorityStringBuilder != null)
            {
                if (!PasswordTokenSeen)
                {
                    Username = AuthorityStringBuilder!.ToString();
                }
                else
                {
                    Password = AuthorityStringBuilder!.ToString();
                }

                AuthorityStringBuilder.Clear();
            }

            Pointer -= (Buf?.ToString() ?? "").EnumerateRunes().Count() + 1;
            Buf?.Clear();
            State = InternalUrlParserState.Host;
        }
        else
        {
            GetBuf().AppendRune(_currentRune);
        }
    }

    protected override void HostState(char c)
    {
        if (c == ':' && !_arrFlag)
        {
            if (BufLength == 0)
            {
                Error = UrlErrorCode.HostMissing;
                return;
            }

            var input = Buf?.ToString() ?? "";
            var parseResult = HostParser.Parse(input, true);
            if (!parseResult)
            {
                Error = parseResult.Error;
                return;
            }

            Host = parseResult.Value.ToComponent(input);
            Buf?.Clear();
            State = InternalUrlParserState.Port;
        }
        else if (c is '/' or '?' or '#' || IsSpecial && c == '\\' || Pointer == Length)
        {
            Pointer--;

            if (IsSpecial && BufLength == 0)
            {
                Error = UrlErrorCode.HostMissing;
                return;
            }

            var input = Buf?.ToString() ?? "";
            var parseResult = HostParser.Parse(input, !IsSpecial);
            if (!parseResult)
            {
                Error = parseResult.Error;
                return;
            }

            Host = parseResult.Value.ToComponent(input);
            Buf?.Clear();
            State = InternalUrlParserState.PathStart;
        }
        else
        {
            if (c == '[')
                _arrFlag = true;
            else if (c == ']')
                _arrFlag = false;

            AppendCurrent(c);
        }
    }

    protected override void PathState(char c)
    {
        if (Pointer == Length || c is '/' or '?' or '#' || (c == '\\' && IsSpecial))
        {
            if (IsSpecial && c == '\\')
                Debug.WriteLine("invalid-reverse-solidus");

            var str = Buf?.ToString() ?? "";
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

            Buf?.Clear();
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

    // https://url.spec.whatwg.org/#query-state
    protected override void QueryState(char c)
    {
        // skipping this since we don't support other encodings
        // 1. If encoding is not UTF-8 and one of the following is true: ...

        // 2. If one of the following is true:
        // state override is not given and c is U+0023 (#)
        // c is the EOF code point
        if (c == '#' || Pointer >= Length)
        {
            var inputToEncode = Buf?.ToString() ?? "";
            var buf = GetBuf();
            buf.Clear();

            if (IsSpecial)
                PercentEncoding.PercentEncode(inputToEncode, PercentEncoding.InSpecialQueryEncodeSet, buf);
            else
                PercentEncoding.PercentEncode(inputToEncode, PercentEncoding.InQueryEncodeSet, buf);

            Query = new UrlComponent(buf.ToString());
            buf.Clear();

            // If c is U+0023 (#), then set url’s fragment to the empty string and state to fragment state.
            if (c == '#')
            {
                State = InternalUrlParserState.Fragment;
            }
        }
        else
        {
            AppendCurrent(c);
        }
    }

    // https://url.spec.whatwg.org/#fragment-state
    protected override void FragmentState(char c)
    {
        if (Pointer == Length)
        {
            Fragment = new UrlComponent(Buf?.ToString() ?? "");
            Buf?.Clear();
            return;
        }

        AppendCurrentEncoded(c, PercentEncoding.FragmentEncodeSetLookup);
    }

    // helper with bound guard
    protected override char NextChar(int n) =>
        Pointer + n >= Length
            ? '\0'
            : _inputRunes[Pointer + n].ToChar();
}
