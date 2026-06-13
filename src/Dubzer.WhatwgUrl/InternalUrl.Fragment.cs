using Dubzer.WhatwgUrl.BclInternal;
using static Dubzer.WhatwgUrl.PercentEncoding.AppendEncodedSimpleResult;

namespace Dubzer.WhatwgUrl;

internal partial class InternalUrl
{
    // https://url.spec.whatwg.org/#fragment-state
    protected virtual void FragmentState(char c)
    {
        var fragment = Remainder;
        var vsb = new ValueStringBuilder(Consts.MaxLengthOnStack.Char);
        try
        {
            var handled = PercentEncoding.AppendEncodedSimple(fragment, ref vsb, PercentEncoding.FragmentEncodeSet);
            Fragment = handled switch
            {
                Handled => new UrlComponent(vsb.ToString()),
                NoProcessing => new UrlComponent(Pointer, fragment.Length),
                _ => Query
            };
        }
        finally
        {
            vsb.Dispose();
        }

        Pointer += fragment.Length;
        Buf?.Clear();
    }

    internal string SerializeHash() =>
        !Fragment.HasValue || Fragment.IsEmpty
            ? ""
            : SerializeComponent(Fragment, Input, '#');
}
