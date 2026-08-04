# Базовые правила проекта

> Автоматически определённые соглашения по текущему C#/ASP.NET коду. Редактируй при изменении стандартов.

## Именование

- **Файлы:** один основной тип на файл, имя = имя типа.
- **Namespaces:** `Server.<Area>` — Battles, Configs, Services, Entities и т.д.
- **Типы, методы, свойства:** `PascalCase`.
- **Локальные переменные и параметры:** `camelCase`.
- **Private fields:** `_camelCase`.
- **Интерфейсы:** префикс `I`.

## Структура модулей

- `Assets/Core` — конфиги, constants, `ConfigDistributor`, `GameConfigService`, shared parsers.
- `Assets/Game` — доменные области: Battles, Entities, Stories, Perks, Statuses, Bonuses, Equipments.
- `Program.cs` — DI registration, middleware, minimal API endpoints.
- Каждая доменная область: `Configs/Mappers`, `Managers`, `Models`, `Services` по необходимости.

## Dependency Rules

- `Game` может зависеть от `Core`; `Core` не зависит от `Game`.
- Composition root — `Program.ConfigureServices`; новые singleton services регистрировать там.
- Mapper classes — flat data + read-only properties; lookup/indexing — в `*MapperManager`.
- Runtime-код без LINQ; `for`, `foreach`, `Dictionary`, pre-sized `List<T>`.

## Error Handling

- DI-зависимости и целостные config-объекты — not-null по контракту.
- Проверки на null — для внешних источников: HTTP input, Google API, файловая система.
- Глобальные исключения — `UseExceptionHandler` в `Program.cs`; domain errors — через return values или exceptions с логированием.

## Control Flow

- Guard clauses и early return вместо глубокой вложенности.
- `condition == false`, не `!condition`; не писать `== true`.
- Однострочные `if`/`for`/`foreach` — без braces, если это уже стиль в файле.

## Logging

- `ILogger<T>` через DI; `LogError` для unhandled exceptions.
- `Console.WriteLine` только для startup banner (как в `Program.cs`).

## API и DTO

- Request/response models в `Game/*/Models` или рядом с endpoint contract.
- JSON: camelCase через Newtonsoft `CamelCasePropertyNamesContractResolver`.
- Секреты и credentials — `appsettings` / env vars, не hardcode в production code.

## Tests

- Целевой проект: `LewdventureServer.Tests` с NUnit + `Microsoft.NET.Test.Sdk`.
- Battle logic — unit tests на `BattleSimulatorService`, perks, skills, mappers.
- Naming: `MethodName_Scenario_ExpectedBehavior`.
