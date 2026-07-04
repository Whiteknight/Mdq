Feature: PipeTable

A pipe-delimited table is treated as a paragraph. Selectors .header, .row(N), and .cell(N)
allow navigating into the table structure.

Rule: Table navigation

    Background: 
        Given I have markdown text:
            """
            | Header 1 | Header 2 |
            |----------|----------|
            | R1 C1    | R1 C2    |
            | R2 C1    | R2 C2    |
            """

    Scenario: Table Header
        When I execute selector ".header"
        Then The result text should be:
            """
            | Header 1 | Header 2 |
            """

    Scenario: Table Row 1
        When I execute selector ".row(1)"
        Then The result text should be:
            """
            | R1 C1 | R1 C2 |
            """

    Scenario: Table Row 2
        When I execute selector ".row(2)"
        Then The result text should be:
            """
            | R2 C1 | R2 C2 |
            """

    Scenario: Table Row 1 Cell 1
        When I execute selector ".row(1).cell(1)"
        Then The result text should be:
            """
            R1 C1
            """

    Scenario: Table Row 2 Cell 2
        When I execute selector ".row(2).cell(2)"
        Then The result text should be:
            """
            R2 C2
            """

    Scenario: Table Row 1 Cell 2
        When I execute selector ".row(1).cell(2)"
        Then The result text should be:
            """
            R1 C2
            """

    Scenario: Table Header Cell 1
        When I execute selector ".header.cell(1)"
        Then The result text should be:
            """
            Header 1
            """

    Scenario: Table Header Cell 2
        When I execute selector ".header.cell(2)"
        Then The result text should be:
            """
            Header 2
            """

    Scenario: Table as paragraph
        When I execute selector ".paragraph(1)"
        Then The result text should be:
            """
            | Header 1 | Header 2 |
            | -------- | -------- |
            | R1 C1    | R1 C2    |
            | R2 C1    | R2 C2    |
            """

Rule: Table under a heading

    Scenario: Table row under a section
        Given I have markdown text:
            """
            # Data

            | Name  | Age |
            |-------|-----|
            | Alice | 30  |
            | Bob   | 25  |
            """
        When I execute selector "#Data.row(2)"
        Then The result text should be:
            """
            | Bob | 25 |
            """

    Scenario: Table cell under a section
        Given I have markdown text:
            """
            # Data

            | Name  | Age |
            |-------|-----|
            | Alice | 30  |
            """
        When I execute selector "#Data.row(1).cell(1)"
        Then The result text should be:
            """
            Alice
            """

Rule: Table type filter

    Scenario: Filter by type table
        Given I have markdown text:
            """
            Some text paragraph.

            | Col A | Col B |
            |-------|-------|
            | X     | Y     |
            """
        When I execute selector ".text[type=table].row(1).cell(1)"
        Then The result text should be:
            """
            X
            """

Rule: Row zero is the header

    Background:
        Given I have markdown text:
            """
            | Name  | Age |
            |-------|-----|
            | Alice | 30  |
            """

    Scenario: Row 0 returns the header row
        When I execute selector ".row(0)"
        Then The result text should be:
            """
            | Name | Age |
            """

    Scenario: Row 0 cell returns a header cell
        When I execute selector ".row(0).cell(1)"
        Then The result text should be:
            """
            Name
            """

Rule: Column extraction with cell on a table

    Background:
        Given I have markdown text:
            """
            | Name  | Age |
            |-------|-----|
            | Alice | 30  |
            | Bob   | 25  |
            """

    Scenario: Cell without row returns column values
        When I execute selector ".cell(1)"
        Then The result text should be:
            """
            Alice
            Bob
            """

    Scenario: Cell 2 without row returns second column values
        When I execute selector ".cell(2)"
        Then The result text should be:
            """
            30
            25
            """

Rule: Table among multiple paragraphs

    Scenario: Paragraph selector extracts table from section with mixed content
        Given I have markdown text:
            """
            # Report

            Here is the summary.

            | Item   | Count |
            |--------|-------|
            | Apples | 5     |
            | Pears  | 3     |

            Some trailing notes.
            """
        When I execute selector "#Report.paragraph(2)"
        Then The result text should be:
            """
            | Item   | Count |
            | ------ | ----- |
            | Apples | 5     |
            | Pears  | 3     |
            """
