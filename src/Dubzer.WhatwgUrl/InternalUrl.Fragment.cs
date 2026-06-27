using static Dubzer.WhatwgUrl.PercentEncoding.AppendEncodedSimpleResult;

namespace Dubzer.WhatwgUrl;

internal partial class InternalUrl
{
    // https://url.spec.whatwg.org/#fragment-state
    protected virtual void FragmentState(char c)
    {
        var fragment = Remainder;
        var handled = PercentEncoding.AppendEncodedSimple(fragment, PercentEncoding.FragmentEncodeSet, out var encodedFragment);
        Fragment = handled switch
        {
            Handled => new UrlComponent(encodedFragment),
            NoProcessing => new UrlComponent(Pointer, fragment.Length),
            _ => Query
        };

        Pointer += fragment.Length;
    }

    internal string SerializeHash() =>
        !Fragment.HasValue || Fragment.IsEmpty
            ? ""
            : SerializeComponent(Fragment, Input, '#');
}
