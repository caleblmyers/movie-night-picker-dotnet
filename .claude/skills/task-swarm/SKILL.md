---
name: task-swarm
description: Spawn a new AI swarm wave — create worktrees, plan tasks from todos.md, write tasks.json, and output copy-paste prompts for workers and reviewer. Use when the user says "set up swarm", "spawn workers", "next wave", or "start swarm".
disable-model-invocation: false
user-invocable: true
---

# Spawn AI Swarm Wave

You are setting up a new swarm wave. Follow these steps precisely.

## Pre-flight Checks

1. **Verify worktree state:**
   - Run `git status` — if working tree is dirty, that's OK (another agent may be working). Worktrees branch from HEAD (last commit), so uncommitted changes are excluded and unaffected. Just note it and proceed.
   - Run `git worktree list` — no stale worktrees should exist (run teardown if needed)
   - Check for stale swarm branches: `git branch | grep swarm`

2. **Read current state:**
   - Read `.claude-knowledge/todos.md` to understand available work sets
   - Read the Parallelism Matrix to identify which sets can run together
   - Check the Completed section to know what's already done

## Planning

3. **Select sets for this wave:**
   - Pick 3 sets (one per worker) that have NO file overlap per the Parallelism Matrix
   - If the user specified sets, use those
   - Otherwise, select the highest-value conflict-free combination
   - Present the plan to the user for confirmation before proceeding

4. **Research source files:**
   - For each selected set, read the relevant source files
   - Understand current code state so task descriptions are precise and detailed

5. **Write tasks following the Task Sizing rules (CRITICAL):**
   - Each task MUST represent **30-60 minutes** of agentic work
   - Combine into **full vertical slices** (e.g., schema + API + frontend in ONE task)
   - Never create tasks that are just config changes or single-file edits
   - Each worker should have **2-4 tasks** totaling 30-60 min
   - Task descriptions should be 2-3 paragraphs with specific file paths, code snippets, and implementation details
   - Include acceptance criteria that are concrete and verifiable
   - **Task dependencies:** When a task depends on another, add `"dependsOn": ["task-001"]` to the dependent task. Workers and reviewer use this to enforce ordering.
   - **Worker parallelism:** When a worker has multiple tasks, minimize file overlap between them so the worker can start the next task while the previous one is in review.

   <!-- CUSTOMIZE: Add stack-specific acceptance criteria reminders here, e.g.:
   - If a task modifies DB schema, remind workers to run migrations
   - If a task installs new packages, remind to verify types
   -->

## Execution

6. **Spawn worktrees:**
   ```bash
   bash scripts/taskswarm/spawn.sh 3
   ```

7. **Write tasks.json** with all planned tasks.

8. **Output copy-paste prompts** for the user:

   <!-- CUSTOMIZE: Update paths below for your project -->

   ### Worker 1 (`cd ~/projects/movie-night-picker-dotnet-worker-1 && claude`)
   ```
   /task-worker
   ```

   ### Worker 2 (`cd ~/projects/movie-night-picker-dotnet-worker-2 && claude`)
   ```
   /task-worker
   ```

   ### Worker 3 (`cd ~/projects/movie-night-picker-dotnet-worker-3 && claude`)
   ```
   /task-worker
   ```

   ### Reviewer (`cd ~/projects/movie-night-picker-dotnet-reviewer && claude`)
   ```
   /task-reviewer
   ```

9. **Show a summary table** of the wave plan:

   | Worker | Set | Tasks | Description |
   |--------|-----|-------|-------------|
   | worker-1 | ... | task-001, task-002 | ... |
   | worker-2 | ... | task-003, task-004 | ... |
   | worker-3 | ... | task-005, task-006 | ... |

10. **Remind the user of the post-wave flow:**
    After workers and reviewer finish: `/task-release` (handles teardown, validation, docs, push, and CI monitoring in one step)
