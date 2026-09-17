# Описание проекта

## Обзор

LewdventureServer — ASP.NET Core Web API для серверной симуляции боёв Lewdventure. Сервер детерминированно считает бой по snapshot двух команд и seed и возвращает пошаговый battle script для Unity-клиента. Игровые конфиги импортируются из Google Sheets, хранятся как версионные снапшоты в MongoDB и атомарно подменяются без рестарта.

Парный Unity-клиент: `C:\UnityProjects\Lewdventure`.

## Стек

- **Runtime:** .NET 10, ASP.NET Core Minimal API.
- **Язык:** C# (nullable enabled, implicit usings, `LangVersion latest`).
- **JSON:** Newtonsoft.Json для контракта боя (camelCase, ignore nulls).
- **Данные:** MongoDB 8 replica set, MongoDB.Driver 3.
- **Конфиги:** Google Sheets API v4, mapper/manager pipeline, снапшоты sha256.
- **Поставка:** Docker (chiseled non-root), docker compose, Caddy, GitHub Actions, GHCR.
- **Наблюдаемость:** Microsoft.Extensions.Logging (JSON), health checks, `System.Diagnostics.Metrics`, алерты в Discord.
- **Тесты:** NUnit, WebApplicationFactory, Testcontainers.MongoDb, BenchmarkDotNet, собственный LoadTest.
- **Пакеты:** Central Package Management + lock-файлы.

## Архитектура

Модульный монолит из проектов `Contracts`, `GameConfig`, `Battle`, `Infrastructure`, `Api`. Подробно: `.ai-factory/ARCHITECTURE.md`, `docs/architecture/README.md`.

Окружения: `Local`, `Development`, `Staging`, `Production`, `Testing`.

Namespace root: `Server.*` (исторический, не совпадает с именами проектов).

## Соглашения

- Типы, методы, свойства — `PascalCase`; локальные и параметры — `camelCase`; private fields — `_camelCase`.
- Новый код — `internal sealed`; интерфейсы с префиксом `I`; один тип на файл.
- `== false` вместо `!`; без LINQ и без static в проектном коде.
- Mapper configs — data-only; lookup и индексы в managers.
- Без комментариев в коде и конфигах.

## Рекомендуемые skills

- `csharp-nunit` — unit-тесты.
- Встроенные `aif-*` skills — планирование, implement, fix, verify.
