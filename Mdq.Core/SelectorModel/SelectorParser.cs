using Mdq.Core.Shared;
using ParserObjects;
using static ParserObjects.Parsers;
using static ParserObjects.Parsers.C;
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
     *      When used on a Heading, returns the text of the heading without leading `#`
     *      When used on a ListItem, returns the text of the list item without the leading bullets
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

        var errors = chain.Segments.OfType<Selector.Error>();
        if (errors.Any())
            return new SelectorParseError(string.Join(", ", errors.Select(e => e.Message)), 0);

        return chain;
    }

    private static IParser<char, SelectorChain> GetSelectorChainParser()
    {
        var ows = OptionalWhitespace();
        var poundHeading = GetPoundHeadingParser();

        var dotText = Match(".text").Map(_ => Selector.DotText());

        var dotHeading = Match(".heading").Map(_ => Selector.DotHeading());

        var dotItems = Match(".items").Map(_ => Selector.DotItems());

        var dotFlatten = Match(".flatten").Map(_ => Selector.DotFlatten());

        var dotParagraph = GetDotParagraphParser();
        var dotItemAt = GetDotItemParser();
        var dotUnknown = GetDotUnknownParser();
        var filterBlock = GetFilterParser();

        var selector = First(
            poundHeading,
            dotText,
            dotHeading,
            dotParagraph,
            dotItemAt,
            dotItems,
            dotFlatten,
            filterBlock,
            dotUnknown);

        return Rule(
            selector.List(ows, 1).Map(l => new SelectorChain(l)),
            End(),
            (sc, _) => sc);
    }

    private static IParser<char, Selector> GetDotUnknownParser()
        => Capture(
            MatchChar('.'),
            Match(c => c != '#' && c != '.').ListCharToString())
            .Map(c => Selector.ErrorMessage($"Unknown selector '{new string(c)}'"));

    private static IParser<char, Selector> GetDotItemParser()
        => Rule(
            Match(".item("),
            // Once we have '.item(', we MUST have a positive integer and a ')' or else we get some kind of error
            First(
                Rule(
                    DigitsAsInteger(1, 5).Map(i => i > 0
                        ? Selector.DotItemParenIndex(i)
                        : Selector.ErrorMessage("Numeric value must be non-zero positive")),
                    MatchChar(')'),
                    (d, _) => d),
                Rule(
                    MatchChar(c => c != ')').ListCharToString().Map(v => Selector.ErrorMessage($"Expected positive numeric index and ')' but found '{v}'")),
                    MatchChar(')').Optional(),
                    (x, _) => x)
            ),
            (_, n) => n);

    private static IParser<char, Selector> GetDotParagraphParser()
        => Rule(
            Match(".paragraph("),
            // Once we have '.paragraph(', we MUST have a positive integer and a ')' or else we get some kind of error
            First(
                Rule(
                    DigitsAsInteger(1, 5).Map(i => i > 0
                        ? Selector.DotParagraphParenIndex(i)
                        : Selector.ErrorMessage("Numeric value must be non-zero positive")),
                    MatchChar(')'),
                    (d, _) => d),
                Rule(
                    MatchChar(c => c != ')').ListCharToString().Map(v => Selector.ErrorMessage($"Expected positive numeric index and ')' but found '{v}'")),
                    MatchChar(')').Optional(),
                    (x, _) => x)
            ),
            (_, n) => n);

    private static IParser<char, Selector> GetPoundHeadingParser()
        => Rule(
            MatchChar('#'),
            // TODO: Probably need a way to escape # and . characters
            MatchChar(c => c != '#' && c != '.').ListCharToString().Optional(() => string.Empty),
            (_, name) => Selector.PoundHeading(name.Trim()));

    private static IParser<char, Selector> GetFilterParser()
        => Rule(
            MatchChar('['),
            Identifier(),
            MatchChar('='),
            MatchChar(c => c != ']').ListCharToString(),
            MatchChar(']'),
            (_, property, op, value, _) => Selector.FilterBlock(property, op.ToString(), value));
}
