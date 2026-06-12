# AI Swarm Orchestration

Parallelize development across multiple Claude Code instances, each working in its own git worktree on a separate branch. A planner decomposes work, workers implement in isolation, and a reviewer validates and merges.

## Architecture

```
Main repo (planner runs here)
  .ai/taskswarm/tasks.json           <- shared task queue (gitignored)
  .ai/taskswarm/prompts/*.md         <- role templates (committed)
  scripts/taskswarm/*.sh             <- spawn/teardown/status (committed)
       |
  movie-night-picker-dotnet-worker-1/              <- worktree on branch swarm/worker-1
  movie-night-picker-dotnet-worker-2/              <- worktree on branch swarm/worker-2
  movie-night-picker-dotnet-worker-3/              <- worktree on branch swarm/worker-3
  movie-night-picker-dotnet-reviewer/              <- worktree on branch swarm/reviewer
```

## Prerequisites

- Git installed
- Project dependencies installable (Node/Python/etc.)
- `gh` CLI authenticated — run `gh auth status` to verify (reviewer needs this for PRs)
- Clean working tree on `main` — commit or stash all changes before spawning
- Enough terminal tabs or tmux panes: 1 planner + N workers + 1 reviewer

## Quick Start

### 1. Spawn the swarm

```bash
bash scripts/taskswarm/spawn.sh 3    # creates 3 workers + 1 reviewer
```

This creates sibling worktree directories, installs dependencies, copies env files, and appends role-specific instructions to each worktree's `CLAUDE.md`.

### 2. Start the planner (main repo terminal)

```bash
cd ~/projects/movie-night-picker-dotnet
claude
```

Paste this kickoff prompt:

> You are the planner agent. Read `.ai/taskswarm/prompts/planner.md` for your role instructions. Plan the work from `.claude-knowledge/todos.md` and distribute across 3 workers.

### 3. Start the workers (one terminal each)

```bash
cd ~/projects/movie-night-picker-dotnet-worker-1
claude
```

Paste this kickoff prompt:

> You are a worker agent. Your role instructions are at the bottom of this CLAUDE.md file. Read the task queue and begin working on your assigned tasks.

Repeat for each worker directory.

### 4. Start the reviewer

```bash
cd ~/projects/movie-night-picker-dotnet-reviewer
claude
```

Paste this kickoff prompt:

> You are the reviewer agent. Your role instructions are at the bottom of this CLAUDE.md file. Check the task queue for completed tasks and begin reviewing.

### 5. Monitor progress

```bash
bash scripts/taskswarm/status.sh
```

### 6. Teardown

```bash
bash scripts/taskswarm/teardown.sh
```

## Task Lifecycle

```
pending -> in_progress -> completed -> review -> merged
               |                         |
            blocked              in_progress (needs fixes)
```

- **pending**: task is planned but not started
- **in_progress**: worker is actively implementing
- **completed**: worker finished, awaiting review
- **review**: reviewer is checking the work
- **merged**: squash-merged into main locally
- **blocked**: worker hit an issue (see `reviewNotes`)

## Parallel Execution Model

Work is organized into **Task Sets** in `.claude-knowledge/todos.md`:

- **Schema/shared sets** — touch shared files (DB schema, configs). Only ONE runs per wave.
- **Independent sets** — no shared file conflicts. Run freely in parallel.

Each **wave** = 1 shared set + N independent sets running simultaneously.

### Assignment Rules

1. **One shared set per wave** — never assign two schema/shared sets to separate workers.
2. **Independent sets run freely** — assign one per remaining worker.
3. **Self-contained sets** — all tasks in a set go to the same worker. Never split across workers.
4. **Priority order** — when auto-selecting, pick highest-priority unfinished sets.
5. **Workers only touch files in their task's `files` array** — enforced by convention.

## Helper Scripts

### Task status updates

```bash
# Claim a task
bash scripts/taskswarm/task-update.sh task-001 in_progress --startedAt

# Mark complete
bash scripts/taskswarm/task-update.sh task-001 completed --completedAt

# Mark merged
bash scripts/taskswarm/task-update.sh task-001 merged --reviewedAt

# Send back for fixes
bash scripts/taskswarm/task-update.sh task-001 in_progress --reviewNotes="typecheck fails in auth.ts"

# Mark blocked
bash scripts/taskswarm/task-update.sh task-001 blocked --reviewNotes="need file not in list"
```

### Merge a worker branch

```bash
# Merge with validation
bash scripts/taskswarm/merge-worker.sh swarm/worker-1 --validate

# Merge without validation
bash scripts/taskswarm/merge-worker.sh swarm/worker-1
```

### View task statuses

```bash
bash scripts/taskswarm/status.sh
```

## Design Notes

- **No remote pushes from workers/reviewer** — only the user pushes from main when ready
- **Workers loop until done** — workers auto-rebase before each task, self-fix on review feedback, and loop until all their tasks are `merged`
- **Local merges** — the reviewer squash-merges worker branches into main locally (no PRs)
- **Helper scripts minimize approvals** — replace inline commands and cross-directory git operations

## Limitations

- **No automatic orchestration** — you manually open terminals and start each Claude Code instance
- **File-based coordination without locking** — works because agents are slow (minutes between writes) and each only modifies its own task status fields
- **Workers must not touch files outside their task's `files` array** — enforced by convention
- **One swarm at a time** — single tasks.json file

## Customization Checklist

When adapting for a new project, update:
- [ ] `movie-night-picker-dotnet` references in this file and prompts
- [ ] Validation commands in reviewer prompt (typecheck, lint, build)
- [ ] ORM/migration instructions in worker and planner prompts (or remove if not applicable)
- [ ] Helper script paths and commands
- [ ] `.gitignore` entries for `tasks.json` and `issues.md`
