[← Runbooks](README.md)

# Подготовка VPS

Один VPS на все окружения: dev, stage и prod — отдельные compose-проекты со своими базами, томами, портами и `.env`. Снаружи — Cloudflare (бесплатный план: DNS, защита от DDoS, скрытие IP), на VPS — Caddy, который принимает трафик только от Cloudflare.

```text
игрок / Apps Script
  → Cloudflare (proxy, DDoS, TLS)
  → VPS :443, firewall пропускает только IP Cloudflare
  → Caddy (origin-сертификат Cloudflare, реальный IP из CF-Connecting-IP)
  → lewdventure-dev-api / lewdventure-stage-api / lewdventure-prod-api :5000
```

Разнести окружения по разным VPS позже — см. [раздел 9](#9-перенос-окружения-на-отдельный-vps).

## Размер VPS

| | api, лимит | mongo, лимит |
| --- | --- | --- |
| dev | 512 MB, 1 CPU | 768 MB, 1 CPU |
| stage | 512 MB, 1 CPU | 768 MB, 1 CPU |
| prod | 1 GB, 2 CPU | 1.5 GB, 2 CPU |

Это верхние границы, а не резерв: api под нагрузкой держится около 100 MB. Рекомендуется 4 vCPU / 8 GB (например, Hetzner CX32), минимум 2 vCPU / 4 GB. Диск от 40 GB.

## 1. Система

- Ubuntu 24.04 LTS, автообновления безопасности (`unattended-upgrades`).
- Пользователь `deploy` без пароля, вход только по SSH-ключу, `PasswordAuthentication no`, `PermitRootLogin no`.
- Docker Engine и compose plugin из официального репозитория Docker, `deploy` в группе `docker`.
- Пакеты для firewall: `sudo apt install ipset iptables curl`.
- `ufw`: `ufw default deny incoming`, `ufw allow OpenSSH`, `ufw allow 80,443/tcp`, `ufw enable`. Ограничение 80/443 только для Cloudflare делает скрипт из раздела 5 (ufw не фильтрует порты, опубликованные Docker).
- Если провайдер даёт внешний firewall (Hetzner Cloud Firewall) — открыть там 22, 80, 443; SSH по возможности только со своих IP.

## 2. Каталоги

```bash
sudo mkdir -p /opt/lewdventure/proxy/certs /opt/lewdventure/proxy/sites
sudo mkdir -p /opt/lewdventure/dev/compose/secrets /opt/lewdventure/stage/compose/secrets /opt/lewdventure/prod/compose
sudo chown -R deploy:deploy /opt/lewdventure
chmod 700 /opt/lewdventure/*/compose/secrets /opt/lewdventure/proxy/certs
```

Раскладка окружения после первого деплоя:

```text
/opt/lewdventure/<env>/
  compose/            compose.yaml, compose.vps.yaml, compose.<env>.yaml, .env, secrets/, transfer/
  mongo/              init-replica.js, mongod-entrypoint.sh
  scripts/            deploy.sh, collect-logs.sh, config-transfer.sh
  deploy-history.log  история деплоев
```

`compose/`, `mongo/`, `scripts/` перезаписываются каждым деплоем; `.env`, `secrets/`, история и тома Docker — нет.

## 3. Секреты окружений

`/opt/lewdventure/<env>/compose/.env`, права `600`. Шаблон — `.env.example`, ключи — [configuration.md](configuration.md).

| Переменная | dev | stage | prod |
| --- | --- | --- | --- |
| `DEPLOY_ENVIRONMENT` | `dev` | `stage` | `prod` |
| `API_HOST_PORT` | `5001` | `5002` | `5000` |
| `OPS_HOST_PORT` | `9091` | `9092` | `9090` |
| `MONGO_DATABASE` | `lewdventure_dev` | `lewdventure_stage` | `lewdventure_prod` |

Остальное генерируется отдельно для каждого окружения (`openssl rand -hex 32`): `MONGO_ROOT_PASSWORD`, `MONGO_APP_PASSWORD`, `Admin__ApiKey`; для dev и stage ещё `ConfigPublisher__ApiKey` (`openssl rand -hex 24`). Плюс `MONGO_ROOT_USERNAME=root`, `MONGO_APP_USERNAME=lewdventure_app`, `Alerts__DiscordWebhookUrl`.

Если Discord ещё не настроен: `Alerts__Enabled=false`.

dev и stage: ключ Google в `compose/secrets/google-credentials.json`, права `600`. На prod ключ Google не кладётся.

Keyfile Mongo генерируется контейнером при первом старте.

## 4. Cloudflare

1. Добавить домен в Cloudflare (Free), сменить NS у регистратора на выданные Cloudflare.
2. DNS: A-записи `api-dev`, `api-stage`, `api` на IP VPS, режим **Proxied** (оранжевое облако).
3. SSL/TLS → Overview: режим **Full (strict)**.
4. SSL/TLS → Origin Server → Create Certificate: RSA, хосты `example.com` и `*.example.com`, срок 15 лет. Сохранить на VPS:
   - сертификат → `/opt/lewdventure/proxy/certs/origin.pem`;
   - ключ → `/opt/lewdventure/proxy/certs/origin-key.pem`, права `600`.
5. SSL/TLS → Edge Certificates: включить Always Use HTTPS, минимальная версия TLS 1.2.
6. Security: Bot Fight Mode — выключен (мешает Apps Script и клиенту без браузера). При атаке — Security Level «I'm Under Attack» только для сайта-витрины, не для API.
7. Security → WAF → Rate limiting rules (1 правило в Free): например, `/api/battle/*` — не больше 100 запросов за 10 секунд с IP, действие Block на 10 секунд. Это первый рубеж, основной лимит остаётся в сервере.

## 5. Caddy и firewall

Скопировать из репозитория в `/opt/lewdventure/proxy/`:
- `deploy/proxy/compose.yaml`, `Caddyfile`, `cloudflare-firewall.sh` (права `755`);
- из `deploy/proxy/sites/` — файлы окружений, которые живут на этом VPS (сейчас все три: `dev.caddy`, `stage.caddy`, `prod.caddy`).

`/opt/lewdventure/proxy/.env`:

```text
DEV_DOMAIN=api-dev.example.com
STAGE_DOMAIN=api-stage.example.com
PROD_DOMAIN=api.example.com
```

Firewall и список IP Cloudflare (создаёт `cloudflare.env`, который читает Caddy):

```bash
sudo /opt/lewdventure/proxy/cloudflare-firewall.sh /opt/lewdventure/proxy
sudo cp deploy/proxy/systemd/lewdventure-cloudflare-firewall.* /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable --now lewdventure-cloudflare-firewall.timer
```

Скрипт:
- скачивает актуальные диапазоны `https://www.cloudflare.com/ips-v4` и `ips-v6`;
- на внешнем интерфейсе пропускает новые соединения на 80/443 только с этих адресов — в цепочках `DOCKER-USER` (порты, опубликованные Docker) и `INPUT`;
- обновляет `cloudflare.env` и перезапускает Caddy, если диапазоны изменились;
- идемпотентен, таймер повторяет его после загрузки и раз в сутки.

Запуск прокси:

```bash
cd /opt/lewdventure/proxy
docker network create lewdventure-edge
docker compose up -d
```

Caddy:
- отдаёт origin-сертификат Cloudflare, собственные сертификаты не выпускает;
- берёт реальный IP игрока из `CF-Connecting-IP` только если запрос пришёл с адреса Cloudflare, и передаёт его в api через `X-Forwarded-For` — rate limit сервера считает по игрокам;
- снаружи отвечает 404 на `/health*`, `/admin/*`, `/swagger*`, на prod ещё и на `/api/config/*`.

Проверка с другой машины: `curl -I https://api-dev.example.com/api/ping` — 200; `curl -I --resolve api-dev.example.com:443:<IP VPS> https://api-dev.example.com/api/ping` — таймаут (прямой заход закрыт).

## 6. GitHub

### Доступ деплоя

1. `ssh-keygen -t ed25519 -C github-deploy -f deploy_key`, публичную часть — в `~deploy/.ssh/authorized_keys`.
2. `ssh-keyscan -p 22 <IP VPS>` — значение `SSH_KNOWN_HOSTS`.
3. Образы в GHCR: проще сделать пакеты `lewdventure-server` и `lewdventure-config-tool` публичными (секретов в образах нет). Для приватных — PAT с `read:packages` в `GHCR_PULL_TOKEN`.

### Environments

Settings → Environments: `dev`, `stage`, `prod`. Пока VPS один, значения одинаковые во всех трёх — так перенос окружения на другой сервер сводится к смене секретов одного environment.

| Имя | Тип | Значение |
| --- | --- | --- |
| `SSH_HOST` | secret | IP или хост VPS |
| `SSH_USER` | secret | `deploy` |
| `SSH_PRIVATE_KEY` | secret | `deploy_key` |
| `SSH_KNOWN_HOSTS` | secret | вывод `ssh-keyscan` |
| `GHCR_PULL_TOKEN` | secret | только для приватных пакетов |
| `GHCR_PULL_USER` | variable | логин владельца токена |
| `SSH_PORT` | variable | если не 22 |
| `DEPLOY_PATH` | variable | если не `/opt/lewdventure/<env>` |

Секрет репозитория `DISCORD_DEPLOY_WEBHOOK_URL` — уведомления о деплоях (необязательно).

`prod`: Required reviewers, Deployment branches — только `master`. `stage`: только `master`.

Settings → Branches: защита `master` — merge через PR, обязательные проверки `build-test`, `integration-tests`, `docker-smoke`. Settings → Code security: secret scanning и push protection.

## 7. Первый деплой

1. Merge в `master` → `cd` деплоит dev. Конфиги dev создаются из Google Sheets автоматически.
2. `promote` с `target=stage` — то же для stage.
3. prod конфиги из Google не берёт:
   1. `promote` с `target=prod` — api не стартует без конфигов, деплой `failed`, Mongo поднят. Ожидаемо.
   2. `config-promote` с `version=active` — переносит активный снапшот stage.
   3. `promote` с `target=prod` повторно — `ok`.

## 8. Бэкапы Mongo

Обязательно до пользовательских данных, см. [mongo-conventions.md](../architecture/mongo-conventions.md#бэкапы). На одном VPS копии нужно выносить наружу (любое S3-совместимое хранилище, Cloudflare R2 Free — 10 GB).

## 9. Перенос окружения на отдельный VPS

Окружения ни от чего общего, кроме прокси, не зависят, поэтому, например, prod выносится так:

1. Новый VPS: разделы 1, 2 (только `prod`), 3 (перенести `.env` prod), 5 (в `sites/` — только `prod.caddy`).
2. Данные: `mongodump` на старом VPS (`lewdventure-prod`), `mongorestore` на новом после первого запуска Mongo, либо заново `config-promote`, если пользовательских данных ещё нет.
3. GitHub environment `prod`: новые `SSH_HOST`, `SSH_KNOWN_HOSTS`, при необходимости ключ.
4. `promote` с `target=prod` на новый VPS, проверка.
5. Cloudflare: A-запись `api` на новый IP (переключение без простоя, IP скрыт за Cloudflare).
6. Старый VPS: удалить `sites/prod.caddy`, `docker compose up -d` в `proxy`, остановить и удалить проект `lewdventure-prod` после проверки.

Изменений в коде, compose и workflows не требуется.
