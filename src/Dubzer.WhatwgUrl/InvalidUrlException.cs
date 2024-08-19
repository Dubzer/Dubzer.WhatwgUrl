using System;

namespace Dubzer.WhatwgUrl;

/// <summary>
/// This exception is thrown when the input URL is invalid.
/// </summary>
public class InvalidUrlException : Exception
{
    /// <summary>
    /// <para><b>This field is purely informative.</b></para>
    /// You should not rely on its value in production, but paired with the examples from the
    /// <a href="https://url.spec.whatwg.org/#writing">WHATWG URL spec</a>, it might be useful for debugging.
    /// </summary>
    public UrlErrorCode UrlError { get; }

    /// <inheritdoc cref="InvalidUrlException"/>
    internal InvalidUrlException(string message, UrlErrorCode urlError = UrlErrorCode.Unknown) : base(message)
    {
        UrlError = urlError;
    }
}