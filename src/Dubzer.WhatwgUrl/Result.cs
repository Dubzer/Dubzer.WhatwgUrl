namespace Dubzer.WhatwgUrl;

/// <summary>
/// <para><b>These codes are purely informative.</b></para>
/// You should not rely on them in production, but paired with the examples from the
/// <a href="https://url.spec.whatwg.org/#writing">WHATWG URL spec</a>, they might be useful for debugging.
/// </summary>
public enum UrlErrorCode
{
    /// <summary>
    /// The error is out of specification or not specified in it.
    /// </summary>
    Unknown = 0,
    DomainToAscii = 1,
    DomainToUnicode = 2,
    DomainInvalidCodePoint = 3,
    HostInvalidCodePoint = 4,
    Ipv4EmptyPart = 5,
    Ipv4TooManyParts = 6,
    Ipv4NonNumericPart = 7,
    Ipv4NonDecimalPart = 8,
    Ipv4OutOfRangePart = 9,
    Ipv6Unclosed = 10,
    Ipv6InvalidCompression = 11,
    Ipv6TooManyPieces = 12,
    Ipv6MultipleCompressions = 13,
    Ipv6InvalidCodePoint = 14,
    Ipv6TooFewPieces = 15,
    Ipv4InIpv6TooManyPieces = 16,
    Ipv4InIpv6InvalidCodePoint = 17,
    Ipv4InIpv6OutOfRangePart = 18,
    Ipv4InIpv6TooFewParts = 19,
    InvalidUrlUnit = 20,
    SpecialSchemeMissingFollowingSolidus = 21,
    MissingSchemeNonRelativeUrl = 22,
    InvalidReverseSolidus = 23,
    InvalidCredentials = 24,
    HostMissing = 25,
    PortOutOfRange = 26,
    PortInvalid = 27,
    FileInvalidWindowsDriveLetter = 28,
    FileInvalidWindowsDriveLetterHost = 29,
}

internal readonly struct Result<T>
{
    internal readonly T? Value;
    internal readonly UrlErrorCode? Error;

    private Result(T? value, UrlErrorCode? error)
    {
        Value = value;
        Error = error;
    }

    internal static Result<T> Success(T value) => new(value, null);
    internal static Result<T> Failure(UrlErrorCode error) => new(default, error);

    public static implicit operator bool(Result<T> result) => result.Value is not null;
}
