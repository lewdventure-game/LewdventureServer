# Аналитика и A/B-эксперименты на снапшотах конфигов

Branch: feature/config-versions-and-experiments
Created: 2026-10-04
Status: фазы 1, 2 и 5 (веб-админка) готовы и выкачены на dev; дальше фаза 3 — приём аналитики в формате Isekai

## Settings

- Testing: yes — unit на выбор версии, назначение групп и валидацию процентов; integration на Mongo и ClickHouse (Testcontainers); golden боя без изменений
- Logging: новые теги `[Experiment]`, `[Analytics]` — согласовать с владельцем до первой задачи фаз 2 и 3; фаза 1 обходится существующими `[Config][Snapshot]` и `[Run]`
- Docs: yes — `docs/server/config-sync.md`, `docs/server/runs.md`, новые `docs/server/experiments.md`, `docs/server/analytics.md`, runbook `analytics-stack.md`
- Roadmap: фазы 0–6, каждая отдельным PR

## Решения владельца

- Аналитика — своя площадка сразу, на текущем VPS (владелец расширяет его до 8 ГБ RAM), отдельным compose-проектом; перед продом переезд на свой VPS должен проходить без изменений кода и клиента.
- A/B-назначение живёт в игровом сервере; на отдельный сервер при переезде уходят ClickHouse, Grafana и веб-админка.
- Бэкапы, R2 и прочая подготовка prod — перед продом, сейчас идёт демо.
- Лог-теги `[Experiment]` и `[Analytics]` согласованы.
- Админка A/B — сначала admin API на ops-порту, потом веб-страница за Cloudflare Access.
- Порядок фаз — на усмотрение исполнителя: начинаем с фазы 1.

## Термины

| Термин | В коде |
| --- | --- |
| Снапшот | `GameConfigSnapshot`, версия `sha256:<hex>`, коллекция `config_snapshots` |
| Мастер-снапшот | активная версия `config_state.active`, то, что сейчас `IGameConfigSetProvider.Current` |
| Эксперимент | набор групп, у каждой свой снапшот; одновременно может идти несколько экспериментов |
| Группа | снапшот + процент + фильтры + статус |
| Назначение | запись в `users`: какой эксперимент и группа, когда назначено; не меняется при перезаходе |

## Что есть сейчас и чего не хватает

- Сервер держит одну версию конфигов: `GameConfigSetProvider.Current`, scoped `IConfigDistributor` всегда резолвится в неё.
- Забег пишет `ConfigVersion = Current.Version` при старте, но бои и награды внутри забега считаются по `Current`. Активация новых конфигов посреди забега меняет правила на ходу, хотя `docs/server/runs.md` обещает обратное.
- Гео не собирается: Caddy не прокидывает `CF-IPCountry`, в `users` нет страны.
- Аналитики нет: метрики только в `Meter`, логи — json-file docker с ротацией 50 МБ.

## Фаза 0. Инфраструктура

- Отложено до подготовки prod: бэкап prod (`lewdventure-backup@prod.timer`), выгрузка в R2 (`LEWD_BACKUP_UPLOAD_COMMAND`), проверочный restore.
- Сделано в фазе 2: Caddy пробрасывает входящие заголовки как есть, сервер читает `CF-IPCountry` напрямую (`ClientCountryReader`); прямой заход на origin закрыт firewall, подделать заголовок можно только локально. В Cloudflare должна быть включена IP Geolocation (по умолчанию включена).
- `users.country` (ISO-3166 alpha-2, `XX` если неизвестно) пишется при создании аккаунта и обновляется на `auth/device`/`refresh` в `users.lastCountry`. Для фильтров эксперимента используется страна на момент назначения.

## Фаза 1. Несколько версий конфигов одновременно

Цель: каждый запрос игрока работает на своей версии конфигов, забег — на закреплённой. Для игроков без экспериментов поведение не меняется.

- `IGameConfigSetProvider` остаётся источником мастер-версии (`Current`, `Swap`).
- `GameConfigSetCache` (singleton, Infrastructure — GameConfig собирается и под netstandard2.1 для Unity, серверный кэш туда не кладём): собранные `GameConfigSet` по версии, мастер не вытесняется, остальные — LRU, лимит 8 (константа `GameConfigSetCache.Capacity`; в опции вынести, если понадобится). Сборка одной версии идёт один раз даже при параллельных запросах.
- Недостающую версию кэш читает напрямую из `ConfigSnapshotRepository`; при `Source=File` и без Mongo кэша и выбора нет — только мастер.
- `GameConfigSelection` (scoped, Infrastructure): выбранный на запрос `GameConfigSet`. Регистрация `IConfigDistributor` читает его, а если выбора нет — мастер. Так весь scoped-граф боя и забега получает нужную версию без правок сервисов.
- `GameConfigSelectionMiddleware` после `UseAuthorization`, только для эндпоинтов с `GameConfigRequiredMetadata` и опознанным игроком. Порядок выбора версии:
  1. у игрока есть текущий забег — `run.ConfigVersion`;
  2. (фаза 2) игрок в группе эксперимента — снапшот группы;
  3. иначе мастер.
