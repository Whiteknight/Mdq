Feature: CodeBlock2

A short summary of the feature

Rule: Get code block 

    Background: 
        Given I have markdown text:
            """
                First code block is indented
                by four spaces

            ```
            Second code block is fenced
            but with no language
            ```

            ```csharp
            var thirdCodeBlock = FencedWithLanguage(
                "csharp"
            );
            ```
            """

    Scenario: I can get the first code block
        When I execute selector ".paragraph(1)"
        Then The result text should be:
            """
            ```
            First code block is indented
            by four spaces
            ```
            """

    Scenario: I can get the second code block
        When I execute selector ".paragraph(2)"
        Then The result text should be:
            """
            ```
            Second code block is fenced
            but with no language
            ```
            """

    Scenario: I can get the third code block
        When I execute selector ".paragraph(3)"
        Then The result text should be:
            """
            ```csharp
            var thirdCodeBlock = FencedWithLanguage(
                "csharp"
            );
            ```
            """
