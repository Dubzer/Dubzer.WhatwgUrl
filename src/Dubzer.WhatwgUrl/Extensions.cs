using System.Text;

namespace Dubzer.WhatwgUrl;

internal static class StringBuilderExtensions
{
    {
        var codepoint = rune.Value;

        if (codepoint <= ushort.MaxValue)
            stringBuilder.Append((char) codepoint);

        stringBuilder.Append((char) (codepoint + 56557568U >> 10));
        stringBuilder.Append((char) ((codepoint & 1023) + 56320));
    }

    public static void InsertRune(this StringBuilder stringBuilder, int index, Rune rune)
    {
        var codepoint = rune.Value;

        // Inlined Rune.IsBmp
        if (codepoint <= ushort.MaxValue)
        {
            stringBuilder.Insert(index, (char) codepoint);
            return;
        }

        // Inlined Rune.EncodeToUtf16 => UnicodeUtility.GetUtf16SurrogatesFromSupplementaryPlaneScalar
        stringBuilder.Insert(index, (char) (codepoint + 56557568U >> 10));
        stringBuilder.Insert(index + 1, (char) ((codepoint & 1023) + 56320));
    }
}
