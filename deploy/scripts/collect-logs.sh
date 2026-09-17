#!/usr/bin/env bash
set -euo pipefail

[ "$#" -ge 1 ] || { echo "usage: collect-logs.sh <dev|stage|prod> [since]" >&2; exit 2; }

ENVIRONMENT="$1"
SINCE="${2:-24h}"
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
COMPOSE_DIR="$ROOT_DIR/compose"
STAMP="$(date -u +%Y%m%dT%H%M%SZ)"
WORK_DIR="$(mktemp -d)"
ARCHIVE="$ROOT_DIR/logs-$ENVIRONMENT-$STAMP.tar.gz"

compose() {
  docker compose --env-file "$COMPOSE_DIR/.env" -p "lewdventure-$ENVIRONMENT" --project-directory "$COMPOSE_DIR" \
    -f "$COMPOSE_DIR/compose.yaml" -f "$COMPOSE_DIR/compose.vps.yaml" -f "$COMPOSE_DIR/compose.$ENVIRONMENT.yaml" "$@"
}

compose ps > "$WORK_DIR/ps.txt" 2>&1 || true
compose logs --no-color --timestamps --since "$SINCE" api > "$WORK_DIR/api.log" 2>&1 || true
compose logs --no-color --timestamps --since "$SINCE" mongo > "$WORK_DIR/mongo.log" 2>&1 || true
cp "$ROOT_DIR/deploy-history.log" "$WORK_DIR/" 2>/dev/null || true
curl -fsS "http://127.0.0.1:$(grep -E '^OPS_HOST_PORT=' "$COMPOSE_DIR/.env" | cut -d= -f2)/health" > "$WORK_DIR/health.json" 2>&1 || true

tar -czf "$ARCHIVE" -C "$WORK_DIR" .
rm -rf "$WORK_DIR"

echo "$ARCHIVE"
