using System;
using System.Buffers;
using Dubzer.WhatwgUrl.BclInternal;

namespace Dubzer.WhatwgUrl;

internal static partial class PercentEncoding
{
    public enum AppendEncodedPathResult : byte
    {
        // vsb contains a valid result
        Handled,
        // the input does not require processing and can be used as is
        NoProcessing,
        // the input requires a slow path
        Fallback
    }

    // https://url.spec.whatwg.org/#path-percent-encode-set
    // ' ', '"', '#', '<', '>', '?', '^', '`', '{', '}' + C0
    internal static ReadOnlySpan<byte> PathEncodeSetLookup =>
    [
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 0, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 1, 1,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0,
        1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 1, 0, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1
    ];

    // Inverted https://url.spec.whatwg.org/#path-percent-encode-set
    private static readonly SearchValues<char> PathEncodeSet = SearchValues.Create(
        "!$%&'()*+,-./0123456789:;=@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]_abcdefghijklmnopqrstuvwxyz|~"
        );


    public static AppendEncodedPathResult AppendEncodedPath(ReadOnlySpan<char> input, ref ValueStringBuilder vsb)
    {
        if (input.IsEmpty) return AppendEncodedPathResult.Handled;
        if (RequiresDotHandling(input)) return AppendEncodedPathResult.Fallback;

        var processing = input;
        // the input does not require processing and can be used as is
        var noProcessing = true;

        while (!processing.IsEmpty)
        {
            var requireEncodeIndex = processing.IndexOfAnyExcept(PathEncodeSet);

            if (noProcessing && requireEncodeIndex == -1)
                return AppendEncodedPathResult.NoProcessing;

            noProcessing = false;
            switch (requireEncodeIndex)
            {
                case -1: // No characters to encode
                    vsb.Append(processing);
                    return AppendEncodedPathResult.Handled;

                case 0: // Missing fragment that DOES NOT require character encoding
                    var notRequireEncodeIndex = processing.IndexOfAny(PathEncodeSet);

                    if (notRequireEncodeIndex == -1)
                    {
                        // The entire remainder of the string needs encoding
                        foreach (var c in processing)
                            EncodeToUtf8HexWithPercent(c, ref vsb);
                        return AppendEncodedPathResult.Handled;
                    }

                    foreach (var c in processing[..notRequireEncodeIndex])
                        EncodeToUtf8HexWithPercent(c, ref vsb);

                    processing = processing[notRequireEncodeIndex..];
                    break;
                default:
                    // Copy the safe prefix without further processing
                    vsb.Append(processing[..requireEncodeIndex]);
                    processing = processing[requireEncodeIndex..];
                    break;
            }
        }

        return AppendEncodedPathResult.Handled;
    }
    private static readonly SearchValues<char> SpecialHandlingChars = SearchValues.Create(['\\', '%']);

#if NET9_0_OR_GREATER
    private static readonly SearchValues<string> SpecialHandlingStrings = SearchValues.Create(["..", "/.", "./"], StringComparison.Ordinal);

    private static bool RequiresDotHandling(ReadOnlySpan<char> input)
    {
        // .NET will start using the much slower Aho-Corasick variant with dictionary validation, so you should not combine one-character and two-character strings into a single set
        return input[0] == '.' || input[^1] == '.' || input.ContainsAny(SpecialHandlingChars) || input.ContainsAny(SpecialHandlingStrings);
    }
#else
    private static bool RequiresDotHandling(ReadOnlySpan<char> input)
    {
        // Maybe a little bit sped up by repeating the implementation from .NET 9, but overall, not much of a bottleneck in the current implementation
        return input[0] == '.' || input[^1] == '.' || input.ContainsAny(SpecialHandlingChars) ||
               input.Contains("..", StringComparison.Ordinal) || input.Contains("/.", StringComparison.Ordinal) ||
               input.Contains("./", StringComparison.Ordinal);
    }
#endif
}
