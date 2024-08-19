using System.Collections.Generic;
using System.Diagnostics;
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

    #region State machine switch

        protected void RunStateMachine(char c)
    #endregion

    // https://url.spec.whatwg.org/#scheme-start-state
    {
        if (char.IsAsciiLetter(c))
        {
        }
        // (the state override is not supported, thus it will the last branch of this state)
        // Otherwise, if state override is not given, set state to no scheme state and decrease pointer by 1.
        else
        {
            --_pointer;
        }
    }

    // https://url.spec.whatwg.org/#scheme-state
    {
        // 1. If c is an ASCII alphanumeric, U+002B (+), U+002D (-), or U+002E (.),
        if (char.IsAsciiLetterOrDigit(c) || c is '+' or '-' or '.')
        {
        }
        // 2. Otherwise, if c is U+003A (:), then:
        else if (c == ':')
        {
            // currently not supporting state override

            // 2. Set url’s scheme to buffer.

            // skipping state override here too

            // 4. Set buffer the empty string.

            if (Scheme == Schemes.File)
            {
                // If remaining does not start with "//", special-scheme-missing-following-solidus validation error.
                    Debug.WriteLine("special-scheme-missing-following-solidus");

            }
            // 6. Otherwise, if url is special, base is non-null, and base’s scheme is url’s scheme:
            else if (IsSpecial && _baseUrl?.Scheme == Scheme)
            {
                // Assert: base is special (and therefore does not have an opaque path).

            }
            else if (IsSpecial)
            {
                if (_baseUrl != null && _baseUrl.Scheme == Scheme)
                {
                    return;
                }

            }
            // 8. Otherwise, if remaining starts with an U+002F (/),
            // set state to path or authority state and increase pointer by 1.
            {
                _pointer++;
            }
            else
            {
            }
        }
        // (the state override is not supported, thus it will the last branch of this state)
        // Otherwise, if state override is not given,
        else
        {
            _pointer = -1;  // and start over (from the first code point in input).
        }
    }

    // https://url.spec.whatwg.org/#no-scheme-state
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

            return;
        }

        _state = _baseUrl.Scheme != Schemes.File

        _pointer--; // and decrease pointer by 1.
    }

    // https://url.spec.whatwg.org/#path-or-authority-state
    {
        if (c == '/')
        else
        {
            _pointer--;
        }
    }

    // https://url.spec.whatwg.org/#relative-state
    {
            "Failed: base’s scheme is not \"file\".");

        Scheme = _baseUrl.Scheme;

        if (c == '/')
        {
        }
        else if (IsSpecial && c == '\\')
        {
            Debug.WriteLine("invalid-reverse-solidus");
        }
        else
        {
            Username = _baseUrl.Username;
            Password = _baseUrl.Password;
            Host = _baseUrl.Host;
            Port = _baseUrl.Port;
            Query = _baseUrl.Query;

            if (c == '?')
            {
            }
            else if (c == '#')
            {
            }
            else if (c != '\u0000')
            {
                Query = null;
                ShortenPath();
                _pointer--;
            }
        }
    }

    // https://url.spec.whatwg.org/#relative-slash-state
    {
        if (IsSpecial && c is '/' or '\\')
        {
            Debug.WriteLineIf(c == '\\', "invalid-reverse-solidus");
        }
        else if (c == '/')
        else
        {
            Username = _baseUrl!.Username;
            Password = _baseUrl!.Password;
            Host = _baseUrl!.Host;
            Port = _baseUrl!.Port;

            _pointer--;
        }
    }

    // https://url.spec.whatwg.org/#special-relative-or-authority-state
    {
        {
            _pointer++;
        }
        else
        {
            Debug.WriteLine("special-scheme-missing-following-solidus");
            _pointer--;
        }
    }

    // https://url.spec.whatwg.org/#special-authority-slashes-state
    {
        {
            _pointer++;
        }
        else
        {
            Debug.WriteLine("special-scheme-missing-following-solidus");
            _pointer--;
        }
    }

    // https://url.spec.whatwg.org/#special-authority-ignore-slashes-state
    {
        if (c is not '/' and not '\\')
        {
            _pointer--;
        }
        else
        {
            Debug.WriteLine("special-scheme-missing-following-solidus");
        }
    }

    // https://url.spec.whatwg.org/#authority-state
    {
        if (c == '@')
        {
            Debug.WriteLine("invalid-credentials");
            if (_atSignSeen)
            else
                _atSignSeen = true;

            {
                {
                }
            }

        }
        else if (c is '/' or '?' or '#' || (IsSpecial && c == '\\') || _pointer == _length)
        {
            {
                _error = UrlErrorCode.HostMissing;
                return;
            }

        }
        else
        {
        }
    }

    // https://url.spec.whatwg.org/#host-state
    {
        if (c == ':' && !_arrFlag)
        {
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
        }
        else if (c is '/' or '?' or '#' || IsSpecial && c == '\\' || _pointer == _length)
        {
            _pointer--;

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
        }
        else
        {
            if (c == '[')
                _arrFlag = true;
            else if (c == ']')
                _arrFlag = false;

        }
    }

    // https://url.spec.whatwg.org/#port-state
    {
        if (char.IsAsciiDigit(c))
        {
        }
        // pointer is past the port, which means we can parse it
        else if (_pointer == _length || c is '/' or '?' or '#' || IsSpecial && c == '\\')
        {
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

            }
            // If state override is given, then return.

            _pointer--;
        }
        else
        {
            _error = UrlErrorCode.PortInvalid;
        }
    }

    // https://url.spec.whatwg.org/#file-state
    {
        Scheme = Schemes.File;
        Host = "";

        if (c is '/' or '\\')
        {
            Debug.WriteLineIf(c == '\\', "invalid-reverse-solidus");
        }
        else if (_baseUrl is { Scheme: Schemes.File })
        {
            Host = _baseUrl.Host;
            Query = _baseUrl.Query;
            if (c == '?')
            {
            }
            else if (c == '#')
            {
            }
            else if (c != '\u0000')
            {
                Query = null;
                // If the code point substring from pointer to the end of input does not start with a Windows drive letter
                    ShortenPath();
                else
                {
                    Debug.WriteLine("file-invalid-Windows-drive-letter");

                    // 2. Set url’s path to « ».
                    // (I've verified with the JSDom implementation that Clear()ing is the correct behavior)
                    _path.Clear();
                }

                _pointer--;
            }
        }
        else
        {
            _pointer--;
        }
    }

    // https://url.spec.whatwg.org/#file-slash-state
    {
        if (c is '/' or '\\')
        {
            if (c == '\\')
                Debug.WriteLine("invalid-reverse-solidus");

        }
        else
        {
            if (_baseUrl is { Scheme: Schemes.File })
            {
                Host = _baseUrl.Host;

                // If the code point substring from pointer to the end of input does not start with a Windows drive letter
                    // and base’s path[0] is a normalized Windows drive letter,
                    && IsNormalizedWindowDriveLetter(_baseUrl._path[0]))
                {
                    // then append base’s path[0] to url’s path.
                    _path.Add(_baseUrl._path[0]);
                }
            }

            _pointer--;
        }
    }

    // https://url.spec.whatwg.org/#file-host-state
    {
        if (_pointer == _length || c is '/' or '\\' || c == '?' || c == '#')
        {
            _pointer--;
            // state override here
            if (_buf.Length == 2 && char.IsAsciiLetter(_buf[0]) && _buf[1] is ':' or '|')
            {
                Debug.WriteLine("file-invalid-Windows-drive-letter-host");
                return;
            }

            {
                Host = "";
                // If state override is given, then return.
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
        }
        else
        {
        }
    }

    // https://url.spec.whatwg.org/#path-start-state
    {
        if (IsSpecial)
        {
            if (c == '\\')
                Debug.WriteLine("invalid-reverse-solidus");

            if (c != '/' && c != '\\')
                _pointer--;
        }
        else if (c == '?')
        {
        }
        else if (c == '#')
        {
        }
        else if (_pointer != _length)
        {
            if (c != '/')
                _pointer--;
        }
    }

    // https://url.spec.whatwg.org/#path-state
    {
        if (_pointer == _length || c is '/' or '?' or '#' || (c == '\\' && IsSpecial))
        {
            if (IsSpecial && c == '\\')
                Debug.WriteLine("invalid-reverse-solidus");

            {
                ShortenPath();

                if (c != '/' && !(c == '\\' && IsSpecial))
                    _path.Add("");
            }
            {
                _path.Add("");
            }
            {
                if (Scheme == Schemes.File
                    && _path.Count == 0
                {
                }

            }

            switch (c)
            {
                case '?':
                    break;
                case '#':
                    break;
            }
        }
        else
        {
            // add parse error here
                Debug.WriteLine("invalid-URL-unit");

        }
    }

    // https://url.spec.whatwg.org/#cannot-be-a-base-url-path-state
    {
        if (c == '?')
        {
        }
        else if (c == '#')
        {
        }
        else
        {
            if (_pointer < _length)
            {
            }
        }
    }

    // https://url.spec.whatwg.org/#query-state
    {
        // skipping this since we don't support other encodings
        // 1. If encoding is not UTF-8 and one of the following is true: ...

        // 2. If one of the following is true:
        // state override is not given and c is U+0023 (#)
        // c is the EOF code point
        if (c == '#' || _pointer >= _length)
        {
            if (IsSpecial)
            else


            // If c is U+0023 (#), then set url’s fragment to the empty string and state to fragment state.
            if (c == '#')
            {
            }
        }
        else
        {
            // TODO: If c is not a URL code point and not U+0025 (%), invalid-URL-unit validation error.

            // If c is U+0025 (%) and remaining does not start with two ASCII hex digits,
            // invalid-URL-unit validation error.
                Debug.WriteLine("invalid-URL-unit");

        }
    }

    // https://url.spec.whatwg.org/#fragment-state
    {
        if (_pointer == _length)
            return;

        // TODO: If c is not a URL code point and not "%", parse error.
            Debug.WriteLine("invalid-URL-unit");

    }

    // helper with bound guard
        _pointer + n >= _length

    // https://url.spec.whatwg.org/#shorten-a-urls-path
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
    {
        var length = input.Length;
        // its length is greater than or equal to 2
        if (length < 2)
            return false;

        // its first two code points are a Windows drive letter
            return false;

        // its length is 2 or its third code point is U+002F (/), U+005C (\), U+003F (?), or U+0023 (#).
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
            {
                sb.Append(Username);
                    sb.Append(':').Append(Password);

                sb.Append('@');
            }

            sb.Append(SerializeHost());
        }

        // 3. If url’s host is null, url does not have an opaque path, url’s path’s size is greater than 1,
        // and url’s path[0] is the empty string
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
