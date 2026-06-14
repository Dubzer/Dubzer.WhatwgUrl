using System;
using Dubzer.WhatwgUrl.BclInternal;
using static Dubzer.WhatwgUrl.PercentEncoding.AppendEncodedSimpleResult;

namespace Dubzer.WhatwgUrl;

internal partial class InternalUrl
{
    // https://url.spec.whatwg.org/#query-state
    protected virtual void QueryState(char c)
    {
        // skipping this since we don't support other encodings
        // 1. If encoding is not UTF-8 and one of the following is true: ...

        var query = Remainder;

        var end = query.IndexOf('#');
        var endsWithFragment = end != -1;
        if (endsWithFragment)
            query = query[..end];

        var set = IsSpecial ? PercentEncoding.SpecialQueryEncodeSet : PercentEncoding.QueryEncodeSet;
        var vsb = new ValueStringBuilder(Consts.MaxLengthOnStack.Char);
        try
        {
            var handled = PercentEncoding.AppendEncodedSimple(query, ref vsb, set);
            Query = handled switch
            {
                Handled => new UrlComponent(vsb.ToString()),
                NoProcessing => new UrlComponent(Pointer, query.Length),
                _ => Query
            };
        }
        finally
        {
            vsb.Dispose();
        }

        Pointer += query.Length;
        if (endsWithFragment)
        {
            State = InternalUrlParserState.Fragment;
        }

        _buf?.Clear();
    }

    private static UrlComponent CloneQuery(InternalUrl source) =>
        source.Query.Materialize(source.Input);

    internal string SerializeSearch() =>
        !Query.HasValue || Query.IsEmpty
            ? ""
            : SerializeComponent(Query, Input, '?');
}
