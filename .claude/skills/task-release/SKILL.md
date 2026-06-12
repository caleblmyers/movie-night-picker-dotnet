---
name: task-release
description: Complete post-swarm cleanup and release — verify tasks, tear down worktrees, validate build, update docs, push, and monitor CI. Use when the user says "release", "push and verify", "deploy wave", "cleanup and release".
disable-model-invocation: false
user-invocable: true
---

# Release Wave

You are the release agent. Your job is to clean up after a swarm wave, validate everything, update documentation, push, and verify CI. This is a single end-to-end flow.

## 1. Verify Task Completion

Read `.ai/taskswarm/tasks.json` and check all task statuses:
```bash
cat .ai/taskswarm/tasks.json | node -e "const d=JSON.parse(require('fs').readFileSync('/dev/stdin','utf8')); d.tasks.forEach(t => console.log(t.id, t.status.padEnd(12), t.assignee.padEnd(10), t.title.slice(0,65)))"
```

- If any tasks are NOT `merged`, report which ones and stop — the wave isn't done yet.
- If all tasks are `merged`, proceed.

## 2. Check for Leftover Issues

- Run `git status` to check for uncommitted changes from the reviewer
- Check `.ai/taskswarm/issues.md` for any process issues logged by workers/reviewer — summarize them for the user

## 3. Tear Down Worktrees

Check if worktrees exist beyond main:
```bash
git worktree list
```

If swarm worktrees exist, tear them down:
```bash
bash scripts/taskswarm/teardown.sh
```

Verify cleanup — only the main worktree should remain.

## 4. Pre-push Validation

Run ALL checks from the repo root:

<!-- CUSTOMIZE: Replace with your project's validation commands -->
```bash
dotnet build
```
```bash
dotnet format --verify-no-changes
```
```bash
dotnet build
```
```bash
dotnet test
```

<!-- CUSTOMIZE: Add runtime smoke test if applicable, e.g.:
### Runtime Smoke Test
```bash
node dist/index.js &
BGPID=$!
sleep 3
curl -sf http://localhost:3000/health || echo "FAILED"
kill $BGPID 2>/dev/null
```
-->

If ANY check fails:
- Identify the specific error
- Report it to the user — do NOT attempt to fix it
- Stop until the user decides how to proceed

## 5. Update Documentation

- Read `.claude-knowledge/todos.md`
- Move completed sets from "Remaining" to "Completed" section
- Mark items as `[x]` with the wave number and date
- **Add follow-up items as new todos:** Read `issues.md` and any changelog for incomplete work or gaps discovered during the wave. Add each as a `- [ ]` item under the appropriate work set.
- **Verify follow-ups are in todos:** Before committing, check todos.md for each open follow-up mentioned in issues.md. If any are missing, add them.
- Update the Parallelism Matrix if set relationships changed
- Update `.claude-knowledge/changelog.md` if it exists

## 6. Update App Docs (if wave changed the data model, auth, or file structure)

Check if this wave added/changed DB models, auth flow, API endpoints, or major file structure. If so, update `.claude-knowledge/app-overview.md` and any relevant docs.

Skip this step if the wave was purely frontend, cleanup, or documentation changes.

## 7. Commit Documentation

Stage and commit the documentation updates:
```bash
git add .claude-knowledge/todos.md .claude-knowledge/changelog.md .ai/taskswarm/issues.md
```

Use Conventional Commits format:
```
chore(docs): update todos and changelog after Wave N completion

- Mark [sets] as completed
- [summary of issues.md if any]

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>
```

## 8. Check Git State

```bash
git status
```
If dirty after the docs commit, warn and stop.

```bash
git log --oneline origin/main..HEAD
```
Show the user what commits will be pushed.

## 9. Push

```bash
git push origin main
```

If push fails (e.g., rejected due to remote changes), report and stop. Do NOT force push.

## 10. Monitor CI

After push, monitor CI:

```bash
gh run list --branch main --limit 1
```

Get the run ID and watch it:
```bash
gh run watch <run-id>
```

If CI fails:
- Identify the specific failing step and error
- Report to the user with the error details
- Suggest: "Fix the issue, commit, and re-run `/task-release`"
- Do NOT attempt to fix CI failures yourself

<!-- CUSTOMIZE: Add deploy verification steps if applicable, e.g.:
## 11. Verify Deployment
```bash
curl -sf https://your-app.com/health
```
Compare deployed version to HEAD commit SHA.
-->

## 11. Report

Format the output clearly:

```
## Release Report — Wave N

| Check | Status |
|-------|--------|
| Tasks merged | ✓ All N tasks |
| Worktree teardown | ✓ Clean |
| TypeScript | ✓ Pass |
| Lint | ✓ Pass |
| Build | ✓ Pass |
| Tests | ✓ Pass (N tests) |
| Docs updated | ✓ Committed |
| Push | ✓ N commits pushed |
| CI Pipeline | ✓ All jobs passed |

### Commits pushed (N):
[list of commit messages]

### Verdict
✓ Wave N released successfully — all checks pass, CI green.
OR
✗ Release blocked — [specific failure]
```

Do NOT push to remote until all pre-push checks pass.
