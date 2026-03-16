# TODO Items

1. syntax highlighting on output/render (hidden behind a flag?) (probably want this configurable somehow. Maybe a .mdq file in the ~ directory, with yaml config?)
2. Wildcards on heading names.
3. line-editing (ability to specify a section/paragraph with the selector, and choose to prepend/replace/append an additional line of text there. If the block is a List we should add a new list item at the selected location, etc)
4. Make sure we support ``` code blocks
   1. Syntax to extract just the contents of a code block (which we can then pipe to another utility, such as a pager/highlighter, etc)
5. Support pipe tables
   1. selector syntax to select a specific table row/cell
6. 