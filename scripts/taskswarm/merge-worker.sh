#!/usr/bin/env bash
set -euo pipefail

# Usage: merge-worker.sh <worker-branch> [--validate]
# Merges a worker branch into main in the main repo.
# Run from any directory — always operates on the main repo.

# ============================================================================
# CUSTOMIZE: Update these for your project
# ============================================================================
# Dependency install command
INSTALL_CMD="dotnet restore"
# Validation commands (run in sequence, all must pass)
# Add "dotnet test" once a test project exists in the solution.
VALIDATE_CMDS=("dotnet build")
# File pattern that triggers post-install regeneration. Leave empty to skip.
# EF Core has no client-gen step; run "dotnet ef migrations add" manually when models change.
REGEN_PATTERN=""
# Command to run when REGEN_PATTERN matches
REGEN_CMD=""
# ============================================================================

MAIN_REPO="$(cd "$(dirname "$0")/../.." && pwd)"

if [ $# -lt 1 ]; then
  echo "Usage: merge-worker.sh <branch> [--validate]"
  echo ""
  echo "Examples:"
  echo "  merge-worker.sh swarm/worker-1"
  echo "  merge-worker.sh swarm/worker-1 --validate"
  exit 1
fi

BRANCH="$1"
VALIDATE=false
[ "${2:-}" = "--validate" ] && VALIDATE=true

echo "=== Merging $BRANCH into main ==="

# Ensure we're on main
CURRENT=$(git -C "$MAIN_REPO" branch --show-current)
if [ "$CURRENT" != "main" ]; then
  echo "Error: main repo is on branch '$CURRENT', expected 'main'"
  exit 1
fi

# Show what will be merged
echo "Commits to merge:"
git -C "$MAIN_REPO" log --oneline "main..$BRANCH" 2>/dev/null || {
  echo "Error: branch '$BRANCH' not found"
  exit 1
}
echo ""

# Validate if requested
if [ "$VALIDATE" = true ]; then
  echo "Running validation..."
  # Temporarily merge to test
  git -C "$MAIN_REPO" merge --no-commit --no-ff "$BRANCH" 2>/dev/null || {
    echo "Error: merge conflicts detected"
    git -C "$MAIN_REPO" merge --abort
    exit 1
  }

  # Install deps if package files changed
  if git -C "$MAIN_REPO" diff --cached --name-only | grep -q 'package.json\|requirements.txt\|Cargo.toml\|go.mod'; then
    echo "Dependency file changes detected — installing..."
    if ! (cd "$MAIN_REPO" && eval "$INSTALL_CMD" 2>&1); then
      echo "Install failed — aborting merge"
      git -C "$MAIN_REPO" merge --abort
      exit 1
    fi
  fi

  # Run regeneration command if pattern matches
  if [ -n "$REGEN_PATTERN" ] && git -C "$MAIN_REPO" diff --cached --name-only | grep -q "$REGEN_PATTERN"; then
    echo "Regeneration trigger detected — running: $REGEN_CMD"
    if ! (cd "$MAIN_REPO" && eval "$REGEN_CMD" 2>&1); then
      echo "Regeneration failed — aborting merge"
      git -C "$MAIN_REPO" merge --abort
      exit 1
    fi
  fi

  # Run all validation commands
  for cmd in "${VALIDATE_CMDS[@]}"; do
    if ! (cd "$MAIN_REPO" && eval "$cmd" 2>&1); then
      echo "$cmd failed — aborting merge"
      git -C "$MAIN_REPO" merge --abort
      exit 1
    fi
  done

  # Abort the test merge, we'll do the real one below
  git -C "$MAIN_REPO" merge --abort
fi

# Check if worker branch has diverged from main
DIVERGED=$(git -C "$MAIN_REPO" rev-list --count "$BRANCH"..HEAD 2>/dev/null || echo "0")

if [ "$DIVERGED" -gt 0 ]; then
  echo "Worker branch is behind main by $DIVERGED commit(s) — using cherry-pick to avoid overwriting."
  COMMITS=$(git -C "$MAIN_REPO" rev-list --reverse "main..$BRANCH")
  if [ -z "$COMMITS" ]; then
    echo "No commits to cherry-pick."
    exit 0
  fi
  for COMMIT in $COMMITS; do
    git -C "$MAIN_REPO" cherry-pick --no-commit "$COMMIT" || {
      echo "Error: cherry-pick conflict on $COMMIT. Resolve manually."
      git -C "$MAIN_REPO" cherry-pick --abort 2>/dev/null || true
      exit 1
    }
  done
else
  # Worker branch is up-to-date — safe to squash merge
  git -C "$MAIN_REPO" merge --squash "$BRANCH" || {
    echo "Error: merge conflicts. Resolve manually or ask the worker to rebase."
    git -C "$MAIN_REPO" merge --abort 2>/dev/null || true
    exit 1
  }
fi

# Strip swarm role content from CLAUDE.md if present
CLAUDE_MD="$MAIN_REPO/CLAUDE.md"
if [ -f "$CLAUDE_MD" ] && grep -q '<!-- swarm-role -->' "$CLAUDE_MD"; then
  echo "Stripping swarm role content from CLAUDE.md..."
  sed -i '/<!-- swarm-role -->/,$d' "$CLAUDE_MD"
  sed -i -e :a -e '/^\n*$/{$d;N;ba' -e '}' "$CLAUDE_MD"
  git -C "$MAIN_REPO" add CLAUDE.md
fi

echo ""
echo "Squash merge staged. Review with 'git diff --cached' then commit."
echo "Suggested: git commit -m 'swarm(<worker>): [task-XXX] description'"
