using System;
using System.Text;

namespace Dubzer.WhatwgUrl;

internal readonly struct UrlComponent
{
    private readonly string? _encodedValue;

    private readonly int _start;

    private readonly int _length;

    internal static readonly UrlComponent Empty = new("");
    internal static readonly UrlComponent Missing = new(null, 0, int.MinValue);

    private UrlComponent(string? encodedValue, int start, int length)
    {
        _encodedValue = encodedValue;
        _start = start;
        _length = length;
    }

    internal UrlComponent(string encodedValue)
        : this(encodedValue, 0, 0)
    {
    }

    internal UrlComponent(int start, int length)
        : this(null, start, length)
    {
    }

    internal bool HasValue => _length != int.MinValue;

    internal bool IsEmpty => HasValue && _length == 0 && _encodedValue is not { Length: > 0 };

    internal int SerializedLength => _encodedValue?.Length ?? _length;

    internal ReadOnlySpan<char> AsSpan(string input) =>
        _encodedValue != null
            ? _encodedValue.AsSpan()
            : HasValue
                ? input.AsSpan(_start, _length)
                : ReadOnlySpan<char>.Empty;

    internal UrlComponent Materialize(string input) =>
        !HasValue || _encodedValue != null
            ? this
            : new UrlComponent(input.Substring(_start, _length));
}

internal partial class InternalUrl
{
    private static void AppendSerializedComponent(StringBuilder sb, UrlComponent component, string input, char prefix)
    {
        if (!component.HasValue)
            return;

        sb.Append(prefix);

        var componentSpan = component.AsSpan(input);
        if (!componentSpan.IsEmpty)
            sb.Append(componentSpan);
    }

    private static string SerializeComponent(UrlComponent component, string input, char prefix)
    {
        if (!component.HasValue)
            return "";

        return string.Create(
            component.SerializedLength + 1,
            (Component: component, Input: input, Prefix: prefix),
            static (buffer, state) =>
            {
                buffer[0] = state.Prefix;
                state.Component.AsSpan(state.Input).CopyTo(buffer[1..]);
            });
    }
}
