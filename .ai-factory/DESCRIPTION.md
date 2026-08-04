# Описание проекта

## Обзор

LewdventureServer — ASP.NET Core 9 Web API для headless симуляции боёв Lewdventure. Сервер загружает игровые конфиги из Google Sheets, держит runtime-индексы в mapper managers и воспроизводит бой по детерминированному seed, возвращая пошаговый battle script для Unity-клиента.

Парный Unity-клиент: `D:\Project\Lewdventure`.

## Обнаруженный стек

- **Runtime:** .NET 9.0, ASP.NET Core Minimal API + controllers.
- **Язык:** C# (nullable enabled, implicit usings).
- **JSON:** Newtonsoft.Json (camelCase, ignore nulls).
- **Конфиги:** Google Sheets API v4, CsvHelper, mapper/manager pipeline.
- **Документация API:** Swashbuckle (Swagger UI в Development).
- **Тесты:** пока не выделены в отдельный проект (целевой стандарт — NUnit + `dotnet test`).

## Архитектурные наблюдения

- Код живёт в `Assets/` с разделением `Core` (инфраструктура, конфиги, constants) и `Game` (доменные области: Battles, Entities, Stories, Perks, Statuses, Bonuses, Equipments).
- `Program.cs` — composition root: DI registrations, middleware, minimal API endpoints.
- `IConfigDistributor` агрегирует все `*MapperManager`; `GameConfigService` обновляет данные из Google Sheets.
- Battle simulation: `BattleSimulatorService` + `BattleStatusSimulator`, perks/skills через factories.
- Namespace root: `Server.*` (не совпадает с путями `Assets/`, это legacy convention).

## Выявленные соглашения

- Типы, методы, свойства — `PascalCase`; локальные и параметры — `camelCase`.
- Private fields — `_camelCase`; новый код — `internal sealed` где возможно.
- Интерфейсы с префиксом `I`; один тип на файл.
- Условия: `== false` вместо `!`; без LINQ в runtime-коде.
- Mapper configs — data + read-only properties; lookup/indexing в managers.

## Рекомендуемые skills

- `csharp-nunit` — unit-тесты для battle logic и mappers.
- Встроенные `aif-*` skills — планирование, implement, fix, verify.

## Architecture

Подробные архитектурные правила — `.ai-factory/ARCHITECTURE.md`.

**Pattern:** Modular Monolith (Core / Game domains)
