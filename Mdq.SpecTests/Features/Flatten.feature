Feature: Flatten

A short summary of the feature

Rule: Can flatten a document

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

    Scenario: I can flatten the whole document
        When I execute selector ".flatten"
        Then The result text should be:
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

    Scenario: I can flatten the whole document and get text
        When I execute selector ".flatten[type=text]"
        Then The result text should be:
            """
            First parentless paragraph

            Second parentless paragraph

            First paragraph under First Level Heading

            Second paragraph under First Level Heading

            First paragraph under Second Level Heading

            Second paragraph under Second Level Heading
            """

    Scenario: I can flatten the whole document and get headings
        When I execute selector ".flatten[type=heading]"
        Then The result text should be:
            """
            # First Level Heading

            ## Second Level Heading
            """

     Scenario: I can flatten the whole document and get second headings
        When I execute selector ".flatten[type=heading][level=2]"
        Then The result text should be:
            """
            ## Second Level Heading
            """
