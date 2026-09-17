#!/usr/bin/env bash
set -euo pipefail

PROXY_DIR="${1:-/opt/lewdventure/proxy}"
PORTS="80,443"
CHAIN="LEWD-CLOUDFLARE"
SET_V4="lewd-cloudflare-v4"
SET_V6="lewd-cloudflare-v6"
ENV_FILE="$PROXY_DIR/cloudflare.env"

[ "$(id -u)" -eq 0 ] || { echo "run as root" >&2; exit 1; }

for tool in curl ipset iptables ip6tables; do
  command -v "$tool" >/dev/null || { echo "$tool is required" >&2; exit 1; }
done

EXTERNAL_IF="${EXTERNAL_IF:-$(ip -4 route show default | awk '{print $5; exit}')}"
[ -n "$EXTERNAL_IF" ] || { echo "cannot detect external interface, set EXTERNAL_IF" >&2; exit 1; }

WORK_DIR="$(mktemp -d)"
trap 'rm -rf "$WORK_DIR"' EXIT

curl -fsS --retry 3 https://www.cloudflare.com/ips-v4 -o "$WORK_DIR/v4"
curl -fsS --retry 3 https://www.cloudflare.com/ips-v6 -o "$WORK_DIR/v6"

grep -Eq '^[0-9.]+/[0-9]+$' "$WORK_DIR/v4" || { echo "unexpected ips-v4 content" >&2; exit 1; }
grep -Eq '^[0-9a-f:]+/[0-9]+$' "$WORK_DIR/v6" || { echo "unexpected ips-v6 content" >&2; exit 1; }

load_set() {
  local name="$1" family="$2" file="$3"

  ipset create "$name" hash:net family "$family" -exist
  ipset create "$name-next" hash:net family "$family" -exist
  ipset flush "$name-next"

  while read -r network; do
    [ -n "$network" ] && ipset add "$name-next" "$network" -exist
  done < "$file"

  ipset swap "$name-next" "$name"
  ipset destroy "$name-next"
}

build_chain() {
  local tool="$1" set="$2"

  "$tool" -N "$CHAIN" 2>/dev/null || true
  "$tool" -F "$CHAIN"
  "$tool" -A "$CHAIN" -m set --match-set "$set" src -j RETURN
  "$tool" -A "$CHAIN" -j DROP
}

attach_chain() {
  local tool="$1" parent="$2"

  "$tool" -L "$parent" -n >/dev/null 2>&1 || return 0

  if ! "$tool" -C "$parent" -i "$EXTERNAL_IF" -p tcp -m multiport --dports "$PORTS" -m conntrack --ctstate NEW -j "$CHAIN" 2>/dev/null; then
    "$tool" -I "$parent" 1 -i "$EXTERNAL_IF" -p tcp -m multiport --dports "$PORTS" -m conntrack --ctstate NEW -j "$CHAIN"
  fi
}

load_set "$SET_V4" inet "$WORK_DIR/v4"
load_set "$SET_V6" inet6 "$WORK_DIR/v6"

build_chain iptables "$SET_V4"
build_chain ip6tables "$SET_V6"

attach_chain iptables DOCKER-USER
attach_chain iptables INPUT
attach_chain ip6tables DOCKER-USER
attach_chain ip6tables INPUT

NEXT_ENV="CLOUDFLARE_IPS=$(cat "$WORK_DIR/v4" "$WORK_DIR/v6" | tr '\n' ' ' | sed 's/ *$//')"

if [ ! -f "$ENV_FILE" ] || [ "$(cat "$ENV_FILE")" != "$NEXT_ENV" ]; then
  printf '%s\n' "$NEXT_ENV" > "$ENV_FILE"
  echo "cloudflare ranges updated in $ENV_FILE"

  if [ -f "$PROXY_DIR/compose.yaml" ] && docker compose --project-directory "$PROXY_DIR" ps --status running --services 2>/dev/null | grep -qx caddy; then
    docker compose --project-directory "$PROXY_DIR" up -d caddy
  fi
fi

echo "cloudflare firewall applied interface = $EXTERNAL_IF ports = $PORTS"
