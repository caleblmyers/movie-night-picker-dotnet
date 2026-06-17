#!/usr/bin/env bash
set -euo pipefail

# ============================================================================
# CUSTOMIZE: Update these for your project
# ============================================================================
# Dependency install command (.NET)
INSTALL_CMD="dotnet restore"
# Env files to copy to worktrees (relative to repo root)
ENV_FILES_REL=("appsettings.Development.json")
# Post-install commands (e.g., "dotnet tool restore" once a tool manifest exists)
# Leave empty if none needed
POST_INSTALL_CMD=""
# ============================================================================

MAIN_REPO="$(cd "$(dirname "$0")/../.." && pwd)"
WORKER_COUNT="${1:-3}"
MAX_WORKERS=5
SWARM_ID="swarm-$(date +%Y%m%d-%H%M%S)"
TASKS_FILE="$MAIN_REPO/.ai/taskswarm/tasks.json"
PROMPTS_DIR="$MAIN_REPO/.ai/taskswarm/prompts"
PARENT_DIR="$(dirname "$MAIN_REPO")"
REPO_NAME="$(basename "$MAIN_REPO")"

if [ "$WORKER_COUNT" -gt "$MAX_WORKERS" ]; then
  echo "Error: max $MAX_WORKERS workers allowed"
  exit 1
fi

if [ "$WORKER_COUNT" -lt 1 ]; then
  echo "Error: need at least 1 worker"
  exit 1
fi

# Warn if working tree is dirty
if ! git -C "$MAIN_REPO" diff --quiet || ! git -C "$MAIN_REPO" diff --cached --quiet; then
  echo "Warning: working tree has uncommitted changes."
  echo "  Worktrees will branch from HEAD (last commit) — uncommitted changes are excluded."
  echo ""
fi

echo "=== Spawning swarm: $SWARM_ID ==="
echo "Main repo: $MAIN_REPO"
echo "Workers: $WORKER_COUNT"
echo ""

# Collect env files to copy
ENV_FILES=()
for rel in "${ENV_FILES_REL[@]}"; do
  f="$MAIN_REPO/$rel"
  [ -f "$f" ] && ENV_FILES+=("$f")
done

setup_worktree() {
  local DIR="$1"
  local ROLE="$2"  # "worker" or "reviewer"
  local PROMPT_FILE="$3"
  local BRANCH="$4"
  local WORKER_ID="${5:-}"

  # Copy env files
  for f in "${ENV_FILES[@]}"; do
    REL="${f#$MAIN_REPO/}"
    mkdir -p "$(dirname "$DIR/$REL")"
    cp "$f" "$DIR/$REL"
  done

  # Install dependencies
  echo "Installing dependencies for $ROLE..."
  (cd "$DIR" && eval "$INSTALL_CMD" 2>/dev/null) || true

  # Run post-install
  if [ -n "$POST_INSTALL_CMD" ]; then
    echo "Running post-install for $ROLE..."
    (cd "$DIR" && eval "$POST_INSTALL_CMD" 2>&1) || echo "Warning: post-install failed (non-fatal)"
  fi

  # Grant full tool permissions (agents run unattended)
  mkdir -p "$DIR/.claude"
  cat > "$DIR/.claude/settings.json" << 'SETTINGS_EOF'
{
  "permissions": {
    "allow": [
      "Bash(*)",
      "Read(*)",
      "Write(*)",
      "Edit(*)",
      "Glob(*)",
      "Grep(*)",
      "WebFetch(*)",
      "WebSearch(*)"
    ],
    "deny": [
      "Bash(rm -rf *)",
      "Bash(git push *)",
      "Bash(git reset --hard *)",
      "Bash(git clean *)",
      "Bash(docker rm *)",
      "Bash(docker system *)"
    ]
  }
}
SETTINGS_EOF
  # settings.json is untracked (gitignored or never committed) in some repos, so
  # assume-unchanged can fail — keep it non-fatal and fall back to a local exclude.
  # In a linked worktree ".git" is a file, so resolve the real exclude path.
  if ! git -C "$DIR" update-index --assume-unchanged .claude/settings.json 2>/dev/null; then
    EXCLUDE_FILE="$(git -C "$DIR" rev-parse --git-path info/exclude 2>/dev/null || true)"
    [ -n "$EXCLUDE_FILE" ] && echo ".claude/settings.json" >> "$EXCLUDE_FILE" 2>/dev/null || true
  fi

  # Write role prompt
  if [ -f "$PROMPT_FILE" ]; then
    local SED_ARGS=(-e "s|{{MAIN_REPO}}|$MAIN_REPO|g")
    if [ -n "$WORKER_ID" ]; then
      SED_ARGS+=(-e "s|{{WORKER_ID}}|$WORKER_ID|g")
      SED_ARGS+=(-e "s|{{BRANCH}}|$BRANCH|g")
    fi
    PROMPT=$(sed "${SED_ARGS[@]}" "$PROMPT_FILE")
    printf '%s\n' "$PROMPT" > "$DIR/.claude/swarm-role.md"
    printf '\n\n<!-- swarm-role -->\n---\n\n%s\n' "$PROMPT" >> "$DIR/CLAUDE.md"
    git -C "$DIR" update-index --assume-unchanged CLAUDE.md
  fi
}

# Create worker worktrees
for i in $(seq 1 "$WORKER_COUNT"); do
  WORKER_DIR="$PARENT_DIR/${REPO_NAME}-worker-$i"
  BRANCH="swarm/worker-$i"

  if [ -d "$WORKER_DIR" ]; then
    echo "Warning: $WORKER_DIR already exists, skipping"
    continue
  fi

  echo "Creating worker-$i worktree..."
  git -C "$MAIN_REPO" worktree add -b "$BRANCH" "$WORKER_DIR" HEAD
  setup_worktree "$WORKER_DIR" "worker-$i" "$PROMPTS_DIR/worker.md" "$BRANCH" "worker-$i"
  echo "  -> $WORKER_DIR (branch: $BRANCH)"
done

# Create reviewer worktree
REVIEWER_DIR="$PARENT_DIR/${REPO_NAME}-reviewer"
REVIEWER_BRANCH="swarm/reviewer"

if [ -d "$REVIEWER_DIR" ]; then
  echo "Warning: $REVIEWER_DIR already exists, skipping"
else
  echo "Creating reviewer worktree..."
  git -C "$MAIN_REPO" worktree add -b "$REVIEWER_BRANCH" "$REVIEWER_DIR" HEAD
  setup_worktree "$REVIEWER_DIR" "reviewer" "$PROMPTS_DIR/reviewer.md" "$REVIEWER_BRANCH"
  echo "  -> $REVIEWER_DIR (branch: $REVIEWER_BRANCH)"
fi

# Initialize tasks.json
echo "Initializing task queue..."
node -e "
const data = {
  version: 1,
  created: new Date().toISOString(),
  swarmId: '$SWARM_ID',
  config: {
    mainRepo: '$MAIN_REPO',
    workerCount: $WORKER_COUNT,
    groups: []
  },
  tasks: []
};
require('fs').writeFileSync('$TASKS_FILE', JSON.stringify(data, null, 2) + '\n');
"

echo ""
echo "=== Swarm ready ==="
echo "Task queue: $TASKS_FILE"
echo ""
echo "Next steps:"
echo "  1. Open a terminal in $MAIN_REPO and run 'claude' (planner)"
echo "  2. Open terminals in each worker dir and run 'claude'"
echo "  3. Open a terminal in $REVIEWER_DIR and run 'claude'"
echo "  4. Monitor: bash $MAIN_REPO/scripts/taskswarm/status.sh"
