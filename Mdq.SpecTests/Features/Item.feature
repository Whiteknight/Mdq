Feature: Item

A short summary of the feature

Rule: Get list items by index

    Background: 
        Given I have markdown text:
            """
            - First list, first item
            - First list, second item

            * Second list, first item
            * Second list, second item

            1. Third list, first item
            2. Third list, second item
            """

    Scenario: I can get paragraph(1), item(1)
        When I execute selector ".paragraph(1).item(1)"
        Then The result text should be:
            """
            - First list, first item
            """

    Scenario: I can get paragraph(1), item(2)
        When I execute selector ".paragraph(1).item(2)"
        Then The result text should be:
            """
            - First list, second item
            """

    Scenario: I can get paragraph(2), item(1)
        When I execute selector ".paragraph(2).item(1)"
        Then The result text should be:
            """
            - Second list, first item
            """

    Scenario: I can get paragraph(2), item(2)
        When I execute selector ".paragraph(2).item(2)"
        Then The result text should be:
            """
            - Second list, second item
            """

    Scenario: I can get paragraph(3), item(1)
        When I execute selector ".paragraph(3).item(1)"
        Then The result text should be:
            """
            1. Third list, first item
            """

    Scenario: I can get paragraph(3), item(2)
        When I execute selector ".paragraph(3).item(2)"
        Then The result text should be:
            """
            2. Third list, second item
            """
            
Rule: Get list items by index, raw text

    Background: 
        Given I have markdown text:
            """
            - First list, first item
            - First list, second item

            * Second list, first item
            * Second list, second item

            1. Third list, first item
            2. Third list, second item
            """

    Scenario: I can get paragraph(1), item(1) text
        When I execute selector ".paragraph(1).item(1).text"
        Then The result text should be:
            """
            First list, first item
            """

    Scenario: I can get paragraph(1), item(2) text
        When I execute selector ".paragraph(1).item(2).text"
        Then The result text should be:
            """
            First list, second item
            """

    Scenario: I can get paragraph(2), item(1) text
        When I execute selector ".paragraph(2).item(1).text"
        Then The result text should be:
            """
            Second list, first item
            """

    Scenario: I can get paragraph(2), item(2) text
        When I execute selector ".paragraph(2).item(2).text"
        Then The result text should be:
            """
            Second list, second item
            """

    Scenario: I can get paragraph(3), item(1) text
        When I execute selector ".paragraph(3).item(1).text"
        Then The result text should be:
            """
            Third list, first item
            """

    Scenario: I can get paragraph(3), item(2) text
        When I execute selector ".paragraph(3).item(2).text"
        Then The result text should be:
            """
            Third list, second item
            """