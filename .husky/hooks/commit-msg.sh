#!/usr/bin/env sh
# commit-msg hook — enforces Conventional Commits format.
# Invoked by Husky.Net on every commit.

set -e

# Read the commit message from the file passed as the first arg.
COMMIT_MSG_FILE="$1"
COMMIT_MSG=$(cat "$COMMIT_MSG_FILE")

# Conventional Commits regex (simplified):
#   <type>(<scope>): <subject>
#   <type>: <subject>
# where type is one of: feat, fix, docs, refactor, test, chore, perf, build, ci, style, i18n
PATTERN='^(feat|fix|docs|refactor|test|chore|perf|build|ci|style|i18n)(\([a-z0-9_-]+\))?!?: .+'

if ! echo "$COMMIT_MSG" | head -1 | grep -qE "$PATTERN"; then
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
  exit 1
fi
