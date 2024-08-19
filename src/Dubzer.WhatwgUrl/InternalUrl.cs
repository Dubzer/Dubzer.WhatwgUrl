using System;
using System.Buffers;
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

    protected UrlErrorCode? _error;

    protected int _pointer;
    protected InternalUrlParserState _state = InternalUrlParserState.SchemeStart;
    protected StringBuilder _buf = null!;
    protected StringBuilder? _authorityStringBuilder;
    protected string _input = "";
    protected int _length;

    protected bool IsSpecial => Schemes.Special.ContainsKey(Scheme);

    protected InternalUrl? _baseUrl;

    protected bool _atSignSeen;
    protected bool _passwordTokenSeen;

    /// <summary>
    /// Pointer is inside an array (ipv6)
    /// </summary>
    private bool _arrFlag;

    private string? _opaquePath;
    private List<string> _path = [];

    public virtual Result<InternalUrl> Parse(string input, InternalUrl? baseUrl = null)
    {
        _baseUrl = baseUrl;

        _input = InputUtils.Format(input);
        _buf = new StringBuilder(_input.Length);

        _length = _input.Length;

        for (; _pointer <= _length; _pointer++)
        {
            var c = _pointer < _length ? _input[_pointer] : '\0';

            Debug.WriteLine($"State: {_state}, char: {c}");
            RunStateMachine(c);
            if (_error != null)
                return Result<InternalUrl>.Failure(_error.Value);
        }

        return Result<InternalUrl>.Success(this);
    }

    protected virtual void AppendCurrent(char c)
    {
        _buf.Append(c);
    }

    protected virtual void AppendCurrentEncoded(char c, FrozenSet<char> set)
    {
        PercentEncoding.AppendEncoded(c, _buf, set);
    }

    protected virtual void AppendCurrentEncodedInC0(char c)
    {
        PercentEncoding.AppendEncodedInC0(c, _buf);
    }

    #region State machine switch

        protected void RunStateMachine(char c)
    {
        switch (_state)
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
            _buf.Append(char.ToLowerInvariant(c));
            _state = InternalUrlParserState.Scheme;
        }
        // (the state override is not supported, thus it will the last branch of this state)
        // Otherwise, if state override is not given, set state to no scheme state and decrease pointer by 1.
        else
        {
            _state = InternalUrlParserState.NoScheme;
            --_pointer;
        }
    }

    // https://url.spec.whatwg.org/#scheme-state
    protected void SchemeState(char c)
    {
        // 1. If c is an ASCII alphanumeric, U+002B (+), U+002D (-), or U+002E (.),
        if (char.IsAsciiLetterOrDigit(c) || c is '+' or '-' or '.')
        {
            _buf.Append(char.ToLowerInvariant(c));   // append c, lowercased, to buffer.
        }
        // 2. Otherwise, if c is U+003A (:), then:
        else if (c == ':')
        {
            // currently not supporting state override

            // 2. Set url’s scheme to buffer.
            Scheme = _buf.ToString();

            // skipping state override here too

            // 4. Set buffer the empty string.
            _buf.Clear();

            if (Scheme == Schemes.File)
            {
                // If remaining does not start with "//", special-scheme-missing-following-solidus validation error.
                if (NextChar(1) != '/' || NextChar(2) != '/')
                    Debug.WriteLine("special-scheme-missing-following-solidus");

                _state = InternalUrlParserState.File;
            }
            // 6. Otherwise, if url is special, base is non-null, and base’s scheme is url’s scheme:
            else if (IsSpecial && _baseUrl?.Scheme == Scheme)
            {
                // Assert: base is special (and therefore does not have an opaque path).

                _state = InternalUrlParserState.SpecialRelativeOrAuthority;
            }
            else if (IsSpecial)
            {
                if (_baseUrl != null && _baseUrl.Scheme == Scheme)
                {
                    _state = InternalUrlParserState.SpecialAuthoritySlashes;
                    return;
                }

                _state = InternalUrlParserState.SpecialAuthoritySlashes;
            }
            // 8. Otherwise, if remaining starts with an U+002F (/),
            // set state to path or authority state and increase pointer by 1.
            else if (NextChar(1) == '/')
            {
                _state = InternalUrlParserState.PathOrAuthority;
                _pointer++;
            }
            else
            {
                // set url’s path to the empty string
                _state = InternalUrlParserState.OpaquePath;
            }
        }
        // (the state override is not supported, thus it will the last branch of this state)
        // Otherwise, if state override is not given,
        else
        {
            _buf.Clear();  // set buffer to the empty string
            _state = InternalUrlParserState.NoScheme; // state to no scheme state
            _pointer = -1;  // and start over (from the first code point in input).
        }
    }

    // https://url.spec.whatwg.org/#no-scheme-state
    protected void NoSchemeState(char c)
    {
        // If base is null, or base has an opaque path and c is not U+0023 (#)
        if (_baseUrl == null || (_baseUrl._opaquePath != null && c != '#'))
        {
            _error = UrlErrorCode.MissingSchemeNonRelativeUrl;
            return;
        }

        // Otherwise, if base has an opaque path and c is U+0023 (#),
        if (_baseUrl._opaquePath != null && c == '#')
        {
            Scheme = _baseUrl.Scheme; // set url’s scheme to base’s scheme,

            // (since base has an opaque path, setting it instead of the _path)
            _opaquePath = _baseUrl._opaquePath; // url’s path to base’s path,

            Query = _baseUrl.Query;   // url’s query to base’s query,
            _buf.EnsureCapacity(_length - _pointer); // url’s fragment to the empty string,

            _state = InternalUrlParserState.Fragment; // and set state to fragment state.
            return;
        }

        _state = _baseUrl.Scheme != Schemes.File
            ? InternalUrlParserState.Relative // if base’s scheme is not "file", set state to relative state
            : InternalUrlParserState.File;    // Otherwise, set state to file state

        _pointer--; // and decrease pointer by 1.
    }

    // https://url.spec.whatwg.org/#path-or-authority-state
    protected void PathOrAuthorityState(char c)
    {
        if (c == '/')
            _state = InternalUrlParserState.Authority;
        else
        {
            _state = InternalUrlParserState.Path;
            _pointer--;
        }
    }

    // https://url.spec.whatwg.org/#relative-state
    protected void RelativeState(char c)
    {
        Debug.WriteLineIf(_baseUrl!.Scheme != Schemes.File,
            "Failed: base’s scheme is not \"file\".");

        Scheme = _baseUrl.Scheme;

        if (c == '/')
        {
            _state = InternalUrlParserState.RelativeSlash;
        }
        else if (IsSpecial && c == '\\')
        {
            Debug.WriteLine("invalid-reverse-solidus");
            _state = InternalUrlParserState.RelativeSlash;
        }
        else
        {
            Username = _baseUrl.Username;
            Password = _baseUrl.Password;
            Host = _baseUrl.Host;
            Port = _baseUrl.Port;
            _path = [.._baseUrl._path];
            Query = _baseUrl.Query;

            if (c == '?')
            {
                _state = InternalUrlParserState.Query;
            }
            else if (c == '#')
            {
                _buf.EnsureCapacity(_length - _pointer);
                _state = InternalUrlParserState.Fragment;
            }
            else if (c != '\u0000')
            {
                Query = null;
                ShortenPath();
                _state = InternalUrlParserState.Path;
                _pointer--;
            }
        }
    }

    // https://url.spec.whatwg.org/#relative-slash-state
    protected void RelativeSlashState(char c)
    {
        if (IsSpecial && c is '/' or '\\')
        {
            Debug.WriteLineIf(c == '\\', "invalid-reverse-solidus");
            _state = InternalUrlParserState.SpecialAuthorityIgnoreSlashes;
        }
        else if (c == '/')
            _state = InternalUrlParserState.Authority;
        else
        {
            Username = _baseUrl!.Username;
            Password = _baseUrl!.Password;
            Host = _baseUrl!.Host;
            Port = _baseUrl!.Port;

            _state = InternalUrlParserState.Path;
            _pointer--;
        }
    }

    // https://url.spec.whatwg.org/#special-relative-or-authority-state
    protected void SpecialRelativeOrAuthorityState(char c)
    {
        if (c == '/' && NextChar(1) == '/')
        {
            _state = InternalUrlParserState.SpecialAuthorityIgnoreSlashes;
            _pointer++;
        }
        else
        {
            Debug.WriteLine("special-scheme-missing-following-solidus");
            _state = InternalUrlParserState.Relative;
            _pointer--;
        }
    }

    // https://url.spec.whatwg.org/#special-authority-slashes-state
    protected void SpecialAuthoritySlashesState(char c)
    {
        if (c == '/' && NextChar(1) == '/')
        {
            _pointer++;
            _state = InternalUrlParserState.SpecialAuthorityIgnoreSlashes;
        }
        else
        {
            Debug.WriteLine("special-scheme-missing-following-solidus");
            _state = InternalUrlParserState.SpecialAuthorityIgnoreSlashes;
            _pointer--;
        }
    }

    // https://url.spec.whatwg.org/#special-authority-ignore-slashes-state
    protected void SpecialAuthorityIgnoreSlashesState(char c)
    {
        if (c is not '/' and not '\\')
        {
            _state = InternalUrlParserState.Authority;
            _pointer--;
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
            if (_atSignSeen)
                _buf.Insert(0, "%40");
            else
                _atSignSeen = true;

            _authorityStringBuilder ??= new StringBuilder();
            foreach (var chunk in _buf.GetChunks())
            {
                foreach (var bufC in chunk.Span)
                {
                    if (bufC == ':' && !_passwordTokenSeen)
                    {
                        Username = _authorityStringBuilder.ToString();
                        _authorityStringBuilder.Clear();
                        _passwordTokenSeen = true;
                        continue;
                    }

                    PercentEncoding.AppendEncoded(bufC, _authorityStringBuilder, PercentEncoding.UserInfoEncodeSet);
                }
            }

            _buf.Clear();
        }
        else if (c is '/' or '?' or '#' || (IsSpecial && c == '\\') || _pointer == _length)
        {
            if (_atSignSeen && _buf.Length == 0)
            {
                _error = UrlErrorCode.HostMissing;
                return;
            }

            if (_authorityStringBuilder != null)
            {
                if (!_passwordTokenSeen)
                {
                    Username = _authorityStringBuilder!.ToString();
                } else
                {
                    Password = _authorityStringBuilder!.ToString();
                }

                _authorityStringBuilder.Clear();
            }

            _pointer -= _buf.Length + 1;
            _buf.Clear();
            _state = InternalUrlParserState.Host;
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
            if (_buf.Length == 0)
            {
                _error = UrlErrorCode.HostMissing;
                return;
            }

            var parseResult = HostParser.Parse(_buf.ToString(), true);
            if (!parseResult)
            {
                _error = parseResult.Error;
                return;
            }

            Host = parseResult.Value;
            _buf.Clear();
            _state = InternalUrlParserState.Port;
        }
        else if (c is '/' or '?' or '#' || IsSpecial && c == '\\' || _pointer == _length)
        {
            _pointer--;

            if (IsSpecial && _buf.Length == 0)
            {
                _error = UrlErrorCode.HostMissing;
                return;
            }

            var parseResult = HostParser.Parse(_buf.ToString(), !IsSpecial);
            if (!parseResult)
            {
                _error = parseResult.Error;
                return;
            }

            Host = parseResult.Value;
            _buf.Clear();
            _state = InternalUrlParserState.PathStart;
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
            _buf.Append(c);
        }
        // pointer is past the port, which means we can parse it
        else if (_pointer == _length || c is '/' or '?' or '#' || IsSpecial && c == '\\')
        {
            if (_buf.Length != 0)
            {
                // 2. If port is greater than 2^16 − 1
                if (!ushort.TryParse(_buf.ToString(), CultureInfo.InvariantCulture, out var port))
                {
                    _error = UrlErrorCode.PortOutOfRange;
                    return;
                }

                if (Schemes.Special.TryGetValue(Scheme, out var specialPort) && port == specialPort)
                    Port = null;
                else
                    Port = port;

                _buf.Clear();
            }
            // If state override is given, then return.

            _state = InternalUrlParserState.PathStart;
            _pointer--;
        }
        else
        {
            _error = UrlErrorCode.PortInvalid;
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
            _state = InternalUrlParserState.FileSlash;
        }
        else if (_baseUrl is { Scheme: Schemes.File })
        {
            Host = _baseUrl.Host;
            _path = [.._baseUrl._path];
            Query = _baseUrl.Query;
            if (c == '?')
            {
                _state = InternalUrlParserState.Query;
            }
            else if (c == '#')
            {
                _buf.EnsureCapacity(_length - _pointer);
                _state = InternalUrlParserState.Fragment;
            }
            else if (c != '\u0000')
            {
                Query = null;
                // If the code point substring from pointer to the end of input does not start with a Windows drive letter
                if (!StartsWithAWindowsDriveLetter(_input.AsSpan()[_pointer..]))
                    ShortenPath();
                else
                {
                    Debug.WriteLine("file-invalid-Windows-drive-letter");

                    // 2. Set url’s path to « ».
                    // (I've verified with the JSDom implementation that Clear()ing is the correct behavior)
                    _path.Clear();
                }

                _state = InternalUrlParserState.Path;
                _pointer--;
            }
        }
        else
        {
            _state = InternalUrlParserState.Path;
            _pointer--;
        }
    }

    // https://url.spec.whatwg.org/#file-slash-state
    protected void FileSlashState(char c)
    {
        if (c is '/' or '\\')
        {
            if (c == '\\')
                Debug.WriteLine("invalid-reverse-solidus");

            _state = InternalUrlParserState.FileHost;
        }
        else
        {
            if (_baseUrl is { Scheme: Schemes.File })
            {
                Host = _baseUrl.Host;

                // If the code point substring from pointer to the end of input does not start with a Windows drive letter
                if (!StartsWithAWindowsDriveLetter(_input.AsSpan()[_pointer..])
                    // and base’s path[0] is a normalized Windows drive letter,
                    && IsNormalizedWindowDriveLetter(_baseUrl._path[0]))
                {
                    // then append base’s path[0] to url’s path.
                    _path.Add(_baseUrl._path[0]);
                }
            }

            _state = InternalUrlParserState.Path;
            _pointer--;
        }
    }

    // https://url.spec.whatwg.org/#file-host-state
    private void FileHostState(char c)
    {
        if (_pointer == _length || c is '/' or '\\' || c == '?' || c == '#')
        {
            _pointer--;
            // state override here
            if (_buf.Length == 2 && char.IsAsciiLetter(_buf[0]) && _buf[1] is ':' or '|')
            {
                Debug.WriteLine("file-invalid-Windows-drive-letter-host");
                _state = InternalUrlParserState.Path;
                return;
            }

            if (_buf.Length == 0)
            {
                Host = "";
                // If state override is given, then return.
                _state = InternalUrlParserState.PathStart;
                return;
            }

            var parseResult = HostParser.Parse(_buf.ToString(), !IsSpecial);
            if (!parseResult)
            {
                _error = parseResult.Error;
                return;
            }

            Host = parseResult.Value == "localhost"
                ? ""
                : parseResult.Value;

            // If state override is given, then return.
            _buf.Clear();
            _state = InternalUrlParserState.PathStart;
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

            _state = InternalUrlParserState.Path;
            if (c != '/' && c != '\\')
                _pointer--;
        }
        else if (c == '?')
        {
            _state = InternalUrlParserState.Query;
        }
        else if (c == '#')
        {
            _buf.EnsureCapacity(_length - _pointer);
            _state = InternalUrlParserState.Fragment;
        }
        else if (_pointer != _length)
        {
            _state = InternalUrlParserState.Path;
            if (c != '/')
                _pointer--;
        }
    }

    // https://url.spec.whatwg.org/#path-state
    private void PathState(char c)
    {
        if (_pointer == _length || c is '/' or '?' or '#' || (c == '\\' && IsSpecial))
        {
            if (IsSpecial && c == '\\')
                Debug.WriteLine("invalid-reverse-solidus");

            var str = _buf.ToString();
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

            _buf.Clear();
            switch (c)
            {
                case '?':
                    _state = InternalUrlParserState.Query;
                    break;
                case '#':
                    _buf.EnsureCapacity(_length - _pointer);
                    _state = InternalUrlParserState.Fragment;
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
            _opaquePath = _buf.ToString();
            _buf.Clear();
            _state = InternalUrlParserState.Query;
        }
        else if (c == '#')
        {
            _opaquePath = _buf.ToString();
            _buf.Clear();
            _buf.EnsureCapacity(_length - _pointer);
            _state = InternalUrlParserState.Fragment;
        }
        else
        {
            if (_pointer < _length)
            {
                // not url codepoint
                if (!char.IsAsciiHexDigit(c) && c != '%')
                    Debug.WriteLine("invalid-URL-unit");

                // If c is U+0025 (%) and remaining does not start with two ASCII hex digits, invalid-URL-unit validation error.
                AppendCurrentEncodedInC0(c);
            } else
            {
                _opaquePath = _buf.ToString();
                _buf.Clear();
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
        if (c == '#' || _pointer >= _length)
        {
            var inputToEncode = _buf.ToString();
            _buf.Clear();

            if (IsSpecial)
                PercentEncoding.PercentEncode(inputToEncode, PercentEncoding.InSpecialQueryEncodeSet, _buf);
            else
                PercentEncoding.PercentEncode(inputToEncode, PercentEncoding.InQueryEncodeSet, _buf);

            Query = _buf.ToString();
            _buf.Clear();

            // If c is U+0023 (#), then set url’s fragment to the empty string and state to fragment state.
            if (c == '#')
            {
                _buf.EnsureCapacity(_length - _pointer);
                _state = InternalUrlParserState.Fragment;
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
        if (_pointer == _length)
        {
            Fragment = _buf.ToString();
            _buf.Clear();
            return;
        }

        // TODO: If c is not a URL code point and not "%", parse error.
        if (c == '%' && !char.IsAsciiHexDigit(NextChar(1)) && !char.IsAsciiHexDigit(NextChar(2)))
            Debug.WriteLine("invalid-URL-unit");

        AppendCurrentEncoded(c, PercentEncoding.FragmentEncodeSet);
    }

    // helper with bound guard
    protected virtual char NextChar(int n) =>
        _pointer + n >= _length
            ? '\0'
            : _input[_pointer + n];

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
