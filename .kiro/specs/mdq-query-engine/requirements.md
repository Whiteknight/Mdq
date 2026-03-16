# Requirements Document

## Introduction

mdq is a CLI tool for querying Markdown documents using a selector-based query language, analogous to how jq operates on JSON or XPath on XML. Given a Markdown file and a query selector string, mdq outputs the matched content to stdout. This initial scope covers the query language parser, the Markdown document model, and the query execution engine. Editing capabilities are out of scope.

## Glossary

- **mdq**: The CLI application that accepts a Markdown file and a query selector, then outputs matched content to stdout.
- **Query_Selector**: A string expression that identifies a location within a Markdown document. Composed of one or more Selector_Segments.
- **Selector_Segment**: An individual component of a Query_Selector, such as a heading selector (`#Name`) or a content selector (`.text`, `.heading`, `.paragraph(N)`, `.item(N)`).
- **Parser**: The component that transforms a raw Query_Selector string into a structured Selector_Chain.
- **Selector_Chain**: The parsed, ordered sequence of Selector_Segments that the Query_Engine traverses.
- **Query_Engine**: The component that evaluates a Selector_Chain against a Document_Model and produces the matched content.
- **Document_Model**: The internal tree representation of a parsed Markdown file, organized by heading hierarchy and content blocks.
- **Section**: A region of the Document_Model rooted at a heading. A Section includes the heading text, body content, and any child Sections nested under subheadings.
- **Paragraph**: A block of content within a Section separated from other blocks by two or more newlines. Numbered lists, bulleted lists, and block quotes each count as a single Paragraph.
- **List**: A Paragraph that consists of bulleted or numbered items.
- **List_Item**: A single entry within a List, which may itself contain a nested sub-List.
## Requirements

### Requirement 1: Parse Markdown into a Document Model

**User Story:** As a user, I want mdq to parse a Markdown file into a structured document model, so that sections and content blocks can be queried by the engine.

#### Acceptance Criteria

1. WHEN a valid Markdown file is provided, THE Document_Model SHALL represent the file as a tree of Sections organized by heading level (H1 through H6).
2. WHEN a Markdown file contains content before the first heading, THE Document_Model SHALL store that content as a root-level Section with no heading.
3. WHEN a Markdown file contains consecutive text blocks separated by two or more newlines, THE Document_Model SHALL treat each block as a distinct Paragraph within the enclosing Section.
4. WHEN a Markdown file contains a bulleted or numbered list, THE Document_Model SHALL represent the entire list as a single Paragraph containing individually addressable List_Items.
5. WHEN a list item contains a nested sub-list, THE Document_Model SHALL represent the sub-list as a child List within that List_Item.
6. WHEN a Markdown file contains a block quote, THE Document_Model SHALL represent the block quote as a single Paragraph.
7. IF the Markdown file is empty, THEN THE Document_Model SHALL represent an empty document with no Sections and no Paragraphs.

### Requirement 2: Parse Query Selector Strings

**User Story:** As a user, I want to write query selectors as concise strings, so that I can specify which part of a Markdown document to extract.

#### Acceptance Criteria

1. WHEN an empty Query_Selector string is provided, THE Parser SHALL produce an empty Selector_Chain that represents the entire document.
2. WHEN a Query_Selector string contains one or more heading selectors in the form `#Name`, THE Parser SHALL produce a Selector_Chain with one Selector_Segment per heading, ordered from left to right.
3. WHEN a heading selector is followed by another `#` heading selector, THE Parser SHALL interpret the second selector as targeting a subheading one level deeper within the section matched by the first.
4. WHEN a Query_Selector string contains a `.text` selector, THE Parser SHALL produce a Selector_Segment that targets the body content of the current section, excluding the heading line.
5. WHEN a Query_Selector string contains a `.heading` selector, THE Parser SHALL produce a Selector_Segment that targets only the heading text of the current section.
6. WHEN a Query_Selector string contains a `.paragraph(N)` selector where N is a positive integer, THE Parser SHALL produce a Selector_Segment that targets the Nth Paragraph (1-indexed) within the current section.
7. WHEN a Query_Selector string contains an `.item(N)` selector where N is a positive integer, THE Parser SHALL produce a Selector_Segment that targets the Nth List_Item within the currently selected List or sub-List.
8. WHEN multiple `.item(N)` selectors are chained, THE Parser SHALL produce Selector_Segments that navigate progressively into nested sub-Lists.
9. IF a Query_Selector string contains invalid syntax, THEN THE Parser SHALL return a descriptive error indicating the position and nature of the problem.
10. IF a `.paragraph(N)` or `.item(N)` selector contains a non-positive integer or non-integer value, THEN THE Parser SHALL return a descriptive error.