- Если версия забега не загружается (снапшот удалён или не собирается) — мастер, warning `[Config][Snapshot]` и алерт: лучше сменить правила, чем запереть игрока в забеге.
- `RunService.StartAsync` пишет версию из `GameConfigSelection`, а не из `Current`.
- `GET /api/config/bundle` отдаёт бандл выбранной версии; `ETag` уже версионный, кэш бандлов — по версии.
- Версии в кэше в `/admin/config/status` — перенесено в фазу 2.
- Тесты: выбор версии для игрока в забеге и без; активация новой версии посреди забега не меняет бой забега (сравнение скрипта с эталоном на старой версии); LRU и однократная сборка; падение загрузки → мастер.

## Фаза 2. Эксперименты

### Модель

`experiments`:

| Поле | Смысл |
| --- | --- |
| `_id` | `exp_<slug>` |
| `name`, `description` | для людей |
| `status` | `Draft`, `Running`, `Finished` |
| `groups[]` | `groupId`, `name`, `snapshotVersion`, `percent` (0–100, шаг 0.01), `filter`, `status` (`Recruiting`, `Frozen`, `Removed`) |
| `filter` | `newPlayersOnly`, `countries[]` (пусто = все), задел: `minProgress` (не реализуем) |
| `createdAt`, `startedAt`, `finishedAt`, `updatedBy` | аудит |

`experiment_changes` — журнал изменений (кто, когда, что), как `config_activations`.

`users.experiment`: `experimentId`, `groupId`, `assignedAt`, `country`. Плюс `users.experimentsSeen[]` — эксперименты, для которых игрок уже прошёл розыгрыш и не попал: без этого старые игроки разыгрывались бы на каждом входе.

### Назначение

- Когда: при создании аккаунта (`auth/device`) и при `auth/refresh`, если игрок ещё не в группе и есть `Recruiting` группы без `newPlayersOnly`, которые он не видел.
- Кандидаты: группы `Recruiting` в `Running` экспериментах, чей фильтр подходит игроку; группы из `experimentsSeen` пропускаются.
- Розыгрыш: одно случайное число `0 ≤ r < 100`, группы-кандидаты в стабильном порядке (`startedAt`, `groupId`) занимают подряд отрезки длиной `percent`; не попал ни в один — мастер. Это даёт сценарий 2: игрок из США видит 10 + 10 + 25 + 25 + 25, остаток — мастер.
- Результат пишется один раз условным апдейтом (`experiment == null`), гонки двух устройств не дают двойного назначения.
- Один игрок — одна группа за раз.

### Инварианты

- Для любой страны сумма `percent` групп `Recruiting` ≤ 100: проверка при каждом изменении, иначе 409 с перечнем стран и суммой.
- Снапшот группы должен существовать и собираться; совместимость с мастером по id (персонажи, саммоны, снаряжение, ресурсы, story levels) проверяется через `ConfigSnapshotDiff`: удалённые id — ошибка, изменённые значения — норма.
- `Removed` группа или `Finished` эксперимент: игроки при следующем запросе получают мастер, назначение сохраняется для аналитики с отметкой `endedAt`.
- `Frozen`: новых не набираем, участники остаются на снапшоте.

### Операции (admin API, ops-порт, `X-Admin-Key`)

| Endpoint | Действие |
| --- | --- |
| `GET /admin/experiments` | список с числом участников по группам |
| `POST /admin/experiments` | создать `Draft` |
| `POST /admin/experiments/{id}/start` | `Running`, проверки инвариантов |
| `POST /admin/experiments/{id}/groups/{groupId}/freeze` | остановить набор |
| `POST /admin/experiments/{id}/groups/{groupId}/remove` | вывести группу на мастер |
| `POST /admin/experiments/{id}/finish` | закончить, все на мастер |
| `POST /admin/experiments/{id}/rollout` | `{groupId}`: активировать снапшот группы как мастер и закончить эксперимент |
| `GET /admin/player/{userId}` | дополняется назначением |

