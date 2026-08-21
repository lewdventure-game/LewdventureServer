# Architecture: Modular Monolith (Structured Modules / Technical Layer)

## Overview

LewdventureServer — один deployable ASP.NET Core сервис с чёткими модульными границами: `Core` (инфраструктура и конфиги) и `Game` (доменные подсистемы). Battle simulation — изолированный bounded context внутри `Game/Battles`.

Паттерн соответствует Unity-клиенту Lewdventure: общий контракт данных, но без Zenject/ECS/Addressables — только чистый C# и ASP.NET DI. Документ описывает **текущую** структуру репозитория (adapt to reality), а не целевой рефакторинг.

## Decision Rationale

- **Project type:** headless battle simulator + config sync API.
- **Tech stack:** .NET 9, ASP.NET Core, Google Sheets, Newtonsoft.Json.
- **Key factor:** доменные границы уже выражены через `Core`/`Game`, managers и services; новые конфиг-домены (trainings / artifacts / aspects) живут рядом с Entities/Equipments по тому же mapper+manager шаблону.

## Folder Structure

```text
Assets/
  Core/
    Collections/               # BaseManager / BaseDictionaryManager
    Configs/                   # UrlConfig, parsers, delimited converters, ConstantsMapper
    Managers/                  # ConstantsMapperManager
    Services/
      ConfigDistributor/       # Aggregates all mapper managers (runtime owner)
      GameConfigService/       # Google Sheets download + cache update
  Documents/                   # GDD + Server API/sync docs
  Game/
    Artifacts/                 # Artifact mappers + managers (Sheets sync stub-capable)
    Aspects/                   # Aspect mappers + managers
    Battles/
      Models/                  # DTO: simulation request/response, steps, commands
      Services/                # BattleSimulatorService, perks, skills, unit state
    Bonuses/                   # Bonus mappers + managers
    Common/                    # Shared mappers (skins, XP patterns)
    Entities/                  # Characters, enemies, summons, masteries
    Equipments/
    Perks/
    Statuses/
    Stories/                   # Story levels, stages, events
    Trainings/                 # Training mappers + managers
Program.cs                     # Composition root, endpoints, middleware
.ai-factory/                   # AI context, specs, rules
```

## Dependency Rules

- Allowed: `Game` depends on `Core`.
- Allowed: domain services depend on `IConfigDistributor` for configs (managers живут внутри distributor).
- Allowed: `Program.cs` registers concrete implementations in DI.
- Forbidden: `Core` depends on `Game`.
- Forbidden: mapper classes содержат lookup/cache logic — только managers.
- Forbidden: battle logic обращается к HTTP/Google API напрямую.
- Forbidden: регистрировать отдельные `I*MapperManager` в DI рядом с `IConfigDistributor` (получатся пустые дубликаты инстансов).

## Layer Communication

- ASP.NET DI (`IServiceCollection`) — единственный composition root.
- Config flow: Google Sheets → `GameConfigService` → `IConfigDistributor` → list (`BaseManager`) / dictionary (`BaseDictionaryManager`) indexes.
- Battle flow: HTTP POST → `BattleSimulationData` → `BattleSimulatorService.Simulate` → `BattleScriptResponse`.
- Cross-domain: через `IConfigDistributor`, не через static globals и не через отдельные manager singletons.
- Battle script ordering: мутации HP/статов могут идти в локальный `List<BattleCommand>`, но side-effect notify (`NotifyAnyDamage` / perk action steps) — только **после** `BattleScriptBuilder.Add` родительского damage-step (как status damage over time).

## Key Principles

1. Держать `Core` стабильным и без gameplay-логики.
2. Новая battle-механика — в `Game/Battles/Services`, не в `Program.cs`.
3. Mappers data-only; индексы и lookup — в managers.
4. Детерминированность: `SeededRandomService` + `Seed` в response для replay на клиенте.
5. API DTO и simulation state — раздельные типы; не смешивать HTTP models с internal state.
6. Новый конфиг-домен: `Assets/Game/<Domain>/{Configs,Managers}` + wiring в `IConfigDistributor` / `GameConfigService` + grants в `UnitStateBuilder` при необходимости.

## Code Organization Note

- **New Features:** Новый код следует границам и шаблонам этого документа, где это практично.
- **Existing Code:** Структура задокументирована as-is. При правках предпочитаем эти conventions, без rewrite ради выравнивания.
- **Interoperability:** Новые домены подключаются через `IConfigDistributor`, не через прямые зависимости battle → Google Sheets.

## Code Examples

### Service Boundary

```csharp
internal sealed class BattleSimulatorService : IBattleSimulatorService
{
    private readonly IBattleStatusSimulator _battleStatusSimulator;
    private readonly IConfigDistributor _configDistributor;

    public BattleSimulatorService(
        IBattleStatusSimulator battleStatusSimulator,
        IConfigDistributor configDistributor)
    {
        _battleStatusSimulator = battleStatusSimulator;
        _configDistributor = configDistributor;
    }
}
```

### Manager Lookup (not in mapper)

```csharp
public ICharacterMapper GetById(int id)
{
    return _characters[id];
}
```

## Anti-Patterns

- Вызывать `NotifyAnyDamage` / perk `BattleScriptBuilder.Add` пока родительский damage ещё только в локальном `commands` (ActionReward окажется в script раньше ShowDamage).
- Регистрировать mapper managers и в DI, и внутри `ConfigDistributor` как разные инстансы.
- Класть lookup/cache в mapper classes.
- Тянуть Google Sheets / HTTP из `Game/Battles`.

## API Surface

| Endpoint | Method | Purpose |
| --- | --- | --- |
| `/` | GET | Health hello |
| `/api/ping` | GET | Server status + UTC time |
| `/api/battle/simulate` | POST | Run battle simulation |
| `/api/battle/replay` | POST | Replay battle with fixed seed |
| `/api/config/update` | POST | Refresh configs from Google Sheets (header secret) |

Default URL: `http://localhost:5000` (`ServerConfig.Port`).
