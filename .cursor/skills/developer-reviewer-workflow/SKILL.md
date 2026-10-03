---
name: developer-reviewer-workflow
description: Mandatory developer, reviewer, developer workflow for development work. Use whenever the agent receives a task to implement, create, add, change, fix, refactor, or otherwise write code, in the backend (C#/.NET) or the frontend (React/TypeScript). Launches the developer subagent first, then the reviewer subagent to check quality, then the developer again to apply the reviewer's findings.
---

# Developer → Reviewer → Developer workflow

Any development task goes through three steps, in this order. The main agent coordinates and does not write the code itself.

```
Task progress:
- [ ] Step 1: developer implements the task
- [ ] Step 2: reviewer checks the developer's work and writes up findings
- [ ] Step 3: developer applies the reviewer's findings
- [ ] Step 4: report to the user
```

## Step 1: Launch `developer`

Call the Task tool with `subagent_type="developer"`. The subagent has no memory of this conversation, so the prompt must be self-contained:

- The task, in the user's words, plus any decisions or constraints already agreed
- Relevant file paths, docs (`docs/API.md`, `docs/ARCHITECTURE.md`) and acceptance criteria
- A request to build and run the affected tests, and to list the files it changed

Wait for it to finish. Do not start the reviewer before the developer is done.

## Step 2: Launch `reviewer`

Call the Task tool with `subagent_type="reviewer"`. Include:

- A short description of the task and what the developer reported (files changed, verification results)
- An instruction to review the uncommitted changes (`git diff`) for this task

The reviewer must write its findings out, grouped as **Critical**, **Warnings** and **Suggestions**, each with `path:line`, the problem and a concrete fix. Critical errors, flaws and optimization opportunities must all be reported, not silently skipped. The reviewer does not edit files.

## Step 3: Launch `developer` again

Read the reviewer's report.

- **Findings exist** (any Critical, Warning or Suggestion that is an optimization or improvement): launch `developer` again with `subagent_type="developer"`. Pass the reviewer's full findings verbatim and tell it to implement all of them, then rebuild and rerun the tests. If you judge a suggestion wrong or out of scope, say so in the prompt and give the reason, but do not drop Critical findings.
- **No findings** (verdict Approve with nothing to fix): skip this step.

## Step 4: Report to the user

Give a short summary:

- What was implemented
- What the reviewer found and what the second developer pass changed
- Final build, test and lint results, as reported by the subagents
- Anything left unresolved, stated plainly

## Rules

- Order is fixed: developer, then reviewer, then developer. Never skip the reviewer for a development task.
- Run the subagents one after another, not in parallel, because each depends on the previous result.
- Do one fix round. If the second developer pass leaves Critical issues unresolved, report them to the user instead of looping again.
- Do not apply this workflow to questions, explanations, code reading or pure documentation edits.
