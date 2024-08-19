using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Dubzer.WhatwgUrl;

internal class InternalUrl
{
    internal string Scheme = "";
    internal string? Host;
    internal int? Port;
    internal string? Query;
    internal string? Fragment;
    internal string Username = "";
    internal string Password = "";

    protected UrlErrorCode? Error;

    protected int Pointer;
    protected InternalUrlParserState State = InternalUrlParserState.SchemeStart;
    protected StringBuilder Buf = null!;
    protected StringBuilder? AuthorityStringBuilder;
    protected string Input = "";
    protected int Length;

    protected bool IsSpecial => Schemes.Special.ContainsKey(Scheme);

    protected InternalUrl? BaseUrl;

    protected bool AtSignSeen;
    protected bool PasswordTokenSeen;

    /// <summary>
    /// Pointer is inside an array (ipv6)
    /// </summary>
    private bool _arrFlag;

    private string? _opaquePath;
    private List<string> _path = [];

    public virtual Result<InternalUrl> Parse(string input, InternalUrl? baseUrl = null)
    {
        BaseUrl = baseUrl;

        Input = InputUtils.Format(input);
        Buf = new StringBuilder(Input.Length);

        Length = Input.Length;

        for (; Pointer <= Length; Pointer++)
        {
            var c = Pointer < Length ? Input[Pointer] : '\0';

            Debug.WriteLine($"State: {State}, char: {c}");
            RunStateMachine(c);
            if (Error != null)
                return Result<InternalUrl>.Failure(Error.Value);
        }

        return Result<InternalUrl>.Success(this);
    }

    protected virtual void AppendCurrent(char c)
    {
        Buf.Append(c);
    }

    protected virtual void AppendCurrentEncoded(char c, FrozenSet<char> set)
    {
        PercentEncoding.AppendEncoded(c, Buf, set);
    }

    protected virtual void AppendCurrentEncodedInC0(char c)
    {
        PercentEncoding.AppendEncodedInC0(c, Buf);
    }

    #region State machine switch

        protected void RunStateMachine(char c)
    {
        switch (State)
        {
            case InternalUrlParserState.SchemeStart:
                SchemeStartState(c);

                break;
            case InternalUrlParserState.Scheme:
                SchemeState(c);

                break;
            case InternalUrlParserState.NoScheme:
                NoSchemeState(c);

                break;
            case InternalUrlParserState.SpecialRelativeOrAuthority:
                SpecialRelativeOrAuthorityState(c);

                break;
            case InternalUrlParserState.PathOrAuthority:
                PathOrAuthorityState(c);

                break;
            case InternalUrlParserState.Relative:
                RelativeState(c);

                break;
            case InternalUrlParserState.RelativeSlash:
                RelativeSlashState(c);

                break;
            case InternalUrlParserState.SpecialAuthoritySlashes:
                SpecialAuthoritySlashesState(c);

                break;
            case InternalUrlParserState.SpecialAuthorityIgnoreSlashes:
                SpecialAuthorityIgnoreSlashesState(c);

                break;
            case InternalUrlParserState.Authority:
                AuthorityState(c);

                break;
            case InternalUrlParserState.Host:
                HostState(c);

                break;
            case InternalUrlParserState.Port:
                PortState(c);

                break;
            case InternalUrlParserState.File:
                FileState(c);

                break;
            case InternalUrlParserState.FileSlash:
                FileSlashState(c);

                break;
            case InternalUrlParserState.FileHost:
                FileHostState(c);

                break;
            case InternalUrlParserState.PathStart:
                PathStartState(c);

                break;
            case InternalUrlParserState.Path:
                PathState(c);

                break;
            case InternalUrlParserState.OpaquePath:
                OpaquePathState(c);

                break;
            case InternalUrlParserState.Query:
                QueryState(c);

                break;
            case InternalUrlParserState.Fragment:
                FragmentState(c);

                break;
            default:
                throw new InvalidOperationException();
        }
    }

