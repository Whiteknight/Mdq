namespace Mdq.Core.DocumentModel;

public record Section(
    string? HeadingText,
    int HeadingLevel,
    IReadOnlyList<Paragraph> Paragraphs,
    IReadOnlyList<Section> Children);
