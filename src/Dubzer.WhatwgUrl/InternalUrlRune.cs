using System.Collections.Frozen;
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

    public override Result<InternalUrl> Parse(string input, InternalUrl? baseUrl = null)
    {
        _baseUrl = baseUrl;

        _input = InputUtils.Format(input);
        _buf = new StringBuilder(_input.Length);

        _inputRunes = _input.EnumerateRunes().ToArray();
        _length = _inputRunes.Length;

        for (; _pointer <= _length; _pointer++)
        {
            _currentRune = _pointer < _length ? _inputRunes[_pointer] : new Rune('\u0000');
            var c = _currentRune.ToChar();

            Debug.WriteLine($"State: {_state}, rune: {_currentRune}");
            RunStateMachine(c);
            if (_error != null)
                return Result<InternalUrl>.Failure(_error.Value);
        }

        return Result<InternalUrl>.Success(this);
    }

    // char arguments are not used in these methods.
    // Tried moving them to the class field,
    // but it resulted in worse performance for the
    // 100k benchmark, which is critical
    protected override void AppendCurrent(char c)
    {
        _buf.AppendRune(_currentRune);
    }

    protected override void AppendCurrentEncoded(char c, FrozenSet<char> set)
    {
        PercentEncoding.AppendEncoded(_currentRune, _buf, set);
    }

    protected override void AppendCurrentEncodedInC0(char c)
    {
        PercentEncoding.AppendEncodedInC0(_currentRune, _buf);
    }

    protected override void AuthorityState(char c)
    {
        if (c == '@')
        {
            Debug.WriteLine("invalid-credentials");
            if (_atSignSeen)
                _buf.Insert(0, "%40");
            else
                _atSignSeen = true;

            _authorityStringBuilder ??= new StringBuilder();
            foreach (var rune in _buf.ToString().EnumerateRunes())
            {
                if (rune == new Rune(':') && !_passwordTokenSeen)
                {
                    Username = _authorityStringBuilder.ToString();
                    _authorityStringBuilder.Clear();
                    _passwordTokenSeen = true;
                    continue;
                }

                PercentEncoding.AppendEncoded(rune, _authorityStringBuilder, PercentEncoding.UserInfoEncodeSet);
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
                }
                else
                {
                    Password = _authorityStringBuilder!.ToString();
                }

                _authorityStringBuilder.Clear();
            }

            _pointer -= _buf.ToString().EnumerateRunes().Count() + 1;
            _buf.Clear();
            _state = InternalUrlParserState.Host;
        }
        else
        {
            _buf.AppendRune(_currentRune);
        }
    }

    // helper with bound guard
    protected override char NextChar(int n) =>
        _pointer + n >= _length
            ? '\0'
            : _inputRunes[_pointer + n].ToChar();
}