### Requirement 3: Execute Queries Against the Document Model

**User Story:** As a user, I want to run a query selector against a parsed Markdown document, so that I receive only the content I asked for.

#### Acceptance Criteria

1. WHEN the Selector_Chain is empty, THE Query_Engine SHALL return the full content of the Document_Model.
2. WHEN the Selector_Chain contains a heading selector `#Name`, THE Query_Engine SHALL return the Section whose heading text matches `Name`, including the heading line, all body content, and all nested subsections.
3. WHEN the Selector_Chain contains chained heading selectors such as `#A#B`, THE Query_Engine SHALL first locate Section `A` at the expected heading level, then locate Section `B` one heading level deeper within `A`.
4. WHEN the Selector_Chain contains a `.text` selector after a heading selector, THE Query_Engine SHALL return the body content of the matched Section, excluding the heading line but including all paragraphs and nested subsections.
5. WHEN the Selector_Chain contains a `.heading` selector, THE Query_Engine SHALL return only the heading text of the matched Section, without the Markdown heading prefix characters.
6. WHEN the Selector_Chain contains a `.paragraph(N)` selector, THE Query_Engine SHALL return the Nth Paragraph (1-indexed) within the currently selected Section.
7. WHEN the Selector_Chain contains an `.item(N)` selector, THE Query_Engine SHALL return the Nth List_Item (1-indexed) from the currently selected List.
8. WHEN the Selector_Chain contains chained `.item(N)` selectors, THE Query_Engine SHALL navigate into nested sub-Lists for each successive `.item()` selector.
9. IF a heading selector does not match any Section at the expected level, THEN THE Query_Engine SHALL return an error indicating the unmatched heading name and the heading level searched.
10. IF a `.paragraph(N)` selector references an index beyond the number of Paragraphs in the Section, THEN THE Query_Engine SHALL return an error indicating the requested index and the actual Paragraph count.
11. IF an `.item(N)` selector references an index beyond the number of List_Items in the List, THEN THE Query_Engine SHALL return an error indicating the requested index and the actual List_Item count.
12. IF an `.item(N)` selector is applied to a Paragraph that is not a List, THEN THE Query_Engine SHALL return an error indicating that the selected Paragraph is not a list.

### Requirement 4: CLI Interface

**User Story:** As a user, I want to invoke mdq from the command line with a file path and a query selector, so that I can extract Markdown content in scripts and pipelines.

#### Acceptance Criteria

1. THE mdq CLI SHALL accept a file path as the first positional argument and a Query_Selector string as the second positional argument.
2. WHEN both arguments are valid and the query matches content, THE mdq CLI SHALL write the matched Markdown content to stdout and exit with code 0.
3. IF the specified file does not exist or cannot be read, THEN THE mdq CLI SHALL write a descriptive error message to stderr and exit with a non-zero exit code.
4. IF the Query_Selector string is syntactically invalid, THEN THE mdq CLI SHALL write the Parser error message to stderr and exit with a non-zero exit code.
5. IF the query does not match any content in the document, THEN THE mdq CLI SHALL write a descriptive error message to stderr and exit with a non-zero exit code.
6. WHEN the mdq CLI is invoked with no arguments, THE mdq CLI SHALL write a usage summary to stderr and exit with a non-zero exit code.