    #endregion

    // https://url.spec.whatwg.org/#scheme-start-state
    protected void SchemeStartState(char c)
    {
        if (char.IsAsciiLetter(c))
        {
            Buf.Append(char.ToLowerInvariant(c));
            State = InternalUrlParserState.Scheme;
        }
        // (the state override is not supported, thus it will the last branch of this state)
        // Otherwise, if state override is not given, set state to no scheme state and decrease pointer by 1.
        else
        {
            State = InternalUrlParserState.NoScheme;
            --Pointer;
        }
    }

    // https://url.spec.whatwg.org/#scheme-state
    protected void SchemeState(char c)
    {
        // 1. If c is an ASCII alphanumeric, U+002B (+), U+002D (-), or U+002E (.),
        if (char.IsAsciiLetterOrDigit(c) || c is '+' or '-' or '.')
        {
            Buf.Append(char.ToLowerInvariant(c));   // append c, lowercased, to buffer.
        }
        // 2. Otherwise, if c is U+003A (:), then:
        else if (c == ':')
        {
            // currently not supporting state override

            // 2. Set url’s scheme to buffer.
            Scheme = Buf.ToString();

            // skipping state override here too

            // 4. Set buffer the empty string.
            Buf.Clear();

            if (Scheme == Schemes.File)
            {
                // If remaining does not start with "//", special-scheme-missing-following-solidus validation error.
                if (NextChar(1) != '/' || NextChar(2) != '/')
                    Debug.WriteLine("special-scheme-missing-following-solidus");

                State = InternalUrlParserState.File;
            }
            // 6. Otherwise, if url is special, base is non-null, and base’s scheme is url’s scheme:
            else if (IsSpecial && BaseUrl?.Scheme == Scheme)
            {
                // Assert: base is special (and therefore does not have an opaque path).

                State = InternalUrlParserState.SpecialRelativeOrAuthority;
            }
            else if (IsSpecial)
            {
                if (BaseUrl != null && BaseUrl.Scheme == Scheme)
                {
                    State = InternalUrlParserState.SpecialAuthoritySlashes;
                    return;
                }

                State = InternalUrlParserState.SpecialAuthoritySlashes;
            }
            // 8. Otherwise, if remaining starts with an U+002F (/),
            // set state to path or authority state and increase pointer by 1.
            else if (NextChar(1) == '/')
            {
                State = InternalUrlParserState.PathOrAuthority;
                Pointer++;
            }
            else
            {
                // set url’s path to the empty string
                State = InternalUrlParserState.OpaquePath;
            }
        }
        // (the state override is not supported, thus it will the last branch of this state)
        // Otherwise, if state override is not given,
        else
        {
            Buf.Clear();  // set buffer to the empty string
            State = InternalUrlParserState.NoScheme; // state to no scheme state
            Pointer = -1;  // and start over (from the first code point in input).
        }
    }

    // https://url.spec.whatwg.org/#no-scheme-state
    protected void NoSchemeState(char c)
    {
        // If base is null, or base has an opaque path and c is not U+0023 (#)
        if (BaseUrl == null || (BaseUrl._opaquePath != null && c != '#'))
        {
            Error = UrlErrorCode.MissingSchemeNonRelativeUrl;
            return;
        }

        // Otherwise, if base has an opaque path and c is U+0023 (#),
        if (BaseUrl._opaquePath != null && c == '#')
        {
            Scheme = BaseUrl.Scheme; // set url’s scheme to base’s scheme,

            // (since base has an opaque path, setting it instead of the _path)
            _opaquePath = BaseUrl._opaquePath; // url’s path to base’s path,

            Query = BaseUrl.Query;   // url’s query to base’s query,
            Buf.EnsureCapacity(Length - Pointer); // url’s fragment to the empty string,

            State = InternalUrlParserState.Fragment; // and set state to fragment state.
            return;
        }

        State = BaseUrl.Scheme != Schemes.File
            ? InternalUrlParserState.Relative // if base’s scheme is not "file", set state to relative state
            : InternalUrlParserState.File;    // Otherwise, set state to file state

        Pointer--; // and decrease pointer by 1.
    }

