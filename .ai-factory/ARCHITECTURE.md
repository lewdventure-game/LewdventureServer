# Architecture: Modular Monolith

## Overview

LewdventureServer — один deployable ASP.NET Core сервис с чёткими модульными границами: `Core` (инфраструктура и конфиги) и `Game` (доменные подсистемы). Battle simulation — изолированный bounded context внутри `Game/Battles`.

Паттерн соответствует Unity-клиенту Lewdventure: общий контракт данных, но без Zenject/ECS/Addressables — только чистый C# и ASP.NET DI.

## Decision Rationale

- **Project type:** headless battle simulator + config sync API.
- **Tech stack:** .NET 9, ASP.NET Core, Google Sheets, Newtonsoft.Json.
- **Key factor:** доменные границы уже выражены через `Core`/`Game`, managers и services.

## Folder Structure

```text
Assets/
  Core/
    Configs/                   # UrlConfig, parsers, delimited converters, ConstantsMapper
    Managers/                  # ConstantsMapperManager
    Services/
      ConfigDistributor/       # Aggregates all mapper managers
      GameConfigService/       # Google Sheets download + cache update
  Game/
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
Program.cs                     # Composition root, endpoints, middleware
.ai-factory/                   # AI context, specs, rules
```

## Dependency Rules

- Allowed: `Game` depends on `Core`.
- Allowed: domain services depend on `IConfigDistributor` / specific `I*MapperManager` interfaces.
- Allowed: `Program.cs` registers concrete implementations in DI.
- Forbidden: `Core` depends on `Game`.
- Forbidden: mapper classes содержат lookup/cache logic — только managers.
- Forbidden: battle logic обращается к HTTP/Google API напрямую.

## Layer Communication

- ASP.NET DI (`IServiceCollection`) — единственный composition root.
- Config flow: Google Sheets → `GameConfigService` → `IConfigDistributor` → `*MapperManager` indexes.
- Battle flow: HTTP POST → `BattleSimulationData` → `BattleSimulatorService.Simulate` → `BattleScriptResponse`.
- Cross-domain: через `IConfigDistributor` или узкие manager interfaces, не через static globals.

## Key Principles

1. Держать `Core` стабильным и без gameplay-логики.
2. Новая battle-механика — в `Game/Battles/Services`, не в `Program.cs`.
3. Mappers data-only; индексы и lookup — в managers.
4. Детерминированность: `SeededRandomService` + `Seed` в response для replay на клиенте.
5. API DTO и simulation state — раздельные типы; не смешивать HTTP models с internal state.

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

## API Surface

| Endpoint | Method | Purpose |
| --- | --- | --- |
| `/` | GET | Health hello |
| `/api/ping` | GET | Server status + UTC time |
| `/api/battle/simulate` | POST | Run battle simulation |
| `/api/config/update` | POST | Refresh configs from Google Sheets (header secret) |

Default URL: `http://localhost:5000` (`ServerConfig.Port`).
