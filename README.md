# Mdq

Selector-based queries and line editing for markdown documents

## Overview

Markdown documents have structure and are inherently hierarchical: Sections are demarcated with headings and may contain paragraphs, lists, block quotes, and child sections (denoted with lower-level headings), etc. List items may contain sublists.

`mdq` allows navigating markdown documents and extracting text from them using a selector syntax inspired in part by things like **XPath** or **jq**. With `mdq` selectors, you start with the document object and drill down to find the element(s) you want.

## Execution

### Selector Syntax

- `# Heading` returns the section with that name from the current context, starting at the root of the document and drilling down one level at a time. For example, to get this text you're reading now, we would use a selector `#Mdq #Execution #Selector Syntax` or the short-hand `###Selector Syntax`
    - Note that heading names may be `*` as a wildcard so you don't need to specify the entire verbatim text.
- `#` Moves down to the next level of heading, without having to specify the exact title. 
- `.text` Returns the complete text ("paragraphs" or "blocks") at the current location. For example, to get this list of selector syntax items (and everything else under the "Selector Syntax" heading) we would do `###Selector Syntax.text`
- `.paragraph(n)` Returns the specified paragraph (or block) in the current section. To get this list by itself we would do `###Selector Syntax.paragraph(1)`.
- `.heading` Gets just the heading with the leading `#` characters. For example the selector `#.heading` would return "`# Mdq`".
    - To get just the text of the heading without the leading `#` characters, use `#.heading.text` for just "`Mdq`".
- `.item(n)` Returns a single item from a numbered or bulleted list. To get this bulleted item that you are reading right now, you would use `###Selector Syntax.paragraph(1).item(6)`.
- `[property=value]` allows filtering of values. Notice that the available properties and their possible values are determined by the kinds of items in the current working list.
- `.items` enumerates the individual items in a list block. This is useful when you want to filter out certain list items using the `[property=value]` syntax, for example.
- `.row(n)` For a pipe-delimited table, return the contents of the given row. `.row(0)` is the header row.
- `.cell(n)` For a pipe-delimited table, return the contents of the given column.

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

Document contains one or more Sections. The first section has no heading and has level 0.

Section contains a heading, a list of paragraphs, and a list of sub-sections.

A paragraph is an abstract type which has multiple possible implementations: TextBlock, ListBlock, CodeBlock, BlockQuote, etc.

A ListBlock contains one or more ListItems.

ListItems may contain sublists.

## Development

### Build

    dotnet build

### Test

    dotnet test

## Examples

The examples below are self-referential: each selector is applied to this README.md file itself. The automated test suite parses this section and verifies each selector produces the stated output.

**Selector:** `#Mdq.heading`

```markdown
# Mdq
```

Get the heading of the top-level section by navigating to it by name and extracting the heading.

**Selector:** `##Development.heading`

```markdown
## Development
```

Navigate two levels deep using shorthand `##` and extract the heading.

**Selector:** `##Development#Test.heading`

```markdown
### Test
```

Navigate three levels deep to a named subsection and extract its heading.

**Selector:** `##Execution#Selector Syntax.paragraph(1).item(1)`

```markdown
- `# Heading` returns the section with that name from the current context, starting at the root of the document and drilling down one level at a time. For example, to get this text you're reading now, we would use a selector `#Mdq #Execution #Selector Syntax` or the short-hand `###Selector Syntax`
    - Note that heading names may be `*` as a wildcard so you don't need to specify the entire verbatim text.
```

Navigate to the Selector Syntax section, get the first paragraph (the bulleted list), and extract the first item.
