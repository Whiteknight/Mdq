Feature: Heading

A short summary of the feature

Rule: Get heading names for sections

    Background: 
        Given I have markdown text:
            """
            # Project Name

            This is summary text

            ## Development

            ### Build

            Steps to build the project

            ### Test

            Steps to test the project
            """

    Scenario: I can get the first heading name
        When I execute selector "#.heading"
        Then The result text should be:
            """
            # Project Name
            """

    Scenario: I can get the second heading name
        When I execute selector "##.heading"
        Then The result text should be:
            """
            ## Development
            """

    Scenario: I can get the third heading name
        When I execute selector "###.heading"
        Then The result text should be:
            """
            ### Build

            ### Test
            """

Rule: Get raw heading names for sections

    Background: 
        Given I have markdown text:
            """
            # Project Name

            This is summary text

            ## Development

            ### Build

            Steps to build the project

            ### Test

            Steps to test the project
            """

    Scenario: I can get the first heading name raw
        When I execute selector "#.heading.text"
        Then The result text should be:
            """
            Project Name
            """

    Scenario: I can get the second heading name raw
        When I execute selector "##.heading.text"
        Then The result text should be:
            """
            Development
            """

    Scenario: I can get the third heading name raw
        When I execute selector "###.heading.text"
        Then The result text should be:
            """
            Build

            Test
            """