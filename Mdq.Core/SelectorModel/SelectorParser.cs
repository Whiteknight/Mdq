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

        var dotSelector = GetDotSelectorWithNoArgumentsParser();
        var dotSelectorWithSingleNumber = GetDotSelectorWithSingleNumberArgumentParser();
        var filterBlock = GetFilterParser();

        var selector = First(
            poundHeading,
            filterBlock,
            dotSelectorWithSingleNumber,
            dotSelector);

        var selectorChain = selector
            .List(ows, 1)
            .Map(l => new SelectorChain(l));

        return Rule(
            selectorChain,
            End(),
            (sc, _) => sc);
    }

    private static IParser<char, Selector> GetDotSelectorWithNoArgumentsParser()
        => Rule(
            MatchChar('.'),
            First(
                Trie<string>(t => t
                    .Add("text")
                    .Add("heading")
                    .Add("items")
                    .Add("flatten")
                    .Add("header")
                ),
                // .ListCharToString() can return an empty list of 0 chars, so if we just have a '.'
                // it will return an empty name and the error message below will still work.
                Match(c => c != '#' && c != '.')
                    .ListCharToString()
            ),
            (_, name) => name switch
            {
                "text" => Selector.DotText(),
                "heading" => Selector.DotHeading(),
                "items" => Selector.DotItems(),
                "flatten" => Selector.DotFlatten(),
                "header" => Selector.DotHeader(),
                { Length: 0 } => Selector.ErrorMessage("Missing selector"),
                _ => Selector.ErrorMessage($"Unknown selector '.{name}'")
            });

    private static IParser<char, Selector> GetDotSelectorWithSingleNumberArgumentParser()
        => Rule(
            MatchChar('.'),
            First(
                Trie<string>(t => t
                    .Add("item")
                    .Add("paragraph")
                    .Add("skip")
                    .Add("take")
                    .Add("row")
                    .Add("cell")
                ),
                Match(c => c != '#' && c != '.').ListCharToString()
            ),
            MatchChar('('),
            // Once we have '.name(', we MUST have a non-negative integer and a ')' or else we get some kind of error
            First(
                Rule(
                    DigitsAsInteger(1, 5)
                        .Map(i => (Selector)new Selector.TemporaryInteger(i)),
                    MatchChar(')'),
                    (d, _) => d),
                Rule(
                    // Error fall-back case. Best-effort to parse up as much as we can before returning an error
                    MatchChar(c => c != ')')
                        .ListCharToString()
                        .Map(v => Selector.ErrorMessage($"Expected positive numeric index and ')' but found '{v}'")),
                    MatchChar(')').Optional(),
                    (x, _) => x)
            ),
            (_, name, _, n) => n switch
            {
                Selector.Error error => error,
                Selector.TemporaryInteger temp => name switch
                {
                    "item" when temp.Value > 0 => Selector.DotItemParenIndex(temp.Value),
                    "paragraph" when temp.Value > 0 => Selector.DotParagraphParenIndex(temp.Value),
                    "skip" when temp.Value > 0 => Selector.DotSkipTake(temp.Value, 0),
                    "take" when temp.Value > 0 => Selector.DotSkipTake(0, temp.Value),

                    // .row(0) is the header row. All other indexing starts at 1
                    "row" => Selector.DotRowParenIndex(temp.Value),
                    "cell" when temp.Value > 0 => Selector.DotCellParenIndex(temp.Value),
                    { Length: 0 } => Selector.ErrorMessage("Missing selector"),
                    _ => Selector.ErrorMessage($"Numeric value must be non-zero positive for '.{name}'")
                },
                _ => Selector.ErrorMessage($"Unknown selector sequence {n}")
            });

    private static IParser<char, Selector> GetPoundHeadingParser()
        => Rule(
            MatchChar('#'),
            // TODO: Probably need a way to escape # and . characters
            MatchChar(c => c != '#' && c != '.')
                .ListCharToString(),
            (_, name) => Selector.PoundHeading(name.Trim()));

    private static IParser<char, Selector> GetFilterParser()
        => Rule(
            MatchChar('['),
            Identifier(),
            // TODO: Allow other kinds of operators?
            MatchChars("="),
            MatchChar(c => c != ']')
                .ListCharToString(),
            MatchChar(']'),
            (_, property, op, value, _) => Selector.FilterBlock(property, op, value));
}
