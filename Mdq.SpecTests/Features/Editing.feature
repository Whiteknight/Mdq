Feature: Editing

Mutation operations --add and --set modify Markdown document content in-place.

Rule: --add appends content to matched nodes

    Background:
        Given I have markdown text for editing:
            """
            # Introduction

            Welcome to the project.

            - first item
            - second item

            ```csharp
            int x = 1;
            ```

            > This is a quote.

            ## Details

            More detail here.
            """

    Scenario: --add on a TextBlock appends text with a space separator
        When I add "See also the wiki." to selector "#Introduction.paragraph(1)"
        Then the edited document should contain "Welcome to the project. See also the wiki."

    Scenario: --add on a ListBlock appends a new list item
        When I add "third item" to selector "#Introduction.paragraph(2)"
        Then the edited document should contain "- third item"
        And the edited document should contain "- first item"
        And the edited document should contain "- second item"

    Scenario: --add on a CodeBlock appends a new line
        When I add "int y = 2;" to selector "#Introduction.paragraph(3)"
        Then the edited document should contain "int x = 1;"
        And the edited document should contain "int y = 2;"

    Scenario: --add on a BlockQuote appends text with a space separator
        When I add "And another sentence." to selector "#Introduction.paragraph(4)"
        Then the edited document should contain "> This is a quote. And another sentence."

    Scenario: --add on a Section returns an unsupported error
        When I attempt to add "new paragraph" to selector "#Introduction"
        Then the edit should fail with error containing "does not support"

Rule: --set replaces content on matched nodes

    Background:
        Given I have markdown text for editing:
            """
            # Introduction

            Welcome to the project.

            - first item
            - second item

            ## Details

            More detail here.
            """

    Scenario: --set on a TextBlock replaces content
        When I set "Replaced paragraph." on selector "#Introduction.paragraph(1)"
        Then the edited document should contain "Replaced paragraph."
        And the edited document should not contain "Welcome to the project."

    Scenario: --set on a ListItem replaces content
        When I set "updated item" on selector "#Introduction.paragraph(2).item(1)"
        Then the edited document should contain "- updated item"
        And the edited document should not contain "first item"

    Scenario: --set on a Section renames the heading
        When I set "Overview" on selector "#Introduction"
        Then the edited document should contain "# Overview"
        And the edited document should not contain "# Introduction"

    Scenario: --set with multiple matches returns a multi-match error
        When I attempt to set "x" on selector "#.text"
        Then the edit should fail with error containing "--set resolved to"

Rule: error conditions

    Background:
        Given I have markdown text for editing:
            """
            # Introduction

            Welcome to the project.
            """

    Scenario: Selector resolving to zero nodes produces a no-match error
        When I attempt to add "text" to selector "#NonExistent.text"
        Then the edit should fail with error containing "zero nodes"

    Scenario: Missing text argument produces a usage error
        When I attempt to add "" to selector "#Introduction.text"
        Then the edit should fail with error containing "must not be empty"

Rule: --in-place writes result back to file

    Scenario: --in-place writes the mutated document back to the source file
        Given a temporary file containing:
            """
            # Notes

            Original content.
            """
        When I run --add --in-place "Extra sentence." on selector "#Notes.text" against the temp file
        Then the temp file should contain "Original content. Extra sentence."

    Scenario: --in-place with an edit error leaves the file unmodified
        Given a temporary file containing:
            """
            # Notes

            Original content.
            """
        When I attempt --add --in-place "" on selector "#Notes.text" against the temp file
        Then the temp file should contain "Original content."
        And the temp file should not contain "Extra"
