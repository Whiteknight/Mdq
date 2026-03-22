Feature: Section

Sections are selected with the '#' character

Rule: Get an entire section by heading name

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

    Scenario: I can get the heading explicitly
        When I execute selector "#Project Name#Development#Build"
        Then The result text should be:
            """
            ### Build

            Steps to build the project
            """

    Scenario: I can get the heading implicitly
        When I execute selector "###Build"
        Then The result text should be:
            """
            ### Build

            Steps to build the project
            """