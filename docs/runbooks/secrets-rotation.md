[← Runbooks](README.md)

# Секреты и их ротация

## Где что лежит

| Секрет | Где | Кто использует |
| --- | --- | --- |
| Ключ service account Google | `compose/secrets/google-credentials.json` окружений dev и stage на VPS; локально `google-credentials.json` в корне, вне git | импорт Sheets |
| Origin-сертификат Cloudflare | `/opt/lewdventure/proxy/certs/` | TLS между Cloudflare и Caddy |
| `Admin__ApiKey` | `.env` окружения | `/admin/config/*`, `config-transfer.sh` |
| `ConfigPublisher__ApiKey` | `.env` dev/stage, Script Properties Apps Script | публикация из таблицы |
| `Alerts__DiscordWebhookUrl` | `.env` окружения | алерты сервера |
| `MONGO_ROOT_PASSWORD`, `MONGO_APP_PASSWORD` | `.env` окружения | Mongo |
| `SSH_PRIVATE_KEY`, `SSH_KNOWN_HOSTS` | GitHub Environments | деплой |
| `GHCR_PULL_TOKEN` | GitHub Environments, `~/.docker/config.json` на VPS | pull образов |
| `DISCORD_DEPLOY_WEBHOOK_URL` | секрет репозитория | уведомления о деплое |

В git секретов нет: `.gitignore` исключает `.env*` (кроме `.env.example`), `secrets/`, `google-credentials.json`.

## Ключ Google (срочно)

Старый ключ был закоммичен в историю репозитория, поэтому считается скомпрометированным.

1. GCP → IAM → Service Accounts → аккаунт чтения таблиц → Keys → Add key (JSON).
2. Положить новый ключ на VPS в `/opt/lewdventure/dev/compose/secrets/` и `/opt/lewdventure/stage/compose/secrets/`, права `600`, и локально разработчикам.
3. Перезапустить api dev и stage: `scripts/deploy.sh <env> $(scripts/deploy.sh <env> current)`.
4. Проверить публикацию из таблицы на dev.
5. Удалить старый ключ в GCP.
6. Посмотреть Cloud Audit Logs аккаунта за период, пока ключ был в истории.
7. Решение по истории git (`git filter-repo --path google-credentials.json --invert-paths` + force push) принимает владелец: ломает существующие клоны, ротация нужна в любом случае.

## API-ключи сервера

`Admin__ApiKey` (не короче 32 символов) и `ConfigPublisher__ApiKey` (не короче 24):

1. `openssl rand -hex 32`.
2. Обновить значение в `.env` окружения.
3. Перезапустить api тем же тегом: `scripts/deploy.sh <env> $(scripts/deploy.sh <env> current)`.
4. Для `ConfigPublisher__ApiKey` — обновить Script Properties в Apps Script.

Ключ читается при старте, поэтому между шагами 2–3 и 4 публикация из таблицы недоступна несколько секунд.

## Пароли Mongo

Пароль приложения:

```bash
docker compose ... exec mongo mongosh -u root -p "$MONGO_ROOT_PASSWORD" --authenticationDatabase admin \
  --eval 'db.getSiblingDB("lewdventure_prod").changeUserPassword("lewdventure_app", "<новый>")'
```

Затем `MONGO_APP_PASSWORD` в `.env` и перезапуск api. `mongo-init` создаёт пользователя только если его нет и пароль существующего не меняет, поэтому смена пароля — только этой командой.

Пароль root меняется так же через `db.getSiblingDB("admin").changeUserPassword("root", …)` и обновление `MONGO_ROOT_PASSWORD`.

## Discord webhook

Удалить webhook в настройках канала, создать новый, обновить `.env` и перезапустить api. Для деплойного канала — секрет `DISCORD_DEPLOY_WEBHOOK_URL`.

## Origin-сертификат Cloudflare

Срок 15 лет, при компрометации ключа: Cloudflare → SSL/TLS → Origin Server → Create Certificate, заменить файлы в `proxy/certs/`, `docker compose restart caddy` в `/opt/lewdventure/proxy`, затем Revoke старого сертификата.

## SSH и GHCR

- SSH: новый ключ в `authorized_keys`, обновить `SSH_PRIVATE_KEY` в окружении GitHub, проверить `rollback` без тега на dev, удалить старый ключ.
- GHCR: новый PAT с `read:packages`, обновить `GHCR_PULL_TOKEN`, следующий деплой перелогинит VPS; отозвать старый.
