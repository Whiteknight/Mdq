using AwesomeAssertions;
using Mdq.Core.SelectorModel;
using Mdq.Core.Shared;

using SM = Mdq.Core.SelectorModel;

namespace Mdq.Tests.SelectorModel;

[TestFixture]
public class SelectorParserTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static SM.SelectorChain ParseOk(string selector)
    {
        var result = SM.SelectorParser.Parse(selector);
        result.Should().BeOfType<Result<SM.SelectorChain, MdqError>.Ok>();
        return ((Result<SM.SelectorChain, MdqError>.Ok)result).Value;
    }

    private static SM.SelectorParseError ParseErr(string selector)
    {
        var result = SM.SelectorParser.Parse(selector);
        result.Should().BeOfType<Result<SM.SelectorChain, MdqError>.Err>();
        return (((Result<SM.SelectorChain, MdqError>.Err)result).Error as SelectorParseError)!;
    }

    // -------------------------------------------------------------------------
    // Req 2.1 -- empty selector
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_EmptyString_ReturnsEmptyChain()
    {
        var chain = ParseOk(string.Empty);
        chain.IsEmpty.Should().BeTrue();
        chain.Segments.Should().BeEmpty();
    }

    [Test]
    public void Parse_NullString_ReturnsEmptyChain()
    {
        var chain = ParseOk(null!);
        chain.IsEmpty.Should().BeTrue();
    }

    // -------------------------------------------------------------------------
    // Req 2.2 -- single heading selector
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_SingleHeading_ReturnsSingleHeadingSegment()
    {
        var chain = ParseOk("#Introduction");

        chain.Segments.Should().HaveCount(1);
        chain.Segments[0].Should().BeOfType<SM.SelectorSegment.Heading>()
            .Which.Name.Should().Be("Introduction");
    }

    [Test]
    public void Parse_HeadingWithSpacesInName_TrimsName()
    {
        // Names are trimmed; spaces within the name boundary are preserved by trim
        var chain = ParseOk("# Introduction ");

        chain.Segments.Should().HaveCount(1);
        chain.Segments[0].Should().BeOfType<SM.SelectorSegment.Heading>()
            .Which.Name.Should().Be("Introduction");
    }

    [Test]
    public void Parse_HashWithNoName()
    {
        var chain = ParseOk("#");

        chain.Segments.Should().HaveCount(1);
        chain.Segments[0].Should().BeOfType<SM.SelectorSegment.Heading>()
            .Which.Name.Should().Be("");
    }

    // -------------------------------------------------------------------------
    // Req 2.3 -- chained heading selectors
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_ChainedHeadings_ReturnsMultipleHeadingSegments()
    {
        var chain = ParseOk("#Chapter#Section");

        chain.Segments.Should().HaveCount(2);
        chain.Segments[0].Should().BeOfType<SM.SelectorSegment.Heading>()
            .Which.Name.Should().Be("Chapter");
        chain.Segments[1].Should().BeOfType<SM.SelectorSegment.Heading>()
            .Which.Name.Should().Be("Section");
    }

    [Test]
    public void Parse_ThreeChainedHeadings_ReturnsThreeSegments()
    {
        var chain = ParseOk("#A#B#C");

        chain.Segments.Should().HaveCount(3);
        chain.Segments[0].Should().BeOfType<SM.SelectorSegment.Heading>().Which.Name.Should().Be("A");
        chain.Segments[1].Should().BeOfType<SM.SelectorSegment.Heading>().Which.Name.Should().Be("B");
        chain.Segments[2].Should().BeOfType<SM.SelectorSegment.Heading>().Which.Name.Should().Be("C");
    }

    [Test]
    public void Parse_HashFollowedImmediatelyByHash()
    {
        var chain = ParseOk("##Section");

        chain.Segments.Should().HaveCount(2);
        chain.Segments[0].Should().BeOfType<SM.SelectorSegment.Heading>()
            .Which.Name.Should().Be("");
        chain.Segments[1].Should().BeOfType<SM.SelectorSegment.Heading>()
            .Which.Name.Should().Be("Section");
    }

    [Test]
    public void Parse_HashFollowedImmediatelyByDot()
    {
        var chain = ParseOk("#.text");

        chain.Segments.Should().HaveCount(2);
        chain.Segments[0].Should().BeOfType<SM.SelectorSegment.Heading>()
            .Which.Name.Should().Be("");
        chain.Segments[1].Should().BeOfType<SM.SelectorSegment.Text>();
    }

    // -------------------------------------------------------------------------
    // Req 2.4 -- .text selector
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_TextSelector_ReturnsTextSegment()
    {
        var chain = ParseOk("#Intro.text");

        chain.Segments.Should().HaveCount(2);
        chain.Segments[0].Should().BeOfType<SM.SelectorSegment.Heading>();
        chain.Segments[1].Should().BeOfType<SM.SelectorSegment.Text>();
    }

    [Test]
    public void Parse_StandaloneTextSelector_ReturnsTextSegment()
    {
        var chain = ParseOk(".text");

        chain.Segments.Should().HaveCount(1);
        chain.Segments[0].Should().BeOfType<SM.SelectorSegment.Text>();
    }

    // -------------------------------------------------------------------------
    // Req 2.5 -- .heading selector
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_HeadingContentSelector_ReturnsHeadingContentSegment()
    {
        var chain = ParseOk("#Intro.heading");

        chain.Segments.Should().HaveCount(2);
        chain.Segments[1].Should().BeOfType<SM.SelectorSegment.HeadingContent>();
    }

    // -------------------------------------------------------------------------
    // Req 2.6 -- .paragraph(N) selector
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_ParagraphSelector_ReturnsParagraphAtSegment()
    {
        var chain = ParseOk("#Intro.paragraph(3)");

        chain.Segments.Should().HaveCount(2);
        chain.Segments[1].Should().BeOfType<SM.SelectorSegment.ParagraphAt>()
            .Which.Index.Should().Be(3);
    }

    [Test]
    public void Parse_ParagraphSelectorIndexOne_ReturnsParagraphAtOne()
    {
        var chain = ParseOk(".paragraph(1)");

        chain.Segments.Should().HaveCount(1);
        chain.Segments[0].Should().BeOfType<SM.SelectorSegment.ParagraphAt>()
            .Which.Index.Should().Be(1);
    }

    // -------------------------------------------------------------------------
    // Req 2.7 -- .item(N) selector
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_ItemSelector_ReturnsItemAtSegment()
    {
        var chain = ParseOk("#List.paragraph(1).item(2)");

        chain.Segments.Should().HaveCount(3);
        chain.Segments[2].Should().BeOfType<SM.SelectorSegment.ItemAt>()
            .Which.Index.Should().Be(2);
    }

    // -------------------------------------------------------------------------
    // Req 2.8 -- chained .item(N) selectors
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_ChainedItemSelectors_ReturnsMultipleItemAtSegments()
    {
        var chain = ParseOk(".item(1).item(3)");

        chain.Segments.Should().HaveCount(2);
        chain.Segments[0].Should().BeOfType<SM.SelectorSegment.ItemAt>().Which.Index.Should().Be(1);
        chain.Segments[1].Should().BeOfType<SM.SelectorSegment.ItemAt>().Which.Index.Should().Be(3);
    }

    // -------------------------------------------------------------------------
    // Req 2.9 -- invalid syntax returns error with position
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_UnknownDotKeyword_ReturnsErrorWithPosition()
    {
        var error = ParseErr(".foo");

        error.Position.Should().Be(0);
        error.Message.Should().Contain(".foo");
    }

    [Test]
    public void Parse_UnexpectedCharacterAtStart_ReturnsErrorWithPosition()
    {
        var error = ParseErr("@invalid");

        error.Position.Should().Be(0);
        error.Message.Should().Contain("@");
    }

    [Test]
    public void Parse_MissingClosingParen_ReturnsError()
    {
        var error = ParseErr(".paragraph(2");

        error.Position.Should().BeGreaterThanOrEqualTo(0);
        error.Message.Should().Contain("')'");
    }

    // -------------------------------------------------------------------------
    // Req 2.10 -- non-positive and non-integer arguments
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_ParagraphWithZeroIndex_ReturnsError()
    {
        var error = ParseErr(".paragraph(0)");

        error.Message.Should().Contain("positive");
    }

    [Test]
    public void Parse_ParagraphWithNegativeIndex_ReturnsError()
    {
        var error = ParseErr(".paragraph(-1)");

        error.Message.Should().Contain("positive");
    }

    [Test]
    public void Parse_ItemWithNonIntegerArgument_ReturnsError()
    {
        var error = ParseErr(".item(abc)");

        error.Message.Should().Contain("abc");
    }

    [Test]
    public void Parse_ParagraphWithFloatArgument_ReturnsError()
    {
        var error = ParseErr(".paragraph(1.5)");

        error.Message.Should().Contain("1.5");
    }

    // -------------------------------------------------------------------------
    // ToString / round-trip (Req 2.2, 2.4, 2.5, 2.6, 2.7, 2.8)
    // -------------------------------------------------------------------------

    [TestCase("#Intro")]
    [TestCase("#A#B")]
    [TestCase("#Intro.text")]
    [TestCase("#Intro.heading")]
    [TestCase("#Intro.paragraph(2)")]
    [TestCase("#Intro.paragraph(1).item(3)")]
    [TestCase(".item(1).item(2)")]
    public void ToString_ValidChain_RoundTripsCorrectly(string selector)
    {
        var chain = ParseOk(selector);
        chain.ToString().Should().Be(selector);
    }
}
