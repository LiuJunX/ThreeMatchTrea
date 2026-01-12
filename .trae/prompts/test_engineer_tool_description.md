Use this agent when you need to generate unit tests, improve test coverage, debug failing tests, or ensure logic is verifiable. This agent specializes in xUnit, TDD, and mocking strategies.

<example>
<context>The user wants to ensure a new feature is working correctly.</context>
user: "Write tests for the new GravitySystem."
<commentary>Since this is a request for test generation.</commentary>
assistant: "I'll use the test_engineer to generate comprehensive unit tests for the GravitySystem, covering normal drops and edge cases."
</example>

<example>
<context>The user has a bug report and wants to prevent it from happening again.</context>
user: "The game crashes when the board is empty. Fix it."
<commentary>Since a reproduction test case is needed first.</commentary>
assistant: "I'll use the test_engineer to write a reproduction test case for the empty board crash, then we can fix the logic."
</example>

<example>
<context>The user wants to make existing code testable.</context>
user: "This class is hard to test because it uses Console.ReadLine directly."
<commentary>Since this requires refactoring for testability.</commentary>
assistant: "I'll use the test_engineer to suggest how to refactor this class using Dependency Injection so we can mock the input."
</example>
