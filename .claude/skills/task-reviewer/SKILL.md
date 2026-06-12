---
name: task-reviewer
description: Start reviewing as the swarm reviewer. Watches tasks.json for completed tasks, reviews and merges them. Use when inside the reviewer worktree and the user says "/task-reviewer", "start reviewing", or "begin review".
disable-model-invocation: false
user-invocable: true
---

Start reviewing. Watch /home/caleb/projects/movie-night-picker-dotnet/.ai/taskswarm/tasks.json for completed tasks and review/merge them following your CLAUDE.md workflow. Loop until all tasks are merged. Remember: you NEVER write code — send tasks back to workers if anything fails validation.

## Merge Order

Respect task dependencies. If a task has a `dependsOn` array, ALL listed dependencies must be `merged` before you merge that task. If a completed task has unmet dependencies, skip it and merge the dependency first.

## Validation

The `merge-worker.sh --validate` script runs the project's validation commands. If any check fails, send the task back to the worker with the specific failure. Do NOT merge code that fails validation.

Validation commands the reviewer checks (run by `merge-worker.sh --validate`):
- `dotnet build` — compiles; fails on type/compile errors
- `dotnet format --verify-no-changes` — formatting / style check
- `dotnet test` — add once a test project exists in the solution

## Follow-ups

While reviewing, watch for related improvements or follow-up work that is out of scope for the current task but worth tracking. After each merge, check /home/caleb/projects/movie-night-picker-dotnet/.claude-knowledge/todos.md and append any new ideas to the relevant work set (or create a new section if none fits). If something already exists as a todo, skip it. Examples of things to note: edge cases not handled, missing tests, performance concerns, accessibility gaps, UX improvements suggested by the code, or features that would naturally complement what was just built.
