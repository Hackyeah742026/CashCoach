# implement

Implement the task described by the user's message that follows this command, using the developer → reviewer → developer workflow.

Read `.cursor/skills/developer-reviewer-workflow/SKILL.md` first and follow it exactly:

1. Launch the `developer` subagent to implement the task.
2. When it finishes, launch the `reviewer` subagent to check the developer's work and write up its findings.
3. If the reviewer reports any findings, launch the `developer` subagent again to implement all of them.
4. Report the result to the user: what was built, what the review found, what changed in the fix round, the final build and test results, and anything left unresolved.

If no task text follows this command, ask the user what to implement before launching any subagent.

Do not write the code yourself. You only coordinate the subagents.
