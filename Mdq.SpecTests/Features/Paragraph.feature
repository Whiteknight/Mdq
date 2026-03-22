Feature: Paragraph

A short summary of the feature


Rule: Get an entire section by heading name

    Background: 
        Given I have markdown text:
            """
            First parentless paragraph

            Second parentless paragraph

            # First Level Heading

            First paragraph under First Level Heading

            Second paragraph under First Level Heading

            ## Second Level Heading

            First paragraph under Second Level Heading

            Second paragraph under Second Level Heading
            """

    Scenario: I can get paragraph(1) at root
        When I execute selector ".paragraph(1)"
        Then The result text should be:
            """
            First parentless paragraph
            """

    Scenario: I can get paragraph(2) at root
        When I execute selector ".paragraph(2)"
        Then The result text should be:
            """
            Second parentless paragraph
            """

    Scenario: I can get paragraph(1) at first heading
        When I execute selector "#.paragraph(1)"
        Then The result text should be:
            """
            First paragraph under First Level Heading
            """

    Scenario: I can get paragraph(2) at first heading
        When I execute selector "#.paragraph(2)"
        Then The result text should be:
            """
            Second paragraph under First Level Heading
            """

     Scenario: I can get paragraph(1) at second heading
        When I execute selector "##.paragraph(1)"
        Then The result text should be:
            """
            First paragraph under Second Level Heading
            """

    Scenario: I can get paragraph(2) at second heading
        When I execute selector "##.paragraph(2)"
        Then The result text should be:
            """
            Second paragraph under Second Level Heading
            """
