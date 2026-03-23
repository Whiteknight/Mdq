# TODO Items

- syntax highlighting on output/render (hidden behind a flag?) (probably want this configurable somehow. Maybe a .mdq file in the ~ directory, with yaml config?)
- line-editing (ability to specify a section/paragraph with the selector, and choose to prepend/replace/append an additional line of text there. If the block is a List we should add a new list item at the selected location, etc)
- selector for ``` code blocks
   - Selector to get code blocks with a specific language tag (e.g. ```csharp) like `.code(csharp)` or `.code[lang=csharp]`
- Support pipe tables
   - selector to get the entire table (or would that already be covered by `.paragraph()`?
   - selector syntax to select a specific table row/cell `.cell(2,3)` or `.row(2)`
- Selectors to flatten the document structure (basically requires infinite drill-down step to accumulate certain content sections)
    - Selector to get all paragraphs (without headings)
    - Selector to get all lists (without headings)
- Selector to get all headings (without contents, basically a TOC). Also can we turn this into a graphical tree? 
    - Maybe a `--tree` flag that gets all headings and then a custom renderer that turns it into a tree?
- Change CLI args to `mdq <selector> <file>`