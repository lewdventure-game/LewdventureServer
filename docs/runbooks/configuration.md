[← Runbooks](README.md)

# Ключи конфигурации

Все настройки сервера. Порядок приоритета (последний побеждает): `appsettings.json` → `appsettings.<Environment>.json` → переменные окружения. В env разделитель секций — двойное подчёркивание: `GameConfig:Source` → `GameConfig__Source`, элемент списка — индекс: `ReverseProxy__KnownNetworks__0`.

В файлах конфигурации и `.env.example` комментариев нет, описание каждого ключа — здесь.

## Окружение

| Переменная | Значения | Описание |
| --- | --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Local`, `Development`, `Staging`, `Production`, `Testing` | профиль API; в Dockerfile по умолчанию `Production` |
| `DOTNET_ENVIRONMENT` | те же | профиль ConfigTool |

## Server

| Ключ | По умолчанию | Описание |
| --- | --- | --- |
| `Server:PublicPort` | `5000` | публичный порт: бой, `/api/config/*`, Swagger |
| `Server:OpsPort` | `9090` | ops-порт: health, admin; должен отличаться от публичного |
| `Server:BindAddress` | `Any` | `Loopback` (только 127.0.0.1, Local) или `Any` (контейнеры) |
| `Server:EnableSwagger` | `false` | Swagger UI на публичном порту; запрещён в Production |
| `Server:ShutdownTimeoutSeconds` | `30` | сколько ждать завершения запросов и фоновых сервисов при остановке; `stop_grace_period` в compose должен быть больше |

## GameConfig

| Ключ | По умолчанию | Описание |
| --- | --- | --- |
| `GameConfig:Source` | `File` | `File` или `Mongo`; `Mongo` требует `Mongo:Enabled` |
| `GameConfig:FilePath` | фикстура репозитория | путь к файлу снапшота при `Source=File`, относительный — от content root |
| `GameConfig:FailStartupIfUnavailable` | `false` | упасть на старте, если конфиги не загрузились; иначе старт с 503 на бою |
| `GameConfig:PinnedVersion` | пусто | загружать эту версию вместо активной (`sha256:<hex>`), для аварийного закрепления |
| `GameConfig:LocalCachePath` | пусто | файл последнего загруженного снапшота; используется, если Mongo недоступен при старте |
| `GameConfig:BootstrapFilePath` | пусто | снимок в файле; публикуется и активируется, если в Mongo нет активной версии или она не собирается. Локально — фикстура из репозитория |
| `GameConfig:ReloadMode` | `Manual` | `Manual` (только `/admin/config/reload`) или `Poll` (опрос активной версии в Mongo) |
| `GameConfig:PollIntervalSeconds` | `30` | период опроса при `Poll`, 5–3600 |

## ConfigSheets

Каталог таблиц: по нему `GET /api/config/sheets` говорит Apps Script, какие листы собирать. Сервер сам в Google не ходит и ключа не требует.

| Ключ | По умолчанию | Описание |
| --- | --- | --- |
| `ConfigSheets:Sheets` | список в `appsettings.json` | `Domain` и `SpreadsheetId` на каждый из 16 обязательных доменов; менять только по решению владельца |

## Auth

Аккаунты игроков и профиль, подробности в [player-state.md](../server/player-state.md).

| Ключ | По умолчанию | Описание |
| --- | --- | --- |
| `Auth:Enabled` | `false` | включает `/api/auth/*` и `/api/player/*`; требует `Mongo:Enabled` |
| `Auth:SigningKey` | пусто | ключ подписи JWT, минимум 32 символа, только через env или `secrets/` |
| `Auth:Issuer` | `lewdventure-server` | issuer токена |
| `Auth:Audience` | `lewdventure-client` | audience токена |
| `Auth:AccessTokenMinutes` | `60` | срок access-токена, 5–1440 |
| `Auth:RefreshTokenDays` | `90` | срок refresh-токена, 1–365 |

На dev авторизация включена жёстко в `deploy/compose/compose.dev.yaml` (`Auth__Enabled: "true"`), поэтому ключ подписи обязателен: без него валидация опций не проходит и API не стартует. Порядок включения в окружении:

```bash
openssl rand -base64 48
```

Значение положить в `/opt/lewdventure/<env>/compose/.env` как `Auth__SigningKey=...` **до** деплоя, затем задеплоить. Ключ не коммитить; ротация — по [secrets-rotation.md](secrets-rotation.md): меняется ключ, все выданные access-токены становятся недействительными, клиенты перелогиниваются по refresh-токену.

Следствия включения:

- `/api/auth/*`, `/api/player/*` и `/api/run/*` начинают работать и требуют `Authorization: Bearer`;
- `/api/battle/simulate` и `/api/battle/replay` уезжают на ops-порт и требуют админ-ключ: играть через них нельзя, это инструмент разработки.

### Стартовый набор нового аккаунта

Профиль создаётся пустым, поэтому забег без стартового контента не начнётся (`Loadout has no character.`). Что выдать новому игроку, задаёт константа в листе `Constants`:

| Константа | Пример значения | Что делает |
| --- | --- | --- |
| `start_content` | `character:1:1,resource:soft_money:500` | выдаётся один раз при создании профиля |

Синтаксис — как у наград боя: `тип:id:количество`, элементы через запятую. Количество обязательно: `character:1` без него парсер отбросит, и набор окажется пустым.

Диагностика в логах: `[Player] start content constant is missing` — константы нет; `[Player] start content granted nothing` — константа есть, но из неё не разобралась ни одна награда (почти всегда забытое количество); `[Player] start content granted` — выдано.

## Mongo

| Ключ | По умолчанию | Описание |
| --- | --- | --- |
| `Mongo:Enabled` | `false` | включить Mongo (хранилище снапшотов, health check) |
| `Mongo:ConnectionString` | пусто | `mongodb://user:password@mongo:27017/?replicaSet=rs0&authSource=<db>`; на VPS собирается в `compose.vps.yaml` из `.env` |
| `Mongo:DatabaseName` | пусто | `lewdventure_<env>`: строчные буквы, цифры и `_` |
| `Mongo:ApplicationName` | `lewdventure-server` | имя клиента в логах Mongo |
| `Mongo:ServerSelectionTimeoutSeconds` | `5` | таймаут выбора сервера, 1–120 |
| `Mongo:RequireReplicaSet` | `true` | запретить standalone (нужны транзакции) |
| `Mongo:ApplyIndexesOnStartup` | `true` | создавать индексы при старте |

## Admin

| Ключ | По умолчанию | Описание |
| --- | --- | --- |
| `Admin:Enabled` | `false` | включить `/admin/config/*` на ops-порту (нужен и `Mongo:Enabled`) |
| `Admin:ApiKey` | пусто | ключ, не короче 32 символов (8 в Local) |
| `Admin:HeaderName` | `X-Admin-Key` | заголовок ключа |

## ConfigPublisher

| Ключ | По умолчанию | Описание |
| --- | --- | --- |
| `ConfigPublisher:Enabled` | `false` | включить `/api/config/upload`, `/api/config/sheets`, `/api/config/status`; запрещён в Production |
| `ConfigPublisher:ApiKey` | пусто | ключ для Apps Script, не короче 24 символов (8 в Local) |
| `ConfigPublisher:HeaderName` | `X-Config-Key` | заголовок ключа |
| `ConfigPublisher:LegacyHeaderName` | `X-Config-Secret` | устаревший заголовок, принимается с warning |

## RateLimit

| Ключ | По умолчанию | Описание |
| --- | --- | --- |
| `RateLimit:Enabled` | `true` | включить лимиты; выключен в Local и Testing |
| `RateLimit:Battle:PermitLimit` | `30` | запросов боя на IP за окно |
| `RateLimit:Battle:WindowSeconds` | `10` | окно боя |
| `RateLimit:Battle:QueueLimit` | `0` | очередь сверх лимита |
| `RateLimit:BattleConcurrencyLimit` | `0` | одновременных боёв на процесс, `0` — без ограничения |
| `RateLimit:Config:PermitLimit` / `WindowSeconds` / `QueueLimit` | `6` / `60` / `0` | лимит публикации конфигов |
| `RateLimit:Admin:PermitLimit` / `WindowSeconds` / `QueueLimit` | `30` / `60` / `0` | лимит admin-эндпоинтов |

## RequestLimits

| Ключ | По умолчанию | Описание |
| --- | --- | --- |
| `RequestLimits:MaxRequestBodyBytes` | `1048576` | лимит тела Kestrel для всех запросов |
| `RequestLimits:BattleMaxRequestBodyBytes` | `262144` | лимит тела боя, сверх — 413 |

## ReverseProxy

| Ключ | По умолчанию | Описание |
| --- | --- | --- |
| `ReverseProxy:Enabled` | `false` | доверять `X-Forwarded-For` / `X-Forwarded-Proto` |
| `ReverseProxy:KnownNetworks` | пусто | CIDR сетей прокси, на VPS `172.16.0.0/12` (сети Docker) |
| `ReverseProxy:KnownProxies` | пусто | отдельные IP прокси |

## Alerts

| Ключ | По умолчанию | Описание |
| --- | --- | --- |
| `Alerts:Enabled` | `false` | отправлять алерты; в dev/stage/prod включено, требует webhook |
| `Alerts:DiscordWebhookUrl` | пусто | https-адрес webhook канала окружения, секрет |
| `Alerts:EnvironmentLabel` | пусто | подпись окружения в сообщении (`dev`, `stage`, `prod`) |
| `Alerts:CooldownSeconds` | `300` | не повторять алерт с тем же ключом чаще |
| `Alerts:HealthCheckPeriodSeconds` | `30` | период фоновой проверки health для алертов |
| `Alerts:NotifyOnStartup` | `false` | сообщение о старте сервера |
| `Alerts:HttpTimeoutSeconds` | `5` | таймаут запроса в Discord |
| `Alerts:QueueCapacity` | `256` | размер очереди, при переполнении теряются самые старые |

## Logging

Стандартная секция `Logging`. Вне Local — JSON-консоль в UTC. Полезные переключатели:

| Ключ | Описание |
| --- | --- |
| `Logging:LogLevel:Server.Battles` | `Information` включает подробный лог каждого хода, по умолчанию `Warning` |
| `Logging:LogLevel:Microsoft.AspNetCore.HttpLogging.HttpLoggingMiddleware` | `Warning` отключает строку на каждый запрос |
| `Logging:Console:FormatterName` | `json` или `simple` |

## Переменные compose на VPS

Файл `/opt/lewdventure/<env>/compose/.env` (шаблон — `.env.example` в корне репозитория):

| Переменная | Описание |
| --- | --- |
| `DEPLOY_ENVIRONMENT` | `dev`, `stage` или `prod`; даёт алиас `lewdventure-<env>-api` в сети Caddy |
| `IMAGE_REGISTRY` | реестр образов, по умолчанию `ghcr.io/abromus` |
| `IMAGE_TAG` | не задавать: выставляет `deploy.sh` |
| `API_HOST_PORT` | порт API на `127.0.0.1` хоста: dev `5001`, stage `5002`, prod `5000`; на одном VPS должны различаться |
| `OPS_HOST_PORT` | ops-порт на `127.0.0.1` хоста: dev `9091`, stage `9092`, prod `9090`; на одном VPS должны различаться |
| `MONGO_DATABASE` | `lewdventure_<env>` |
| `MONGO_ROOT_USERNAME`, `MONGO_ROOT_PASSWORD` | root Mongo, используется только `mongo-init` и бэкапами |
| `MONGO_APP_USERNAME`, `MONGO_APP_PASSWORD` | пользователь приложения с `readWrite` на свою базу |
| ключи приложения | `Admin__ApiKey`, `ConfigPublisher__ApiKey`, `Alerts__DiscordWebhookUrl` и любые из таблиц выше |

## Переменные Caddy

Файл `/opt/lewdventure/proxy/.env`:

| Переменная | Описание |
| --- | --- |
| `DEV_DOMAIN`, `STAGE_DOMAIN`, `PROD_DOMAIN` | домены окружений; нужны только те, чьи файлы лежат в `proxy/sites/` |

Файл `/opt/lewdventure/proxy/cloudflare.env` не редактируется вручную: `CLOUDFLARE_IPS` пишет `cloudflare-firewall.sh`.

Сертификат origin Cloudflare: `proxy/certs/origin.pem` и `proxy/certs/origin-key.pem`.

## Переменные локального compose и тестов

| Переменная | Описание |
| --- | --- |
| `LEWD_IT_ENABLED=1` | включить интеграционные тесты Mongo |
| `LEWD_IT_MONGO` | внешний Mongo для интеграционных тестов вместо Testcontainers (только локальный хост) |
| `LEWD_GOLDEN_UPDATE=1` | перезаписать эталоны golden-тестов |
