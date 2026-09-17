#!/usr/bin/env bash
set -euo pipefail

usage() {
  echo "usage: config-transfer.sh <dev|stage|prod> export <version|active>" >&2
  echo "       config-transfer.sh <dev|stage|prod> import <actor> <reason>" >&2
  echo "       config-transfer.sh <dev|stage|prod> status" >&2
  exit 2
}

[ "$#" -ge 2 ] || usage

ENVIRONMENT="$1"
COMMAND="$2"

case "$ENVIRONMENT" in
  dev|stage|prod) ;;
  *) usage ;;
esac

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
COMPOSE_DIR="$ROOT_DIR/compose"
TRANSFER_DIR="$COMPOSE_DIR/transfer"
HISTORY_FILE="$ROOT_DIR/deploy-history.log"

IMAGE_TAG="$(awk '$3 == "ok" {print $2}' "$HISTORY_FILE" 2>/dev/null | tail -n 1)"
[ -n "$IMAGE_TAG" ] || IMAGE_TAG="$(awk '{print $2}' "$HISTORY_FILE" 2>/dev/null | tail -n 1)"
[ -n "$IMAGE_TAG" ] || { echo "no deployment found for $ENVIRONMENT" >&2; exit 1; }
export IMAGE_TAG

tool() {
  docker compose --env-file "$COMPOSE_DIR/.env" -p "lewdventure-$ENVIRONMENT" --project-directory "$COMPOSE_DIR" \
    -f "$COMPOSE_DIR/compose.yaml" -f "$COMPOSE_DIR/compose.vps.yaml" -f "$COMPOSE_DIR/compose.$ENVIRONMENT.yaml" \
    --profile tools run --rm --no-deps -T --user "$(id -u):$(id -g)" config-tool "$@"
}

read_env() {
  grep -E "^$1=" "$COMPOSE_DIR/.env" | tail -n 1 | cut -d= -f2-
}

reload_api() {
  local admin_key ops_port
  admin_key="$(read_env Admin__ApiKey)"
  ops_port="$(read_env OPS_HOST_PORT)"

  if [ -z "$admin_key" ] || [ -z "$ops_port" ]; then
    echo "api reload skipped: Admin__ApiKey or OPS_HOST_PORT is not set" >&2
    return 0
  fi

  if ! curl -fsS -o /dev/null "http://127.0.0.1:$ops_port/health/live" 2>/dev/null; then
    echo "api reload skipped: api is not running" >&2
    return 0
  fi

  if ! curl -fsS -X POST --header @<(printf 'X-Admin-Key: %s' "$admin_key") "http://127.0.0.1:$ops_port/admin/config/reload" >&2; then
    echo "snapshot is active in mongo but api reload failed" >&2
    return 4
  fi

  echo >&2
}

mkdir -p "$TRANSFER_DIR"

case "$COMMAND" in
  export)
    [ "$#" -eq 3 ] || usage
    rm -f "$TRANSFER_DIR/snapshot.json"
    tool export --version "$3" --out /transfer/snapshot.json >&2
    cat "$TRANSFER_DIR/snapshot.json"
    rm -f "$TRANSFER_DIR/snapshot.json"
    ;;
  import)
    [ "$#" -eq 4 ] || usage
    cat > "$TRANSFER_DIR/snapshot.json"
    set +e
    tool publish --file /transfer/snapshot.json --activate --actor "$3" --reason "$4"
    STATUS=$?
    set -e
    rm -f "$TRANSFER_DIR/snapshot.json"
    [ "$STATUS" -eq 0 ] || exit "$STATUS"
    reload_api
    ;;
  status)
    tool status
    ;;
  *)
    usage
    ;;
esac
