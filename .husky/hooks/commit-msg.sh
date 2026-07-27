#!/usr/bin/env sh
# commit-msg hook — enforces Conventional Commits format.
# Invoked by Husky.Net on every commit.

# Resolve the commit message file.
# Husky.Net's task-runner does not always forward git's $1 to the script,
# so we fall back to the well-known location git always writes to.
COMMIT_MSG_FILE=""
if [ -n "$1" ] && [ -f "$1" ]; then
  COMMIT_MSG_FILE="$1"
elif [ -f ".git/COMMIT_EDITMSG" ]; then
  COMMIT_MSG_FILE=".git/COMMIT_EDITMSG"
fi

if [ -z "$COMMIT_MSG_FILE" ]; then
  echo "[husky] commit-msg: no commit message file found, skipping validation"
  exit 0
fi

COMMIT_MSG=$(cat "$COMMIT_MSG_FILE")

# Conventional Commits regex (simplified):
#   <type>(<scope>): <subject>
#   <type>: <subject>
# where type is one of: feat, fix, docs, refactor, test, chore, perf, build, ci, style, i18n
PATTERN='^(feat|fix|docs|refactor|test|chore|perf|build|ci|style|i18n)(\([a-z0-9_-]+\))?!?: .+'

FIRST_LINE=$(printf '%s\n' "$COMMIT_MSG" | head -1)

if ! printf '%s\n' "$FIRST_LINE" | grep -qE "$PATTERN"; then
  echo ""
  echo "[husky] commit message does not match Conventional Commits format."
  echo ""
  echo "Expected: <type>(<scope>): <subject>"
  echo "          <type>: <subject>"
  echo ""
  echo "Where <type> is one of:"
  echo "  feat, fix, docs, refactor, test, chore, perf, build, ci, style, i18n"
  echo ""
  echo "Example: feat(mcp): add cards_move_by_label tool"
  echo "Example: docs: rebrand as standalone kanban"
  echo "Example: fix(api): handle null board in card.move"
  echo ""
  echo "Got: $FIRST_LINE"
  echo ""
  exit 1
fi

exit 0