    // https://url.spec.whatwg.org/#path-or-authority-state
    protected void PathOrAuthorityState(char c)
    {
        if (c == '/')
            State = InternalUrlParserState.Authority;
        else
        {
            State = InternalUrlParserState.Path;
            Pointer--;
        }
    }

    // https://url.spec.whatwg.org/#relative-state
    protected void RelativeState(char c)
    {
        Debug.WriteLineIf(BaseUrl!.Scheme != Schemes.File,
            "Failed: base’s scheme is not \"file\".");

        Scheme = BaseUrl.Scheme;

        if (c == '/')
        {
            State = InternalUrlParserState.RelativeSlash;
        }
        else if (IsSpecial && c == '\\')
        {
            Debug.WriteLine("invalid-reverse-solidus");
            State = InternalUrlParserState.RelativeSlash;
        }
        else
        {
            Username = BaseUrl.Username;
            Password = BaseUrl.Password;
            Host = BaseUrl.Host;
            Port = BaseUrl.Port;
            _path = [..BaseUrl._path];
            Query = BaseUrl.Query;

            if (c == '?')
            {
                State = InternalUrlParserState.Query;
            }
            else if (c == '#')
            {
                Buf.EnsureCapacity(Length - Pointer);
                State = InternalUrlParserState.Fragment;
            }
            else if (c != '\u0000')
            {
                Query = null;
                ShortenPath();
                State = InternalUrlParserState.Path;
                Pointer--;
            }
        }
    }

    // https://url.spec.whatwg.org/#relative-slash-state
    protected void RelativeSlashState(char c)
    {
        if (IsSpecial && c is '/' or '\\')
        {
            Debug.WriteLineIf(c == '\\', "invalid-reverse-solidus");
            State = InternalUrlParserState.SpecialAuthorityIgnoreSlashes;
        }
        else if (c == '/')
            State = InternalUrlParserState.Authority;
        else
        {
            Username = BaseUrl!.Username;
            Password = BaseUrl!.Password;
            Host = BaseUrl!.Host;
            Port = BaseUrl!.Port;

            State = InternalUrlParserState.Path;
            Pointer--;
        }
    }

    // https://url.spec.whatwg.org/#special-relative-or-authority-state
    protected void SpecialRelativeOrAuthorityState(char c)
    {
        if (c == '/' && NextChar(1) == '/')
        {
            State = InternalUrlParserState.SpecialAuthorityIgnoreSlashes;
            Pointer++;
        }
        else
        {
            Debug.WriteLine("special-scheme-missing-following-solidus");
            State = InternalUrlParserState.Relative;
            Pointer--;
        }
    }

    // https://url.spec.whatwg.org/#special-authority-slashes-state
    protected void SpecialAuthoritySlashesState(char c)
    {
        if (c == '/' && NextChar(1) == '/')
        {
            Pointer++;
            State = InternalUrlParserState.SpecialAuthorityIgnoreSlashes;
        }
        else
        {
            Debug.WriteLine("special-scheme-missing-following-solidus");
            State = InternalUrlParserState.SpecialAuthorityIgnoreSlashes;
            Pointer--;
        }
    }

    // https://url.spec.whatwg.org/#special-authority-ignore-slashes-state
    protected void SpecialAuthorityIgnoreSlashesState(char c)
    {
        if (c is not '/' and not '\\')
        {
            State = InternalUrlParserState.Authority;
            Pointer--;
        }
        else
        {
            Debug.WriteLine("special-scheme-missing-following-solidus");
        }
    }

