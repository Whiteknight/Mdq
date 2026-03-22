using Mdq.Core.Shared;
using ParserObjects;
using static ParserObjects.Parsers;
using static ParserObjects.Parsers<char>;

namespace Mdq.Core.SelectorModel;

/// <summary>
/// Parses a query selector string into a <see cref="SelectorChain"/>.
///
/// Grammar:
///   selector_chain   = { selector_segment }
///   selector_segment = heading_selector | content_selector
///   heading_selector = "#" name?
///   content_selector = ".text" | ".heading"
///                    | ".paragraph(" integer ")"
///                    | ".item(" integer ")"
///   name             = one or more characters that are not '#' or '.'
///   integer          = positive integer (>= 1)
/// </summary>
public static class SelectorParser
{
    /* .text
     *      When used on a MarkdownDocument, returns child paragraphs before the first heading
     *      When used on a Section, returns the child paragraphs of the section, excluding the heading and sub-sections
     *      Otherwise returns nothing
     * .heading
     *      When used on a Section, returns the text of the Heading only (no body text or sub-sections)
     *      Otherwise returns nothing
     * .paragraph(n)
     *      When used on a Section, returns the paragraph at index n
     *      Otherwise returns nothing
     * .item(n)
     *      When used on a ListBlock returns the ListItem at index n
     *      Otherwise returns nothing
     * [Property=Value]
     *      Compares values, depending on the type of object and property name
     */

    public static Result<SelectorChain, MdqError> Parse(string selector)
    {
        if (string.IsNullOrEmpty(selector))
            return new SelectorChain([]);

        var parser = GetSelectorChainParser();
        var parseResult = parser.Parse(selector);
        if (!parseResult.Success)
            return new SelectorParseError(parseResult.ErrorMessage, parseResult.Location.Column);

        var chain = parseResult.GetValueOrDefault(null!);

        var errors = chain.Segments.OfType<SelectorSegment.Error>();
        if (errors.Any())
            return new SelectorParseError(string.Join(", ", errors.Select(e => e.Message)), 0);

        return chain;
    }

    private static IParser<char, SelectorChain> GetSelectorChainParser()
    {
        var poundHeading = GetPoundHeadingParser();

        var dotText = Match(".text").Map(_ => SelectorSegment.DotText());

        var dotHeading = Match(".heading").Map(_ => SelectorSegment.DotHeading());
        var dotParagraph = GetDotParagraphParser();
        var dotItemAt = GetDotItemParser();
        var dotUnknown = GetDotUnknownParser();

        var selector = First(
            poundHeading,
            dotText,
            dotHeading,
            dotParagraph,
            dotItemAt,
            dotUnknown);

        return Rule(
            selector.List(1).Map(l => new SelectorChain(l)),
            End(),
            (sc, _) => sc);
    }

    private static IParser<char, SelectorSegment> GetDotUnknownParser()
        => Capture(
            MatchChar('.'),
            Match(c => c != '#' && c != '.').ListCharToString())
            .Map(c => SelectorSegment.ErrorMessage($"Unknown selector '{new string(c)}'"));

    private static IParser<char, SelectorSegment> GetDotItemParser()
        => Rule(
            Match(".item("),
            // Once we have '.item(', we MUST have a positive integer and a ')' or else we get some kind of error
            First(
                Rule(
                    DigitsAsInteger(1, 5).Map(i => i > 0
                        ? SelectorSegment.DotItemParenIndex(i)
                        : SelectorSegment.ErrorMessage("Numeric value must be non-zero positive")),
                    MatchChar(')'),
                    (d, _) => d),
                Rule(
                    MatchChar(c => c != ')').ListCharToString().Map(v => SelectorSegment.ErrorMessage($"Expected positive numeric index and ')' but found '{v}'")),
                    MatchChar(')').Optional(),
                    (x, _) => x)
            ),
            (_, n) => n);

    private static IParser<char, SelectorSegment> GetDotParagraphParser()
        => Rule(
            Match(".paragraph("),
            // Once we have '.paragraph(', we MUST have a positive integer and a ')' or else we get some kind of error
            First(
                Rule(
                    DigitsAsInteger(1, 5).Map(i => i > 0
                        ? SelectorSegment.DotParagraphParenIndex(i)
                        : SelectorSegment.ErrorMessage("Numeric value must be non-zero positive")),
                    MatchChar(')'),
                    (d, _) => d),
                Rule(
                    MatchChar(c => c != ')').ListCharToString().Map(v => SelectorSegment.ErrorMessage($"Expected positive numeric index and ')' but found '{v}'")),
                    MatchChar(')').Optional(),
                    (x, _) => x)
            ),
            (_, n) => n);

    private static IParser<char, SelectorSegment> GetPoundHeadingParser()
        => Rule(
            MatchChar('#'),
            // TODO: Probably need a way to escape # and . characters
            MatchChar(c => c != '#' && c != '.').ListCharToString().Optional(() => string.Empty),
            (_, name) => SelectorSegment.PoundHeading(name.Trim()));
}
