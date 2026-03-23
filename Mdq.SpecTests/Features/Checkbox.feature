Feature: Checkbox

A short summary of the feature

Rule: Filter out list items which are checkboxes

    Background: 
        Given I have markdown text:
            """
            - First list, first item
            - First list, second item

            separator

            - [ ] Second list, first item
            - [x] Second list, second item

            separator

            - [ ]* Third list, first item
            - [x]* Third list, second item
            """

    Scenario: Checkable true, not checkable
        When I execute selector ".paragraph(1).items[checkable=true]"
        Then The result text should be:
            """
            """

    Scenario: Checkable false, not checkable
        When I execute selector ".paragraph(1).items[checkable=false]"
        Then The result text should be:
            """
            - First list, first item
            - First list, second item
            """

    Scenario: Checkable true, checkable
        When I execute selector ".paragraph(3).items[checkable=true]"
        Then The result text should be:
            """
            - [ ] Second list, first item
            - [x] Second list, second item
            """

    Scenario: Checkable false, checkable
        When I execute selector ".paragraph(3).items[checkable=false]"
        Then The result text should be:
            """
            """

Rule: Filter out list items which are checked

    Background: 
        Given I have markdown text:
            """
            - [ ] Second list, first item
            - [x] Second list, second item
            """

    Scenario: Checked true, checked
        When I execute selector ".paragraph(1).items[checked=true]"
        Then The result text should be:
            """
            - [x] Second list, second item
            """

    Scenario: Checked false, not checked
        When I execute selector ".paragraph(1).items[checked=false]"
        Then The result text should be:
            """
            - [ ] Second list, first item
            """

Rule: Filter out list items which are optional

    Background: 
        Given I have markdown text:
            """
            - [ ] First item
            - [x] Second item
            - [ ]* Third item
            - [x]* Fourth item
            """

    Scenario: Optional true
        When I execute selector ".paragraph(1).items[optional=true]"
        Then The result text should be:
            """
            - [ ]* Third item
            - [x]* Fourth item
            """

    Scenario: Optional false
        When I execute selector ".paragraph(1).items[optional=false]"
        Then The result text should be:
            """
            - [ ] First item
            - [x] Second item
            """
