#!/bin/bash
set -euo pipefail

KEYFILE=/data/configdb/replica.key

if [ ! -f "$KEYFILE" ]; then
  openssl rand -base64 756 > "$KEYFILE"
fi

chmod 400 "$KEYFILE"
chown 999:999 "$KEYFILE"

exec docker-entrypoint.sh mongod --replSet "${MONGO_REPLICA_SET:-rs0}" --keyFile "$KEYFILE" --bind_ip_all
