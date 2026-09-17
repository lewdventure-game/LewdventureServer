#!/usr/bin/env bash
set -euo pipefail

[ "$#" -ge 1 ] || { echo "usage: collect-logs.sh <dev|stage|prod> [since]" >&2; exit 2; }

ENVIRONMENT="$1"
SINCE="${2:-24h}"
DEPLOY_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
STAMP="$(date -u +%Y%m%dT%H%M%SZ)"
WORK_DIR="$(mktemp -d)"
ARCHIVE="$DEPLOY_DIR/logs-$ENVIRONMENT-$STAMP.tar.gz"

cd "$DEPLOY_DIR"

compose() {
  docker compose --env-file .env -p "lewdventure-$ENVIRONMENT" -f compose.yaml -f compose.vps.yaml -f "compose.$ENVIRONMENT.yaml" "$@"
}

compose ps > "$WORK_DIR/ps.txt" 2>&1 || true
compose logs --no-color --timestamps --since "$SINCE" api > "$WORK_DIR/api.log" 2>&1 || true
compose logs --no-color --timestamps --since "$SINCE" mongo > "$WORK_DIR/mongo.log" 2>&1 || true
cp deploy-history.log "$WORK_DIR/" 2>/dev/null || true
curl -fsS "http://127.0.0.1:$(grep -E '^OPS_HOST_PORT=' .env | cut -d= -f2)/health" > "$WORK_DIR/health.json" 2>&1 || true

tar -czf "$ARCHIVE" -C "$WORK_DIR" .
rm -rf "$WORK_DIR"

echo "$ARCHIVE"
