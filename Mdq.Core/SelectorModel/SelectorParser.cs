using Mdq.Core.Shared;

namespace Mdq.Core.SelectorModel;

/// <summary>
/// Parses a query selector string into a <see cref="SelectorChain"/>.
///
/// Grammar:
///   selector_chain   = { selector_segment }
///   selector_segment = heading_selector | content_selector
///   heading_selector = "#" name
///   content_selector = ".text" | ".heading"
///                    | ".paragraph(" integer ")"
///                    | ".item(" integer ")"
///   name             = one or more characters that are not '#' or '.'
///   integer          = positive integer (>= 1)
/// </summary>
public static class SelectorParser
{
    public static Result<SelectorChain, SelectorParseError> Parse(string selector)
    {
        if (string.IsNullOrEmpty(selector))
            return Ok(new SelectorChain([]));

        var segments = new List<SelectorSegment>();
        int pos = 0;

        while (pos < selector.Length)
        {
            var ch = selector[pos];

            if (ch == '#')
            {
                var result = ParseHeading(selector, pos);
                if (result is Result<(SelectorSegment, int), SelectorParseError>.Err err)
                    return Fail(err.Error);

                var (segment, next) = ((Result<(SelectorSegment, int), SelectorParseError>.Ok)result).Value;
                segments.Add(segment);
                pos = next;
                continue;
            }

            if (ch == '.')
            {
                var result = ParseContentSelector(selector, pos);
                if (result is Result<(SelectorSegment, int), SelectorParseError>.Err err)
                    return Fail(err.Error);

                var (segment, next) = ((Result<(SelectorSegment, int), SelectorParseError>.Ok)result).Value;
                segments.Add(segment);
                pos = next;
                continue;
            }

            return Fail(new SelectorParseError(
                $"Unexpected character '{ch}' at position {pos}. Expected '#' or '.'.",
                pos));
        }

        return Ok(new SelectorChain(segments));
    }

    // -------------------------------------------------------------------------
    // Segment parsers
    // -------------------------------------------------------------------------

    private static Result<(SelectorSegment, int), SelectorParseError> ParseHeading(string input, int pos)
    {
        // pos points at '#'
        int nameStart = pos + 1;

        if (nameStart >= input.Length || input[nameStart] == '#' || input[nameStart] == '.')
            return SegmentErr($"Expected heading name after '#' at position {pos}.", pos);

        int nameEnd = nameStart;
        while (nameEnd < input.Length && input[nameEnd] != '#' && input[nameEnd] != '.')
            nameEnd++;

        var name = input[nameStart..nameEnd].Trim();
        if (name.Length == 0)
            return SegmentErr($"Heading name at position {nameStart} is empty or whitespace.", nameStart);

        return SegmentOk(new SelectorSegment.Heading(name), nameEnd);
    }

    private static Result<(SelectorSegment, int), SelectorParseError> ParseContentSelector(string input, int pos)
    {
        // pos points at '.'
        // Read the keyword up to '(' or end-of-string or next '#'/'.'
        int keyStart = pos + 1;
        int keyEnd = keyStart;
        while (keyEnd < input.Length && input[keyEnd] != '(' && input[keyEnd] != '.' && input[keyEnd] != '#')
            keyEnd++;

        var keyword = input[keyStart..keyEnd];

        return keyword switch
        {
            "text"    => SegmentOk(new SelectorSegment.Text(), keyEnd),
            "heading" => SegmentOk(new SelectorSegment.HeadingContent(), keyEnd),
            "paragraph" => ParseIndexedSelector(input, keyEnd, pos,
                            idx => new SelectorSegment.ParagraphAt(idx)),
            "item"    => ParseIndexedSelector(input, keyEnd, pos,
                            idx => new SelectorSegment.ItemAt(idx)),
            _ => SegmentErr(
                    $"Unknown selector '.{keyword}' at position {pos}. " +
                    "Expected '.text', '.heading', '.paragraph(N)', or '.item(N)'.",
                    pos)
        };
    }

    private static Result<(SelectorSegment, int), SelectorParseError> ParseIndexedSelector(
        string input,
        int pos,           // points at '(' (or end-of-string if malformed)
        int selectorStart, // position of the leading '.' for error reporting
        Func<int, SelectorSegment> factory)
    {
        if (pos >= input.Length || input[pos] != '(')
            return SegmentErr(
                $"Expected '(' after selector keyword at position {pos}.",
                pos);

        int argStart = pos + 1;
        int argEnd = argStart;
        while (argEnd < input.Length && input[argEnd] != ')')
            argEnd++;

        if (argEnd >= input.Length)
            return SegmentErr(
                $"Missing closing ')' for selector starting at position {selectorStart}.",
                selectorStart);

        var argText = input[argStart..argEnd].Trim();

        if (!int.TryParse(argText, out int index))
            return SegmentErr(
                $"Selector argument '{argText}' at position {argStart} is not an integer.",
                argStart);

        if (index <= 0)
            return SegmentErr(
                $"Selector argument {index} at position {argStart} must be a positive integer (>= 1).",
                argStart);

        return SegmentOk(factory(index), argEnd + 1); // +1 to consume ')'
    }

    // -------------------------------------------------------------------------
    // Result helpers
    // -------------------------------------------------------------------------

    private static Result<SelectorChain, SelectorParseError> Ok(SelectorChain chain) =>
        new Result<SelectorChain, SelectorParseError>.Ok(chain);

    private static Result<SelectorChain, SelectorParseError> Fail(SelectorParseError error) =>
        new Result<SelectorChain, SelectorParseError>.Err(error);

    private static Result<(SelectorSegment, int), SelectorParseError> SegmentOk(SelectorSegment seg, int next) =>
        new Result<(SelectorSegment, int), SelectorParseError>.Ok((seg, next));

    private static Result<(SelectorSegment, int), SelectorParseError> SegmentErr(string message, int position) =>
        new Result<(SelectorSegment, int), SelectorParseError>.Err(new SelectorParseError(message, position));
}
