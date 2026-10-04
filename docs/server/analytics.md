[← Сервер](README.md)

# Аналитика

Своя площадка: клиент шлёт события на игровой сервер, сервер дописывает к ним контекст игрока и пачками пишет в ClickHouse; смотрим в Grafana (`https://admin.lewdventure.online/grafana/`, вход через админку). Формат событий повторяет Isekai (Amplitude HTTP v2): `event_type`, `time`, `event_properties`, `user_properties`, `session_id`, `insert_id`.

## Приём: `POST /api/analytics/events`

Bearer-токен игрока, лимит игрока по частоте, тело до 512 КБ, до 500 событий за запрос.

```json
{
  "events": [
    {
      "event_type": "battle_complete",
      "time": 1759570000123,
      "session_id": 1759569000000,
      "insert_id": "4f1c…-1",
      "platform": "android",
      "app_version": "0.4.1",
      "device_id": "3f0c…",
      "event_properties": { "stage_id": 4, "location": "forest", "win": true },
      "user_properties": { "soft_money": 500, "player_level": 7 }
    }
  ]
}
```

| Поле | Правило |
| --- | --- |
| `event_type` | обязательно, `[a-z][a-z0-9_.]{0,63}` |
| `time` | unix ms события на клиенте; пусто, старше 30 дней или дальше часа в будущем — берётся время сервера |
| `session_id` | unix ms старта сессии клиента |
| `insert_id` | уникален для события, по нему ClickHouse схлопывает повторы при ретраях; пусто — сервер сгенерирует |
| `event_properties`, `user_properties` | JSON-объекты до 16 КБ; значения — нормальные JSON-типы (числа числами, bool — `true/false`), а не строки, как было в Isekai |
| `platform`, `app_version`, `device_id` | до 64 символов |

`user_id` клиент не передаёт: сервер берёт его из токена.

Ответ `202`: `{accepted, rejected, dropped, stored, errors}`. `rejected` — не прошли проверку (первые 10 причин в `errors`), повторять их не нужно. `dropped` — очередь сервера переполнена, такие события можно отправить снова. `stored=false` — аналитика на окружении выключена, события не сохраняются.

Что и как делать клиенту — [client-analytics.md](client-analytics.md). Коротко: копить события в очереди с сохранением на диск, слать пачкой раз в 15–30 секунд и при сворачивании, при 429/5xx/сети повторять с backoff, при 400 не повторять.

## Что дописывает сервер

| Колонка | Откуда |
| --- | --- |
| `user_id` | токен |
| `country` | `CF-IPCountry` запроса; для серверных событий — последняя страна игрока |
| `experiment_id`, `group_id` | активная группа эксперимента игрока на момент записи, пусто — мастер |
| `config_version` | версия конфигов, на которой играет игрок (забег, группа или мастер) |
| `server_time` | приём сервером |
| `source` | `client` или `server` |

## Серверные события

| `event_type` | Когда | `event_properties` |
| --- | --- | --- |
| `account_created` | создан аккаунт устройства | `client_version` |
| `experiment_assigned` | игрок попал в группу | `experiment_id`, `group_id`, `new_player` |
| `run_started` | старт забега | `run_id`, `story_level_id`, `stages` |
| `battle_finished` | бой внутри забега | `run_id`, `story_level_id`, `stage_index`, `stage_id`, `event_id`, `outcome`, `steps`, `health_before` |
| `run_finished` | забег закончен | `run_id`, `story_level_id`, `status` (`completed`, `failed`, `abandoned`), `stage_index`, `stages`, `experience_level`, `perks`, `duration_seconds` |

## Хранение

Таблица `analytics_<env>.events` (`deploy/analytics/clickhouse/schema.sql`): `ReplacingMergeTree` по `insert_id`, партиции по месяцам, хранение 400 дней. Свойства — JSON-строки, в запросах `JSONExtractInt(event_properties, 'stage_id')` и т. п. Для точного счёта без повторов — `FROM events FINAL`.

Путь записи: эндпоинт → ограниченная очередь в памяти (`Analytics:QueueCapacity`) → фоновый писатель раз в `FlushIntervalSeconds` или по `BatchSize` → ClickHouse HTTP `JSONEachRow`. ClickHouse недоступен — пачка ложится в `Analytics:SpoolPath` и дописывается позже; переполнение буфера и очереди — warning `[Analytics]` и алерт в Discord.

## Настройки

| Ключ | Пример |
| --- | --- |
| `Analytics__Enabled` | `true` |
| `Analytics__ClickHouseUrl` | `http://lewdventure-clickhouse:8123` |
| `Analytics__Database` | `analytics_dev` |
| `Analytics__User`, `Analytics__Password` | `ingest_dev` и его пароль из `/opt/lewdventure/analytics/.env` |
| `Analytics__SpoolPath` | `/app/cache/analytics-spool` |

Без Mongo или с `Enabled=false` эндпоинт принимает события и отвечает `stored=false`.

## Дашборд

Grafana, папка Lewdventure, «Lewdventure: обзор», переменная «Окружение»: DAU, новые аккаунты по странам, события по типам, итоги забегов, сравнение групп экспериментов (игроки, события на игрока, забеги, средний достигнутый этап), удержание D1/D3/D7 по группам, последние события. Дашборд можно править в UI, исходник — `deploy/analytics/grafana/dashboards/overview.json`.
