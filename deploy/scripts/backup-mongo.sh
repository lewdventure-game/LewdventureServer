#!/usr/bin/env bash
set -euo pipefail

[ "$#" -ge 1 ] || { echo "usage: backup-mongo.sh <dev|stage|prod> [keep-days]" >&2; exit 2; }

ENVIRONMENT="$1"

case "$ENVIRONMENT" in
  dev|stage|prod) ;;
  *) echo "usage: backup-mongo.sh <dev|stage|prod> [keep-days]" >&2; exit 2 ;;
esac

KEEP_DAYS="${2:-7}"
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
COMPOSE_DIR="$ROOT_DIR/compose"
BACKUP_DIR="$ROOT_DIR/backups/$ENVIRONMENT"
STAMP="$(date -u +%Y%m%dT%H%M%SZ)"
ARCHIVE="$BACKUP_DIR/$ENVIRONMENT-$STAMP.archive.gz"

read_env() {
  grep -E "^$1=" "$COMPOSE_DIR/.env" | tail -n 1 | cut -d= -f2-
}

MONGO_DATABASE="$(read_env MONGO_DATABASE)"
MONGO_ROOT_USERNAME="$(read_env MONGO_ROOT_USERNAME)"
MONGO_ROOT_PASSWORD="$(read_env MONGO_ROOT_PASSWORD)"

[ -n "$MONGO_DATABASE" ] || { echo "MONGO_DATABASE is not set in $COMPOSE_DIR/.env" >&2; exit 1; }
[ -n "$MONGO_ROOT_USERNAME" ] || { echo "MONGO_ROOT_USERNAME is not set in $COMPOSE_DIR/.env" >&2; exit 1; }
[ -n "$MONGO_ROOT_PASSWORD" ] || { echo "MONGO_ROOT_PASSWORD is not set in $COMPOSE_DIR/.env" >&2; exit 1; }

mkdir -p "$BACKUP_DIR"

docker compose --env-file "$COMPOSE_DIR/.env" -p "lewdventure-$ENVIRONMENT" --project-directory "$COMPOSE_DIR" \
  -f "$COMPOSE_DIR/compose.yaml" -f "$COMPOSE_DIR/compose.vps.yaml" -f "$COMPOSE_DIR/compose.$ENVIRONMENT.yaml" \
  exec -T mongo mongodump --archive --gzip \
  --username "$MONGO_ROOT_USERNAME" --password "$MONGO_ROOT_PASSWORD" --authenticationDatabase admin \
  --db "$MONGO_DATABASE" > "$ARCHIVE"

SIZE="$(stat -c %s "$ARCHIVE")"

[ "$SIZE" -gt 1024 ] || { echo "backup $ARCHIVE is suspiciously small: $SIZE bytes" >&2; rm -f "$ARCHIVE"; exit 1; }

find "$BACKUP_DIR" -name "$ENVIRONMENT-*.archive.gz" -type f -mtime "+$KEEP_DAYS" -delete

if [ -n "${LEWD_BACKUP_UPLOAD_COMMAND:-}" ]; then
  "$SHELL" -c "$LEWD_BACKUP_UPLOAD_COMMAND" "$ARCHIVE" "$ARCHIVE"
fi

echo "$ARCHIVE"