    // https://url.spec.whatwg.org/#authority-state
    protected virtual void AuthorityState(char c)
    {
        if (c == '@')
        {
            Debug.WriteLine("invalid-credentials");
            if (AtSignSeen)
                Buf.Insert(0, "%40");
            else
                AtSignSeen = true;

            AuthorityStringBuilder ??= new StringBuilder();
            foreach (var chunk in Buf.GetChunks())
            {
                foreach (var bufC in chunk.Span)
                {
                    if (bufC == ':' && !PasswordTokenSeen)
                    {
                        Username = AuthorityStringBuilder.ToString();
                        AuthorityStringBuilder.Clear();
                        PasswordTokenSeen = true;
                        continue;
                    }

                    PercentEncoding.AppendEncoded(bufC, AuthorityStringBuilder, PercentEncoding.UserInfoEncodeSet);
                }
            }

            Buf.Clear();
        }
        else if (c is '/' or '?' or '#' || (IsSpecial && c == '\\') || Pointer == Length)
        {
            if (AtSignSeen && Buf.Length == 0)
            {
                Error = UrlErrorCode.HostMissing;
                return;
            }

            if (AuthorityStringBuilder != null)
            {
                if (!PasswordTokenSeen)
                {
                    Username = AuthorityStringBuilder!.ToString();
                } else
                {
                    Password = AuthorityStringBuilder!.ToString();
                }

                AuthorityStringBuilder.Clear();
            }

            Pointer -= Buf.Length + 1;
            Buf.Clear();
            State = InternalUrlParserState.Host;
        }
        else
        {
            AppendCurrent(c);
        }
    }

