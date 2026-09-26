#!/bin/sh
set -eu

SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
WEB_DIR=$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd)
ROOT_DIR=$(CDPATH= cd -- "$WEB_DIR/.." && pwd)
COMPOSE_FILE="$ROOT_DIR/docker-compose.e2e.yml"
PROJECT_NAME="carinsurance-e2e"
PLAYWRIGHT_IMAGE="mcr.microsoft.com/playwright:v1.63.0-noble"

compose() {
  docker compose -p "$PROJECT_NAME" -f "$COMPOSE_FILE" "$@"
}

cleanup() {
  compose down -v --remove-orphans >/dev/null 2>&1 || true
}

trap cleanup EXIT INT TERM
cleanup
compose up -d --build --remove-orphans

attempt=0
while :; do
  api_ready=0
  web_ready=0

  compose exec -T web wget -qO- http://api:8080/api/health >/dev/null 2>&1 && api_ready=1 || true
  compose exec -T web wget -qO- http://127.0.0.1/ >/dev/null 2>&1 && web_ready=1 || true

  if [ "$api_ready" -eq 1 ] && [ "$web_ready" -eq 1 ]; then
    break
  fi

  attempt=$((attempt + 1))
  if [ "$attempt" -ge 60 ]; then
    compose ps
    compose logs --no-color api web
    exit 1
  fi
  sleep 2
done

docker run --rm \
  --network "${PROJECT_NAME}_default" \
  --mount "type=bind,source=$WEB_DIR,target=/work" \
  --mount type=volume,target=/work/node_modules \
  -w /work \
  -e E2E_EXTERNAL_SERVER=1 \
  -e E2E_BASE_URL=http://web \
  "$PLAYWRIGHT_IMAGE" \
  sh -lc 'npm ci && npx playwright test'
