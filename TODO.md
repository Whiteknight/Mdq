# TODO Items

- syntax highlighting on output/render (hidden behind a flag?) (Probably need to detect if we're piping, and only emit color codes if we're in an interactive terminal)
- Support pipe tables
   - selector to get the entire table (or would that already be covered by `.paragraph()`?
   - selector syntax to select a specific table row/cell `.cell(2,3)` or `.row(2)`
