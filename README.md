# LewdventureServer

> ASP.NET Core 9 headless battle simulator для Lewdventure. Google Sheets configs + детерминированная симуляция + battle script для Unity-клиента.

Сервер воспроизводит бой по snapshot двух команд и возвращает пошаговый script с seed для replay на клиенте. Конфиги (characters, perks, statuses, story levels и др.) подтягиваются из Google Sheets.

## Quick Start

```powershell
# Требуется .NET 9 SDK
cd D:\Project\LewdventureServer

# Положить google-credentials.json в output dir (или root с Copy if newer)
dotnet run

# Сервер стартует на http://localhost:5000
# На старте сам дергает sync конфигов (тот же путь, что POST /api/config/update)
# Swagger UI: http://localhost:5000/swagger (Development only)
```

## API

| Endpoint | Method | Описание |
| --- | --- | --- |
| `/` | GET | Hello World |
| `/api/ping` | GET | Status + UTC time |
| `/api/battle/simulate` | POST | Симуляция боя |
| `/api/battle/replay` | POST | Replay боя по seed + тому же snapshot |
| `/api/config/update` | POST | Обновить конфиги из Google Sheets (`X-Config-Secret` header); на старте вызывается автоматически |

## Key Features

- **Modular layout** — `Core` (infra) + `Game` (domains).
- **Google Sheets configs** — live sync через `GameConfigService`.
- **Deterministic battles** — `Seed` в response для client replay.
- **Mapper/Manager pipeline** — data-only mappers, runtime indexes в managers.
- **Battle script** — `BattleStep` + `BattleCommand` для Unity presentation layer.

## Documentation

| Документ | Описание |
| --- | --- |
| [AGENTS.md](AGENTS.md) | Карта проекта для AI agents |
| [Documents](Assets/Documents/README.md) | Корень: GDD + Server docs |
| [Battle API](Assets/Documents/Server/battle-api.md) | Endpoint, flow, примеры request/response |
| [Config Sync](Assets/Documents/Server/config-sync.md) | Google Sheets sync, managers, источник правды |
| [GDD](Assets/Documents/GDD/README.md) | Игровая логика (раскладка под сервер) |
| [Description](.ai-factory/DESCRIPTION.md) | Стек и соглашения |
| [Architecture](.ai-factory/ARCHITECTURE.md) | Modular Monolith, dependency rules |
| [Battle Simulation](.ai-factory/specs/battle-simulation.md) | API contract симуляции |

## AI Factory

Проект настроен для AI Factory / Cursor:

- `/aif` — анализ и контекст
- `/aif-plan` — планирование фич
- `/aif-implement` — выполнение плана
- `/aif-fix` — багфиксы

Конфиг: `.ai-factory/config.yaml` (`ui/artifacts: ru`, `base_branch: master`).

## Related

- Unity client: `D:\Project\Lewdventure`
- GDD: `Assets/Lewdventure GDD.md`

## Notes

- `google-credentials.json` обязателен для config sync; без него сервер не стартует.
- Secret для `/api/config/update` сейчас hardcoded в `Program.cs` — вынести в appsettings.
