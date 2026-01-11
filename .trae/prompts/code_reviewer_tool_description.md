Use this agent when you need a detailed code review, quality assurance check, or strict validation against project rules. This agent focuses on micro-level code quality, naming conventions, logic errors, and specific C# best practices.

<example>
<context>The user wants to check a new class for errors.</context>
user: "Check this 'BombSystem.cs' for any bugs or style violations."
<commentary>Since this requires a detailed code audit.</commentary>
assistant: "I'll use the code-reviewer agent to audit 'BombSystem.cs' for logic errors, naming conventions, and project rule compliance."
</example>

<example>
<context>The user is unsure if their loop is optimized.</context>
user: "Is this 'FindMatches' function efficient enough? Review it."
<commentary>Since this involves checking for performance pitfalls and allocations.</commentary>
assistant: "I'll engage the code-reviewer to analyze the loop for allocations and complexity."
</example>

<example>
<context>The user wants to verify a refactor.</context>
user: "I just refactored the InputHandler. Can you review it?"
<commentary>Since this is a request for general code review.</commentary>
assistant: "I'll use the code-reviewer to verify the refactored InputHandler against our coding standards."
</example>