Команды ConfigTool для экспериментов не делаем: управление через admin API, затем веб-админка (фаза 5).

### Сценарии-тесты

- Сценарий 1: мастер 70%, контроль 15% на том же снапшоте, тест 15%; старые игроки — мастер; перезаход не меняет группу.
- Сценарий 2: три группы по 25% для новых из США + параллельный эксперимент 10% + 10% по миру; `remove` третьей группы; `freeze` первого эксперимента; распределение на 100 000 розыгрышей в пределах допуска.

## Фаза 3. Аналитика: приём и хранение

### Стек

Отдельный compose-проект `lewdventure-analytics` на том же VPS:

- ClickHouse (один узел, лимит памяти 2 ГБ, настройки для малого железа), наружу не публикуется; базы `analytics_dev`, `analytics_stage`, `analytics_prod`.
- Grafana с плагином ClickHouse — дашборды (воронки, retention, сравнение групп эксперимента), доступ через Caddy за Cloudflare Access (`analytics.<домен>`). Её же потом используем для метрик и логов сервера.
- Бэкап ClickHouse (`BACKUP … TO S3`) в R2.
- Сеть `lewdventure-analytics` подключается к api каждого окружения. Переезд на отдельный VPS — смена `Analytics:ClickHouseUrl` и приватный канал (WireGuard или Cloudflare Tunnel).

### Приём

- `POST /api/analytics/events` (Bearer, rate limit игрока, gzip, до 256 КБ): пачка событий клиента.
- Сервер дополняет каждое событие: `userId`, `experimentId`, `groupId`, `configVersion`, `country`, `clientVersion`, `serverTs`, `environment`.
- Сервер сам пишет события, которые знает достоверно: регистрация, старт и итог забега, бой, награды, прокачка, назначение в группу.
- Ограниченный `Channel` → фоновый писатель пачками (раз в N секунд или по M событий) → ClickHouse HTTP `JSONEachRow`. ClickHouse недоступен — пачка ложится в файловый буфер на томе и дописывается позже; переполнение буфера — алерт, старые пачки отбрасываются.
- Дедупликация по `eventId` (UUID от клиента): `ReplacingMergeTree`.

### Формат

Ждём от владельца формат событий виллы/исекая (пример JSON события с подсобытиями). Рабочая схема до этого:

```text
events
  event_id UUID, parent_id UUID, root_id UUID, depth UInt8
  name LowCardinality(String), seq UInt32, session_id String
  user_id String, client_ts DateTime64(3), server_ts DateTime64(3)
  client_version, platform, country LowCardinality(String)
  experiment_id, group_id, config_version LowCardinality(String)
  params JSON
PARTITION BY toYYYYMM(server_ts)
ORDER BY (name, server_ts, user_id)
TTL server_ts + INTERVAL 400 DAY
```

Подсобытия разворачиваются в строки с `parent_id`/`root_id`: так их можно считать отдельно и собирать дерево обратно.

### Приватность

- Без персональных данных: `userId` псевдонимный, IP не храним, только страну.
- `DELETE /api/player/account` и `/admin/player/{userId}` удаляют и события игрока (lightweight delete).

## Фаза 4. Клиент

- `IAnalyticsService`: очередь событий, сессия, `seq`, пачки по таймеру и при сворачивании, буфер на диске при отсутствии сети, повтор с backoff.
- Триггеры событий — по формату из фазы 3.
- Бандл конфигов: проверить, что клиент берёт его под токеном игрока и пересобирает ядро при смене версии (`ISharedCore.ConfigVersion` ≠ `run.configVersion`).

## Фаза 5. Админка

Статическая страница (без runtime-фреймворков на сервере) поверх admin API фаз 2 и 3: эксперименты, группы, проценты по странам, число участников, кнопки freeze/remove/finish/rollout; доступ — Cloudflare Access на отдельном поддомене, Caddy проксирует на ops-порт только этот сайт.

## Фаза 6. Наблюдаемость (по желанию, на том же стеке)

Метрики сервера (`Meter`) через OpenTelemetry в ClickHouse или Prometheus, логи в Loki/ClickHouse, дашборды в той же Grafana.

## Риски

- «Новый игрок» = новый `deviceId`: переустановка даёт нового игрока и повторный розыгрыш. Лечится привязкой платформы, не в этом плане.
- Экспериментальный снапшот не получает фиксы мастера: правка бага в мастере требует опубликовать и снапшоты групп.
- Ресурсы VPS: ClickHouse и Grafana отнимают 2–3 ГБ RAM; при 8 ГБ это укладывается рядом с тремя окружениями, при 4 ГБ — нет.
