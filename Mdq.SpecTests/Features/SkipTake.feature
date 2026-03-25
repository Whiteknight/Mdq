Feature: SkipTake

A short summary of the feature

Rule:Can Skip/Take items

    Background: 
        Given I have markdown text:
            """
            First paragraph
            
            Second paragraph

            Third paragraph

            Fourth paragraph

            Fifth paragraph
            """

    Scenario: Skip 2 take 2
        When I execute selector ".text.skip(2).take(2)"
        Then The result text should be:
            """
            Third paragraph

            Fourth paragraph
            """
