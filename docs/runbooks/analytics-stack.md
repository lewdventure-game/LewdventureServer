[← Runbooks](README.md)

# Хранилище аналитики: ClickHouse

Compose-проект `lewdventure-analytics` в `/opt/lewdventure/analytics` (исходник — `deploy/analytics/`). Формат событий и API — [server/analytics.md](../server/analytics.md). Смотрят данные через раздел «Аналитика» веб-админки ([admin-panel.md](admin-panel.md)).

```text
api <env> ──сеть lewdventure-analytics──> clickhouse (lewdventure-clickhouse:8123)   запись
админка  ──сеть lewdventure-analytics──> clickhouse                                    чтение
```

| Сервис | Что | Лимиты |
| --- | --- | --- |
| `clickhouse` | 25.8, базы `analytics_dev/stage/prod`, системные логи выключены | 2 ГБ, 1.5 CPU |
| `clickhouse-schema` | одноразово применяет `schema.sql` (`IF NOT EXISTS`) при каждом `up` | — |

## Пользователи ClickHouse

`deploy/analytics/clickhouse/users.xml`, пароли — из `/opt/lewdventure/analytics/.env`:

| Пользователь | Права |
| --- | --- |
| `admin` | всё, только для обслуживания |
| `ingest_dev`, `ingest_stage`, `ingest_prod` | `INSERT` в `analytics_<env>.events` своего окружения |
| `panel` | `SELECT` во всех трёх базах, readonly, лимит памяти 1 ГБ и 60 секунд на запрос; им ходит админка |

Админка получает `AdminPanel__ClickHouse__Url/User/Password` и для каждого окружения `AdminPanel__Environments__N__AnalyticsDatabase` (см. `deploy/admin/.env.example`). Значения фильтров уходят в ClickHouse параметрами запроса (`{name:Type}` + `param_name`), в текст SQL не подставляются; имена баз проверяются по `[a-z0-9_]`.

## Развёртывание

1. `cp -r deploy/analytics/. /opt/lewdventure/analytics/`, `.env` по `.env.example`, пароли — `openssl rand -hex 24`.
2. `docker network create lewdventure-analytics` (если нет; `deploy.sh` тоже создаёт её).
3. `docker compose up -d` в `/opt/lewdventure/analytics`.
4. В `.env` окружения — ключи `Analytics__*` из [server/analytics.md](../server/analytics.md#настройки), пароль `CLICKHOUSE_INGEST_<ENV>_PASSWORD`; затем деплой окружения.
5. В `.env` админки — ClickHouse и `AnalyticsDatabase` окружений, пароль `CLICKHOUSE_PANEL_PASSWORD`; админка подключена к сети `lewdventure-analytics` в `deploy/admin/compose.yaml`.

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

1. Новый сервер: этот compose-проект; админку можно перенести туда же (её compose и сайт Caddy).
2. Данные: `BACKUP`/`RESTORE` через S3 или `clickhouse-client --query "SELECT * FROM … FORMAT Native"`; для демо можно начать с пустой базы.
3. В `.env` окружений — новый `Analytics__ClickHouseUrl`, в `.env` админки — `AdminPanel__ClickHouse__Url` (приватная сеть, WireGuard или туннель; порт 8123 наружу не открывать).
4. Клиент не меняется: события по-прежнему идут на игровой сервер.
