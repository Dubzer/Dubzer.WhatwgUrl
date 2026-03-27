using System;
using System.Buffers;
using System.Diagnostics;
using System.Text;

namespace Dubzer.WhatwgUrl;

internal partial class InternalUrl
{
    private static readonly SearchValues<char> AuthorityEnd = SearchValues.Create(['@', '/', '?', '#']);
    private static readonly SearchValues<char> SpecialAuthorityEnd = SearchValues.Create(['@', '/', '?', '#', '\\']);

    internal string Username = "";
    internal string Password = "";

    protected bool AtSignSeen;
    protected bool PasswordTokenSeen;
    protected StringBuilder? AuthorityStringBuilder;

    // https://url.spec.whatwg.org/#authority-state
    protected virtual void AuthorityState(char c)
    {
        var isTerminator = c is '/' or '?' or '#' || (IsSpecial && c == '\\') || Pointer == Length;

        // fast path
        if (!isTerminator && c != '@' && Buf.Length == 0 && !AtSignSeen)
        {
            var end = IsSpecial
                ? Remainder.IndexOfAny(SpecialAuthorityEnd)
                : Remainder.IndexOfAny(AuthorityEnd);

            if (end == -1 || Remainder[end] != '@')
            {
                Pointer--;
                State = InternalUrlParserState.Host;
                return;
            }
        }

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

                    PercentEncoding.AppendEncoded(bufC, AuthorityStringBuilder, PercentEncoding.UserInfoEncodeSetLookup);
                }
            }

            Buf.Clear();
        }
        else if (isTerminator)
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

            Pointer -= Buf.Length + 1;
            Buf.Clear();
            State = InternalUrlParserState.Host;
        }
        else
        {
            AppendCurrent(c);
        }
    }
}