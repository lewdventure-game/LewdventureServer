# AGENTS.md

> Карта проекта для AI-агентов и новых разработчиков. Обновляй при существенных изменениях структуры.

## Обзор

LewdventureServer — ASP.NET Core (.NET 10) сервис, который детерминированно считает бой Lewdventure и отдаёт battle script Unity-клиенту. Конфиги игры — версионные снапшоты Google Sheets в MongoDB. Окружения: Local, Development (dev), Staging (stage), Production (prod), Testing.

## Стек

- C# / .NET 10, ASP.NET Core Minimal API, Newtonsoft.Json для контракта боя.
- MongoDB 8 (replica set `rs0`), MongoDB.Driver 3.
- Google Sheets API v4 для импорта конфигов.
- Docker (chiseled non-root образы), docker compose, Caddy.
- GitHub Actions + GHCR.
- NUnit, WebApplicationFactory, Testcontainers, BenchmarkDotNet.
- Центральное управление пакетами (`Directory.Packages.props`) с lock-файлами.

## Структура

```text
src/Lewdventure.Server.Contracts/        Battles/            wire DTO боя
src/Lewdventure.Server.GameConfig/       <Domain>/           mappers, managers
                                         Services/           ConfigDistributor
                                         GameConfigs/        снапшоты, builder, provider, diff, validator
src/Lewdventure.Server.Battle/           Battles/Services/   симуляция
src/Lewdventure.Server.Infrastructure/   Mongo/              клиент, индексы, транзакции, репозитории снапшотов
                                         GoogleSheets/       импорт листов
                                         Alerts/             очередь, троттлинг, Discord
src/Lewdventure.Server.Api/              Hosting/            ServerHost, startup loader, фоновые сервисы
                                         Composition/        регистрация DI
                                         Endpoints/          battle, config, admin, health
                                         Security/ Options/ Http/ Health/ Metrics/ Json/
tools/Lewdventure.Server.ConfigTool/     CLI снапшотов
tools/Lewdventure.Server.LoadTest/       нагрузка по golden-кейсам
tools/apps-script/                       меню публикации в таблице
tests/                                   Golden, Unit, Integration, Benchmarks
deploy/                                  docker, compose, mongo, proxy, scripts
.github/                                 workflows, composite actions, dependabot
docs/                                    gdd, server, architecture, runbooks
```

## Точки входа

| Файл | Назначение |
| --- | --- |
| `src/Lewdventure.Server.Api/Program.cs` | `--health-probe` или запуск `ServerHost` |
| `src/Lewdventure.Server.Api/Hosting/ServerHost.cs` | Kestrel, middleware, эндпоинты, инициализация Mongo и конфигов |
| `src/Lewdventure.Server.Api/Composition/ServerComposition.cs` | корень DI |
| `src/Lewdventure.Server.Api/Composition/BattleServicesRegistrar.cs` | scoped граф сервисов боя |
| `src/Lewdventure.Server.Api/Hosting/GameConfigStartupLoader.cs` | загрузка конфигов при старте |
| `src/Lewdventure.Server.GameConfig/GameConfigs/Building/GameConfigSetBuilder.cs` | снапшот → `ConfigDistributor` |
| `src/Lewdventure.Server.Infrastructure/Mongo/ConfigSnapshots/ConfigPublishingService.cs` | публикация, активация, загрузка активной версии |
| `src/Lewdventure.Server.Battle/Battles/Services/BattleSimulatorService.cs` | основной цикл боя |
| `src/Lewdventure.Server.Api/appsettings*.json` | настройки по окружениям, Sheet ID |
| `deploy/scripts/deploy.sh` | деплой и откат на VPS |

## Документация

| Документ | Путь |
| --- | --- |
| Оглавление | `docs/README.md` |
| API боя | `docs/server/battle-api.md` |
| Конфиги | `docs/server/config-sync.md` |
| Архитектура | `docs/architecture/README.md`, `.ai-factory/ARCHITECTURE.md` |
| Mongo | `docs/architecture/mongo-conventions.md` |
| Runbooks | `docs/runbooks/README.md` |
| Спека боя | `.ai-factory/specs/battle-simulation.md` |
| GDD | `docs/gdd/README.md` |
| Правила кода | `.ai-factory/rules/base.md`, `.ai-factory/rules/csharp-author-style.mdc`, `.ai-factory/RULES.md` |

## Правила для агентов

- Код и конфиги без комментариев: C#, yaml, compose, Dockerfile, workflows, `.env.example`, Caddyfile, shell, Apps Script. Пояснения — только в `docs/`.
- C#: `internal sealed`, `_camelCase`, без LINQ, без `static` (кроме `Main`), `== false`, один тип на файл, однострочные `if` без скобок.
- Механики и контракт боя меняются только осознанно: golden-тесты должны упасть, эталоны обновляются отдельным изменением (`LEWD_GOLDEN_UPDATE=1`).
- Сервисы боя scoped: состояние боя не хранить в singleton.
- Конфиги в рантайме — только через `IConfigDistributor` (scoped, берётся из `IGameConfigSetProvider.Current`).
- Новые лог-теги согласовывать с владельцем. Используются: `[Startup]`, `[Shutdown]`, `[Health]`, `[Alert]`, `[Mongo]`, `[Security]`, `[Config]`, `[Config][Snapshot]`, `[Error]`, `[Story][Battle]`.
- Секреты только через env и `secrets/` на хосте; `google-credentials.json` не коммитить и не выводить.
- Не менять Sheet ID и диапазоны листов без запроса владельца.
- Не выполнять destructive git без явного запроса.
- Изменения протокола боя — синхронно с клиентом `C:\UnityProjects\Lewdventure` и спекой.
