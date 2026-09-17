# Architecture: Modular Monolith (projects per layer)

## Overview

LewdventureServer — один deployable ASP.NET Core сервис (.NET 10), разнесённый на проекты: контракт, конфиги, бой, инфраструктура, хост. Границы слоёв держит компилятор через ссылки проектов. Battle simulation — изолированный bounded context без ASP.NET, Mongo и Google.

Подробная схема, окружения и наблюдаемость: `docs/architecture/README.md`. Mongo: `docs/architecture/mongo-conventions.md`.

## Decision Rationale

- **Project type:** headless battle simulator + управление версиями игровых конфигов.
- **Tech stack:** .NET 10, ASP.NET Core Minimal API, Newtonsoft.Json, MongoDB, Google Sheets, Docker.
- **Key factor:** механики боя и wire-контракт должны оставаться неизменными при любой инфраструктурной работе; это обеспечивают отдельные проекты и golden-тесты.

## Folder Structure

```text
src/
  Lewdventure.Server.Contracts/        Battles/                   DTO request/response, steps, commands, enum'ы протокола
  Lewdventure.Server.GameConfig/       Collections/ Configs/      BaseManager, парсеры, конвертеры
                                       <Domain>/                  Artifacts, Aspects, Bonuses, Common, Entities, Equipments,
                                                                  Perks, Statuses, Stories, Trainings: mappers + managers
                                       Services/                  ConfigDistributor
                                       GameConfigs/               Snapshots, Building, Sources, Validation, Diff, провайдер набора
  Lewdventure.Server.Battle/           Battles/Services/          BattleSimulatorService, перки, статусы, саммоны, скиллы
                                       Services/                  RNG
  Lewdventure.Server.Infrastructure/   Mongo/                     клиент, индексы, транзакции, ConfigSnapshots/
                                       GoogleSheets/              импорт листов, credentials
                                       Alerts/                    очередь, троттлинг, Discord
  Lewdventure.Server.Api/              Hosting/ Composition/ Endpoints/ Options/ Security/ Http/ Health/ Metrics/ Json/
tools/                                 ConfigTool, LoadTest, apps-script
tests/                                 GoldenTests, UnitTests, IntegrationTests, Benchmarks
deploy/                                docker, compose, mongo, proxy, scripts
docs/                                  gdd, server, architecture, runbooks
```

## Dependency Rules

- `Contracts` ни от чего не зависит.
- `Battle` → `Contracts`, `GameConfig`.
- `Infrastructure` → `GameConfig`.
- `Api` → все проекты `src/`.
- `ConfigTool` → `GameConfig`, `Infrastructure`. `LoadTest` → только HTTP.
- Forbidden: `Battle` и `GameConfig` ссылаются на ASP.NET, Mongo, Google API, HTTP.
- Forbidden: mapper-классы содержат lookup/cache — только managers.
- Forbidden: регистрировать отдельные `I*MapperManager` в DI; конфиги доступны только через `IConfigDistributor`.
- Forbidden: состояние боя в singleton; сервисы боя scoped.

## Layer Communication

- Composition root: `Api/Composition/ServerComposition` + регистраторы (`BattleServicesRegistrar`, `SecurityRegistrar`, `AlertsRegistrar`, `MongoServicesRegistrar`, `ConfigSnapshotStoreRegistrar`). Регистраторы — экземпляры, не static.
- Config flow: Google Sheets → `GoogleSheetsConfigImporter` → `GameConfigSnapshot` → `ConfigSnapshotValidator` → `GameConfigSetBuilder` → `ConfigPublishingService` (Mongo) → `IGameConfigSetProvider.Swap`.
- Runtime config access: scoped `IConfigDistributor` = `IGameConfigSetProvider.Current.Distributor`, фиксируется на весь запрос.
- Battle flow: HTTP POST → endpoint filters (метрики, готовность конфигов) → `BattleSimulatorService.Simulate` → `BattleScriptResponse`.
- Ops: health и admin только на ops-порту (`OpsPortOnlyMetadata` + `OpsPortGuardMiddleware`).
- Battle script ordering: мутации HP/статов идут в локальный `List<BattleCommand>`, side-effect notify (`NotifyAnyDamage` / perk action steps) — только **после** `BattleScriptBuilder.Add` родительского damage-step.

## Key Principles

1. Механики, формулы, порядок RNG, JSON-поля и enum-числа меняются только осознанно, с обновлением golden-эталонов отдельным изменением.
2. Новая battle-механика — в `Battle`, не в `Api`.
3. Mappers data-only; индексы и lookup — в managers.
4. Детерминированность: seeded RNG + `Seed` в response для replay.
5. API DTO (`Contracts`) и simulation state — раздельные типы.
6. Новый конфиг-домен: `GameConfig/<Domain>/{Configs,Managers}` + регистрация в `ConfigDistributor` и `GameConfigSetBuilder` + обязательный лист в `GoogleSheets:Sheets` и `ConfigDomainNames`.
7. Настройки — typed options с `ValidateOnStart` и валидаторами; ничего не хардкодить в коде.
8. Код и конфиги без комментариев; пояснения в `docs/`.

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

### Options Registration

```csharp
services.AddOptions<AlertsOptions>()
    .Bind(_configuration.GetSection(AlertsOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

services.AddSingleton<IValidateOptions<AlertsOptions>, AlertsOptionsValidator>();
```

## Anti-Patterns

- Вызывать `NotifyAnyDamage` / perk `BattleScriptBuilder.Add`, пока родительский damage ещё только в локальном `commands`.
- Держать состояние боя в полях сервисов или регистрировать сервисы боя singleton.
- Мутировать `ConfigDistributor` текущей версии вместо сборки нового набора.
- Тянуть Google Sheets, Mongo или HTTP из `Battle` и `GameConfig`.
- Публиковать ops-порт или `/admin/*` наружу.
- Хранить секреты в `appsettings*.json` или git.

## API Surface

| Endpoint | Port | Purpose |
| --- | --- | --- |
| `GET /`, `GET /api/ping` | public | hello, статус и время |
| `POST /api/battle/simulate` | public | симуляция боя |
| `POST /api/battle/replay` | public | повтор боя по seed |
| `POST /api/config/publish`, `POST /api/config/update`, `GET /api/config/status` | public | публикация конфигов из Sheets (dev/stage) |
| `GET /health`, `/health/live`, `/health/ready` | ops | health |
| `/admin/config/status`, `snapshots`, `activate`, `reload` | ops | управление снапшотами |

Порты по умолчанию: public `5000`, ops `9090`.
