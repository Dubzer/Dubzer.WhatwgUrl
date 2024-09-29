﻿using System;
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
        Buf.AppendRune(_currentRune);
    }

    protected override void AppendCurrentEncoded(char c, FrozenSet<char> set)
    {
        PercentEncoding.AppendEncoded(_currentRune, Buf, set);
    }

    protected override void AppendCurrentEncodedInC0(char c)
    {
        PercentEncoding.AppendEncodedInC0(_currentRune, Buf);
    }

    protected override void AuthorityState(char c)
    {
        if (c == '@')
        {
            Debug.WriteLine("invalid-credentials");
            if (AtSignSeen)
                Buf.Insert(0, "%40");
            else
                AtSignSeen = true;

            AuthorityStringBuilder ??= new StringBuilder();
            foreach (var rune in Buf.ToString().EnumerateRunes())
            {
                if (rune == new Rune(':') && !PasswordTokenSeen)
                {
                    Username = AuthorityStringBuilder.ToString();
                    AuthorityStringBuilder.Clear();
                    PasswordTokenSeen = true;
                    continue;
                }

                PercentEncoding.AppendEncoded(rune, AuthorityStringBuilder, PercentEncoding.UserInfoEncodeSet);
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
                }
                else
                {
                    Password = AuthorityStringBuilder!.ToString();
                }

                AuthorityStringBuilder.Clear();
            }

            Pointer -= Buf.ToString().EnumerateRunes().Count() + 1;
            Buf.Clear();
            State = InternalUrlParserState.Host;
        }
        else
        {
            Buf.AppendRune(_currentRune);
        }
    }

    protected override void PathState(char c)
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

    // helper with bound guard
    protected override char NextChar(int n) =>
        Pointer + n >= Length
            ? '\0'
            : _inputRunes[Pointer + n].ToChar();

    protected override int SegmentLength(ReadOnlySpan<char> input)
    {
        var length = 0;
        var enumerator = input.EnumerateRunes();
        while (enumerator.MoveNext())
        {
            length++;
        }

        return length;
    }
}
