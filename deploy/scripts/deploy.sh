#!/usr/bin/env bash
set -euo pipefail

usage() {
  echo "usage: deploy.sh <dev|stage|prod> <sha-xxxxxxxxxxxx|rollback|current|history>" >&2
  exit 2
}

[ "$#" -eq 2 ] || usage

ENVIRONMENT="$1"
TARGET="$2"

case "$ENVIRONMENT" in
  dev|stage|prod) ;;
  *) usage ;;
esac

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
COMPOSE_DIR="$ROOT_DIR/compose"
HISTORY_FILE="$ROOT_DIR/deploy-history.log"
PROJECT="lewdventure-$ENVIRONMENT"

[ -f "$COMPOSE_DIR/.env" ] || { echo ".env is missing in $COMPOSE_DIR" >&2; exit 1; }

compose() {
  docker compose --env-file "$COMPOSE_DIR/.env" -p "$PROJECT" --project-directory "$COMPOSE_DIR" \
    -f "$COMPOSE_DIR/compose.yaml" -f "$COMPOSE_DIR/compose.vps.yaml" -f "$COMPOSE_DIR/compose.$ENVIRONMENT.yaml" "$@"
}

successful_tags() {
  if [ -f "$HISTORY_FILE" ]; then
    awk '$3 == "ok" {print $2}' "$HISTORY_FILE"
  fi
}

current_tag() {
  successful_tags | tail -n 1
}

previous_tag() {
  local current
  current="$(current_tag)"
  successful_tags | awk -v current="$current" '$0 != current' | tail -n 1
}

record() {
  echo "$(date -u +%Y-%m-%dT%H:%M:%SZ) $1 $2 ${DEPLOY_ACTOR:-$(whoami)}" >> "$HISTORY_FILE"
}

if [ "$TARGET" = "current" ]; then
  current_tag
  exit 0
fi

if [ "$TARGET" = "history" ]; then
  successful_tags
  exit 0
fi

if [ "$TARGET" = "rollback" ]; then
  TARGET="$(previous_tag)"
  [ -n "$TARGET" ] || { echo "no previous deployment to roll back to" >&2; exit 1; }
  echo "rolling back to $TARGET"
fi

[[ "$TARGET" =~ ^sha-[0-9a-f]{12}$ ]] || { echo "invalid image tag $TARGET" >&2; exit 2; }

PREVIOUS="$(current_tag)"

docker network inspect lewdventure-edge >/dev/null 2>&1 || docker network create lewdventure-edge >/dev/null
mkdir -p "$COMPOSE_DIR/transfer"

export IMAGE_TAG="$TARGET"

compose pull api config-tool

if compose up -d --remove-orphans --wait --wait-timeout 180 api; then
  record "$TARGET" ok
  docker image prune -f >/dev/null
  echo "deployed $PROJECT $TARGET"
  exit 0
fi

record "$TARGET" failed
echo "deployment of $TARGET failed" >&2
compose logs --tail 200 api >&2 || true

if [ -n "$PREVIOUS" ] && [ "$PREVIOUS" != "$TARGET" ]; then
  echo "restoring $PREVIOUS" >&2
  export IMAGE_TAG="$PREVIOUS"

  if compose up -d --remove-orphans --wait --wait-timeout 180 api; then
    echo "restored $PREVIOUS" >&2
    exit 1
  fi

  echo "restore of $PREVIOUS failed" >&2
  exit 3
fi

exit 1
