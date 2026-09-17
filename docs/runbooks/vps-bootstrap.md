[← Runbooks](README.md)

# Подготовка VPS

Два сервера: VPS 1 — dev и stage (разные compose-проекты, базы и порты), VPS 2 — prod. Шаги одинаковые, отличаются окружения.

## 1. Система

- Ubuntu 24.04 LTS, автообновления безопасности (`unattended-upgrades`).
- Пользователь `deploy` без пароля, вход только по SSH-ключу, `PasswordAuthentication no`, `PermitRootLogin no`.
- Firewall: открыты 22, 80, 443 (`ufw allow OpenSSH`, `ufw allow 80,443/tcp`, `ufw allow 443/udp`).
- Docker Engine и compose plugin из официального репозитория Docker, `deploy` в группе `docker`.
- Ротация логов контейнеров настроена в compose (`json-file`, 10 MB × 5).

## 2. Каталоги

```bash
sudo mkdir -p /opt/lewdventure/proxy
sudo mkdir -p /opt/lewdventure/dev/compose/secrets /opt/lewdventure/stage/compose/secrets
sudo chown -R deploy:deploy /opt/lewdventure
chmod 700 /opt/lewdventure/*/compose/secrets
```

На VPS 2 вместо dev/stage — `/opt/lewdventure/prod/compose`.

Раскладка после первого деплоя:

```text
/opt/lewdventure/<env>/
  compose/            compose.yaml, compose.vps.yaml, compose.<env>.yaml, .env, secrets/, transfer/
  mongo/              init-replica.js, mongod-entrypoint.sh
  scripts/            deploy.sh, collect-logs.sh, config-transfer.sh
  deploy-history.log  история деплоев: время, тег, ok|failed, кто
```

`compose/`, `mongo/`, `scripts/` перезаписываются каждым деплоем из коммита образа; `.env`, `secrets/`, история и тома Docker — нет.

## 3. Секреты окружения

`/opt/lewdventure/<env>/compose/.env`, права `600`. Основа — `.env.example` из репозитория, описание ключей — [configuration.md](configuration.md). Минимум:

```text
DEPLOY_ENVIRONMENT=stage
API_HOST_PORT=5002
OPS_HOST_PORT=9092
MONGO_DATABASE=lewdventure_stage
MONGO_ROOT_USERNAME=root
MONGO_ROOT_PASSWORD=<openssl rand -hex 32>
MONGO_APP_USERNAME=lewdventure_app
MONGO_APP_PASSWORD=<openssl rand -hex 32>
Admin__ApiKey=<openssl rand -hex 32>
ConfigPublisher__ApiKey=<openssl rand -hex 24>
Alerts__DiscordWebhookUrl=https://discord.com/api/webhooks/...
```

Для prod `ConfigPublisher__ApiKey` не нужен (публикация из таблицы в prod запрещена).

dev и stage: ключ Google `compose/secrets/google-credentials.json`, права `600`. На prod ключ Google не кладётся.

Keyfile Mongo генерируется контейнером при первом старте в томе `mongo-config`, отдельно создавать не нужно.

## 4. Caddy

```bash
cd /opt/lewdventure/proxy
```

Скопировать из репозитория `deploy/proxy/compose.yaml` и нужный Caddyfile, создать `.env`:

```text
CADDYFILE=Caddyfile.dev-stage
ACME_EMAIL=ops@example.com
DEV_DOMAIN=api-dev.example.com
STAGE_DOMAIN=api-stage.example.com
```

На prod: `CADDYFILE=Caddyfile.prod`, `PROD_DOMAIN=api.example.com`.

DNS A/AAAA-записи доменов должны указывать на VPS до запуска (Let's Encrypt).

```bash
docker network create lewdventure-edge
docker compose up -d
```

Caddy снаружи режет `/health*`, `/admin/*`, `/swagger*`, на prod ещё и `/api/config/*`.

## 5. Доступ GitHub Actions

1. Сгенерировать ключ деплоя: `ssh-keygen -t ed25519 -C github-deploy -f deploy_key`, публичную часть добавить в `~deploy/.ssh/authorized_keys`.
2. Снять отпечаток хоста: `ssh-keyscan -p 22 <host>` — это значение `SSH_KNOWN_HOSTS`.
3. Для pull приватных образов: Personal Access Token (classic) с `read:packages` от аккаунта с доступом к пакетам — `GHCR_PULL_TOKEN`, логин — переменная `GHCR_PULL_USER`. Если пакеты публичные, токен можно не задавать.

## 6. GitHub: окружения и секреты

Settings → Environments, создать `dev`, `stage`, `prod`:

| Имя | Тип | dev | stage | prod |
| --- | --- | --- | --- | --- |
| `SSH_HOST` | secret | VPS 1 | VPS 1 | VPS 2 |
| `SSH_USER` | secret | `deploy` | `deploy` | `deploy` |
| `SSH_PRIVATE_KEY` | secret | ключ VPS 1 | ключ VPS 1 | ключ VPS 2 |
| `SSH_KNOWN_HOSTS` | secret | VPS 1 | VPS 1 | VPS 2 |
| `GHCR_PULL_TOKEN` | secret | токен | токен | токен |
| `SSH_PORT` | variable | если не 22 | если не 22 | если не 22 |
| `DEPLOY_PATH` | variable | если не `/opt/lewdventure/dev` | если не `/opt/lewdventure/stage` | если не `/opt/lewdventure/prod` |
| `GHCR_PULL_USER` | variable | логин | логин | логин |

Секрет уровня репозитория: `DISCORD_DEPLOY_WEBHOOK_URL` — канал уведомлений о деплоях (можно не задавать).

Защита `prod`: Required reviewers (владелец), Deployment branches — только `master`. Для `stage` — только `master`.

Settings → Branches: защита `master` — merge только через PR, обязательные проверки `build-test`, `integration-tests`, `docker-smoke`. Settings → Code security: включить secret scanning и push protection.

## 7. Первый деплой

dev и stage: `master` → `cd` деплоит dev автоматически, stage — `promote` с `target=stage`. Конфиги создаются сами из Google Sheets (`BootstrapFromGoogleSheetsIfEmpty`).

prod — конфиги в Google не берутся, поэтому первый деплой идёт в два прохода:

1. `promote` с `target=prod`: api не стартует без конфигов, деплой помечается `failed`, но Mongo уже поднят. Это ожидаемо.
2. `config-promote` с `version=active`: переносит активный снапшот stage и активирует на prod.
3. Повторить `promote` с `target=prod` — теперь `ok`.

## 8. Бэкапы Mongo

Обязательно до пользовательских данных, см. [mongo-conventions.md](../architecture/mongo-conventions.md#бэкапы).
