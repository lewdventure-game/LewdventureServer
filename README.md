# LewdventureServer

Серверный симулятор боя для Lewdventure на ASP.NET Core (.NET 10). Считает бой детерминированно по snapshot двух команд и возвращает пошаговый battle script, который проигрывает Unity-клиент. Игровые конфиги берутся из Google Sheets как версионные снапшоты в MongoDB.

## Быстрый старт

Нужен .NET SDK 10.0.401 (`global.json`) и Docker Desktop.

Windows, для игры из Unity: `local-server.bat` (Docker Desktop, api, MongoDB, ожидание готовности; команды `stop`, `restart`, `logs`, `status`, `reset`).

Полный стек как на VPS (api + Mongo replica set), конфиги при первом старте импортируются из Google Sheets:

```bash
docker compose -f deploy/compose/compose.yaml -f deploy/compose/compose.local.yaml up -d --build --wait
curl -s http://127.0.0.1:9090/health/ready
```

Без Docker, на замороженной фикстуре конфигов:

```bash
dotnet build -c Release
ASPNETCORE_ENVIRONMENT=Local GameConfig__Source=File GameConfig__FilePath=tests/Lewdventure.Server.GoldenTests/Golden/Fixtures/config-snapshot.v1.json dotnet run -c Release --project src/Lewdventure.Server.Api
```

Клиент ходит на `http://localhost:5000`. Подробно: [docs/runbooks/local-dev.md](docs/runbooks/local-dev.md).

## Порты и эндпоинты

| Порт | Назначение |
| --- | --- |
| `5000` | публичный API: бой, публикация конфигов (dev/stage), Swagger (Local/dev) |
| `9090` | ops: `/health`, `/health/live`, `/health/ready`, `/admin/config/*`; наружу не публикуется |

| Endpoint | Описание |
| --- | --- |
| `POST /api/battle/simulate` | симуляция боя, seed в ответе |
| `POST /api/battle/replay` | повтор боя по seed |
| `POST /api/config/publish` | импорт конфигов из Sheets и активация (dev/stage, `X-Config-Key`) |
| `GET /api/config/status` | активная версия конфигов (`X-Config-Key`) |
| `GET /health/ready` | готовность: конфиги и Mongo |
| `/admin/config/*` | управление снапшотами (ops-порт, `X-Admin-Key`) |

## Окружения

| Окружение | `ASPNETCORE_ENVIRONMENT` | Где | Конфиги |
| --- | --- | --- | --- |
| local | `Local` | машина разработчика | Sheets / файл / локальный Mongo |
| dev | `Development` | VPS | Mongo, публикация из таблицы, poll |
| stage | `Staging` | VPS | Mongo, публикация из таблицы, poll |
| prod | `Production` | VPS | Mongo, только `config-promote` со stage |
| tests | `Testing` | CI, тесты | файл-фикстура |

Все окружения живут на одном VPS отдельными compose-проектами за Cloudflare и Caddy; любое окружение можно вынести на свой VPS без изменений кода ([vps-bootstrap](docs/runbooks/vps-bootstrap.md)).

Поток релиза: merge в `master` → CI → образы `sha-<12>` в GHCR → деплой dev → `promote` на stage → `promote` на prod с одобрением. Схема и откат: [docs/runbooks/deploy-and-rollback.md](docs/runbooks/deploy-and-rollback.md).

## Структура

```text
src/
  Lewdventure.Server.Contracts        wire DTO боя
  Lewdventure.Server.GameConfig       mappers, managers, ConfigDistributor, снапшоты конфигов
  Lewdventure.Server.Battle           симуляция боя
  Lewdventure.Server.Infrastructure   Mongo, Google Sheets, алерты Discord
  Lewdventure.Server.Api              хост, эндпоинты, options, health, безопасность, метрики
tools/
  Lewdventure.Server.ConfigTool       CLI снапшотов: import, validate, diff, publish, activate, export
  Lewdventure.Server.LoadTest         нагрузочный прогон replay по golden-кейсам
  apps-script/                        меню публикации конфигов в Google-таблице
tests/
  Lewdventure.Server.GoldenTests      эталоны ответов боя байт-в-байт
  Lewdventure.Server.UnitTests        options, безопасность, алерты, снапшоты
  Lewdventure.Server.IntegrationTests Mongo через Testcontainers
  Lewdventure.Server.Benchmarks       BenchmarkDotNet
deploy/                               Dockerfile, compose, Mongo, Caddy, скрипты VPS
.github/                              CI, CD, promote, rollback, config-promote
docs/                                 GDD, API, runbooks, архитектура
```

## Тесты

```bash
dotnet test LewdventureServer.slnx -c Release
LEWD_IT_ENABLED=1 dotnet test tests/Lewdventure.Server.IntegrationTests -c Release
dotnet run -c Release --project tools/Lewdventure.Server.LoadTest -- --target http://localhost:5000
```

Golden-тесты защищают механики: любое изменение ответа боя роняет их. Как обновлять эталоны: [docs/runbooks/golden-tests.md](docs/runbooks/golden-tests.md).

## Документация

| Документ | О чём |
| --- | --- |
| [docs/README.md](docs/README.md) | оглавление всей документации |
| [docs/server/battle-api.md](docs/server/battle-api.md) | API боя, коды ответов |
| [docs/server/config-sync.md](docs/server/config-sync.md) | снапшоты конфигов, публикация, ConfigTool |
| [docs/architecture/README.md](docs/architecture/README.md) | проекты, зависимости, окружения, наблюдаемость |
| [docs/runbooks/README.md](docs/runbooks/README.md) | эксплуатация: локальный запуск, ключи конфигурации, VPS, деплой, секреты |
| [.ai-factory/specs/battle-simulation.md](.ai-factory/specs/battle-simulation.md) | канон протокола боя |
| [AGENTS.md](AGENTS.md) | карта проекта для AI-агентов |

Парный Unity-клиент: `C:\UnityProjects\Lewdventure`.
