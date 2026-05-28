# TODO Items

- syntax highlighting on output/render (hidden behind a flag?) (Probably need to detect if we're piping, and only emit color codes if we're in an interactive terminal)
- Support pipe tables
   - selector to get the entire table (or would that already be covered by `.paragraph()`?
   - selector syntax to select a specific table row/cell `.cell(2,3)` or `.row(2)`

## Replace Markdig with a custom parser

### Motivation

- **Round-trip fidelity**: Markdig parses for HTML rendering, not source preservation. Whitespace, indentation, and original formatting are discarded or normalized. The renderer has to guess at reconstruction (PadRight, dynamic column widths are symptoms).
- **Unnecessary complexity**: We don't care about inline formatting (bold, italic, code spans, links). Markdig parses all of that into nested inline nodes that we flatten back to text anyway.
- **Allocations**: Markdig creates StringSlice, ContainerBlock, LeafBlock, various inline objects — all heap-allocated. For a query tool that identifies structure and slices text, this is overhead with no benefit.
- **Impedance mismatch**: The MatchableItem model is intentionally simpler than Markdig's AST. The mapping layer (MarkdownParser.cs) exists solely to bridge that gap. If we own the parser, the parse output *is* the model.

### What the custom parser needs to handle

Structural elements only:

- **Headings** (`#` through `######`) — ATX style. Maybe setext (`===`/`---`).
- **Paragraphs** — blocks of text separated by blank lines
- **Pipe tables** — header row, separator, data rows
- **List items** — `- `, `* `, `1. ` prefixes, with nesting via indentation
- **Blank lines** — as separators

Everything else (emphasis, links, code spans, images, blockquotes, fenced code blocks, HTML blocks) treated as opaque text within a paragraph/item.

### Architecture

```
Input: ReadOnlyMemory<char> (the whole document)
       ↓
Line scanner: yields lines as ReadOnlySpan<char> or Range pairs (start, end)
       ↓
Block recognizer: classifies each line/group:
  - heading (level + content range)
  - table row (pipe-delimited cell ranges)
  - list item (indent + marker + content range)
  - blank line
  - paragraph line (anything else)
       ↓
Output: flat list of MatchableItem, each holding Range references into the original buffer
```

### Key design choices

1. **Preserve source ranges, not copies.** Each MatchableItem stores Range (or int start, int length) into the original input. Rendering = slicing the original text. Round-trip is perfect by default.

2. **Trivia/whitespace as part of the item:**
   ```csharp
   record struct Span(int Start, int Length);
   record ParsedBlock(
       Span LeadingTrivia,   // whitespace/blank lines before this block
       Span Content,         // the meaningful content
       Span TrailingTrivia   // trailing whitespace before next block
   );
   ```
   Emit `LeadingTrivia + Content + TrailingTrivia` for perfect fidelity, or just `Content` for trimmed output.

3. **Tables are recognized paragraph variants.** A table is a sequence of lines starting with `|` (or having `|` separators). The separator line (`|---|---|`) confirms it. Cell boundaries are `|` positions stored as ranges — no splitting or trimming until someone queries a cell.

4. **Lazy parsing.** Don't split table cells until someone uses `.cell(N)`. The block recognizer just marks "this is a table block, lines 5-9." Cell splitting happens on demand during query execution.

### Complexity estimate

| Concern | Markdig | Custom |
|---------|---------|--------|
| Heading detection | Handled | ~10 lines |
| Table detection | Handled (with extensions) | ~30 lines |
| List detection | Handled (complex) | ~20 lines |
| Paragraph grouping | Handled | ~10 lines |
| Inline parsing | Full parse (unwanted) | Skip entirely |
| Round-trip rendering | Impossible without heuristics | Trivial (emit original ranges) |
| Allocation | Heavy | Near-zero (ranges into original buffer) |

Total parser: ~200-300 lines.

### Risks

1. **Escaped pipes** (`\|`) and pipes inside code spans in table cells. Decide: probably only "pipes inside backticks don't split" is worth handling.
2. **List nesting**: Markdown nesting rules are tricky (4-space vs tab, lazy continuation). If we only need flat or single-level lists, it's simple.
3. **Setext headings and ambiguities**: `---` could be setext underline, thematic break, or table separator. Make opinionated choices.
4. **Maintenance burden**: We own it. But the surface area is small given our narrow needs.

### Migration path

Build the new parser alongside Markdig, get spec tests passing against both, then remove Markdig. Or replace outright — the spec tests define expected behavior regardless of which parser produces results.