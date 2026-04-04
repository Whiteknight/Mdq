# Mdq

Selector-based queries for markdown documents

## Overview

Markdown documents have structure and are inherently hierarchical: Sections are demarcated with headings, and may contain paragraphs, lists, block quotes, and child sections (denoted with lower-level headings), etc. List items may contain sublists.

`mdq` allows navigating markdown documents and extracting text from them using a selector syntax inspired in part by things like **XPath** or **jq**. With `mdq` selectors, you start with the document object and drill down to find the element you want.

## Execution

### Selector Syntax

- `# Heading` returns the section with that name from the current context, starting at the root of the document and drilling down one level at a time. For example, to get this text you're reading now, we would use a selector `#Mdq #Selector Syntax`.
    - Note that heading names may be `*` as a wildcard so you don't need to specify the entire verbatim text.
- `#` Moves down to the next level of heading, without having to specify the exact text. Since this document only has a single Level-1 heading, you can navigate to the text you are currently reading with the shorthand `##Selector Syntax`.
- `.text` Returns the text ("paragraphs" or "blocks") at the current location. For example, to get this list of selector syntax items (and everything else under the "Selector Syntax" heading) we would do `##Selector Syntax.text`
- `.paragraph(n)` Returns the specified paragraph (or block) in the current section. To get this list by itself we would do `##Selector Syntax.paragraph(n)`.
- `.heading` Gets just the text of a heading without the leading `#` characters or the body text. For example the selector `#.heading` would return "`Mdq`".
- `.item(n)` Returns a single item from a numbered or bulleted list. To get this bulleted item that you are reading right now, you would use `##Selector Syntax.paragraph(1).item(6)`.
- `[property=value]` allows filtering of values. Notice that the available properties and their possible values are determined by the kinds of items in the current working list.
- `.items` enumerates the individual items in a list block. This is useful when you want to filter out certain list items using the `[property=value]` syntax, for example.

### Item Types

- Document
- Section
- Heading
- Paragraph
    - TextBlock
    - ListBlock
    - CodeBlock
    - BlockQuote
- ListItem

Document contains one or more Section. The first section has no heading and has level 0.

Section contains a heading, a list of paragraphs, and a list of sub-sections.

A paragraph is an abstract type which has multiple possible implementations: TextBlock, ListBlock, CodeBlock, BlockQuote, etc.

A ListBlock contains one or more ListItems.

ListItems may contain sublists.

## Development

### Build

    dotnet build

### Test

    dotnet test
