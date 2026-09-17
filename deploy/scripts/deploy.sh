#!/usr/bin/env bash
set -euo pipefail

usage() {
  echo "usage: deploy.sh <dev|stage|prod> <image-tag|rollback>" >&2
  exit 2
}

[ "$#" -eq 2 ] || usage

ENVIRONMENT="$1"
TARGET="$2"

case "$ENVIRONMENT" in
  dev|stage|prod) ;;
  *) usage ;;
esac

DEPLOY_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
HISTORY_FILE="$DEPLOY_DIR/deploy-history.log"
PROJECT="lewdventure-$ENVIRONMENT"

cd "$DEPLOY_DIR"

[ -f .env ] || { echo ".env is missing in $DEPLOY_DIR" >&2; exit 1; }

compose() {
  docker compose --env-file .env -p "$PROJECT" -f compose.yaml -f compose.vps.yaml -f "compose.$ENVIRONMENT.yaml" "$@"
}

current_tag() {
  if [ -f "$HISTORY_FILE" ]; then
    tail -n 1 "$HISTORY_FILE" | awk '{print $2}'
  fi
}

previous_tag() {
  if [ -f "$HISTORY_FILE" ]; then
    tail -n 2 "$HISTORY_FILE" | head -n 1 | awk '{print $2}'
  fi
}

if [ "$TARGET" = "rollback" ]; then
  TARGET="$(previous_tag)"
  [ -n "$TARGET" ] || { echo "no previous deployment to roll back to" >&2; exit 1; }
  echo "rolling back to $TARGET"
fi

PREVIOUS="$(current_tag)"

docker network inspect lewdventure-edge >/dev/null 2>&1 || docker network create lewdventure-edge >/dev/null

export IMAGE_TAG="$TARGET"

compose pull api config-tool

if compose up -d --remove-orphans --wait --wait-timeout 180 api; then
  echo "$(date -u +%Y-%m-%dT%H:%M:%SZ) $TARGET" >> "$HISTORY_FILE"
  docker image prune -f >/dev/null
  echo "deployed $PROJECT $TARGET"
  exit 0
fi

echo "deployment of $TARGET failed" >&2
compose logs --tail 200 api >&2 || true

if [ -n "$PREVIOUS" ] && [ "$PREVIOUS" != "$TARGET" ]; then
  echo "restoring $PREVIOUS" >&2
  export IMAGE_TAG="$PREVIOUS"
  compose up -d --remove-orphans --wait --wait-timeout 180 api || true
fi

exit 1