    // https://url.spec.whatwg.org/#host-state
    private void HostState(char c)
    {
        if (c == ':' && !_arrFlag)
        {
            if (Buf.Length == 0)
            {
                Error = UrlErrorCode.HostMissing;
                return;
            }

            var parseResult = HostParser.Parse(Buf.ToString(), true);
            if (!parseResult)
            {
                Error = parseResult.Error;
                return;
            }

            Host = parseResult.Value;
            Buf.Clear();
            State = InternalUrlParserState.Port;
        }
        else if (c is '/' or '?' or '#' || IsSpecial && c == '\\' || Pointer == Length)
        {
            Pointer--;

            if (IsSpecial && Buf.Length == 0)
            {
                Error = UrlErrorCode.HostMissing;
                return;
            }

            var parseResult = HostParser.Parse(Buf.ToString(), !IsSpecial);
            if (!parseResult)
            {
                Error = parseResult.Error;
                return;
            }

            Host = parseResult.Value;
            Buf.Clear();
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

    // https://url.spec.whatwg.org/#port-state
    protected void PortState(char c)
    {
        if (char.IsAsciiDigit(c))
        {
            Buf.Append(c);
        }
        // pointer is past the port, which means we can parse it
        else if (Pointer == Length || c is '/' or '?' or '#' || IsSpecial && c == '\\')
        {
            if (Buf.Length != 0)
            {
                // 2. If port is greater than 2^16 − 1
                if (!ushort.TryParse(Buf.ToString(), CultureInfo.InvariantCulture, out var port))
                {
                    Error = UrlErrorCode.PortOutOfRange;
                    return;
                }

                if (Schemes.Special.TryGetValue(Scheme, out var specialPort) && port == specialPort)
                    Port = null;
                else
                    Port = port;

                Buf.Clear();
            }
            // If state override is given, then return.

            State = InternalUrlParserState.PathStart;
            Pointer--;
        }
        else
        {
            Error = UrlErrorCode.PortInvalid;
        }
    }

    // https://url.spec.whatwg.org/#file-state
    protected void FileState(char c)
    {
        Scheme = Schemes.File;
        Host = "";

        if (c is '/' or '\\')
        {
            Debug.WriteLineIf(c == '\\', "invalid-reverse-solidus");
            State = InternalUrlParserState.FileSlash;
        }
        else if (BaseUrl is { Scheme: Schemes.File })
        {
            Host = BaseUrl.Host;
            _path = [..BaseUrl._path];
            Query = BaseUrl.Query;
            if (c == '?')
            {
                State = InternalUrlParserState.Query;
            }
            else if (c == '#')
            {
                Buf.EnsureCapacity(Length - Pointer);
                State = InternalUrlParserState.Fragment;
            }
            else if (c != '\u0000')
            {
                Query = null;
                // If the code point substring from pointer to the end of input does not start with a Windows drive letter
                if (!StartsWithAWindowsDriveLetter(Input.AsSpan()[Pointer..]))
                    ShortenPath();
                else
                {
                    Debug.WriteLine("file-invalid-Windows-drive-letter");

                    // 2. Set url’s path to « ».
                    // (I've verified with the JSDom implementation that Clear()ing is the correct behavior)
                    _path.Clear();
                }

                State = InternalUrlParserState.Path;
                Pointer--;
            }
        }
        else
        {
            State = InternalUrlParserState.Path;
            Pointer--;
        }
    }

    // https://url.spec.whatwg.org/#file-slash-state
    protected void FileSlashState(char c)
    {
        if (c is '/' or '\\')
        {
            if (c == '\\')
                Debug.WriteLine("invalid-reverse-solidus");

            State = InternalUrlParserState.FileHost;
        }
        else
        {
            if (BaseUrl is { Scheme: Schemes.File })
            {
                Host = BaseUrl.Host;

                // If the code point substring from pointer to the end of input does not start with a Windows drive letter
                if (!StartsWithAWindowsDriveLetter(Input.AsSpan()[Pointer..])
                    // and base’s path[0] is a normalized Windows drive letter,
                    && IsNormalizedWindowDriveLetter(BaseUrl._path[0]))
                {
                    // then append base’s path[0] to url’s path.
                    _path.Add(BaseUrl._path[0]);
                }
            }

            State = InternalUrlParserState.Path;
            Pointer--;
        }
    }

    // https://url.spec.whatwg.org/#file-host-state
    private void FileHostState(char c)
    {
        if (Pointer == Length || c is '/' or '\\' || c == '?' || c == '#')
        {
            Pointer--;
            // state override here
            if (Buf.Length == 2 && char.IsAsciiLetter(Buf[0]) && Buf[1] is ':' or '|')
            {
                Debug.WriteLine("file-invalid-Windows-drive-letter-host");
                State = InternalUrlParserState.Path;
                return;
            }

            if (Buf.Length == 0)
            {
                Host = "";
                // If state override is given, then return.
                State = InternalUrlParserState.PathStart;
                return;
            }

            var parseResult = HostParser.Parse(Buf.ToString(), !IsSpecial);
            if (!parseResult)
            {
                Error = parseResult.Error;
                return;
            }

            Host = parseResult.Value == "localhost"
                ? ""
                : parseResult.Value;

            // If state override is given, then return.
            Buf.Clear();
            State = InternalUrlParserState.PathStart;
        }
        else
        {
            AppendCurrent(c);
        }
    }

    // https://url.spec.whatwg.org/#path-start-state
    protected void PathStartState(char c)
    {
        if (IsSpecial)
        {
            if (c == '\\')
                Debug.WriteLine("invalid-reverse-solidus");

            State = InternalUrlParserState.Path;
            if (c != '/' && c != '\\')
                Pointer--;
        }
        else if (c == '?')
        {
            State = InternalUrlParserState.Query;
        }
        else if (c == '#')
        {
            Buf.EnsureCapacity(Length - Pointer);
            State = InternalUrlParserState.Fragment;
        }
        else if (Pointer != Length)
        {
            State = InternalUrlParserState.Path;
            if (c != '/')
                Pointer--;
        }
    }

    // https://url.spec.whatwg.org/#path-state
    private void PathState(char c)
    {
        if (Pointer == Length || c is '/' or '?' or '#' || (c == '\\' && IsSpecial))
        {
            if (IsSpecial && c == '\\')
                Debug.WriteLine("invalid-reverse-solidus");

            var str = Buf.ToString();
            if (Util.IsDoubleDot(str))
            {
                ShortenPath();

                if (c != '/' && !(c == '\\' && IsSpecial))
                    _path.Add("");
            }
            else if (Util.IsSingleDot(str) && c != '/' && !(c == '\\' && IsSpecial))
            {
                _path.Add("");
            }
            else if (!Util.IsSingleDot(str))
            {
                if (Scheme == Schemes.File
                    && _path.Count == 0
                    && str.Length == 2
                    && char.IsAsciiLetter(str[0])
                    && str[1] is '|')
                {
                    str = $"{str[0]}:";
                }

                _path.Add(str);
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

    // https://url.spec.whatwg.org/#cannot-be-a-base-url-path-state
    private void OpaquePathState(char c)
    {
        if (c == '?')
        {
            _opaquePath = Buf.ToString();
            Buf.Clear();
            State = InternalUrlParserState.Query;
        }
        else if (c == '#')
        {
            _opaquePath = Buf.ToString();
            Buf.Clear();
            Buf.EnsureCapacity(Length - Pointer);
            State = InternalUrlParserState.Fragment;
        }
        else
        {
            if (Pointer < Length)
            {
                // not url codepoint
                if (!char.IsAsciiHexDigit(c) && c != '%')
                    Debug.WriteLine("invalid-URL-unit");

                // If c is U+0025 (%) and remaining does not start with two ASCII hex digits, invalid-URL-unit validation error.
                AppendCurrentEncodedInC0(c);
            } else
            {
                _opaquePath = Buf.ToString();
                Buf.Clear();
            }
        }
    }

    // https://url.spec.whatwg.org/#query-state
    private void QueryState(char c)
    {
        // skipping this since we don't support other encodings
        // 1. If encoding is not UTF-8 and one of the following is true: ...

        // 2. If one of the following is true:
        // state override is not given and c is U+0023 (#)
        // c is the EOF code point
        if (c == '#' || Pointer >= Length)
        {
            var inputToEncode = Buf.ToString();
            Buf.Clear();

            if (IsSpecial)
                PercentEncoding.PercentEncode(inputToEncode, PercentEncoding.InSpecialQueryEncodeSet, Buf);
            else
                PercentEncoding.PercentEncode(inputToEncode, PercentEncoding.InQueryEncodeSet, Buf);

            Query = Buf.ToString();
            Buf.Clear();

            // If c is U+0023 (#), then set url’s fragment to the empty string and state to fragment state.
            if (c == '#')
            {
                Buf.EnsureCapacity(Length - Pointer);
                State = InternalUrlParserState.Fragment;
            }
        }
        else
        {
            // TODO: If c is not a URL code point and not U+0025 (%), invalid-URL-unit validation error.

            // If c is U+0025 (%) and remaining does not start with two ASCII hex digits,
            // invalid-URL-unit validation error.
            if (c == '%' && !char.IsAsciiHexDigit(NextChar(1)) && !char.IsAsciiHexDigit(NextChar(2)))
                Debug.WriteLine("invalid-URL-unit");

            AppendCurrent(c);
        }
    }

    // https://url.spec.whatwg.org/#fragment-state
    private void FragmentState(char c)
    {
        if (Pointer == Length)
        {
            Fragment = Buf.ToString();
            Buf.Clear();
            return;
        }

        // TODO: If c is not a URL code point and not "%", parse error.
        if (c == '%' && !char.IsAsciiHexDigit(NextChar(1)) && !char.IsAsciiHexDigit(NextChar(2)))
            Debug.WriteLine("invalid-URL-unit");

        AppendCurrentEncoded(c, PercentEncoding.FragmentEncodeSet);
    }

    // helper with bound guard
    protected virtual char NextChar(int n) =>
        Pointer + n >= Length
            ? '\0'
            : Input[Pointer + n];

    // https://url.spec.whatwg.org/#shorten-a-urls-path
    protected void ShortenPath()
    {
        // If url’s scheme is "file", path’s size is 1, and path[0] is a normalized Windows drive letter, then return.
        if (Scheme == Schemes.File && _path.Count == 1 && IsNormalizedWindowDriveLetter(_path[0]))
            return;

        // Remove path’s last item, if any.
        if (_path.Count > 0)
            _path.RemoveAt(_path.Count - 1);
    }

    // https://url.spec.whatwg.org/#normalized-windows-drive-letter
    private static bool IsNormalizedWindowDriveLetter(string input) =>
        input.Length == 2 && char.IsAsciiLetter(input[0]) && input[1] == ':';

    // https://url.spec.whatwg.org/#start-with-a-windows-drive-letter
    private static bool StartsWithAWindowsDriveLetter(ReadOnlySpan<char> input)
    {
        var length = input.Length;
        // its length is greater than or equal to 2
        if (length < 2)
            return false;

        // its first two code points are a Windows drive letter
        if (!char.IsAsciiLetter(input[0]) || input[1] is not (':' or '|'))
            return false;

        // its length is 2 or its third code point is U+002F (/), U+005C (\), U+003F (?), or U+0023 (#).
        if (length > 2 && input[2] is not ('/' or '\\' or '?' or '#'))
            return false;

        return true;
    }

    // https://url.spec.whatwg.org/#url-serializing
    internal string SerializeUrl(bool excludeFragment = false)
    {
        // 1. Let output be url’s scheme and U+003A (:) concatenated.
        var sb = new StringBuilder();
        sb.Append(Scheme).Append(':');

        if (Host != null)
        {
            sb.Append("//");
            if (!string.IsNullOrEmpty(Username) || !string.IsNullOrEmpty(Password))
            {
                sb.Append(Username);
                if (!string.IsNullOrEmpty(Password))
                    sb.Append(':').Append(Password);

                sb.Append('@');
            }

            sb.Append(SerializeHost());
        }

        // 3. If url’s host is null, url does not have an opaque path, url’s path’s size is greater than 1,
        // and url’s path[0] is the empty string
        if (Host == null && _opaquePath == null && _path.Count > 1 && string.IsNullOrEmpty(_path[0]))
            sb.Append("/.");

        sb.Append(SerializePathname());
        if (Query != null)
            sb.Append('?').Append(Query);

        if (!excludeFragment && Fragment != null)
            sb.Append('#').Append(Fragment);

        return sb.ToString();
    }

    // https://url.spec.whatwg.org/#host-serializing
    internal string SerializeHost()
    {
        if (Host == null)
            return "";

        return Port != null
            ? $"{Host}:{Port}"
            : Host;
    }

    // TODO: needs to be verified with the spec. Currently works like in the ada
    internal string SerializeOrigin()
    {
        if (IsSpecial && Scheme != Schemes.File)
        {
            return $"{Scheme}://{SerializeHost()}";
        }

        if (Scheme == Schemes.Blob && !string.IsNullOrEmpty(_opaquePath))
        {
            var parseResult = new InternalUrl().Parse(_opaquePath);
            if (!parseResult)
                return "null";

            var parsedUrl = parseResult.Value!;

            if (parsedUrl.Scheme is Schemes.Http or Schemes.Https)
            {
                return $"{parsedUrl.Scheme}://{parsedUrl.SerializeHost()}";
            }
        }

        return "null";
    }

    // https://url.spec.whatwg.org/#url-path-serializer
    internal string SerializePathname()
    {
        if (_opaquePath != null)
            return _opaquePath;

        var sb = new StringBuilder();
        foreach (var segment in _path)
        {
            sb.Append('/');
            sb.Append(segment);
        }

        return sb.ToString();
    }
}
