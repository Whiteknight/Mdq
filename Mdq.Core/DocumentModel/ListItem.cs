namespace Mdq.Core.DocumentModel;

public record ListItem(
    string Content,
    Paragraph.ListBlock? SubList);
