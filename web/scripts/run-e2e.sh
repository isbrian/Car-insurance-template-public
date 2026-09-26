#!/bin/sh
set -eu

SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
ROOT_DIR=$(CDPATH= cd -- "$SCRIPT_DIR/../.." && pwd)
COMPOSE_FILE="$ROOT_DIR/docker-compose.e2e.yml"
PROJECT_NAME="carinsurance-e2e"

cleanup() {
  docker compose -p "$PROJECT_NAME" -f "$COMPOSE_FILE" down -v --remove-orphans >/dev/null 2>&1 || true
}

trap cleanup EXIT INT TERM
npx playwright test "$@"
