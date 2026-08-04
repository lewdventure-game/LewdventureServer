# [AGENTS.md](http://AGENTS.md)

> Карта проекта для AI agents и новых разработчиков. Обновляй при существенных изменениях структуры.

## Обзор проекта

LewdventureServer — ASP.NET Core 9 Web API для Lewdventure. Загружает конфиги из Google Sheets, воспроизводит бой детерминированно и возвращает battle script для Unity-клиента. Подробности — `.ai-factory/DESCRIPTION.md`.

## Tech Stack

- **Язык:** C# (.NET 9).
- **Framework:** ASP.NET Core Minimal API + controllers.
- **JSON:** Newtonsoft.Json.
- **Конфиги:** Google Sheets API v4, CsvHelper.
- **API docs:** Swashbuckle (Development).
- **Тесты:** NUnit (целевой стандарт, skill `csharp-nunit`).



## Структура проекта

```text
Assets/
  Documents/                   # GDD, разложенный под серверную ответственность
  Core/
    Configs/                   # UrlConfig, parsers, converters
    Managers/                  # ConstantsMapperManager
    Services/
      ConfigDistributor/       # Агрегатор всех mapper managers
      GameConfigService/       # Google Sheets sync
  Game/
    Battles/                   # Simulation core
      Models/                  # DTO: request, response, steps
      Services/                # BattleSimulatorService, perks, skills
    Bonuses/
    Common/
    Entities/
    Equipments/
    Perks/
    Statuses/
    Stories/
Program.cs                     # Composition root + endpoints
.ai-factory/                   # AI Factory context
.cursor/
  rules/                       # Cursor style rules
  skills/                      # aif-* built-in skills
.agents/skills/                # External skills (csharp-nunit)
```



## Ключевые точки входа


| Файл                                                          | Назначение                                                   |
| ------------------------------------------------------------- | ------------------------------------------------------------ |
| `Program.cs`                                                  | DI, middleware, `/api/battle/simulate`, `/api/config/update` |
| `Assets/Core/Services/ConfigDistributor/ConfigDistributor.cs` | Все mapper managers                                          |
| `Assets/Core/Services/GameConfigService/GameConfigService.cs` | Загрузка конфигов из Google Sheets                           |
| `Assets/Game/Battles/Services/BattleSimulatorService.cs`      | Основной battle loop                                         |
| `Assets/Core/Configs/UrlConfig.cs`                            | URL paths для endpoints                                      |
| `LewdventureServer.csproj`                                    | .NET 9 Web SDK, package refs                                 |




## Документация


| Документ            | Путь                                        | Описание                           |
| ------------------- | ------------------------------------------- | ---------------------------------- |
| README              | `README.md`                                 | Quick start, endpoints             |
| Project description | `.ai-factory/DESCRIPTION.md`                | Стек и соглашения                  |
| Architecture        | `.ai-factory/ARCHITECTURE.md`               | Modular Monolith, dependency rules |
| Battle spec         | `.ai-factory/specs/battle-simulation.md`    | API contract симуляции             |
| Base rules          | `.ai-factory/rules/base.md`                 | C#/ASP.NET conventions             |
| C# author style     | `.ai-factory/rules/csharp-author-style.mdc` | Форматирование кода                |




## AI Context Files


| Файл                          | Назначение              |
| ----------------------------- | ----------------------- |
| `AGENTS.md`                   | Быстрая карта проекта   |
| `.ai-factory/DESCRIPTION.md`  | Описание и stack        |
| `.ai-factory/ARCHITECTURE.md` | Архитектура             |
| `.ai-factory/RULES.md`        | Короткие аксиомы        |
| `.ai-factory/rules/`          | Детальные правила       |
| `.cursor/rules/`              | Cursor mirror для style |




## Agent Rules

- C# runtime: no LINQ, `_camelCase`, `internal sealed`, без лишних null checks для DI/config.
- Battle-задачи: читать `.ai-factory/specs/battle-simulation.md` перед изменениями.
- Не выполнять destructive git без явного запроса.
- Shell-команды — по одной; не склеивать `git checkout master && git pull`.
- Парный клиент: `D:\Project\Lewdventure` — синхронизировать battle contract при protocol changes.

