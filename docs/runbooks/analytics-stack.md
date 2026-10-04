[← Runbooks](README.md)

# Стек аналитики: ClickHouse и Grafana

Compose-проект `lewdventure-analytics` в `/opt/lewdventure/analytics` (исходник — `deploy/analytics/`). Формат событий и API — [server/analytics.md](../server/analytics.md).

```text
api <env> ──сеть lewdventure-analytics──> clickhouse (lewdventure-clickhouse:8123)
grafana ──lewdventure-analytics──> clickhouse
браузер → Caddy (admin.caddy, /grafana*) → forward_auth в админку → grafana (lewdventure-grafana:3000)
```

| Сервис | Что | Лимиты |
| --- | --- | --- |
| `clickhouse` | 25.8, базы `analytics_dev/stage/prod`, системные логи выключены | 2 ГБ, 1.5 CPU |
| `clickhouse-schema` | одноразово применяет `schema.sql` (`IF NOT EXISTS`) при каждом `up` | — |
| `grafana` | 12.1, плагин ClickHouse, провижининг источника и дашборда, вход только через админку | 512 МБ, 1 CPU |

## Пользователи ClickHouse

`deploy/analytics/clickhouse/users.xml`, пароли — из `.env`:

| Пользователь | Права |
| --- | --- |
| `admin` | всё, только для обслуживания |
| `ingest_dev`, `ingest_stage`, `ingest_prod` | `INSERT` в `analytics_<env>.events` своего окружения |
| `grafana` | `SELECT` во всех трёх базах, readonly, лимит памяти 1 ГБ и 60 секунд на запрос |

## Вход в Grafana

Caddy на `/grafana*` спрашивает `GET /auth/grafana` у админки (`forward_auth`): нет сессии — редирект на вход админки, есть — заголовки `X-WEBAUTH-USER` (логин) и `X-WEBAUTH-ROLE` (`Admin` для admin, `Viewer` для tester). Grafana работает в режиме auth proxy, своей формы входа нет. Клиентские `X-WEBAUTH-*` Caddy вырезает. Grafana подключена только к сетям `lewdventure-analytics` (ClickHouse) и `lewdventure-grafana`, где кроме неё есть только Caddy, поэтому подставить заголовки входа другой контейнер не может.

## Развёртывание

1. `cp -r deploy/analytics/. /opt/lewdventure/analytics/`, `.env` по `.env.example`, пароли — `openssl rand -hex 24`.
2. `docker network create lewdventure-analytics` (если нет; `deploy.sh` тоже создаёт её) и `docker network create lewdventure-grafana`; Caddy подключается к `lewdventure-grafana` через `deploy/proxy/compose.yaml`, после правки — `docker compose up -d` в `/opt/lewdventure/proxy`.
3. `docker compose up -d` в `/opt/lewdventure/analytics`.
4. В `.env` окружения — ключи `Analytics__*` из [server/analytics.md](../server/analytics.md#настройки), пароль `CLICKHOUSE_INGEST_<ENV>_PASSWORD`; затем деплой окружения.
5. Сайт `admin.caddy` уже содержит `/grafana*`; после правки — `docker compose exec caddy caddy reload --config /etc/caddy/Caddyfile` в `/opt/lewdventure/proxy`.

## Обслуживание

```bash
cd /opt/lewdventure/analytics
docker compose exec clickhouse bash -c 'clickhouse-client --user admin --password "$CLICKHOUSE_ADMIN_PASSWORD"'
docker compose exec clickhouse bash -c 'clickhouse-client --user admin --password "$CLICKHOUSE_ADMIN_PASSWORD" --query "SELECT database, formatReadableSize(sum(bytes_on_disk)) FROM system.parts WHERE active GROUP BY database"'
```

- Новые колонки и таблицы — в `schema.sql` через `ALTER TABLE … ADD COLUMN IF NOT EXISTS`, затем `docker compose up -d` (перезапустит `clickhouse-schema`).
- Удаление событий игрока: `ALTER TABLE analytics_<env>.events DELETE WHERE user_id = '<id>'`.
- Бэкапы — перед продом: `BACKUP DATABASE analytics_prod TO S3(...)` в R2.

## Перенос на отдельный VPS

1. Новый сервер: этот compose-проект, плюс прокси с сайтом админки, если админка переезжает вместе с ним.
2. Данные: `BACKUP`/`RESTORE` через S3 или `clickhouse-client --query "SELECT * FROM … FORMAT Native"`; для демо можно начать с пустой базы.
3. В `.env` окружений — новый `Analytics__ClickHouseUrl` (приватная сеть, WireGuard или туннель; порт 8123 наружу не открывать).
4. Клиент не меняется: события по-прежнему идут на игровой сервер.
