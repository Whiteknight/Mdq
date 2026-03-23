Feature: List

A short summary of the feature

Rule: Filter lists by kind

    Background: 
        Given I have markdown text:
            """
            - First list, first item
            - First list, second item

            1. Second list, first item
            2. Second list, second item

            * Third list, first item
            * Third list, second item
            """

    Scenario: Kind = bullet
        When I execute selector ".text[kind=bullet]"
        Then The result text should be:
            """
            - First list, first item
            - First list, second item

            - Third list, first item
            - Third list, second item
            """

    Scenario: Kind = numbered
        When I execute selector ".text[kind=numbered]"
        Then The result text should be:
            """
            1. Second list, first item
            2. Second list, second item
            """
