# Базовые правила проекта

> Соглашения по C#/ASP.NET коду LewdventureServer. Редактируй при изменении стандартов.

## Именование

- **Файлы:** один тип на файл, имя = имя типа.
- **Namespaces:** `Server.<Area>` (`Server.Battles`, `Server.GameConfigs`, `Server.Api.Hosting`, `Server.Infrastructure.Mongo`).
- **Типы, методы, свойства:** `PascalCase`.
- **Локальные переменные и параметры:** `camelCase`, без сокращений (`exception`, не `ex`).
- **Private fields:** `_camelCase`.
- **Интерфейсы:** префикс `I`, у членов явный `public`.

## Структура

- `src/Lewdventure.Server.Contracts` — wire DTO боя.
- `src/Lewdventure.Server.GameConfig` — mappers, managers, `ConfigDistributor`, снапшоты конфигов.
- `src/Lewdventure.Server.Battle` — симуляция боя.
- `src/Lewdventure.Server.Infrastructure` — Mongo, Google Sheets, алерты.
- `src/Lewdventure.Server.Api` — хост, DI, эндпоинты, options, безопасность, health, метрики.
- Доменная область в `GameConfig`: `Configs/Mappers`, `Managers`.

## Dependency Rules

- `Battle` и `GameConfig` не ссылаются на ASP.NET, Mongo, Google API.
- Composition root — `Api/Composition`; регистраторы — экземплярные классы.
- Сервисы боя — scoped; singleton только без состояния боя.
- Конфиги в рантайме — только через `IConfigDistributor`.
- Runtime-код без LINQ: `for`, `foreach`, `Dictionary`, предвыделенные `List<T>`.
- Без `static` (кроме `Main`), без static-хелперов и кэшей.

## Конфигурация

- Любая настройка — typed options (`<Name>Options`, `SectionName`) с `ValidateDataAnnotations`, `ValidateOnStart` и `IValidateOptions<T>` для правил между полями и окружениями.
- База — `appsettings.json`, отличия — `appsettings.<Environment>.json`, секреты — только env.
- Новый ключ описывается в `docs/runbooks/configuration.md` и, если нужен на VPS, в `.env.example`.

## Error Handling

- DI-зависимости и целостные конфиги — not-null по контракту, без защитных проверок.
- Проверки — для внешних источников: HTTP input, Google API, Mongo, файловая система.
- Необработанные исключения — `UnhandledExceptionResponder` (тело `{"error": ...}`, алерт). Формат ошибок боя совместим с клиентом.
- Ловить конкретные исключения (`TimeoutException`, `MongoException`, `IOException`), не `Exception` целиком без фильтра.

## Control Flow

- Guard clauses и early return.
- `condition == false`, не `!condition`; не писать `== true`.
- Однострочные `if`/`for`/`foreach` без фигурных скобок; пустая строка перед `if`/`for`, если перед ними есть код.
- Методы с блочным телом, без expression-bodied.

## Logging

- `ILogger<T>` через DI, structured-шаблоны с `{Name}`.
- Теги в начале сообщения: `[Startup]`, `[Shutdown]`, `[Health]`, `[Alert]`, `[Mongo]`, `[Security]`, `[Config]`, `[Config][Snapshot]`, `[Error]`, `[Story][Battle]`. Новый тег — после согласования с владельцем.
- Секреты и ключи в логи не пишутся.

## Комментарии

- Никаких комментариев в коде и конфигах: C#, json, yaml, compose, Dockerfile, workflows, shell, Caddyfile, `.env.example`, Apps Script.
- Пояснения — в `docs/`.

## API и DTO

- Контракт боя — `Contracts`, JSON Newtonsoft camelCase; изменения синхронно с клиентом и `.ai-factory/specs/battle-simulation.md`.
- Коды ответов боя: 400 входные данные, 413 размер, 429 лимиты, 500 необработанное, 503 конфиги не загружены.

## Tests

- NUnit, naming `MethodName_Scenario_ExpectedBehavior`.
- `tests/Lewdventure.Server.GoldenTests` — эталоны боя, обновление только `LEWD_GOLDEN_UPDATE=1` отдельным изменением.
- `tests/Lewdventure.Server.UnitTests` — options, безопасность, хост, алерты, снапшоты; время через `TimeProvider`/`FakeTimeProvider`.
- `tests/Lewdventure.Server.IntegrationTests` — Mongo, только `LEWD_IT_ENABLED=1`, базы `lewdventure_it_*`.
- Перед коммитом: build, `dotnet test`, интеграционные тесты, `dotnet format whitespace --verify-no-changes`.
